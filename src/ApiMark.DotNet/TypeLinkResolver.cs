// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using Mono.Cecil;

namespace ApiMark.DotNet;

/// <summary>
///     Resolves Mono.Cecil <see cref="TypeReference"/> instances to Markdown link
///     text suitable for table cells in the generated API documentation.
/// </summary>
/// <remarks>
///     Linkification is applied only in table cells — never inside fenced code blocks,
///     because Markdown links do not render inside fences. Three outcomes are possible
///     for each type reference:
///     <list type="bullet">
///       <item>
///         <term>Intra-assembly type</term>
///         <description>
///             Detected when <see cref="TypeReference.Scope"/> is a
///             <see cref="ModuleDefinition"/>, meaning the type is declared inside the
///             assembly being documented. A relative Markdown link to the type's page is
///             emitted.
///         </description>
///       </item>
///       <item>
///         <term>C# primitive or System.* type</term>
///         <description>
///             Emitted as plain text using <see cref="TypeNameSimplifier.Simplify"/>
///             and NOT tracked as an external type.
///         </description>
///       </item>
///       <item>
///         <term>Non-System external type</term>
///         <description>
///             Emitted as plain text and added to the caller-supplied
///             <see cref="ExternalTypeInfo"/> set for later emission in the
///             "External Types" section.
///         </description>
///       </item>
///     </list>
///     Stateless with respect to the type-link resolution itself; mutable state is
///     carried only via the caller-supplied <see cref="ISet{T}"/> parameter.
///     Thread-safe for concurrent resolution of different type references when each
///     caller supplies its own <see cref="ISet{T}"/> instance.
/// </remarks>
internal sealed class TypeLinkResolver
{
    /// <summary>
    ///     Full CLR names of C# primitive types that are always rendered as their keyword
    ///     alias and never tracked as external dependencies.
    /// </summary>
    private static readonly HashSet<string> PrimitiveFullNames = new(StringComparer.Ordinal)
    {
        "System.Boolean", "System.Byte", "System.SByte", "System.Int16", "System.UInt16",
        "System.Int32", "System.UInt32", "System.Int64", "System.UInt64", "System.Single",
        "System.Double", "System.Decimal", "System.Char", "System.String", "System.Object",
        "System.Void", "System.IntPtr", "System.UIntPtr",
    };

    /// <summary>Root namespaces of the assembly, used to derive relative documentation paths.</summary>
    private readonly IReadOnlyList<string> _rootNamespaces;

    /// <summary>
    ///     When <see langword="false"/>, intra-assembly type references are rendered as plain
    ///     text instead of Markdown links. Used by the single-file emitter where relative file
    ///     links are meaningless and same-name anchors across types would collide.
    /// </summary>
    private readonly bool _generateLinks;

    /// <summary>
    ///     Initializes a new instance of <see cref="TypeLinkResolver"/> with the assembly root namespaces.
    /// </summary>
    /// <param name="rootNamespaces">
    ///     The root namespaces identified in the assembly being documented. These are forwarded to
    ///     <see cref="DotNetEmitter.GetNamespaceFolderPath"/> to derive the documentation path for
    ///     each referenced type.
    /// </param>
    /// <param name="generateLinks">
    ///     When <see langword="true"/> (default), intra-assembly types are rendered as relative
    ///     Markdown links. When <see langword="false"/>, all types are rendered as plain text —
    ///     used in single-file output where file-relative links are invalid.
    /// </param>
    public TypeLinkResolver(IReadOnlyList<string> rootNamespaces, bool generateLinks = true)
    {
        _rootNamespaces = rootNamespaces;
        _generateLinks = generateLinks;
    }

