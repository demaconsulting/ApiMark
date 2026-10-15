using Mono.Cecil;

namespace ApiMark.DotNet;

/// <summary>Stateless helper that converts Mono.Cecil <see cref="TypeReference"/> instances to readable C# type names.</summary>
public static class TypeNameSimplifier
{
    /// <summary>C# primitive aliases keyed by full CLR name.</summary>
    private static readonly Dictionary<string, string> Primitives = new(StringComparer.Ordinal)
    {
        ["System.Boolean"] = "bool",
        ["System.Byte"] = "byte",
        ["System.SByte"] = "sbyte",
        ["System.Int16"] = "short",
        ["System.UInt16"] = "ushort",
        ["System.Int32"] = "int",
        ["System.UInt32"] = "uint",
        ["System.Int64"] = "long",
        ["System.UInt64"] = "ulong",
        ["System.Single"] = "float",
        ["System.Double"] = "double",
        ["System.Decimal"] = "decimal",
        ["System.Char"] = "char",
        ["System.String"] = "string",
        ["System.Object"] = "object",
        ["System.Void"] = "void",
    };

    /// <summary>Namespaces whose prefix is stripped when displaying generic or plain types.</summary>
    private static readonly HashSet<string> WellKnownNamespaces = new(StringComparer.Ordinal)
    {
        "System.Collections.Generic",
        "System.Threading.Tasks",
    };

    /// <summary>
    ///     Converts <paramref name="typeRef"/> to a human-readable C# type name, stripping well-known
    ///     namespaces and the <paramref name="contextNamespace"/> prefix where applicable.
    /// </summary>
    /// <remarks>
    ///     Exists to make generated Markdown signatures readable to C# developers — raw Mono.Cecil type
    ///     names include CLR full names and generic arity suffixes that are unfamiliar in documentation.
    ///     Eight simplification rules (numbered 0 through 7) are applied: (0) byref parameter type
    ///     unwrapping (the <c>ref</c>/<c>out</c>/<c>in</c> keyword itself is the caller's responsibility —
    ///     see <see cref="DotNetEmitter.GetRefKindKeyword"/>), (1) C# primitive aliases, (2) array bracket
    ///     notation, (3) Nullable&lt;T&gt; → T?, (4) well-known namespace stripping, (5) context namespace
    ///     prefix stripping, (6) recursive generic argument simplification, and (7) a per-position nullable
    ///     reference <c>?</c> suffix driven by a <see cref="NullabilityCursor"/>. This overload builds a
    ///     cursor that annotates only the outermost position. Stateless and thread-safe; no shared mutable
    ///     state is modified during the call.
    /// </remarks>
    /// <param name="typeRef">The Mono.Cecil type reference to simplify.</param>
    /// <param name="contextNamespace">The namespace of the type that owns this reference, used for prefix stripping.</param>
    /// <param name="isNullableAnnotated">
    ///     When <see langword="true"/>, the member carrying this type reference has a
    ///     <c>NullableAttribute(2)</c> annotation (i.e. the C# 8+ nullable reference type <c>?</c>
    ///     suffix). This information cannot be derived from <paramref name="typeRef"/> alone because
    ///     Mono.Cecil stores nullable annotations on the containing member, not on the type reference
    ///     itself. Callers that inspect member custom attributes should pass <see langword="true"/>
    ///     when byte value 2 is found.
    /// </param>
    /// <returns>A simplified, human-readable C# type name.</returns>
    public static string Simplify(TypeReference typeRef, string contextNamespace, bool isNullableAnnotated = false) =>
        Simplify(typeRef, contextNamespace, NullabilityCursor.ForOutermostFlag(isNullableAnnotated));

    /// <summary>
    ///     Converts <paramref name="typeRef"/> to a human-readable C# type name, applying a per-type
    ///     nullable-reference annotation to every position of the type tree (outer type, array
    ///     elements, and generic arguments) in the order the C# compiler encodes them.
    /// </summary>
    /// <param name="typeRef">The Mono.Cecil type reference to simplify.</param>
    /// <param name="contextNamespace">The namespace of the type that owns this reference, used for prefix stripping.</param>
    /// <param name="nullability">
    ///     The cursor over the nullable flags for the member carrying the type, or
    ///     <see langword="null"/> for a type with no nullable annotations.
    /// </param>
    /// <returns>A simplified, human-readable C# type name.</returns>
    internal static string Simplify(TypeReference typeRef, string contextNamespace, NullabilityCursor? nullability) =>
        SimplifyCore(typeRef, contextNamespace, nullability);

