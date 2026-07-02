// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.Core;
using Mono.Cecil;

namespace ApiMark.DotNet;

/// <summary>
///     Bundles the per-type-page writing context that is constant across all member
///     pages generated for a single type. Used to reduce parameter counts on the
///     helper methods that emit individual member pages and table rows.
/// </summary>
/// <param name="Factory">Factory for creating per-file Markdown writers.</param>
/// <param name="NamespaceName">Full namespace name of the type being documented.</param>
/// <param name="NamespaceFolderPath">Pre-computed file-system folder path for the namespace.</param>
/// <param name="Type">Type definition whose pages are being generated.</param>
/// <param name="XmlDocs">Documentation index for member-level lookups.</param>
/// <param name="Resolver">Type link resolver for table cell generation.</param>
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
/// <param name="NamespaceName">Namespace of the type that owns the method.</param>
/// <param name="XmlDocs">Documentation index for member-level lookups.</param>
/// <param name="Resolver">Type link resolver for table cell generation.</param>
/// <param name="CurrentFolder">Folder path of the containing Markdown file, relative to the documentation output root.</param>
/// <param name="ExternalTypes">Mutable accumulator for external type references found during table cell generation.</param>
internal sealed record MethodDocContext(
    string NamespaceName,
    XmlDocReader XmlDocs,
    TypeLinkResolver Resolver,
    string CurrentFolder,
    ISet<ExternalTypeInfo> ExternalTypes);

/// <summary>
///     Bundles the namespace-level documentation sourced from a NamespaceDoc carrier
///     class, carrying the summary, remarks, and structured example parts so that all
///     three surface on namespace output in the same way they do for types.
/// </summary>
/// <param name="Summary">Single-line namespace summary, or <c>null</c> when absent.</param>
/// <param name="Remarks">Namespace remarks text, or <c>null</c> when absent.</param>
/// <param name="ExampleParts">
///     Structured example parts, each flagged as code or prose; empty when no
///     <c>&lt;example&gt;</c> is present on the carrier.
/// </param>
internal sealed record NamespaceDescription(
    string? Summary,
    string? Remarks,
    IReadOnlyList<(bool IsCode, string Content)> ExampleParts);

/// <summary>
///     Bundles the per-assembly namespace documentation context that is constant
///     across all namespace page writes in a single generation run.
/// </summary>
/// <param name="AllNamespaces">All namespace names present in the assembly, ordered alphabetically.</param>
/// <param name="ByNamespace">Visible types grouped by their namespace name.</param>
/// <param name="RootNamespaces">Root namespaces identified during parse.</param>
/// <param name="NamespaceDescriptions">Optional namespace descriptions sourced from NamespaceDoc carriers.</param>
/// <param name="XmlDocs">Documentation index for namespace-level lookups.</param>
/// <param name="Resolver">Type link resolver for namespace page table cells.</param>
internal sealed record NamespaceDocContext(
    IReadOnlyList<string> AllNamespaces,
    IReadOnlyDictionary<string, IReadOnlyList<TypeDefinition>> ByNamespace,
    IReadOnlyList<string> RootNamespaces,
    IReadOnlyDictionary<string, NamespaceDescription> NamespaceDescriptions,
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
    /// <param name="namespaceDescriptions">Namespace descriptions from NamespaceDoc carriers.</param>
    /// <param name="resolver">Type link resolver for gradual-disclosure output.</param>
    /// <param name="options">Generator configuration options.</param>
    internal DotNetAstModel(
        AssemblyDefinition assembly,
        XmlDocReader xmlDocs,
        IReadOnlyList<string> allNamespaces,
        IReadOnlyDictionary<string, IReadOnlyList<TypeDefinition>> byNamespace,
        IReadOnlyList<string> rootNamespaces,
        IReadOnlyDictionary<string, NamespaceDescription> namespaceDescriptions,
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
    internal IReadOnlyList<string> AllNamespaces { get; }

    /// <summary>Gets the visible types grouped by their namespace name.</summary>
    internal IReadOnlyDictionary<string, IReadOnlyList<TypeDefinition>> ByNamespace { get; }

    /// <summary>Gets the root namespaces identified in the assembly.</summary>
    internal IReadOnlyList<string> RootNamespaces { get; }

    /// <summary>Gets the optional namespace descriptions sourced from NamespaceDoc carriers.</summary>
    internal IReadOnlyDictionary<string, NamespaceDescription> NamespaceDescriptions { get; }

    /// <summary>Gets the type link resolver for gradual-disclosure output.</summary>
    internal TypeLinkResolver Resolver { get; }

    /// <summary>Gets the generator configuration options.</summary>
    internal DotNetGeneratorOptions Options { get; }
}