    /// <summary>
    ///     Resolves <paramref name="typeRef"/> to a Markdown link when it is an intra-assembly
    ///     type, plain text when it is a primitive or System type, or plain text with external
    ///     tracking when it is a non-System type from another assembly.
    /// </summary>
    /// <param name="typeRef">The Mono.Cecil type reference to resolve. Must not be null.</param>
    /// <param name="currentFolder">
    ///     The folder path of the Markdown file that will contain the link, relative to the
    ///     documentation output root (e.g. <c>ApiMark.DotNet.Fixtures/SampleClass</c>).
    ///     Used to compute relative path hrefs. Pass an empty string for root-level files.
    /// </param>
    /// <param name="contextNamespace">
    ///     The namespace of the type that owns this reference. Forwarded to
    ///     <see cref="TypeNameSimplifier.Simplify"/> so that same-namespace names are
    ///     displayed without their namespace prefix.
    /// </param>
    /// <param name="externalTypes">
    ///     Mutable set that accumulates non-System external type references found during
    ///     resolution. The caller creates this set per output file and emits the "External
    ///     Types" section after all table rows have been written.
    /// </param>
    /// <param name="isNullableAnnotated">
    ///     When <see langword="true"/>, the member carrying this type reference has a
    ///     nullable-reference annotation. See
    ///     <see cref="TypeNameSimplifier.Simplify"/> for details on when to pass
    ///     <see langword="true"/>.
    /// </param>
    /// <returns>
    ///     A Markdown string: either a link of the form <c>[Name](relative/path.md)</c>,
    ///     a plain simplified name, or a generic composite such as
    ///     <c>[Container](path.md)\&lt;Arg\&gt;</c>.
    /// </returns>
    public string Linkify(
        TypeReference typeRef,
        string currentFolder,
        string contextNamespace,
        ISet<ExternalTypeInfo> externalTypes,
        bool isNullableAnnotated = false)
    {
        if (typeRef == null)
        {
            return string.Empty;
        }

        // Generic type parameters (e.g. T, TKey) are not real types — render as plain text
        if (typeRef is GenericParameter genericParam)
        {
            return genericParam.Name;
        }

        // Handle Nullable<T> → T? by recursing on the inner type with the nullable flag set
        if (typeRef is GenericInstanceType gitNullable &&
            typeRef.FullName.StartsWith("System.Nullable`1<", StringComparison.Ordinal))
        {
            var inner = gitNullable.GenericArguments[0];
            return Linkify(inner, currentFolder, contextNamespace, externalTypes, true);
        }

        // Handle array types by resolving the element type and appending "[]" (plus "?" when nullable)
        if (typeRef is ArrayType arrayType)
        {
            var elementText = Linkify(arrayType.ElementType, currentFolder, contextNamespace, externalTypes);
            return isNullableAnnotated ? elementText + "[]?" : elementText + "[]";
        }

        // Handle generic instance types: linkify the container when intra-assembly, else track external
        if (typeRef is GenericInstanceType genericType)
        {
            return LinkifyGenericType(genericType, currentFolder, contextNamespace, externalTypes, isNullableAnnotated);
        }

        // Primitives and Nullable<> open type render as plain C# aliases, never as external types
        if (PrimitiveFullNames.Contains(typeRef.FullName) ||
            typeRef.FullName == "System.Nullable`1")
        {
            var alias = TypeNameSimplifier.Simplify(typeRef, contextNamespace);
            return isNullableAnnotated ? alias + "?" : alias;
        }

        // Intra-assembly types get a relative Markdown link to their documentation page
        if (IsIntraAssembly(typeRef))
        {
            return LinkifyIntraAssemblyType(typeRef, currentFolder, isNullableAnnotated);
        }

        // Non-System external types are tracked for the External Types section
        TrackExternalType(typeRef, externalTypes);
        var name = TypeNameSimplifier.StripArity(typeRef.Name);
        return isNullableAnnotated ? name + "?" : name;
    }

    /// <summary>
    ///     Resolves a generic instance type to a Markdown cell value, linking the container
    ///     when it is intra-assembly and tracking it as external otherwise.
    /// </summary>
    /// <param name="genericType">The generic instance type to resolve.</param>
    /// <param name="currentFolder">Folder path of the containing Markdown file.</param>
    /// <param name="contextNamespace">Namespace context for name simplification.</param>
    /// <param name="externalTypes">Accumulator for external type references.</param>
    /// <param name="isNullableAnnotated">Whether the nullable-reference annotation is present.</param>
    /// <returns>A Markdown cell string for the generic type.</returns>
    private string LinkifyGenericType(
        GenericInstanceType genericType,
        string currentFolder,
        string contextNamespace,
        ISet<ExternalTypeInfo> externalTypes,
        bool isNullableAnnotated)
    {
        var containerName = TypeNameSimplifier.StripArity(genericType.Name);
        var argTexts = genericType.GenericArguments
            .Select(arg => Linkify(arg, currentFolder, contextNamespace, externalTypes))
            .ToList();
        var argsText = string.Join(", ", argTexts);
        var suffix = isNullableAnnotated ? "?" : string.Empty;

        if (IsIntraAssembly(genericType))
        {
            if (_generateLinks)
            {
                var pageKey = GetTypePageKey(genericType);
                var relativePath = ComputeRelativePath(currentFolder, pageKey);
                var containerLink = $"[{containerName}]({relativePath})";
                return $"{containerLink}\\<{argsText}\\>{suffix}";
            }

            return $"{containerName}\\<{argsText}\\>{suffix}";
        }

        TrackExternalType(genericType, externalTypes);
        return $"{containerName}\\<{argsText}\\>{suffix}";
    }