    /// <summary>
    ///     Returns <see langword="true"/> when a type position with nullable <paramref name="flag"/>
    ///     is a nullable reference type that should render with a trailing <c>?</c>.
    /// </summary>
    /// <remarks>
    ///     Value types (including <see langword="struct"/>-constrained type parameters) never render
    ///     the reference-type <c>?</c> suffix: a nullable value type is expressed as
    ///     <c>Nullable&lt;T&gt;</c> instead, and a context-inherited flag of 2 must not leak onto them.
    /// </remarks>
    /// <param name="typeRef">The type at this position.</param>
    /// <param name="flag">The nullable flag consumed for the position (2 means annotated).</param>
    /// <returns><see langword="true"/> when a <c>?</c> suffix applies.</returns>
    internal static bool IsAnnotated(TypeReference typeRef, byte flag) =>
        flag == 2 &&
        !typeRef.IsValueType &&
        typeRef.FullName != "System.Void" &&
        !(typeRef is GenericParameter { HasNotNullableValueTypeConstraint: true });

    /// <summary>
    ///     Returns <see langword="true"/> when <paramref name="typeRef"/> is a constructed
    ///     <c>System.ValueTuple</c> type with one to eight type arguments, i.e. a C# tuple type
    ///     (a one-argument instance only occurs as the <c>TRest</c> tail of a larger tuple).
    /// </summary>
    /// <param name="typeRef">The type reference to test.</param>
    /// <returns><see langword="true"/> for a C# tuple type.</returns>
    internal static bool IsTupleType(TypeReference typeRef) =>
        typeRef is GenericInstanceType { GenericArguments.Count: >= 1 and <= 8 } git &&
        git.ElementType.Namespace == "System" &&
        git.ElementType.Name.StartsWith("ValueTuple`", StringComparison.Ordinal);

    /// <summary>
    ///     Returns <see langword="true"/> when the compiler encodes a nullable flag for the type position.
    /// </summary>
    /// <remarks>
    ///     Roslyn omits flags for non-generic value types (<c>int</c>, enums, plain structs) and for
    ///     <c>Nullable&lt;T&gt;</c> itself; reference types, type parameters and generic value types
    ///     (such as <c>ValueTuple&lt;,&gt;</c>) each take one flag.
    /// </remarks>
    /// <param name="typeRef">The type at this position.</param>
    /// <returns><see langword="true"/> when the position consumes a flag.</returns>
    internal static bool ConsumesNullableFlag(TypeReference typeRef) =>
        typeRef switch
        {
            GenericParameter => true,
            GenericInstanceType git => !git.ElementType.FullName.StartsWith("System.Nullable`1", StringComparison.Ordinal),
            _ => !typeRef.IsValueType,
        };

    /// <summary>
    ///     Renders a C# tuple type as <c>(A, B)</c> or <c>(A a, B b)</c> when element names are
    ///     available, flattening the nested <c>TRest</c> encoding used for tuples of eight or more elements.
    /// </summary>
    /// <remarks>
    ///     The tuple type itself must already have consumed its own nullable flag. Element names are
    ///     read from the cursor up front (they precede nested tuple names in the compiler encoding);
    ///     element types are then rendered in order through <paramref name="renderElement"/>, which
    ///     consumes the nullable flags of each element, and of the <c>TRest</c> position, in pre-order.
    /// </remarks>
    /// <param name="tuple">The tuple type (see <see cref="IsTupleType"/>).</param>
    /// <param name="nullability">The cursor providing nullable flags and tuple element names, or <see langword="null"/>.</param>
    /// <param name="renderElement">Renders one element type (consuming its flags from the same cursor).</param>
    /// <returns>The tuple text, for example <c>(int Id, string? Name)</c>.</returns>
    internal static string BuildTuple(GenericInstanceType tuple, NullabilityCursor? nullability, Func<TypeReference, string> renderElement)
    {
        var names = nullability?.TakeTupleNames(CountTupleElements(tuple)) ?? Array.Empty<string?>();
        var elements = new List<string>();
        var index = 0;
        AppendTupleElements(tuple, nullability, renderElement, names, ref index, elements);
        return "(" + string.Join(", ", elements) + ")";
    }

