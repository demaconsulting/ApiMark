using ApiMark.Core;
using Mono.Cecil;

namespace ApiMark.DotNet;

/// <summary>Generates Markdown API documentation from a .NET assembly using Mono.Cecil.</summary>
/// <remarks>
///     Implements <see cref="IApiGenerator"/> for C#/.NET assemblies. Reads assembly metadata
///     via Mono.Cecil without loading the target assembly into the current process, which ensures
///     reliable operation against arbitrary build outputs and dependency graphs. Pairs the metadata
///     with developer-authored content from the XML documentation file to produce a gradual-disclosure
///     Markdown tree. Not thread-safe; construct and use one instance per generation run.
/// </remarks>
public sealed class DotNetGenerator : IApiGenerator
{
    /// <summary>Configuration controlling which assembly, XML doc, and visibility filter to use.</summary>
    private readonly DotNetGeneratorOptions _options;

    /// <summary>Initializes a new instance of <see cref="DotNetGenerator"/> with the specified options.</summary>
    /// <remarks>
    ///     No file system access occurs at construction time; all I/O is deferred to <see cref="Parse"/>.
    /// </remarks>
    /// <param name="options">The generator configuration options.</param>
    public DotNetGenerator(DotNetGeneratorOptions options)
    {
        _options = options;
    }

    /// <summary>
    ///     Parses the configured .NET assembly and returns an emitter ready to produce
    ///     Markdown documentation in the requested format.
    /// </summary>
    /// <remarks>
    ///     Opens the assembly via Mono.Cecil, builds an <see cref="XmlDocReader"/> index from
    ///     the XML documentation file, then collects type and namespace data needed for
    ///     subsequent <see cref="IApiEmitter.Emit"/> calls. The
    ///     <see cref="AssemblyDefinition"/> remains open until <see cref="IApiEmitter.Emit"/>
    ///     completes and is then disposed.
    ///     <para>
    ///         The entrypoint <c>api.md</c> lists only root namespaces followed by a file naming
    ///         and path convention appendix. Each namespace page lists only its immediate child
    ///         namespaces and types, enabling gradual disclosure for AI consumers. Namespace-level
    ///         documentation is sourced from the <c>NamespaceDoc</c> convention: an
    ///         <c>internal static class NamespaceDoc</c> in a namespace carries the namespace
    ///         summary and is excluded from type listings.
    ///     </para>
    /// </remarks>
    /// <param name="context">
    ///     Output channel for informational and error messages. Must not be null. Reserved for
    ///     future use — DotNetGenerator does not currently emit messages through this channel.
    /// </param>
    /// <returns>
    ///     An <see cref="IApiEmitter"/> holding all data required to emit documentation
    ///     in any supported <see cref="OutputFormat"/>. The caller must subsequently
    ///     invoke <see cref="IApiEmitter.Emit"/> to write output.
    /// </returns>
    /// <exception cref="FileNotFoundException">Thrown when the XML documentation file does not exist.</exception>
    public IApiEmitter Parse(IContext context)
    {
        // Fail early if the XML doc is absent rather than producing empty output
        if (!File.Exists(_options.XmlDocPath))
        {
            throw new FileNotFoundException("XML documentation file not found.", _options.XmlDocPath);
        }

        var xmlDocs = new XmlDocReader(_options.XmlDocPath);
        var assembly = AssemblyDefinition.ReadAssembly(_options.AssemblyPath);

        // Build namespace descriptions from the NamespaceDoc convention before filtering
        // visible types so that NamespaceDoc types are available for summary extraction
        // even though they are excluded from the public type listing
        var namespaceDocTypes = assembly.MainModule.Types
            .Where(DotNetEmitter.IsNamespaceDocCarrier)
            .ToList();
        var namespaceDocTypeSet = namespaceDocTypes.ToHashSet();

        var namespaceDescriptions = namespaceDocTypes
            .GroupBy(t => t.Namespace, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(t => xmlDocs.GetSummary(DotNetEmitter.BuildTypeId(t)))
                    .FirstOrDefault(summary => !string.IsNullOrEmpty(summary)),
                StringComparer.Ordinal);

        // Collect all types that pass the visibility, obsolete, and compiler-generated filters.
        // Exclude NamespaceDoc types — they are documentation carriers, not user-facing types.
        var visibleTypes = assembly.MainModule.Types
            .Where(t => !DotNetEmitter.IsCompilerGenerated(t))
            .Where(t => !namespaceDocTypeSet.Contains(t))
            .Where(t => !t.IsNested && _options.Visibility switch
            {
                ApiVisibility.Public => t.IsPublic,
                ApiVisibility.PublicAndProtected => t.IsPublic || t.IsNestedFamily || t.IsNestedFamilyOrAssembly,
                ApiVisibility.All => true,
                _ => t.IsPublic,
            })
            .Where(t => _options.IncludeObsolete || !DotNetEmitter.IsObsolete(t))
            .ToList();

        // Group by namespace and sort for deterministic output
        var byNamespace = visibleTypes
            .GroupBy(t => t.Namespace)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.Name).ToList());

        var allNamespaces = byNamespace.Keys.OrderBy(n => n).ToList();

        // Root namespaces: those not prefixed by any other namespace present in the assembly
        var rootNamespaces = allNamespaces
            .Where(n => !allNamespaces.Any(
                other => !string.Equals(other, n, StringComparison.Ordinal) &&
                         n.StartsWith(other + ".", StringComparison.Ordinal)))
            .OrderBy(n => n)
            .ToList();

        var resolver = new TypeLinkResolver(rootNamespaces);

        return new DotNetEmitter(new DotNetAstModel(
            assembly,
            xmlDocs,
            allNamespaces,
            byNamespace,
            rootNamespaces,
            namespaceDescriptions,
            resolver,
            _options));
    }
}
