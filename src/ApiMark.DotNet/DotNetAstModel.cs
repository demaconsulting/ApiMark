using ApiMark.Core;
using Mono.Cecil;

namespace ApiMark.DotNet;

/// <summary>
///     Bundles the per-type-page writing context that is constant across all member
///     pages generated for a single type. Used to reduce parameter counts on the
///     helper methods that emit individual member pages and table rows.
/// </summary>
internal sealed record TypePageWriteContext(
    IMarkdownWriterFactory Factory,
    string NamespaceName,
    string NamespaceFolderPath,
    TypeDefinition Type,
    XmlDocReader XmlDocs,
    TypeLinkResolver Resolver);

/// <summary>
///     Bundles the per-method documentation writing context passed to
///     <see cref="DotNetEmitterGradualDisclosure"/> so that callers do not need to
///     thread five constant parameters through each call site.
/// </summary>
internal sealed record MethodDocContext(
    string NamespaceName,
    XmlDocReader XmlDocs,
    TypeLinkResolver Resolver,
    string CurrentFolder,
    ISet<ExternalTypeInfo> ExternalTypes);

/// <summary>
///     Bundles the per-assembly namespace documentation context that is constant
///     across all namespace page writes in a single generation run.
/// </summary>
internal sealed record NamespaceDocContext(
    List<string> AllNamespaces,
    Dictionary<string, List<TypeDefinition>> ByNamespace,
    List<string> RootNamespaces,
    IReadOnlyDictionary<string, string?> NamespaceDescriptions,
    XmlDocReader XmlDocs,
    TypeLinkResolver Resolver);

/// <summary>
///     Holds all pre-parsed .NET assembly data needed during the emit phase.
/// </summary>
/// <remarks>
///     Created exclusively by <see cref="DotNetGenerator.Parse"/> and passed to
///     <see cref="DotNetEmitter"/>. All properties are read-only after construction.
/// </remarks>
internal sealed class DotNetAstModel
{
    /// <summary>
    ///     Initializes a new <see cref="DotNetAstModel"/> with all data required for emission.
    /// </summary>
    /// <param name="assembly">Assembly definition; ownership is transferred to this model.</param>
    /// <param name="xmlDocs">Pre-built XML documentation reader.</param>
    /// <param name="allNamespaces">All namespace names in alphabetical order.</param>
    /// <param name="byNamespace">Visible types grouped by namespace.</param>
    /// <param name="rootNamespaces">Root namespaces identified during parse.</param>
    /// <param name="namespaceDescriptions">Namespace summaries from NamespaceDoc carriers.</param>
    /// <param name="resolver">Type link resolver for gradual-disclosure output.</param>
    /// <param name="options">Generator configuration options.</param>
    internal DotNetAstModel(
        AssemblyDefinition assembly,
        XmlDocReader xmlDocs,
        List<string> allNamespaces,
        Dictionary<string, List<TypeDefinition>> byNamespace,
        List<string> rootNamespaces,
        IReadOnlyDictionary<string, string?> namespaceDescriptions,
        TypeLinkResolver resolver,
        DotNetGeneratorOptions options)
    {
        Assembly = assembly;
        XmlDocs = xmlDocs;
        AllNamespaces = allNamespaces;
        ByNamespace = byNamespace;
        RootNamespaces = rootNamespaces;
        NamespaceDescriptions = namespaceDescriptions;
        Resolver = resolver;
        Options = options;
    }

    /// <summary>Gets the assembly definition held open for the duration of emit.</summary>
    internal AssemblyDefinition Assembly { get; }

    /// <summary>Gets the XML documentation reader for member-level lookups.</summary>
    internal XmlDocReader XmlDocs { get; }

    /// <summary>Gets all namespace names present in the assembly, ordered alphabetically.</summary>
    internal List<string> AllNamespaces { get; }

    /// <summary>Gets the visible types grouped by their namespace name.</summary>
    internal Dictionary<string, List<TypeDefinition>> ByNamespace { get; }

    /// <summary>Gets the root namespaces identified in the assembly.</summary>
    internal List<string> RootNamespaces { get; }

    /// <summary>Gets the optional namespace descriptions sourced from NamespaceDoc carriers.</summary>
    internal IReadOnlyDictionary<string, string?> NamespaceDescriptions { get; }

    /// <summary>Gets the type link resolver for gradual-disclosure output.</summary>
    internal TypeLinkResolver Resolver { get; }

    /// <summary>Gets the generator configuration options.</summary>
    internal DotNetGeneratorOptions Options { get; }
}