    /// <summary>Counts the flattened element count of a tuple, following the <c>TRest</c> nesting.</summary>
    /// <param name="tuple">The tuple type.</param>
    /// <returns>The total number of tuple elements.</returns>
    private static int CountTupleElements(GenericInstanceType tuple) =>
        tuple.GenericArguments.Count == 8 && IsTupleType(tuple.GenericArguments[7])
            ? 7 + CountTupleElements((GenericInstanceType)tuple.GenericArguments[7])
            : tuple.GenericArguments.Count;

    /// <summary>Appends the rendered elements of one tuple (and its <c>TRest</c> tail) to <paramref name="elements"/>.</summary>
    /// <param name="tuple">The tuple type.</param>
    /// <param name="nullability">The cursor for nullable flags.</param>
    /// <param name="renderElement">Element renderer.</param>
    /// <param name="names">Flattened element names.</param>
    /// <param name="index">Running index into <paramref name="names"/>.</param>
    /// <param name="elements">Destination list.</param>
    private static void AppendTupleElements(
        GenericInstanceType tuple,
        NullabilityCursor? nullability,
        Func<TypeReference, string> renderElement,
        string?[] names,
        ref int index,
        List<string> elements)
    {
        var arguments = tuple.GenericArguments;
        for (var i = 0; i < arguments.Count; i++)
        {
            if (i == 7 && arguments.Count == 8 && IsTupleType(arguments[7]))
            {
                // The TRest position consumes its own flag, then contributes its elements inline
                nullability?.Next();
                AppendTupleElements((GenericInstanceType)arguments[7], nullability, renderElement, names, ref index, elements);
                continue;
            }

            var text = renderElement(arguments[i]);
            var name = index < names.Length ? names[index] : null;
            index++;
            elements.Add(string.IsNullOrEmpty(name) ? text : $"{text} {name}");
        }
    }

    /// <summary>Applies Rules 0–7 to produce a simplified type name.</summary>
    /// <remarks>
    ///     Shared implementation behind both public/internal <c>Simplify</c> overloads. It recurses
    ///     through the type tree; every position other than a by-reference wrapper consumes exactly one
    ///     flag from <paramref name="nullability"/> in pre-order (Rule 7), so the <c>?</c> suffix is
    ///     applied per position rather than as a post-processing step.
    ///     The switch-expression arms are ordered by structural specificity: composite types (array,
    ///     nullable generic, generic instance) are tested before primitive aliases and plain types to
    ///     ensure that, for example, <c>int?</c> (Nullable&lt;int&gt;) is handled by Rule 3 rather than
    ///     the primitive-alias arm.
    /// </remarks>
    /// <param name="typeRef">The Mono.Cecil type reference to simplify.</param>
    /// <param name="contextNamespace">The namespace of the enclosing type, used for prefix stripping.</param>
    /// <param name="nullability">The nullable flag cursor, or <see langword="null"/> for no annotations.</param>
    /// <returns>A simplified C# type name including any nullable-reference <c>?</c> suffixes.</returns>
    private static string SimplifyCore(TypeReference typeRef, string contextNamespace, NullabilityCursor? nullability)
    {
        // Rule 0: byref parameter types (ref/out/in) — Cecil marks these with a trailing "&"
        // on the type name; the caller is responsible for rendering the ref/out/in keyword,
        // this rule only unwraps to the underlying element type for display purposes. The
        // byref wrapper itself consumes no nullable flag.
        if (typeRef is ByReferenceType byRef)
        {
            return SimplifyCore(byRef.ElementType, contextNamespace, nullability);
        }

        // Every other type position consumes exactly one nullable flag, in pre-order, except
        // non-generic value types and Nullable<T>, which the compiler does not encode.
        var flag = ConsumesNullableFlag(typeRef) ? (nullability?.Next() ?? (byte)0) : (byte)0;
        var suffix = IsAnnotated(typeRef, flag) ? "?" : string.Empty;

        return typeRef switch
        {
            // Rule 2: array types recurse on the element type; rank-aware suffix (e.g., [] for 1-D, [,] for 2-D)
            ArrayType arr
                => SimplifyCore(arr.ElementType, contextNamespace, nullability) + "[" + new string(',', arr.Rank - 1) + "]" + suffix,

            // Rule 3a: C# tuples render as (A a, B b) rather than ValueTuple<A, B>
            GenericInstanceType tuple when IsTupleType(tuple)
                => BuildTuple(tuple, nullability, e => SimplifyCore(e, contextNamespace, nullability)) + suffix,

            // Rule 3: Nullable<T> is represented as T?
            GenericInstanceType { GenericArguments.Count: 1 } git
                when git.ElementType.FullName.StartsWith("System.Nullable", StringComparison.Ordinal)
                => SimplifyCore(git.GenericArguments[0], contextNamespace, nullability) + "?",

            // Rules 4 & 6: generic types — strip well-known namespace or context namespace, then list args
            GenericInstanceType git
                => BuildGenericName(git, contextNamespace, nullability) + suffix,

            // Rule 1: C# primitive aliases (only non-composite types reach this arm)
            _ when Primitives.TryGetValue(typeRef.FullName, out var alias)
                => alias + suffix,

            // Rule 5: plain named type — strip context namespace prefix when present
            { Namespace: var ns } when ns == contextNamespace
                => StripArity(typeRef.Name) + suffix,

            { Namespace: var ns } when !string.IsNullOrEmpty(ns) && !string.IsNullOrEmpty(contextNamespace) && typeRef.Scope is not AssemblyNameReference && ns.StartsWith(contextNamespace + ".", StringComparison.Ordinal)
                => $"{ns.Substring(contextNamespace.Length + 1)}.{StripArity(typeRef.Name)}{suffix}",

            // Rule 7 (remaining positions): plain type name plus the per-position nullable suffix
            _ => StripArity(typeRef.Name) + suffix,
        };
    }

