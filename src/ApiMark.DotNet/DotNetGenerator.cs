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
    /// <summary>Column header label used in all generated Markdown tables for the description column.</summary>
    private const string DescriptionColumnHeader = "Description";

    /// <summary>Placeholder emitted in description cells and paragraphs when no XML doc summary is available.</summary>
    private const string NoDescriptionPlaceholder = "*No description provided.*";

    /// <summary>The .NET metadata method name used for all instance and static constructors.</summary>
    private const string ConstructorMethodName = ".ctor";

    /// <summary>Configuration controlling which assembly, XML doc, and visibility filter to use.</summary>
    private readonly DotNetGeneratorOptions _options;

    /// <summary>Initializes a new instance of <see cref="DotNetGenerator"/> with the specified options.</summary>
    /// <remarks>
    ///     No file system access occurs at construction time; all I/O is deferred to <see cref="Generate"/>.
    /// </remarks>
    /// <param name="options">The generator configuration options.</param>
    public DotNetGenerator(DotNetGeneratorOptions options)
    {
        _options = options;
    }

    /// <summary>Generates API documentation into the provided writer factory.</summary>
    /// <remarks>
    ///     Opens the assembly via Mono.Cecil, builds an <see cref="XmlDocReader"/> index from
    ///     the XML documentation file, then writes the complete Markdown tree: one assembly
    ///     entrypoint page, one namespace summary per namespace, one type page per visible type,
    ///     and one detail page per complex member. The <see cref="AssemblyDefinition"/> is
    ///     disposed before this method returns.
    ///     <para>
    ///         The entrypoint <c>api.md</c> lists only root namespaces followed by a file naming
    ///         and path convention appendix. Each namespace page lists only its immediate child
    ///         namespaces and types, enabling gradual disclosure for AI consumers. Namespace-level
    ///         documentation is sourced from the <c>NamespaceDoc</c> convention: an
    ///         <c>internal static class NamespaceDoc</c> in a namespace carries the namespace
    ///         summary and is excluded from type listings.
    ///     </para>
    /// </remarks>
    /// <param name="factory">The markdown writer factory used to create output files.</param>
    /// <exception cref="FileNotFoundException">Thrown when the XML documentation file does not exist.</exception>
    public void Generate(IMarkdownWriterFactory factory)
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
        var namespaceDescriptions = assembly.MainModule.Types
            .Where(t => t.Name == "NamespaceDoc")
            .ToDictionary(
                t => t.Namespace,
                t => xmlDocs.GetSummary(BuildTypeId(t)),
                StringComparer.Ordinal);

        // Collect all types that pass the visibility, obsolete, and compiler-generated filters.
        // Exclude NamespaceDoc types — they are documentation carriers, not user-facing types.
        var visibleTypes = assembly.MainModule.Types
            .Where(t => !IsCompilerGenerated(t))
            .Where(t => t.Name != "NamespaceDoc")
            .Where(t => IsTypeVisible(t))
            .Where(t => _options.IncludeObsolete || !IsObsolete(t))
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

        // Write the top-level assembly index page with the assembly name as title
        using var apiWriter = factory.CreateMarkdown("", "api");

        // FIX 7: suffix the assembly name with "API Reference" for clarity
        apiWriter.WriteHeading(1, assembly.Name.Name + " API Reference");

        // Emit the assembly description when the AssemblyDescriptionAttribute is present
        var assemblyDescription = assembly.CustomAttributes
            .FirstOrDefault(a => a.AttributeType.FullName == "System.Reflection.AssemblyDescriptionAttribute")
            ?.ConstructorArguments.FirstOrDefault().Value as string;
        if (!string.IsNullOrWhiteSpace(assemblyDescription))
        {
            apiWriter.WriteParagraph(assemblyDescription);
        }

        // Root namespace table — only top-level entries; child namespaces are listed on their
        // parent's namespace page to keep api.md compact for AI consumers
        var nsHeaders = new[] { "Namespace", DescriptionColumnHeader };
        var nsRows = rootNamespaces.Select(nsName =>
        {
            var link = $"{nsName}.md";
            var description = namespaceDescriptions.TryGetValue(nsName, out var desc) && !string.IsNullOrEmpty(desc)
                ? desc
                : NoDescriptionPlaceholder;
            return new[] { $"[{nsName}]({link})", description };
        });
        apiWriter.WriteTable(nsHeaders, nsRows);

        // File naming and path convention appendix — placed at the end of api.md so that
        // the namespace table is immediately accessible without scrolling past prose
        apiWriter.WriteHeading(2, "File Naming and Path Convention");
        apiWriter.WriteParagraph(
            "Documentation paths are derived deterministically from fully-qualified symbol names.");
        var conventionHeaders = new[] { "Symbol kind", "Path pattern" };
        var conventionRows = new[]
        {
            new[] { "Root namespace", "`{Namespace}.md`" },
            new[] { "Child namespace", "`{ParentPath}/{ChildName}.md`" },
            new[] { "Type", "`{NamespacePath}/{TypeName}.md`" },
            new[] { "Member", "`{NamespacePath}/{TypeName}/{MemberName}.md`" },
        };
        apiWriter.WriteTable(conventionHeaders, conventionRows);

        // Write one namespace page per namespace, ordered so parents precede children
        foreach (var namespaceName in allNamespaces)
        {
            WriteNamespacePage(
                factory,
                namespaceName,
                allNamespaces,
                byNamespace,
                rootNamespaces,
                xmlDocs,
                namespaceDescriptions);
        }
    }

    /// <summary>
    ///     Writes the Markdown summary page for a single namespace, listing its immediate
    ///     child namespaces and the types declared directly in it.
    /// </summary>
    /// <param name="factory">Factory for creating output writers.</param>
    /// <param name="namespaceName">The full namespace name to document.</param>
    /// <param name="allNamespaces">All namespaces present in the assembly.</param>
    /// <param name="byNamespace">Types grouped by namespace name.</param>
    /// <param name="rootNamespaces">Namespaces that have no parent in this assembly.</param>
    /// <param name="xmlDocs">XML documentation index for summary lookups.</param>
    /// <param name="namespaceDescriptions">
    ///     Namespace-level summaries sourced from the <c>NamespaceDoc</c> convention,
    ///     keyed by fully-qualified namespace name.
    /// </param>
    private void WriteNamespacePage(
        IMarkdownWriterFactory factory,
        string namespaceName,
        List<string> allNamespaces,
        Dictionary<string, List<TypeDefinition>> byNamespace,
        List<string> rootNamespaces,
        XmlDocReader xmlDocs,
        IReadOnlyDictionary<string, string?> namespaceDescriptions)
    {
        var folderPath = GetNamespaceFolderPath(namespaceName, rootNamespaces);
        SplitPath(folderPath, out var subFolder, out var shortName);

        using var nsWriter = factory.CreateMarkdown(subFolder, shortName);
        nsWriter.WriteHeading(1, namespaceName);

        // Emit the namespace summary when one was supplied via the NamespaceDoc convention
        if (namespaceDescriptions.TryGetValue(namespaceName, out var nsSummary) &&
            !string.IsNullOrEmpty(nsSummary))
        {
            nsWriter.WriteParagraph(nsSummary);
        }

        // List immediate child namespaces (gradual disclosure — one level at a time)
        var children = GetImmediateChildNamespaces(namespaceName, allNamespaces)
            .OrderBy(n => n)
            .ToList();

        if (children.Count > 0)
        {
            var childNsHeaders = new[] { "Namespace", DescriptionColumnHeader };
            var childNsRows = children.Select(child =>
            {
                var childFolderPath = GetNamespaceFolderPath(child, rootNamespaces);
                SplitPath(childFolderPath, out _, out var childShortName);
                var link = $"{shortName}/{childShortName}.md";
                var childDesc = namespaceDescriptions.TryGetValue(child, out var desc) && !string.IsNullOrEmpty(desc)
                    ? desc
                    : NoDescriptionPlaceholder;
                return new[] { $"[{child}]({link})", childDesc };
            });
            nsWriter.WriteTable(childNsHeaders, childNsRows);
        }

        // List types declared directly in this namespace
        if (!byNamespace.TryGetValue(namespaceName, out var nsTypes) || nsTypes.Count == 0)
        {
            return;
        }

        var typeHeaders = new[] { "Type", DescriptionColumnHeader };
        var typeRows = nsTypes.Select(t =>
        {
            var typeMemberId = BuildTypeId(t);
            var summary = xmlDocs.GetSummary(typeMemberId) ?? NoDescriptionPlaceholder;
            var link = $"{shortName}/{t.Name}.md";
            return new[] { $"[{t.Name}]({link})", summary };
        });
        nsWriter.WriteTable(typeHeaders, typeRows);

        foreach (var type in nsTypes)
        {
            WriteTypePage(factory, namespaceName, folderPath, type, xmlDocs);
        }
    }

    /// <summary>
    ///     Writes the Markdown page for a single type, including a members table and
    ///     links to complex member pages.
    /// </summary>
    /// <param name="factory">Factory for creating output writers.</param>
    /// <param name="namespaceName">The namespace that owns <paramref name="type"/>.</param>
    /// <param name="namespaceFolderPath">
    ///     The file-system folder path for the namespace (e.g. <c>ApiMark.DotNet.Fixtures/Inner</c>).
    ///     Used as the subfolder when creating the type's output file.
    /// </param>
    /// <param name="type">The type whose page is being written.</param>
    /// <param name="xmlDocs">XML documentation index for summary and remarks lookups.</param>
    private void WriteTypePage(
        IMarkdownWriterFactory factory,
        string namespaceName,
        string namespaceFolderPath,
        TypeDefinition type,
        XmlDocReader xmlDocs)
    {
        using var typeWriter = factory.CreateMarkdown(namespaceFolderPath, type.Name);
        typeWriter.WriteHeading(2, type.Name);

        // Emit the C# declaration signature so readers can see the type kind and modifiers
        var typeSignature = BuildTypeSignature(type);
        typeWriter.WriteSignature("csharp", typeSignature);

        var typeMemberId = BuildTypeId(type);

        // Always emit a summary paragraph — use the placeholder when no doc is present
        var typeSummary = xmlDocs.GetSummary(typeMemberId);
        typeWriter.WriteParagraph(!string.IsNullOrEmpty(typeSummary) ? typeSummary : NoDescriptionPlaceholder);

        var typeRemarks = xmlDocs.GetRemarks(typeMemberId);
        if (!string.IsNullOrEmpty(typeRemarks))
        {
            typeWriter.WriteParagraph(typeRemarks);
        }

        // Collect visible members: constructors first, then alphabetically
        var members = GetVisibleMembers(type)
            .OrderBy(m => m.Name == ConstructorMethodName ? 0 : 1)
            .ThenBy(m => m.Name)
            .ToList();

        if (members.Count == 0)
        {
            return;
        }

        // Build overload groups so methods sharing the same file name are emitted together
        var methodGroups = members
            .OfType<MethodDefinition>()
            .GroupBy(m => BuildMethodFileName(m, type))
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var documentedMethodGroups = new HashSet<string>(StringComparer.Ordinal);

        // Accumulate rows into per-kind buckets for the grouped sub-table output
        var constructorRows = new List<string[]>();
        var propertyRows = new List<string[]>();
        var methodRows = new List<string[]>();
        var fieldRows = new List<string[]>();
        var eventRows = new List<string[]>();

        // Every member always gets its own detail page and a linked row — navigation is fully
        // deterministic: {TypeName}/{MemberName}.md always exists for every visible member.
        foreach (var member in members)
        {
            if (member is MethodDefinition method)
            {
                var methodFileName = BuildMethodFileName(method, type);
                var overloads = methodGroups[methodFileName];
                var isConstructor = method.Name == ConstructorMethodName;

                if (overloads.Count > 1)
                {
                    // Only the first visit to a method group emits the overload page and row
                    if (!documentedMethodGroups.Add(methodFileName))
                    {
                        continue;
                    }

                    // Ensure deterministic ordering for representative selection and page rendering
                    var orderedOverloads = overloads
                        .OrderBy(m => m.GenericParameters.Count)
                        .ThenBy(m => m.Parameters.Count)
                        .ThenBy(m => string.Join(",", m.Parameters.Select(p => p.ParameterType.FullName)), StringComparer.Ordinal)
                        .ToList();

                    var representative = orderedOverloads[0];
                    var representativeMemberId = BuildMemberId(representative);
                    var representativeSummary = xmlDocs.GetSummary(representativeMemberId) ?? NoDescriptionPlaceholder;
                    var representativeTypeName = GetMemberTypeName(representative, namespaceName);
                    var overloadDisplayName = GetMethodGroupDisplayName(representative, orderedOverloads.Count);

                    WriteMethodOverloadPage(factory, namespaceName, namespaceFolderPath, type, orderedOverloads, xmlDocs);
                    var memberLink = $"{type.Name}/{methodFileName}.md";

                    if (isConstructor)
                    {
                        constructorRows.Add(new[] { $"[{overloadDisplayName}]({memberLink})", representativeSummary });
                    }
                    else
                    {
                        methodRows.Add(new[] { $"[{overloadDisplayName}]({memberLink})", representativeTypeName, representativeSummary });
                    }

                    continue;
                }

                // Single method: always emit a dedicated page and a linked row
                var memberId = BuildMemberId(member);
                var memberSummary = xmlDocs.GetSummary(memberId) ?? NoDescriptionPlaceholder;
                var memberTypeName = GetMemberTypeName(member, namespaceName);
                var memberDisplayName = GetMemberDisplayName(member);
                var sanitizedName = GetSanitizedMemberFileName(member, type);

                WriteMemberPage(factory, namespaceName, namespaceFolderPath, type, member, xmlDocs, memberId);
                var memberPageLink = $"{type.Name}/{sanitizedName}.md";

                if (isConstructor)
                {
                    constructorRows.Add(new[] { $"[{memberDisplayName}]({memberPageLink})", memberSummary });
                }
                else
                {
                    methodRows.Add(new[] { $"[{memberDisplayName}]({memberPageLink})", memberTypeName, memberSummary });
                }
            }
            else
            {
                // Properties, fields, and events: always emit a dedicated page and a linked row
                var memberId = BuildMemberId(member);
                var memberSummary = xmlDocs.GetSummary(memberId) ?? NoDescriptionPlaceholder;
                var memberTypeName = GetMemberTypeName(member, namespaceName);
                var memberDisplayName = GetMemberDisplayName(member);
                var sanitizedName = GetSanitizedMemberFileName(member, type);

                WriteMemberPage(factory, namespaceName, namespaceFolderPath, type, member, xmlDocs, memberId);
                var memberPageLink = $"{type.Name}/{sanitizedName}.md";

                switch (member)
                {
                    case PropertyDefinition:
                        propertyRows.Add(new[] { $"[{memberDisplayName}]({memberPageLink})", memberTypeName, memberSummary });
                        break;
                    case FieldDefinition:
                        fieldRows.Add(new[] { $"[{memberDisplayName}]({memberPageLink})", memberTypeName, memberSummary });
                        break;
                    case EventDefinition:
                        eventRows.Add(new[] { $"[{memberDisplayName}]({memberPageLink})", memberTypeName, memberSummary });
                        break;
                }
            }
        }

        // Emit grouped sub-tables in the canonical order: Constructors, Properties, Methods, Fields, Events.
        // Each section is only emitted when at least one member of that kind is present.
        if (constructorRows.Count > 0)
        {
            typeWriter.WriteHeading(3, "Constructors");

            // Constructors omit the Type/Returns column — they have no meaningful return type
            typeWriter.WriteTable(new[] { "Member", DescriptionColumnHeader }, constructorRows);
        }

        if (propertyRows.Count > 0)
        {
            typeWriter.WriteHeading(3, "Properties");
            typeWriter.WriteTable(new[] { "Member", "Type", DescriptionColumnHeader }, propertyRows);
        }

        if (methodRows.Count > 0)
        {
            typeWriter.WriteHeading(3, "Methods");

            // Use "Returns" instead of "Type" for the method type column — more accurate for return values
            typeWriter.WriteTable(new[] { "Member", "Returns", DescriptionColumnHeader }, methodRows);
        }

        if (fieldRows.Count > 0)
        {
            typeWriter.WriteHeading(3, "Fields");
            typeWriter.WriteTable(new[] { "Member", "Type", DescriptionColumnHeader }, fieldRows);
        }

        if (eventRows.Count > 0)
        {
            typeWriter.WriteHeading(3, "Events");
            typeWriter.WriteTable(new[] { "Member", "Type", DescriptionColumnHeader }, eventRows);
        }
    }

    /// <summary>
    ///     Writes the detailed Markdown page for a single complex member, including
    ///     signature, parameters, returns, exceptions, remarks, and examples.
    /// </summary>
    /// <param name="factory">Factory for creating output writers.</param>
    /// <param name="namespaceName">The namespace that owns the declaring type (used for type name simplification).</param>
    /// <param name="namespaceFolderPath">
    ///     The file-system folder path for the namespace (e.g. <c>ApiMark.DotNet.Fixtures/Inner</c>).
    ///     Used to construct the member file's subfolder path.
    /// </param>
    /// <param name="type">The type that declares <paramref name="member"/>.</param>
    /// <param name="member">The member whose page is being written.</param>
    /// <param name="xmlDocs">XML documentation index.</param>
    /// <param name="memberId">Pre-computed XML doc member identifier for <paramref name="member"/>.</param>
    private static void WriteMemberPage(
        IMarkdownWriterFactory factory,
        string namespaceName,
        string namespaceFolderPath,
        TypeDefinition type,
        IMemberDefinition member,
        XmlDocReader xmlDocs,
        string memberId)
    {
        var sanitizedName = GetSanitizedMemberFileName(member, type);
        using var memberWriter = factory.CreateMarkdown($"{namespaceFolderPath}/{type.Name}", sanitizedName);

        var displayName = GetMemberDisplayName(member);
        memberWriter.WriteHeading(3, displayName);

        if (member is MethodDefinition method)
        {
            WriteMethodDocumentation(memberWriter, namespaceName, method, xmlDocs, memberId);
            return;
        }

        var signature = BuildMemberSignature(member, namespaceName);
        memberWriter.WriteSignature("csharp", signature);

        // Always emit a summary paragraph — use the placeholder when no doc is present
        var summary = xmlDocs.GetSummary(memberId);
        memberWriter.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : NoDescriptionPlaceholder);

        var returns = xmlDocs.GetReturns(memberId);
        if (!string.IsNullOrEmpty(returns))
        {
            memberWriter.WriteParagraph($"**Returns:** {returns}");
        }

        // Emit exception table when documented exceptions exist
        var exceptions = xmlDocs.GetExceptionDetails(memberId);
        if (exceptions.Count > 0)
        {
            var exHeaders = new[] { "Exception", DescriptionColumnHeader };
            var exRows = exceptions.Select(e => new[] { e.Type, e.Description ?? string.Empty });
            memberWriter.WriteTable(exHeaders, exRows);
        }

        var remarks = xmlDocs.GetRemarks(memberId);
        if (!string.IsNullOrEmpty(remarks))
        {
            memberWriter.WriteParagraph(remarks);
        }

        var example = xmlDocs.GetExample(memberId);
        if (!string.IsNullOrEmpty(example))
        {
            memberWriter.WriteCodeBlock("csharp", example);
        }
    }

    private static void WriteMethodOverloadPage(
        IMarkdownWriterFactory factory,
        string namespaceName,
        string namespaceFolderPath,
        TypeDefinition type,
        IReadOnlyList<MethodDefinition> overloads,
        XmlDocReader xmlDocs)
    {
        var sanitizedName = BuildMethodFileName(overloads[0], type);
        using var memberWriter = factory.CreateMarkdown($"{namespaceFolderPath}/{type.Name}", sanitizedName);

        memberWriter.WriteHeading(3, GetMethodGroupName(overloads[0]));

        foreach (var overload in overloads)
        {
            memberWriter.WriteHeading(4, BuildMethodDisplayName(overload));
            WriteMethodDocumentation(memberWriter, namespaceName, overload, xmlDocs, BuildMemberId(overload));
        }
    }

    private static void WriteMethodDocumentation(
        IMarkdownWriter memberWriter,
        string namespaceName,
        MethodDefinition method,
        XmlDocReader xmlDocs,
        string memberId)
    {
        var signature = BuildMethodSignature(method, namespaceName);
        memberWriter.WriteSignature("csharp", signature);

        // Always emit a summary paragraph — use the placeholder when no doc is present
        var summary = xmlDocs.GetSummary(memberId);
        memberWriter.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : NoDescriptionPlaceholder);

        if (method.HasParameters)
        {
            var paramDocs = xmlDocs.GetParams(memberId);
            var paramHeaders = new[] { "Parameter", "Type", DescriptionColumnHeader };
            var paramRows = method.Parameters.Select(p =>
            {
                var desc = paramDocs.FirstOrDefault(pd => pd.Name == p.Name).Description ?? string.Empty;
                var typeName = TypeNameSimplifier.Simplify(p.ParameterType, namespaceName);
                return new[] { p.Name, typeName, desc };
            });
            memberWriter.WriteTable(paramHeaders, paramRows);
        }

        var returns = xmlDocs.GetReturns(memberId);
        if (!string.IsNullOrEmpty(returns))
        {
            memberWriter.WriteParagraph($"**Returns:** {returns}");
        }

        var exceptions = xmlDocs.GetExceptionDetails(memberId);
        if (exceptions.Count > 0)
        {
            var exHeaders = new[] { "Exception", DescriptionColumnHeader };
            var exRows = exceptions.Select(e => new[] { e.Type, e.Description ?? string.Empty });
            memberWriter.WriteTable(exHeaders, exRows);
        }

        var remarks = xmlDocs.GetRemarks(memberId);
        if (!string.IsNullOrEmpty(remarks))
        {
            memberWriter.WriteParagraph(remarks);
        }

        var example = xmlDocs.GetExample(memberId);
        if (!string.IsNullOrEmpty(example))
        {
            memberWriter.WriteCodeBlock("csharp", example);
        }
    }

    /// <summary>
    ///     Computes the file-system folder path for a namespace, treating the root namespace as atomic.
    /// </summary>
    /// <param name="namespaceName">The full namespace name.</param>
    /// <param name="rootNamespaces">All root namespaces identified in the assembly.</param>
    /// <returns>
    ///     The folder path string. For a root namespace the entire dotted name is the path segment
    ///     (e.g. <c>ApiMark.DotNet.Fixtures</c>). For a child namespace the root prefix is kept
    ///     and subsequent segments use forward slashes
    ///     (e.g. <c>ApiMark.DotNet.Fixtures/Inner</c>).
    /// </returns>
    private static string GetNamespaceFolderPath(string namespaceName, List<string> rootNamespaces)
    {
        var root = rootNamespaces.FirstOrDefault(r =>
            string.Equals(r, namespaceName, StringComparison.Ordinal) ||
            namespaceName.StartsWith(r + ".", StringComparison.Ordinal));

        if (root == null)
        {
            return namespaceName; // fallback: no known root, use full name
        }

        if (string.Equals(namespaceName, root, StringComparison.Ordinal))
        {
            return root; // root namespace: full dotted name is the single path segment
        }

        // Child namespace: keep root intact, replace subsequent dots with slashes
        var suffix = namespaceName.Substring(root.Length); // e.g. ".Inner" or ".Inner.Sub"
        return root + suffix.Replace('.', '/');
    }

    /// <summary>
    ///     Returns all immediate child namespaces of <paramref name="parent"/> from the given set.
    ///     A namespace N is an immediate child when it starts with <c>parent.</c> and the
    ///     remaining portion contains no further dots.
    /// </summary>
    /// <param name="parent">The parent namespace name.</param>
    /// <param name="allNamespaces">All namespace names to search.</param>
    /// <returns>An enumerable of immediate child namespace names.</returns>
    private static IEnumerable<string> GetImmediateChildNamespaces(string parent, List<string> allNamespaces)
    {
        var prefix = parent + ".";
        return allNamespaces.Where(n =>
            n.StartsWith(prefix, StringComparison.Ordinal) &&
            !n.Substring(prefix.Length).Contains('.'));
    }

    /// <summary>
    ///     Splits a forward-slash-delimited folder path into a parent subfolder and a short name.
    /// </summary>
    /// <param name="path">The full folder path (e.g. <c>ApiMark.DotNet.Fixtures/Inner</c>).</param>
    /// <param name="subFolder">
    ///     The portion before the last slash, or an empty string when there is no slash.
    /// </param>
    /// <param name="shortName">The portion after the last slash, or the whole string when there is no slash.</param>
    private static void SplitPath(string path, out string subFolder, out string shortName)
    {
        var lastSlash = path.LastIndexOf('/');
        if (lastSlash < 0)
        {
            subFolder = string.Empty;
            shortName = path;
        }
        else
        {
            subFolder = path.Substring(0, lastSlash);
            shortName = path.Substring(lastSlash + 1);
        }
    }

    /// <summary>
    ///     Returns <c>true</c> when <paramref name="type"/> satisfies the visibility
    ///     setting in <see cref="_options"/>.
    /// </summary>
    /// <param name="type">The type definition to test.</param>
    /// <returns><c>true</c> when the type is visible under the current visibility setting.</returns>
    private bool IsTypeVisible(TypeDefinition type)
    {
        // Nested types are not emitted as top-level pages
        if (type.IsNested)
        {
            return false;
        }

        return _options.Visibility switch
        {
            ApiVisibility.Public => type.IsPublic,
            ApiVisibility.PublicAndProtected => type.IsPublic || type.IsNestedFamily || type.IsNestedFamilyOrAssembly,
            ApiVisibility.All => true,
            _ => type.IsPublic,
        };
    }

    /// <summary>
    ///     Returns <c>true</c> when <paramref name="member"/> satisfies the visibility
    ///     setting in <see cref="_options"/>.
    /// </summary>
    /// <param name="member">The member to test.</param>
    /// <returns><c>true</c> when the member is visible under the current visibility setting.</returns>
    private bool IsMemberVisible(IMemberDefinition member)
    {
        return _options.Visibility switch
        {
            ApiVisibility.Public => IsMemberPublic(member),
            ApiVisibility.PublicAndProtected => IsMemberPublicOrProtected(member),
            ApiVisibility.All => true,
            _ => IsMemberPublic(member),
        };
    }

    /// <summary>Returns <c>true</c> when <paramref name="member"/> is publicly accessible.</summary>
    /// <param name="member">The member to test.</param>
    /// <returns><c>true</c> when the member is public.</returns>
    private static bool IsMemberPublic(IMemberDefinition member) => member switch
    {
        MethodDefinition m => m.IsPublic,
        PropertyDefinition p => (p.GetMethod?.IsPublic ?? false) || (p.SetMethod?.IsPublic ?? false),
        FieldDefinition f => f.IsPublic,
        EventDefinition e => e.AddMethod?.IsPublic ?? false,
        _ => false,
    };

    /// <summary>Returns <c>true</c> when <paramref name="member"/> is public or protected.</summary>
    /// <param name="member">The member to test.</param>
    /// <returns><c>true</c> when the member is public or protected.</returns>
    private static bool IsMemberPublicOrProtected(IMemberDefinition member) => member switch
    {
        MethodDefinition m => m.IsPublic || m.IsFamily || m.IsFamilyOrAssembly,
        PropertyDefinition p => IsPropertyPublicOrProtected(p),
        FieldDefinition f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly,
        EventDefinition e => (e.AddMethod?.IsPublic ?? false) || (e.AddMethod?.IsFamily ?? false),
        _ => false,
    };

    /// <summary>Returns <c>true</c> when <paramref name="p"/> has a public or protected getter or setter.</summary>
    /// <param name="p">The property to inspect.</param>
    /// <returns><c>true</c> when at least one accessor is publicly or protected-family accessible.</returns>
    private static bool IsPropertyPublicOrProtected(PropertyDefinition p)
    {
        var getterVisible = p.GetMethod != null && (p.GetMethod.IsPublic || p.GetMethod.IsFamily);
        var setterVisible = p.SetMethod != null && (p.SetMethod.IsPublic || p.SetMethod.IsFamily);
        return getterVisible || setterVisible;
    }

    /// <summary>
    ///     Enumerates all members of <paramref name="type"/> that pass the current visibility,
    ///     obsolete, and compiler-generated filters.
    /// </summary>
    /// <param name="type">The type whose members to inspect.</param>
    /// <returns>An enumerable of visible member definitions.</returns>
    private IEnumerable<IMemberDefinition> GetVisibleMembers(TypeDefinition type)
    {
        // Methods: exclude special-name accessors (property getters/setters, event add/remove)
        // but always include constructors
        foreach (var method in type.Methods
            .Where(m => !IsSpecialNameNonConstructor(m) && !IsCompilerGenerated(m) && ShouldIncludeMember(m)))
        {
            yield return method;
        }

        foreach (var prop in type.Properties.Where(ShouldIncludeMember))
        {
            yield return prop;
        }

        // Fields: skip compiler-generated backing fields (names contain angle brackets) and
        // the compiler-generated enum backing field named "value__" that does not appear
        // in source and has no meaningful documentation
        foreach (var field in type.Fields
            .Where(f => f.Name != "value__" && !IsCompilerGeneratedField(f) && ShouldIncludeMember(f)))
        {
            yield return field;
        }

        foreach (var evt in type.Events.Where(ShouldIncludeMember))
        {
            yield return evt;
        }
    }

    /// <summary>Returns <c>true</c> when <paramref name="member"/> passes both visibility and obsolete filters.</summary>
    /// <param name="member">The member to evaluate.</param>
    /// <returns><c>true</c> when the member is visible and not filtered out by the obsolete setting.</returns>
    private bool ShouldIncludeMember(IMemberDefinition member) =>
        IsMemberVisible(member) && (_options.IncludeObsolete || !IsObsolete(member));

    /// <summary>Returns <c>true</c> when <paramref name="method"/> is a special-name accessor that is not a constructor.</summary>
    /// <param name="method">The method to test.</param>
    /// <returns><c>true</c> for property getters/setters and event add/remove methods.</returns>
    private static bool IsSpecialNameNonConstructor(MethodDefinition method) =>
        method.IsSpecialName && method.Name != ConstructorMethodName;

    /// <summary>Returns <c>true</c> when <paramref name="field"/> is a compiler-generated backing field.</summary>
    /// <param name="field">The field to test.</param>
    /// <returns><c>true</c> when the field name contains angle brackets (compiler-generated backing fields).</returns>
    private static bool IsCompilerGeneratedField(FieldDefinition field) =>
        field.Name.Contains('<') || field.Name.Contains('>');

    /// <summary>Builds the XML doc member identifier for a type (e.g. <c>T:Namespace.TypeName</c>).</summary>
    /// <param name="type">The type definition.</param>
    /// <returns>The XML doc member identifier string.</returns>
    private static string BuildTypeId(TypeDefinition type) =>
        $"T:{type.FullName.Replace('/', '.')}";

    /// <summary>Builds the XML doc member identifier for an arbitrary member.</summary>
    /// <param name="member">The member definition.</param>
    /// <returns>The XML doc member identifier string.</returns>
    private static string BuildMemberId(IMemberDefinition member) => member switch
    {
        MethodDefinition m => BuildMethodId(m),
        PropertyDefinition p => $"P:{p.DeclaringType.FullName.Replace('/', '.')}.{p.Name}",
        FieldDefinition f => $"F:{f.DeclaringType.FullName.Replace('/', '.')}.{f.Name}",
        EventDefinition e => $"E:{e.DeclaringType.FullName.Replace('/', '.')}.{e.Name}",
        _ => string.Empty,
    };

    /// <summary>Builds the XML doc member identifier for a method, including parameter type list when present.</summary>
    /// <param name="method">The method definition.</param>
    /// <returns>The XML doc member identifier string.</returns>
    private static string BuildMethodId(MethodDefinition method)
    {
        var typeName = method.DeclaringType.FullName.Replace('/', '.');
        var methodName = method.Name;

        // XML doc format includes parenthesized parameter list only when parameters exist
        if (!method.HasParameters)
        {
            return $"M:{typeName}.{methodName}";
        }

        var paramList = string.Join(",", method.Parameters.Select(p => p.ParameterType.FullName));
        return $"M:{typeName}.{methodName}({paramList})";
    }

    /// <summary>Builds a human-readable C# declaration signature for a type definition.</summary>
    /// <param name="type">The type definition to represent.</param>
    /// <returns>A string of the form <c>public class Name</c> or <c>public interface Name&lt;T&gt;</c>.</returns>
    private static string BuildTypeSignature(TypeDefinition type)
    {
        var keyword = type switch
        {
            { IsInterface: true } => "interface",
            { IsEnum: true } => "enum",
            { IsValueType: true } => "struct",
            _ => "class",
        };

        var classModifier = string.Empty;
        if (keyword == "class")
        {
            if (type.IsAbstract && type.IsSealed)
            {
                classModifier = "static ";
            }
            else if (type.IsSealed)
            {
                classModifier = "sealed ";
            }
        }

        var name = StripArity(type.Name);
        if (type.HasGenericParameters)
        {
            var args = string.Join(", ", type.GenericParameters.Select(p => p.Name));
            name = $"{name}<{args}>";
        }

        return $"public {classModifier}{keyword} {name}";
    }

    /// <summary>Dispatches to the appropriate signature builder based on the runtime member type.</summary>
    /// <param name="member">The member to represent.</param>
    /// <param name="contextNamespace">Used to simplify type names in the signature.</param>
    /// <returns>A human-readable C# declaration signature string.</returns>
    private static string BuildMemberSignature(IMemberDefinition member, string contextNamespace)
    {
        return member switch
        {
            MethodDefinition m => BuildMethodSignature(m, contextNamespace),
            PropertyDefinition p => BuildPropertySignature(p, contextNamespace),
            FieldDefinition f => BuildFieldSignature(f, contextNamespace),
            EventDefinition e => BuildEventSignature(e, contextNamespace),
            _ => member.Name,
        };
    }

    /// <summary>Builds a human-readable C# method declaration signature.</summary>
    /// <param name="method">The method definition.</param>
    /// <param name="contextNamespace">Used to simplify type names in parameters and return type.</param>
    /// <returns>The method signature string.</returns>
    private static string BuildMethodSignature(MethodDefinition method, string contextNamespace)
    {
        var returnType = TypeNameSimplifier.Simplify(method.ReturnType, contextNamespace);
        var isExtensionMethod = IsExtensionMethod(method);

        // Use the declaring type name for constructors rather than ConstructorMethodName
        var name = method.Name == ConstructorMethodName
            ? StripArity(method.DeclaringType.Name)
            : method.Name;

        var parameters = string.Join(", ", method.Parameters.Select((p, index) =>
        {
            var receiverPrefix = isExtensionMethod && index == 0 ? "this " : string.Empty;
            return $"{receiverPrefix}{TypeNameSimplifier.Simplify(p.ParameterType, contextNamespace)} {p.Name}";
        }));

        var accessibility = GetAccessibilityKeyword(method);
        var staticModifier = method.IsStatic && method.Name != ConstructorMethodName ? " static" : string.Empty;
        return method.Name == ConstructorMethodName
            ? $"{accessibility} {name}({parameters})"
            : $"{accessibility}{staticModifier} {returnType} {name}({parameters})";
    }

    /// <summary>Builds a human-readable C# property declaration signature.</summary>
    /// <param name="prop">The property definition.</param>
    /// <param name="contextNamespace">Used to simplify the property type name.</param>
    /// <returns>The property signature string.</returns>
    private static string BuildPropertySignature(PropertyDefinition prop, string contextNamespace)
    {
        var typeName = TypeNameSimplifier.Simplify(prop.PropertyType, contextNamespace);
        var accessibility = GetAccessibilityKeyword(prop.GetMethod ?? prop.SetMethod!);
        var accessors = BuildPropertyAccessors(prop);
        return $"{accessibility} {typeName} {prop.Name} {{ {accessors} }}";
    }

    /// <summary>Builds the accessor portion of a property signature (e.g. <c>get; internal set;</c>).</summary>
    /// <param name="prop">The property definition.</param>
    /// <returns>The accessor string to embed in the property signature.</returns>
    private static string BuildPropertyAccessors(PropertyDefinition prop)
    {
        var parts = new List<string>();

        if (prop.GetMethod != null)
        {
            // Only prefix the getter when it is less permissive than the property's declared access
            var prefix = prop.GetMethod.IsPublic ? string.Empty : $"{GetAccessibilityKeyword(prop.GetMethod)} ";
            parts.Add($"{prefix}get;");
        }

        if (prop.SetMethod != null)
        {
            var prefix = prop.SetMethod.IsPublic ? string.Empty : $"{GetAccessibilityKeyword(prop.SetMethod)} ";
            parts.Add($"{prefix}set;");
        }

        return string.Join(" ", parts);
    }

    /// <summary>Builds a human-readable C# field declaration signature.</summary>
    /// <param name="field">The field definition.</param>
    /// <param name="contextNamespace">Used to simplify the field type name.</param>
    /// <returns>The field signature string.</returns>
    private static string BuildFieldSignature(FieldDefinition field, string contextNamespace)
    {
        var typeName = TypeNameSimplifier.Simplify(field.FieldType, contextNamespace);

        // Determine modifier(s) from compile-time flags; literals imply IsStatic so they
        // must be tested before the IsStatic arm to avoid showing "static const"
        var modifiers = field switch
        {
            { IsLiteral: true } => "const",
            { IsStatic: true, IsInitOnly: true } => "static readonly",
            { IsStatic: true } => "static",
            { IsInitOnly: true } => "readonly",
            _ => string.Empty,
        };

        var gap = modifiers.Length > 0 ? " " : string.Empty;
        return $"public {modifiers}{gap}{typeName} {field.Name}";
    }

    /// <summary>Builds a human-readable C# event declaration signature.</summary>
    /// <param name="evt">The event definition.</param>
    /// <param name="contextNamespace">Used to simplify the event type name.</param>
    /// <returns>The event signature string.</returns>
    private static string BuildEventSignature(EventDefinition evt, string contextNamespace)
    {
        var typeName = TypeNameSimplifier.Simplify(evt.EventType, contextNamespace);
        return $"public event {typeName} {evt.Name}";
    }

    /// <summary>Returns the C# accessibility keyword for a method (e.g. <c>"public"</c> or <c>"protected"</c>).</summary>
    /// <param name="method">The method whose accessibility to report.</param>
    /// <returns>The lowercase C# accessibility keyword string.</returns>
    private static string GetAccessibilityKeyword(MethodDefinition method) => method switch
    {
        { IsPublic: true } => "public",
        { IsFamilyOrAssembly: true } => "protected internal",
        { IsFamily: true } => "protected",
        { IsAssembly: true } => "internal",
        _ => "private",
    };

    /// <summary>Returns the display name for a member as it should appear in documentation tables.</summary>
    /// <param name="member">The member whose display name to compute.</param>
    /// <returns>The display name string (constructor uses the declaring type's simple name).</returns>
    private static string GetMemberDisplayName(IMemberDefinition member)
    {
        return member switch
        {
            MethodDefinition m => BuildMethodDisplayName(m),
            PropertyDefinition p => p.Name,
            FieldDefinition f => f.Name,
            EventDefinition e => e.Name,
            _ => member.Name,
        };
    }

    /// <summary>Returns the simplified C# type name for a member as it should appear in a type column.</summary>
    /// <param name="member">The member whose type name to compute.</param>
    /// <param name="contextNamespace">Used to simplify the type name.</param>
    /// <returns>The simplified type name string.</returns>
    private static string GetMemberTypeName(IMemberDefinition member, string contextNamespace)
    {
        return member switch
        {
            // Constructors have no meaningful return type — the "type" column is omitted for them
            MethodDefinition m when m.Name == ConstructorMethodName => string.Empty,
            MethodDefinition m => TypeNameSimplifier.Simplify(m.ReturnType, contextNamespace),
            PropertyDefinition p => TypeNameSimplifier.Simplify(p.PropertyType, contextNamespace),
            FieldDefinition f => TypeNameSimplifier.Simplify(f.FieldType, contextNamespace),
            EventDefinition e => TypeNameSimplifier.Simplify(e.EventType, contextNamespace),
            _ => string.Empty,
        };
    }

    /// <summary>
    ///     Returns the file-system-safe name used to form the member page path.
    ///     Constructors use the declaring type name; all others use the member name directly.
    /// </summary>
    /// <param name="member">The member whose file name to compute.</param>
    /// <param name="declaringType">The type that declares <paramref name="member"/>.</param>
    /// <returns>A string suitable for use as a file name (without extension).</returns>
    private static string GetSanitizedMemberFileName(IMemberDefinition member, TypeDefinition declaringType)
    {
        return member switch
        {
            MethodDefinition m => BuildMethodFileName(m, declaringType),
            PropertyDefinition p => p.Name,
            FieldDefinition f => f.Name,
            EventDefinition e => e.Name,
            _ => member.Name,
        };
    }

    private static string BuildMethodDisplayName(MethodDefinition method)
    {
        var baseName = GetMethodGroupName(method);
        var parameters = string.Join(", ", method.Parameters.Select(p =>
            TypeNameSimplifier.Simplify(p.ParameterType, method.DeclaringType.Namespace)));
        return $"{baseName}({parameters})";
    }

    private static string BuildMethodFileName(MethodDefinition method, TypeDefinition declaringType)
    {
        return method.Name == ConstructorMethodName
            ? StripArity(declaringType.Name)
            : method.Name;
    }

    private static string GetMethodGroupDisplayName(MethodDefinition method, int overloadCount)
    {
        var baseName = GetMethodGroupName(method);
        return overloadCount > 1 ? $"{baseName} ({overloadCount} overloads)" : baseName;
    }

    private static string GetMethodGroupName(MethodDefinition method) =>
        method.Name == ConstructorMethodName
            ? StripArity(method.DeclaringType.Name)
            : method.Name;

    private static bool IsExtensionMethod(MethodDefinition method) =>
        method.IsStatic && method.CustomAttributes.Any(attribute =>
            attribute.AttributeType.FullName == "System.Runtime.CompilerServices.ExtensionAttribute");

    /// <summary>
    ///     Returns <c>true</c> when <paramref name="provider"/> carries a
    ///     <c>CompilerGeneratedAttribute</c>, indicating it was synthesized by the compiler.
    /// </summary>
    /// <param name="provider">The metadata element to inspect.</param>
    /// <returns><c>true</c> when the element is compiler-generated.</returns>
    private static bool IsCompilerGenerated(ICustomAttributeProvider provider) =>
        provider.CustomAttributes.Any(a =>
            a.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");

    /// <summary>
    ///     Returns <c>true</c> when <paramref name="type"/> is a compiler-generated
    ///     type (name contains angle brackets, or carries CompilerGeneratedAttribute).
    /// </summary>
    /// <param name="type">The type definition to inspect.</param>
    /// <returns><c>true</c> when the type is compiler-generated.</returns>
    private static bool IsCompilerGenerated(TypeDefinition type) =>
        type.Name.Contains('<') || type.Name.Contains('>') || IsCompilerGenerated((ICustomAttributeProvider)type);

    /// <summary>
    ///     Returns <c>true</c> when <paramref name="provider"/> carries an
    ///     <c>ObsoleteAttribute</c>.
    /// </summary>
    /// <param name="provider">The metadata element to inspect.</param>
    /// <returns><c>true</c> when the element is marked obsolete.</returns>
    private static bool IsObsolete(ICustomAttributeProvider provider) =>
        provider.CustomAttributes.Any(a => a.AttributeType.FullName == "System.ObsoleteAttribute");

    /// <summary>Removes the generic arity suffix (e.g. <c>`1</c>) from a type name.</summary>
    /// <param name="name">The raw type name that may contain a backtick arity suffix.</param>
    /// <returns>The name without the arity suffix.</returns>
    private static string StripArity(string name)
    {
        var tick = name.IndexOf('`');
        return tick >= 0 ? name.Substring(0, tick) : name;
    }
}
