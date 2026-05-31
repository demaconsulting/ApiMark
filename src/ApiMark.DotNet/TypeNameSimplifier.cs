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
    /// <param name="typeRef">The Mono.Cecil type reference to simplify.</param>
    /// <param name="contextNamespace">The namespace of the enclosing type, used for prefix stripping.</param>
    /// <returns>A simplified C# type name without a trailing nullable-reference <c>?</c>.</returns>
    private static string SimplifyCore(TypeReference typeRef, string contextNamespace)
    {
        // Rule 1: primitive check only applies to non-composite types
        if (typeRef is not GenericInstanceType && typeRef is not ArrayType)
        {
            if (Primitives.TryGetValue(typeRef.FullName, out var alias))
            {
                return alias;
            }
        }

        // Rule 2: array types recurse on the element type
        if (typeRef is ArrayType arr)
        {
            return Simplify(arr.ElementType, contextNamespace) + "[]";
        }

        // Rule 3: Nullable<T> is represented as T?
        if (typeRef is GenericInstanceType git &&
            git.ElementType.FullName.StartsWith("System.Nullable", StringComparison.Ordinal) &&
            git.GenericArguments.Count == 1)
        {
            return Simplify(git.GenericArguments[0], contextNamespace) + "?";
        }

        // Rules 4 & 6: generic types — strip well-known namespace or context namespace, then list args
        if (typeRef is GenericInstanceType genericInstance)
        {
            var baseName = StripArity(genericInstance.ElementType.Name);
            var ns = genericInstance.ElementType.Namespace;

            if (!WellKnownNamespaces.Contains(ns))
            {
                baseName = ApplyContextStrip(genericInstance.ElementType, contextNamespace);
                baseName = StripArity(baseName);
            }

            var args = string.Join(", ", genericInstance.GenericArguments
                .Select(a => Simplify(a, contextNamespace)));
            return $"{baseName}<{args}>";
        }

        // Rule 5: plain named type — strip the context namespace prefix when present
        if (!string.IsNullOrEmpty(typeRef.Namespace))
        {
            if (typeRef.Namespace == contextNamespace)
            {
                return StripArity(typeRef.Name);
            }

            if (typeRef.Namespace.StartsWith(contextNamespace + ".", StringComparison.Ordinal))
            {
                var relative = typeRef.Namespace.Substring(contextNamespace.Length + 1);
                return $"{relative}.{StripArity(typeRef.Name)}";
            }
        }

        return StripArity(typeRef.Name);
    }

    /// <summary>Removes the generic arity suffix (e.g. <c>`1</c>) from a type name.</summary>
    /// <param name="name">The raw type name that may contain a backtick arity suffix.</param>
    /// <returns>The name without the arity suffix.</returns>
    private static string StripArity(string name)
    {
        var tick = name.IndexOf('`');
        return tick >= 0 ? name.Substring(0, tick) : name;
    }

    /// <summary>
    ///     Returns the shortest unambiguous name for <paramref name="typeRef"/> relative to
    ///     <paramref name="contextNamespace"/>, stripping that namespace's prefix if present.
    /// </summary>
    /// <param name="typeRef">The type reference to shorten.</param>
    /// <param name="contextNamespace">The namespace of the enclosing type.</param>
    /// <returns>The shortest name that is unambiguous in the given context.</returns>
    private static string ApplyContextStrip(TypeReference typeRef, string contextNamespace)
    {
        if (string.IsNullOrEmpty(typeRef.Namespace))
        {
            return typeRef.Name;
        }

        if (typeRef.Namespace == contextNamespace)
        {
            return typeRef.Name;
        }

        if (typeRef.Namespace.StartsWith(contextNamespace + ".", StringComparison.Ordinal))
        {
            var relative = typeRef.Namespace.Substring(contextNamespace.Length + 1);
            return $"{relative}.{typeRef.Name}";
        }

        return typeRef.Name;
    }
}