    /// <summary>Builds the simplified type name for a generic type instance.</summary>
    /// <remarks>
    ///     Extracted from <see cref="SimplifyCore"/> because generic instance handling requires
    ///     three distinct sub-decisions: (1) whether to strip the well-known namespace prefix
    ///     from the container type, (2) whether to apply context-namespace prefix stripping as
    ///     a fallback, and (3) recursive simplification of every type argument. Isolating these
    ///     steps in a named helper keeps <see cref="SimplifyCore"/> readable and avoids a deeply
    ///     nested conditional inside the switch expression.
    /// </remarks>
    /// <param name="git">The generic instance type to represent.</param>
    /// <param name="contextNamespace">The namespace of the enclosing type, used for prefix stripping.</param>
    /// <param name="nullability">The nullable flag cursor, positioned at the first generic argument.</param>
    /// <returns>A string of the form <c>Name&lt;Arg1, Arg2&gt;</c>.</returns>
    private static string BuildGenericName(GenericInstanceType git, string contextNamespace, NullabilityCursor? nullability)
    {
        var ns = git.ElementType.Namespace;
        var baseName = WellKnownNamespaces.Contains(ns)
            ? StripArity(git.ElementType.Name)
            : StripArity(ApplyContextStrip(git.ElementType, contextNamespace));
        var args = string.Join(", ", git.GenericArguments.Select(a => SimplifyCore(a, contextNamespace, nullability)));
        return $"{baseName}<{args}>";
    }

    /// <summary>Removes the generic arity suffix (e.g. <c>`1</c>) from a type name.</summary>
    /// <remarks>
    ///     Used wherever the arity count carries no meaning for human readers — for example, in
    ///     display names, external-type table entries, and type-page headings. The backtick and
    ///     following digit(s) are dropped entirely. Contrast with <see cref="FlattenArity"/>,
    ///     which replaces the backtick with a hyphen and keeps the digit so that file-system path
    ///     segments remain unique even when both <c>Foo</c> and <c>Foo&lt;T&gt;</c> exist.
    /// </remarks>
    /// <param name="name">The raw type name that may contain a backtick arity suffix.</param>
    /// <returns>The name without the arity suffix.</returns>
    internal static string StripArity(string name)
    {
        var tick = name.IndexOf('`');
        return tick >= 0 ? name.Substring(0, tick) : name;
    }

