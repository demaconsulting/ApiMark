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
    ///     and one detail page per visible member. The <see cref="AssemblyDefinition"/> is
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
    /// <param name="context">
    ///     Output channel for informational and error messages. Must not be null. Reserved for
    ///     future use — DotNetGenerator does not currently emit messages through this channel.
    /// </param>
    /// <exception cref="FileNotFoundException">Thrown when the XML documentation file does not exist.</exception>
    public void Generate(IMarkdownWriterFactory factory, IContext context)
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
            .Where(IsNamespaceDocCarrier)
            .ToList();
        var namespaceDocTypeSet = namespaceDocTypes.ToHashSet();

        var namespaceDescriptions = namespaceDocTypes
            .GroupBy(t => t.Namespace, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => g.Select(t => xmlDocs.GetSummary(BuildTypeId(t)))
                    .FirstOrDefault(summary => !string.IsNullOrEmpty(summary)),
                StringComparer.Ordinal);

        // Collect all types that pass the visibility, obsolete, and compiler-generated filters.
        // Exclude NamespaceDoc types — they are documentation carriers, not user-facing types.
        var visibleTypes = assembly.MainModule.Types
            .Where(t => !IsCompilerGenerated(t))
            .Where(t => !namespaceDocTypeSet.Contains(t))
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

        // All-namespaces table — lists every namespace so AI agents get a complete map in one
        // read; counts reflect only the types declared directly in each namespace (not children)
        var nsHeaders = new[] { "Namespace", "Types", DescriptionColumnHeader };
        var nsRows = allNamespaces.Select(nsName =>
        {
            var folderPath = GetNamespaceFolderPath(nsName, rootNamespaces);
            var link = $"{folderPath}.md";
            var typeCount = byNamespace.TryGetValue(nsName, out var nsTypes) ? nsTypes.Count : 0;
            var description = namespaceDescriptions.TryGetValue(nsName, out var desc) && !string.IsNullOrEmpty(desc)
                ? desc
                : NoDescriptionPlaceholder;
            return new[] { $"[{nsName}]({link})", typeCount.ToString(), description };
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
        var resolver = new TypeLinkResolver(rootNamespaces);
        foreach (var namespaceName in allNamespaces)
        {
            WriteNamespacePage(
                factory,
                namespaceName,
                allNamespaces,
                byNamespace,
                rootNamespaces,
                xmlDocs,
                namespaceDescriptions,
                resolver);
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
    /// <param name="resolver">Type link resolver forwarded to each type page within this namespace.</param>
    private void WriteNamespacePage(
        IMarkdownWriterFactory factory,
        string namespaceName,
        List<string> allNamespaces,
        Dictionary<string, List<TypeDefinition>> byNamespace,
        List<string> rootNamespaces,
        XmlDocReader xmlDocs,
        IReadOnlyDictionary<string, string?> namespaceDescriptions,
        TypeLinkResolver resolver)
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
            WriteTypePage(factory, namespaceName, folderPath, type, xmlDocs, resolver);
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
    /// <param name="resolver">Type link resolver used to emit Markdown links in table type cells.</param>
    private void WriteTypePage(
        IMarkdownWriterFactory factory,
        string namespaceName,
        string namespaceFolderPath,
        TypeDefinition type,
        XmlDocReader xmlDocs,
        TypeLinkResolver resolver)
    {
        using var typeWriter = factory.CreateMarkdown(namespaceFolderPath, type.Name);
        typeWriter.WriteHeading(1, type.Name);

        // Emit the C# declaration signature so readers can see the type kind, modifiers, and direct inheritance
        var typeSignature = BuildTypeSignature(type, namespaceName);
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

        // Delegates carry all their useful information in the declaration signature —
        // the compiler-injected Invoke/BeginInvoke/EndInvoke methods and the synthetic
        // (object, IntPtr) constructor are implementation noise that should never appear
        // in public API docs, analogous to how enum backing fields are suppressed.
        if (IsDelegate(type))
        {
            return;
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

        // Build a case-insensitive map of all members by their sanitized file name to detect
        // collisions between members whose names differ only in case (e.g. field "name" and
        // property "Name"). Members sharing a lowercase key are combined onto a single page.
        var caseInsensitiveGroups = new Dictionary<string, List<IMemberDefinition>>(StringComparer.Ordinal);
        foreach (var member in members)
        {
            var lowerKey = GetSanitizedMemberFileName(member, type).ToLowerInvariant();
            if (!caseInsensitiveGroups.TryGetValue(lowerKey, out var list))
            {
                list = [];
                caseInsensitiveGroups[lowerKey] = list;
            }

            list.Add(member);
        }

        // Track which lowercase keys have had their member page written so collision groups
        // are documented exactly once while their table rows are still emitted individually
        var writtenLowerKeys = new HashSet<string>(StringComparer.Ordinal);

        // Accumulate rows into per-kind buckets for the grouped sub-table output
        var constructorRows = new List<string[]>();
        var propertyRows = new List<string[]>();
        var methodRows = new List<string[]>();
        var fieldRows = new List<string[]>();
        var eventRows = new List<string[]>();

        // Accumulate external type references found in all type-column cells on this page
        var externalTypes = new SortedSet<ExternalTypeInfo>();

        // Emit one page per unique lowercase key and one table row per visible member.
        // Members whose sanitized file names collide case-insensitively share a combined page
        // named after the lowercase key; all their table rows link to that shared page.
        foreach (var member in members)
        {
            var lowerKey = GetSanitizedMemberFileName(member, type).ToLowerInvariant();
            var group = caseInsensitiveGroups[lowerKey];

            if (group.Count == 1)
            {
                // No collision: write an individual page using the member's own display name
                var memberId = BuildMemberId(member);
                var memberSummary = xmlDocs.GetSummary(memberId) ?? NoDescriptionPlaceholder;
                var memberTypeRef = GetMemberTypeRef(member);
                var memberTypeName = memberTypeRef != null
                    ? resolver.Linkify(memberTypeRef, namespaceFolderPath, namespaceName, externalTypes)
                    : string.Empty;
                var memberDisplayName = GetMemberDisplayName(member);
                var sanitizedName = GetSanitizedMemberFileName(member, type);
                var memberPageLink = $"{type.Name}/{sanitizedName}.md";

                if (member is MethodDefinition singleMethod)
                {
                    WriteMemberPage(factory, namespaceName, namespaceFolderPath, type, singleMethod, xmlDocs, memberId, resolver);
                    var isConstructor = singleMethod.Name == ConstructorMethodName;
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
                    WriteMemberPage(factory, namespaceName, namespaceFolderPath, type, member, xmlDocs, memberId, resolver);
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
            else if (IsPureMethodOverloadGroup(group, type))
            {
                // Pure method overloads (all MethodDefinition with the same exact file name):
                // emit a single shared overload page and one representative table row.
                // Skip all but the first occurrence so only one row is added per overload group.
                if (!writtenLowerKeys.Add(lowerKey))
                {
                    continue;
                }

                var methods = group.Cast<MethodDefinition>().ToList();

                // Ensure deterministic ordering for representative selection and page rendering
                var orderedOverloads = methods
                    .OrderBy(m => m.GenericParameters.Count)
                    .ThenBy(m => m.Parameters.Count)
                    .ThenBy(m => string.Join(",", m.Parameters.Select(p => p.ParameterType.FullName)), StringComparer.Ordinal)
                    .ToList();

                var representative = orderedOverloads[0];
                var representativeMemberId = BuildMemberId(representative);
                var representativeSummary = xmlDocs.GetSummary(representativeMemberId) ?? NoDescriptionPlaceholder;
                var representativeTypeRef = GetMemberTypeRef(representative);
                var representativeTypeName = representativeTypeRef != null
                    ? resolver.Linkify(representativeTypeRef, namespaceFolderPath, namespaceName, externalTypes)
                    : string.Empty;
                var overloadDisplayName = GetMethodGroupDisplayName(representative, orderedOverloads.Count);
                var overloadFileName = GetSanitizedMemberFileName(representative, type);
                var memberLink = $"{type.Name}/{overloadFileName}.md";
                var isConstructorGroup = representative.Name == ConstructorMethodName;

                WriteMethodOverloadPage(factory, namespaceName, namespaceFolderPath, type, orderedOverloads, xmlDocs, resolver);

                if (isConstructorGroup)
                {
                    constructorRows.Add(new[] { $"[{overloadDisplayName}]({memberLink})", representativeSummary });
                }
                else
                {
                    methodRows.Add(new[] { $"[{overloadDisplayName}]({memberLink})", representativeTypeName, representativeSummary });
                }
            }
            else
            {
                // Case-insensitive collision: mixed kinds or different-case method names.
                // Write a single combined page named after the lowercase key on first encounter.
                var memberLink = $"{type.Name}/{lowerKey}.md";

                if (writtenLowerKeys.Add(lowerKey))
                {
                    WriteCombinedMemberPage(factory, namespaceName, namespaceFolderPath, type, lowerKey, group, xmlDocs, resolver);
                }

                // Every member in the collision group still contributes its own row to the
                // appropriate sub-table, all linking to the shared combined page
                var memberId = BuildMemberId(member);
                var memberSummary = xmlDocs.GetSummary(memberId) ?? NoDescriptionPlaceholder;
                var memberTypeRef = GetMemberTypeRef(member);
                var memberTypeName = memberTypeRef != null
                    ? resolver.Linkify(memberTypeRef, namespaceFolderPath, namespaceName, externalTypes)
                    : string.Empty;
                var memberDisplayName = GetMemberDisplayName(member);

                switch (member)
                {
                    case MethodDefinition m:
                        var isConstructor = m.Name == ConstructorMethodName;
                        if (isConstructor)
                        {
                            constructorRows.Add(new[] { $"[{memberDisplayName}]({memberLink})", memberSummary });
                        }
                        else
                        {
                            methodRows.Add(new[] { $"[{memberDisplayName}]({memberLink})", memberTypeName, memberSummary });
                        }

                        break;
                    case PropertyDefinition:
                        propertyRows.Add(new[] { $"[{memberDisplayName}]({memberLink})", memberTypeName, memberSummary });
                        break;
                    case FieldDefinition:
                        fieldRows.Add(new[] { $"[{memberDisplayName}]({memberLink})", memberTypeName, memberSummary });
                        break;
                    case EventDefinition:
                        eventRows.Add(new[] { $"[{memberDisplayName}]({memberLink})", memberTypeName, memberSummary });
                        break;
                }
            }
        }

        // Emit grouped sub-tables in the canonical order: Constructors, Properties, Methods, Fields, Events.
        // Each section is only emitted when at least one member of that kind is present.
        if (constructorRows.Count > 0)
        {
            typeWriter.WriteHeading(2, "Constructors");

            // Constructors omit the Type/Returns column — they have no meaningful return type
            typeWriter.WriteTable(new[] { "Member", DescriptionColumnHeader }, constructorRows);
        }

        if (propertyRows.Count > 0)
        {
            typeWriter.WriteHeading(2, "Properties");
            typeWriter.WriteTable(new[] { "Member", "Type", DescriptionColumnHeader }, propertyRows);
        }

        if (methodRows.Count > 0)
        {
            typeWriter.WriteHeading(2, "Methods");

            // Use "Returns" instead of "Type" for the method type column — more accurate for return values
            typeWriter.WriteTable(new[] { "Member", "Returns", DescriptionColumnHeader }, methodRows);
        }

        if (fieldRows.Count > 0)
        {
            typeWriter.WriteHeading(2, "Fields");
            typeWriter.WriteTable(new[] { "Member", "Type", DescriptionColumnHeader }, fieldRows);
        }

        if (eventRows.Count > 0)
        {
            typeWriter.WriteHeading(2, "Events");
            typeWriter.WriteTable(new[] { "Member", "Type", DescriptionColumnHeader }, eventRows);
        }

        // Emit the External Types section when any non-standard external types were referenced
        WriteExternalTypesSection(typeWriter, externalTypes);
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
    /// <param name="resolver">Type link resolver used to linkify parameter type cells.</param>
    private static void WriteMemberPage(
        IMarkdownWriterFactory factory,
        string namespaceName,
        string namespaceFolderPath,
        TypeDefinition type,
        IMemberDefinition member,
        XmlDocReader xmlDocs,
        string memberId,
        TypeLinkResolver resolver)
    {
        var sanitizedName = GetSanitizedMemberFileName(member, type);
        var memberCurrentFolder = $"{namespaceFolderPath}/{type.Name}";
        using var memberWriter = factory.CreateMarkdown(memberCurrentFolder, sanitizedName);

        var displayName = GetMemberDisplayName(member);
        memberWriter.WriteHeading(1, displayName);

        if (member is MethodDefinition method)
        {
            // Method pages use the resolver for parameter type cells
            var externalTypes = new SortedSet<ExternalTypeInfo>();
            WriteMethodDocumentation(memberWriter, namespaceName, method, xmlDocs, memberId, resolver, memberCurrentFolder, externalTypes);
            WriteExternalTypesSection(memberWriter, externalTypes);
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
        XmlDocReader xmlDocs,
        TypeLinkResolver resolver)
    {
        var sanitizedName = BuildMethodFileName(overloads[0], type);
        var overloadCurrentFolder = $"{namespaceFolderPath}/{type.Name}";
        using var memberWriter = factory.CreateMarkdown(overloadCurrentFolder, sanitizedName);

        memberWriter.WriteHeading(1, GetMethodGroupName(overloads[0]));

        // Accumulate external types across all overloads on this shared page
        var externalTypes = new SortedSet<ExternalTypeInfo>();
        foreach (var overload in overloads)
        {
            memberWriter.WriteHeading(2, BuildMethodDisplayName(overload));
            WriteMethodDocumentation(memberWriter, namespaceName, overload, xmlDocs, BuildMemberId(overload), resolver, overloadCurrentFolder, externalTypes);
        }

        WriteExternalTypesSection(memberWriter, externalTypes);
    }

    /// <summary>
    ///     Writes a combined Markdown page for a group of members whose names collide on
    ///     case-insensitive file systems, placing all members on a single page named after
    ///     the shared lowercase key.
    /// </summary>
    /// <remarks>
    ///     This handles the case where a field <c>name</c> and a property <c>Name</c> would
    ///     map to the same file name on case-insensitive file systems.
    ///     All colliding members are documented together under H2 sub-headings that show
    ///     both the exact display name and the member kind (e.g., <c>name (Field)</c>).
    /// </remarks>
    /// <param name="factory">Factory for creating the output writer.</param>
    /// <param name="namespaceName">
    ///     The namespace that owns the declaring type; used to simplify type names in signatures.
    /// </param>
    /// <param name="namespaceFolderPath">
    ///     The file-system folder path for the namespace (e.g. <c>ApiMark.DotNet.Fixtures/Inner</c>).
    ///     Used to construct the member file's subfolder path.
    /// </param>
    /// <param name="type">The type that declares all members in <paramref name="members"/>.</param>
    /// <param name="lowerKey">
    ///     The shared lowercase file name key. Used as both the page file name and the H1
    ///     page heading so the combined page has a stable, predictable address.
    /// </param>
    /// <param name="members">
    ///     The ordered list of members whose sanitized file names collide on case-insensitive
    ///     file systems. Must contain at least two elements.
    /// </param>
    /// <param name="xmlDocs">XML documentation index for summary and detail lookups.</param>
    /// <param name="resolver">Type link resolver used to linkify parameter type cells.</param>
    private static void WriteCombinedMemberPage(
        IMarkdownWriterFactory factory,
        string namespaceName,
        string namespaceFolderPath,
        TypeDefinition type,
        string lowerKey,
        IReadOnlyList<IMemberDefinition> members,
        XmlDocReader xmlDocs,
        TypeLinkResolver resolver)
    {
        var combinedCurrentFolder = $"{namespaceFolderPath}/{type.Name}";
        using var writer = factory.CreateMarkdown(combinedCurrentFolder, lowerKey);

        // The shared lowercase key serves as the page heading so every member in the group
        // can be found at the same predictable path regardless of filesystem case-sensitivity
        writer.WriteHeading(1, lowerKey);

        // Accumulate external types across all members on this shared page
        var externalTypes = new SortedSet<ExternalTypeInfo>();

        foreach (var member in members)
        {
            var displayName = GetMemberDisplayName(member);
            var kindLabel = GetMemberKindLabel(member);
            writer.WriteHeading(2, $"{displayName} ({kindLabel})");

            var memberId = BuildMemberId(member);
            if (member is MethodDefinition method)
            {
                // Reuse the method documentation writer so formatting is consistent
                // with single-method pages and overload pages
                WriteMethodDocumentation(writer, namespaceName, method, xmlDocs, memberId, resolver, combinedCurrentFolder, externalTypes);
            }
            else
            {
                // Non-method: emit signature, summary, and optional documentation sections
                var signature = BuildMemberSignature(member, namespaceName);
                writer.WriteSignature("csharp", signature);

                var summary = xmlDocs.GetSummary(memberId);
                writer.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : NoDescriptionPlaceholder);

                var returns = xmlDocs.GetReturns(memberId);
                if (!string.IsNullOrEmpty(returns))
                {
                    writer.WriteParagraph($"**Returns:** {returns}");
                }

                var exceptions = xmlDocs.GetExceptionDetails(memberId);
                if (exceptions.Count > 0)
                {
                    var exHeaders = new[] { "Exception", DescriptionColumnHeader };
                    var exRows = exceptions.Select(e => new[] { e.Type, e.Description ?? string.Empty });
                    writer.WriteTable(exHeaders, exRows);
                }

                var remarks = xmlDocs.GetRemarks(memberId);
                if (!string.IsNullOrEmpty(remarks))
                {
                    writer.WriteParagraph(remarks);
                }

                var example = xmlDocs.GetExample(memberId);
                if (!string.IsNullOrEmpty(example))
                {
                    writer.WriteCodeBlock("csharp", example);
                }
            }
        }

        WriteExternalTypesSection(writer, externalTypes);
    }

    /// <summary>
    ///     Returns <see langword="true"/> when all members in <paramref name="group"/> are
    ///     <see cref="MethodDefinition"/> instances that share the same exact (case-sensitive)
    ///     sanitized file name, indicating they form a pure method overload group rather than a
    ///     case-insensitive collision between members of different kinds or different names.
    /// </summary>
    /// <param name="group">
    ///     The candidate group of members that share the same lowercase key.
    ///     Must not be null or empty.
    /// </param>
    /// <param name="type">
    ///     The declaring type, required by <see cref="GetSanitizedMemberFileName"/>.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when every member is a <see cref="MethodDefinition"/> and all
    ///     have the same exact file name; <see langword="false"/> otherwise.
    /// </returns>
    private static bool IsPureMethodOverloadGroup(
        IReadOnlyList<IMemberDefinition> group,
        TypeDefinition type)
    {
        // All members must be methods — mixed kinds are never pure overload groups
        if (!group.All(m => m is MethodDefinition))
        {
            return false;
        }

        // All methods must share the same exact (case-sensitive) file name; if they differ
        // only in case they form a collision rather than a classical overload group
        var firstFileName = GetSanitizedMemberFileName(group[0], type);
        return group.All(m =>
            string.Equals(GetSanitizedMemberFileName(m, type), firstFileName, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Returns the human-readable kind label for a member as it should appear in the
    ///     parenthetical qualifier on a combined member page heading
    ///     (e.g., <c>"Field"</c> or <c>"Property"</c>).
    /// </summary>
    /// <param name="member">The member whose kind label to determine.</param>
    /// <returns>A short English noun naming the member kind.</returns>
    private static string GetMemberKindLabel(IMemberDefinition member) => member switch
    {
        FieldDefinition => "Field",
        PropertyDefinition => "Property",
        EventDefinition => "Event",
        MethodDefinition m when m.Name == ConstructorMethodName => "Constructor",
        MethodDefinition => "Method",
        _ => "Member",
    };

    private static void WriteMethodDocumentation(
        IMarkdownWriter memberWriter,
        string namespaceName,
        MethodDefinition method,
        XmlDocReader xmlDocs,
        string memberId,
        TypeLinkResolver resolver,
        string currentFolder,
        ISet<ExternalTypeInfo> externalTypes)
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

            // Linkify parameter types — resolver tracks external types and emits links for intra-assembly types
            var paramRows = method.Parameters.Select(p =>
            {
                var desc = paramDocs.FirstOrDefault(pd => pd.Name == p.Name).Description ?? string.Empty;
                var typeName = resolver.Linkify(p.ParameterType, currentFolder, namespaceName, externalTypes);
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
    internal static string GetNamespaceFolderPath(string namespaceName, IReadOnlyList<string> rootNamespaces)
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

    /// <summary>Returns <see langword="true"/> when <paramref name="type"/> is a delegate type.</summary>
    /// <remarks>
    ///     The C# compiler compiles every <c>delegate</c> declaration into a sealed class that
    ///     inherits <c>System.MulticastDelegate</c>. Testing the base type name is therefore the
    ///     reliable way to detect delegates from compiled metadata.
    /// </remarks>
    /// <param name="type">The type to test.</param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="type"/> derives directly from
    ///     <c>System.MulticastDelegate</c>.
    /// </returns>
    private static bool IsDelegate(TypeDefinition type) =>
        type.BaseType?.FullName == "System.MulticastDelegate";

    /// <summary>
    ///     Builds the source-level <c>public delegate</c> signature for a delegate type,
    ///     derived from the compiler-injected <c>Invoke</c> method's return type and parameters.
    /// </summary>
    /// <remarks>
    ///     The C# compiler injects three methods into every delegate type: <c>Invoke</c>,
    ///     <c>BeginInvoke</c>, and <c>EndInvoke</c>. <c>Invoke</c> carries the exact signature
    ///     the developer wrote in source, so it is used here to reconstruct the canonical
    ///     <c>public delegate ReturnType Name(params)</c> form.
    /// </remarks>
    /// <param name="type">The delegate type (must satisfy <see cref="IsDelegate"/>).</param>
    /// <param name="contextNamespace">Used to simplify type names in the signature.</param>
    /// <returns>A string of the form <c>public delegate void ServiceEvent(DateTime timestamp, ...)</c>.</returns>
    private static string BuildDelegateSignature(TypeDefinition type, string contextNamespace)
    {
        var invoke = type.Methods.FirstOrDefault(m => m.Name == "Invoke");
        if (invoke == null)
        {
            // Malformed delegate — fall back to a bare declaration without parameters
            return $"public delegate {StripArity(type.Name)}()";
        }

        var returnType = TypeNameSimplifier.Simplify(invoke.ReturnType, contextNamespace);
        var name = StripArity(type.Name);
        if (type.HasGenericParameters)
        {
            var args = string.Join(", ", type.GenericParameters.Select(p => p.Name));
            name = $"{name}<{args}>";
        }

        var parameters = string.Join(", ", invoke.Parameters.Select(p =>
            $"{TypeNameSimplifier.Simplify(p.ParameterType, contextNamespace)} {p.Name}"));

        return $"public delegate {returnType} {name}({parameters})";
    }

    /// <summary>Builds a human-readable C# declaration signature for a type definition.</summary>
    /// <remarks>
    ///     Base types <c>System.Object</c>, <c>System.ValueType</c>, <c>System.Enum</c>, and
    ///     <c>System.MulticastDelegate</c> are suppressed because they are implicit compiler-assigned
    ///     roots that carry no information for readers; showing them would add noise to every class,
    ///     struct, enum, and delegate signature. The <paramref name="contextNamespace"/> is forwarded
    ///     to <see cref="TypeNameSimplifier"/> so that types declared in the same namespace are
    ///     rendered without their namespace prefix, keeping signatures concise.
    ///     <para>
    ///         Delegate types are detected and dispatched to <see cref="BuildDelegateSignature"/>
    ///         before the class/struct/enum/interface switch so the output always shows the
    ///         source-level <c>delegate</c> keyword rather than <c>sealed class</c>.
    ///     </para>
    /// </remarks>
    /// <param name="type">The type definition to represent.</param>
    /// <param name="contextNamespace">Used to simplify base type and interface names in the signature.</param>
    /// <returns>
    ///     A string of the form <c>public class Name</c>, <c>public interface Name&lt;T&gt;</c>,
    ///     <c>public delegate void Name(params)</c>, or
    ///     <c>public class Name : BaseClass, IInterface</c> when direct inheritance is present.
    /// </returns>
    private static string BuildTypeSignature(TypeDefinition type, string contextNamespace)
    {
        // Delegates are compiled to sealed classes deriving from MulticastDelegate.
        // Detect them before the generic class/struct/enum/interface switch so that
        // the output shows the original source-level delegate syntax.
        if (IsDelegate(type))
        {
            return BuildDelegateSignature(type, contextNamespace);
        }

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

        // Collect direct base class (excluding well-known root types) and directly declared interfaces
        // so the signature shows inheritance at a glance without opening the source file
        var bases = new List<string>();
        if (type.BaseType != null)
        {
            var baseName = type.BaseType.FullName;

            // Skip the standard implicit base types that carry no information for readers
            if (baseName is not ("System.Object" or "System.ValueType" or "System.Enum" or "System.MulticastDelegate"))
            {
                bases.Add(TypeNameSimplifier.Simplify(type.BaseType, contextNamespace));
            }
        }

        // Include all directly declared interfaces — transitive inheritance is omitted
        foreach (var interfaceRef in type.Interfaces)
        {
            bases.Add(TypeNameSimplifier.Simplify(interfaceRef.InterfaceType, contextNamespace));
        }

        if (bases.Count > 0)
        {
            name = $"{name} : {string.Join(", ", bases)}";
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

    /// <summary>
    ///     Returns the <see cref="TypeReference"/> representing the type of a member, used to
    ///     linkify the type column in documentation tables.
    /// </summary>
    /// <remarks>
    ///     Constructors have no meaningful return type and return <see langword="null"/> so that
    ///     callers can omit the type column from constructor rows.
    /// </remarks>
    /// <param name="member">The member whose type reference to retrieve.</param>
    /// <returns>
    ///     The type reference for properties, fields, events, and non-constructor methods;
    ///     <see langword="null"/> for constructors and unrecognized member kinds.
    /// </returns>
    private static TypeReference? GetMemberTypeRef(IMemberDefinition member) => member switch
    {
        MethodDefinition m when m.Name == ConstructorMethodName => null,
        MethodDefinition m => m.ReturnType,
        PropertyDefinition p => p.PropertyType,
        FieldDefinition f => f.FieldType,
        EventDefinition e => e.EventType,
        _ => null,
    };

    /// <summary>
    ///     Writes the "External Types" section at the end of a generated Markdown page,
    ///     listing all non-standard types referenced in table cells on that page.
    /// </summary>
    /// <remarks>
    ///     The section is emitted only when <paramref name="externalTypes"/> is non-empty.
    ///     Rows are sorted alphabetically by simplified name because <see cref="SortedSet{T}"/>
    ///     preserves the order defined by <see cref="ExternalTypeInfo.CompareTo"/>.
    /// </remarks>
    /// <param name="writer">The Markdown writer for the current page.</param>
    /// <param name="externalTypes">
    ///     The set of external types accumulated during table row generation. May be empty.
    /// </param>
    private static void WriteExternalTypesSection(IMarkdownWriter writer, SortedSet<ExternalTypeInfo> externalTypes)
    {
        if (externalTypes.Count == 0)
        {
            return;
        }

        writer.WriteHeading(2, "External Types");
        writer.WriteTable(
            ["Type", "Namespace"],
            externalTypes.Select(t => new[] { t.SimplifiedName, t.Namespace }));
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

    private static bool IsNamespaceDocCarrier(TypeDefinition type) =>
        type.Name == "NamespaceDoc" && type.IsClass && type.IsAbstract && type.IsSealed && type.IsNotPublic;

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
