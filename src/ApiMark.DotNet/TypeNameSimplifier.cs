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
    ///     Seven simplification rules are applied in a fixed priority order: (1) C# primitive aliases,
    ///     (2) array bracket notation, (3) Nullable&lt;T&gt; → T?, (4) well-known namespace stripping,
    ///     (5) context namespace prefix stripping, (6) recursive generic argument simplification, and
    ///     (7) nullable reference annotation suffix. Stateless and thread-safe; no shared mutable state
    ///     is modified during the call.
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
    public static string Simplify(TypeReference typeRef, string contextNamespace, bool isNullableAnnotated = false)
    {
        var name = SimplifyCore(typeRef, contextNamespace);

        // Rule 7: nullable reference type annotation. Value types use Nullable<T> (Rule 3) for
        // their ? suffix; this rule applies only to reference types carrying NullableAttribute(2).
        if (isNullableAnnotated && !typeRef.IsValueType)
        {
            name += "?";
        }

        return name;
    }

    /// <summary>Applies Rules 1–6 to produce a simplified type name without nullable-reference annotation.</summary>
    /// <remarks>
    ///     Exists as a named helper so that <see cref="Simplify"/> can apply Rule 7 (nullable reference
    ///     annotation) as a single post-processing step without duplicating the core switch logic.
    ///     The switch-expression arms are ordered by structural specificity: composite types (array,
    ///     nullable generic, generic instance) are tested before primitive aliases and plain types to
    ///     ensure that, for example, <c>int?</c> (Nullable&lt;int&gt;) is handled by Rule 3 rather than
    ///     the primitive-alias arm.
    /// </remarks>
    /// <param name="typeRef">The Mono.Cecil type reference to simplify.</param>
    /// <param name="contextNamespace">The namespace of the enclosing type, used for prefix stripping.</param>
    /// <returns>A simplified C# type name without a trailing nullable-reference <c>?</c>.</returns>
    private static string SimplifyCore(TypeReference typeRef, string contextNamespace)
    {
        return typeRef switch
        {
            // Rule 2: array types recurse on the element type; rank-aware suffix (e.g., [] for 1-D, [,] for 2-D)
            ArrayType arr
                => Simplify(arr.ElementType, contextNamespace) + "[" + new string(',', arr.Rank - 1) + "]",

            // Rule 3: Nullable<T> is represented as T?
            GenericInstanceType { GenericArguments.Count: 1 } git
                when git.ElementType.FullName.StartsWith("System.Nullable", StringComparison.Ordinal)
                => Simplify(git.GenericArguments[0], contextNamespace) + "?",

            // Rules 4 & 6: generic types — strip well-known namespace or context namespace, then list args
            GenericInstanceType git
                => BuildGenericName(git, contextNamespace),

            // Rule 1: C# primitive aliases (only non-composite types reach this arm)
            _ when Primitives.TryGetValue(typeRef.FullName, out var alias)
                => alias,

            // Rule 5: plain named type — strip context namespace prefix when present
            { Namespace: var ns } when ns == contextNamespace
                => StripArity(typeRef.Name),

            { Namespace: var ns } when !string.IsNullOrEmpty(ns) && ns.StartsWith(contextNamespace + ".", StringComparison.Ordinal)
                => $"{ns.Substring(contextNamespace.Length + 1)}.{StripArity(typeRef.Name)}",

            _ => StripArity(typeRef.Name),
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
    /// <returns>A string of the form <c>Name&lt;Arg1, Arg2&gt;</c>.</returns>
    private static string BuildGenericName(GenericInstanceType git, string contextNamespace)
    {
        var ns = git.ElementType.Namespace;
        var baseName = WellKnownNamespaces.Contains(ns)
            ? StripArity(git.ElementType.Name)
            : StripArity(ApplyContextStrip(git.ElementType, contextNamespace));
        var args = string.Join(", ", git.GenericArguments.Select(a => Simplify(a, contextNamespace)));
        return $"{baseName}<{args}>";
    }

    /// <summary>Removes the generic arity suffix (e.g. <c>`1</c>) from a type name.</summary>
    /// <remarks>
    ///     Used wherever the arity count carries no meaning for human readers — for example, in
    ///     display names, external-type table entries, and type-page headings. The backtick and
    ///     following digit(s) are dropped entirely. Contrast with <see cref="FlattenArity"/>,
    ///     which removes only the backtick but preserves the digit so that file-system path
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
    ///     Converts the IL backtick arity suffix to a plain numeric suffix, producing a
    ///     file-system-safe name that still distinguishes generic types by parameter count.
    /// </summary>
    /// <remarks>
    ///     For example, <c>Foo`2</c> becomes <c>Foo2</c> and <c>Foo</c> is unchanged.
    ///     This avoids the collision that <see cref="StripArity"/> would cause when both
    ///     <c>Foo</c> and <c>Foo&lt;T&gt;</c> exist in the same namespace.
    /// </remarks>
    /// <param name="name">The raw IL type name that may contain a backtick arity suffix.</param>
    /// <returns>The name with the backtick removed but the arity count preserved.</returns>
    internal static string FlattenArity(string name)
    {
        var tick = name.IndexOf('`');
        return tick >= 0 ? string.Concat(name.AsSpan(0, tick), name.AsSpan(tick + 1)) : name;
    }

    /// <summary>
    ///     Returns the shortest unambiguous name for <paramref name="typeRef"/> relative to
    ///     <paramref name="contextNamespace"/>, stripping that namespace's prefix if present.
    /// </summary>
    /// <remarks>
    ///     Exists as a named helper to encapsulate the context-stripping logic that is shared
    ///     between the plain-type arm of <see cref="SimplifyCore"/> and the generic-container
    ///     name computation in <see cref="BuildGenericName"/>. Returning the raw type name when
    ///     no prefix matches is the safe fallback — it avoids hiding a type behind an incorrect
    ///     short name when the namespace relationship is ambiguous.
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

        if (ns.StartsWith(contextNamespace + ".", StringComparison.Ordinal))
        {
            return $"{ns.Substring(contextNamespace.Length + 1)}.{typeRef.Name}";
        }

        return typeRef.Name;
    }
}