    /// <summary>
    ///     Converts the IL backtick arity suffix to a hyphenated numeric suffix, producing a
    ///     file-system-safe name that still distinguishes generic types by parameter count.
    /// </summary>
    /// <remarks>
    ///     For example, <c>Foo`2</c> becomes <c>Foo-2</c> and <c>Foo</c> is unchanged.
    ///     A hyphen cannot appear in a C# identifier, so <c>Foo-1</c> never collides with a real
    ///     type named <c>Foo1</c>, and the arity keeps <c>Foo</c>, <c>Foo&lt;T&gt;</c> and
    ///     <c>Foo&lt;T1, T2&gt;</c> distinct. The name needs no URL escaping.
    /// </remarks>
    /// <param name="name">The raw IL type name that may contain a backtick arity suffix.</param>
    /// <returns>The name with the backtick replaced by a hyphen and the arity count preserved.</returns>
    internal static string FlattenArity(string name)
    {
        var tick = name.IndexOf('`');
        return tick >= 0 ? string.Concat(name.AsSpan(0, tick), "-", name.AsSpan(tick + 1)) : name;
    }

    /// <summary>
    ///     Returns the shortest unambiguous name for <paramref name="typeRef"/> relative to
    ///     <paramref name="contextNamespace"/>, stripping that namespace's prefix if present.
    /// </summary>
    /// <remarks>
    ///     Exists as a named helper to encapsulate the context-stripping logic that is shared
    ///     between the plain-type arm of <see cref="SimplifyCore"/> and the generic-container
    ///     name computation in <see cref="BuildGenericName"/>. Returning the raw type name when
    ///     no prefix matches avoids hiding a type behind an incorrect short name when the
    ///     namespace relationship is ambiguous.
    /// </remarks>
    /// <param name="typeRef">The type reference to shorten.</param>
    /// <param name="contextNamespace">The namespace of the enclosing type.</param>
    /// <returns>The shortest name that is unambiguous in the given context.</returns>
    private static string ApplyContextStrip(TypeReference typeRef, string contextNamespace)
    {
        var ns = typeRef.Namespace;

        if (string.IsNullOrEmpty(ns) || ns == contextNamespace)
        {
            return typeRef.Name;
        }

        // Types from another assembly are always shown by their short name: partially-qualifying
        // them against the context namespace ("External.Type") is misleading, because the reader
        // cannot tell that "External" is a namespace of a different assembly
        if (typeRef.Scope is AssemblyNameReference)
        {
            return typeRef.Name;
        }

        // The global (empty) context namespace has no prefix to strip
        if (!string.IsNullOrEmpty(contextNamespace) && ns.StartsWith(contextNamespace + ".", StringComparison.Ordinal))
        {
            return $"{ns.Substring(contextNamespace.Length + 1)}.{typeRef.Name}";
        }

        return typeRef.Name;
    }
}

/// <summary>
///     Walks the nullable-reference flags the C# compiler encodes for a member's type, one flag per
///     position of the type tree in pre-order (outer type first, then array element or generic
///     arguments left to right).
/// </summary>
/// <remarks>
///     Part of the TypeNameSimplifier unit: it lives in this file so that it stays within the
///     unit's review set. The flags come from the member's <c>NullableAttribute</c> (single-byte form applies to every
///     position; the byte-array form is indexed by position). When the member carries no attribute,
///     every position inherits the inherited <c>NullableContext</c> byte. Flag values: 0 = oblivious,
///     1 = not annotated, 2 = annotated (<c>?</c>). Not thread-safe; create one cursor per type.
/// </remarks>
internal sealed class NullabilityCursor
{
    /// <summary>The fully-qualified name of the compiler-emitted nullable attribute.</summary>
    internal const string NullableAttributeName = "System.Runtime.CompilerServices.NullableAttribute";

    /// <summary>The fully-qualified name of the compiler-emitted tuple element names attribute.</summary>
    internal const string TupleElementNamesAttributeName = "System.Runtime.CompilerServices.TupleElementNamesAttribute";

    /// <summary>Per-position flags, or <see langword="null"/> when every position uses <see cref="_default"/>.</summary>
    private readonly byte[]? _flags;

    /// <summary>The flag returned when no per-position flag is available.</summary>
    private readonly byte _default;

    /// <summary>Flattened tuple element names in compiler order, or <see langword="null"/> when none.</summary>
    private readonly string?[]? _tupleNames;

    /// <summary>Index of the next per-position flag.</summary>
    private int _index;