    /// <summary>
    ///     Resolves a simple intra-assembly type reference to a Markdown link cell.
    /// </summary>
    /// <param name="typeRef">The intra-assembly type reference.</param>
    /// <param name="currentFolder">Folder path of the containing Markdown file.</param>
    /// <param name="isNullableAnnotated">Whether the nullable-reference annotation is present.</param>
    /// <returns>A Markdown link of the form <c>[Name](relative/path.md)</c>.</returns>
    private string LinkifyIntraAssemblyType(TypeReference typeRef, string currentFolder, bool isNullableAnnotated)
    {
        var simpleName = TypeNameSimplifier.StripArity(typeRef.Name);
        if (!_generateLinks)
        {
            return isNullableAnnotated ? simpleName + "?" : simpleName;
        }

        var pageKey = GetTypePageKey(typeRef);
        var relativePath = ComputeRelativePath(currentFolder, pageKey);
        return isNullableAnnotated
            ? $"[{simpleName}]({relativePath})?"
            : $"[{simpleName}]({relativePath})";
    }

    /// <summary>
    ///     Returns <see langword="true"/> when <paramref name="typeRef"/> is declared in the
    ///     assembly being documented, identified by its scope being a <see cref="ModuleDefinition"/>.
    /// </summary>
    /// <param name="typeRef">The type reference to test.</param>
    /// <returns>
    ///     <see langword="true"/> when the type is in the current module; <see langword="false"/>
    ///     when it is from an external assembly.
    /// </returns>
    private static bool IsIntraAssembly(TypeReference typeRef) =>
        typeRef.Scope is ModuleDefinition;

    /// <summary>
    ///     Computes the documentation page key (relative path without extension) for a type.
    /// </summary>
    /// <param name="typeRef">The type whose page key to compute.</param>
    /// <returns>A forward-slash-separated page key such as <c>MyLib/MyType</c>.</returns>
    private string GetTypePageKey(TypeReference typeRef)
    {
        var folder = DotNetEmitter.GetNamespaceFolderPath(typeRef.Namespace, _rootNamespaces);
        var name = TypeNameSimplifier.FlattenArity(typeRef.Name);
        return folder.Length > 0 ? $"{folder}/{name}" : name;
    }

    /// <summary>
    ///     Computes a relative path from a source folder to a target page file.
    /// </summary>
    /// <param name="currentFolder">
    ///     The folder containing the Markdown file that will contain the link.
    ///     An empty string is treated as the root (current directory <c>.</c>).
    /// </param>
    /// <param name="targetKey">
    ///     The page key of the link target (e.g. <c>MyLib/MyType</c>). The <c>.md</c>
    ///     extension is appended during path computation.
    /// </param>
    /// <returns>
    ///     A forward-slash-separated relative path suitable for a Markdown link href
    ///     (e.g. <c>../MyType.md</c> or <c>MyType.md</c>).
    /// </returns>
    private static string ComputeRelativePath(string currentFolder, string targetKey)
    {
        var from = currentFolder.Length > 0 ? currentFolder : ".";
        return Path.GetRelativePath(from, targetKey + ".md").Replace('\\', '/');
    }

    /// <summary>
    ///     Records <paramref name="typeRef"/> as an external type in <paramref name="externalTypes"/>
    ///     when its namespace does not start with <c>System</c>.
    /// </summary>
    /// <remarks>
    ///     System-namespace types (e.g. <c>System.IO.Stream</c>) are excluded because they are
    ///     part of the .NET runtime and do not require readers to install separate packages.
    ///     Generic instance types record the open-generic form with escaped angle brackets so
    ///     that the External Types table shows a readable representation.
    /// </remarks>
    /// <param name="typeRef">The type reference to evaluate.</param>
    /// <param name="externalTypes">The set to add to when the type qualifies.</param>
    private static void TrackExternalType(TypeReference typeRef, ISet<ExternalTypeInfo> externalTypes)
    {
        if (typeRef.Namespace == "System" ||
            typeRef.Namespace.StartsWith("System.", StringComparison.Ordinal))
        {
            return;
        }

        // Build a display name including escaped generic arguments for readability in the table
        string simpleName;
        if (typeRef is GenericInstanceType git)
        {
            var baseName = TypeNameSimplifier.StripArity(git.Name);
            var args = string.Join(", ", git.GenericArguments.Select(a => TypeNameSimplifier.StripArity(a.Name)));
            simpleName = $"{baseName}\\<{args}\\>";
        }
        else
        {
            simpleName = TypeNameSimplifier.StripArity(typeRef.Name);
        }

        externalTypes.Add(new ExternalTypeInfo(simpleName, typeRef.Namespace));
    }
}