    /// <summary>Index of the next tuple element name.</summary>
    private int _nameIndex;

    /// <summary>Initializes a new instance of <see cref="NullabilityCursor"/>.</summary>
    /// <param name="flags">Per-position flags, or <see langword="null"/>.</param>
    /// <param name="defaultFlag">The flag used when <paramref name="flags"/> is null or exhausted.</param>
    /// <param name="tupleNames">Flattened tuple element names, or <see langword="null"/>.</param>
    private NullabilityCursor(byte[]? flags, byte defaultFlag, string?[]? tupleNames = null)
    {
        _flags = flags;
        _default = defaultFlag;
        _tupleNames = tupleNames;
    }

    /// <summary>Creates a cursor that annotates only the outermost position, matching the legacy boolean behavior.</summary>
    /// <param name="annotated">Whether the outermost position is a nullable reference type.</param>
    /// <returns>A new cursor.</returns>
    internal static NullabilityCursor ForOutermostFlag(bool annotated) =>
        new(new byte[] { annotated ? (byte)2 : (byte)0 }, 0);

    /// <summary>Creates a cursor from a member's custom attributes and the inherited nullable context.</summary>
    /// <param name="attributes">The attributes of the member, return parameter, or parameter.</param>
    /// <param name="context">The inherited <c>NullableContext</c> byte (0 when none).</param>
    /// <returns>A new cursor.</returns>
    internal static NullabilityCursor ForAttributes(IEnumerable<CustomAttribute> attributes, byte context)
    {
        var attributeList = attributes as IList<CustomAttribute> ?? attributes.ToList();
        var tupleNames = ReadTupleNames(attributeList);
        var attribute = attributeList.FirstOrDefault(a => a.AttributeType.FullName == NullableAttributeName);
        if (attribute == null || attribute.ConstructorArguments.Count == 0)
        {
            return new NullabilityCursor(null, context, tupleNames);
        }

        var argument = attribute.ConstructorArguments[0].Value;
        if (argument is byte single)
        {
            return new NullabilityCursor(null, single, tupleNames);
        }

        if (argument is CustomAttributeArgument[] array)
        {
            var flags = array.Select(a => a.Value is byte b ? b : (byte)0).ToArray();
            return new NullabilityCursor(flags, context, tupleNames);
        }

        return new NullabilityCursor(null, context, tupleNames);
    }

    /// <summary>Reads the flattened tuple element names from a <c>TupleElementNamesAttribute</c>, if present.</summary>
    /// <param name="attributes">The attributes to search.</param>
    /// <returns>The names (null entries for unnamed elements), or <see langword="null"/> when absent.</returns>
    private static string?[]? ReadTupleNames(IEnumerable<CustomAttribute> attributes)
    {
        var attribute = attributes.FirstOrDefault(a => a.AttributeType.FullName == TupleElementNamesAttributeName);
        if (attribute != null &&
            attribute.ConstructorArguments.Count > 0 &&
            attribute.ConstructorArguments[0].Value is CustomAttributeArgument[] array)
        {
            return array.Select(a => a.Value as string).ToArray();
        }

        return null;
    }

    /// <summary>Takes the next <paramref name="count"/> tuple element names, advancing the cursor.</summary>
    /// <param name="count">The number of element names needed.</param>
    /// <returns>The names; entries are <see langword="null"/> when unnamed or unavailable.</returns>
    internal string?[] TakeTupleNames(int count)
    {
        var result = new string?[count];
        for (var i = 0; i < count; i++)
        {
            if (_tupleNames != null && _nameIndex < _tupleNames.Length)
            {
                result[i] = _tupleNames[_nameIndex];
            }

            _nameIndex++;
        }

        return result;
    }

    /// <summary>Returns the flag for the next type position and advances the cursor.</summary>
    /// <remarks>
    ///     When a per-position flag array is present it is consumed in order. Once the array is
    ///     exhausted (or when there is no array), every further call returns the context default
    ///     passed at construction (the single-byte attribute value, or the inherited
    ///     <c>NullableContext</c>), never 0 or the last array value.
    /// </remarks>
    /// <returns>0 (oblivious), 1 (not annotated), or 2 (annotated).</returns>
    internal byte Next()
    {
        if (_flags != null && _index < _flags.Length)
        {
            return _flags[_index++];
        }

        return _default;
    }
}
