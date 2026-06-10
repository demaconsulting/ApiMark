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

    /// <summary>
    ///     Bundles the per-type-page writing context that is constant across all member
    ///     pages generated for a single type. Used to reduce parameter counts on the
    ///     private helper methods that emit individual member pages and table rows.
    /// </summary>
    private sealed record TypePageWriteContext(
        IMarkdownWriterFactory Factory,
        string NamespaceName,
        string NamespaceFolderPath,
        TypeDefinition Type,
        XmlDocReader XmlDocs,
        TypeLinkResolver Resolver);

    /// <summary>
    ///     Bundles the per-method documentation writing context passed to
    ///     <see cref="WriteMethodDocumentation"/> so that callers do not need to
    ///     thread five constant parameters through each call site.
    /// </summary>
    private sealed record MethodDocContext(
        string NamespaceName,
        XmlDocReader XmlDocs,
        TypeLinkResolver Resolver,
        string CurrentFolder,
        ISet<ExternalTypeInfo> ExternalTypes);

    /// <summary>
    ///     Bundles the per-assembly namespace documentation context that is constant
    ///     across all namespace page writes in a single generation run.
    /// </summary>
    private sealed record NamespaceDocContext(
        List<string> AllNamespaces,
        Dictionary<string, List<TypeDefinition>> ByNamespace,
        List<string> RootNamespaces,
        IReadOnlyDictionary<string, string?> NamespaceDescriptions,
        XmlDocReader XmlDocs,
        TypeLinkResolver Resolver);

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
            new[] { "Nested type", "`{NamespacePath}/{TypeName}/{NestedTypeName}.md`" },
            new[] { "Member", "`{NamespacePath}/{TypeName}/{MemberName}.md`" },
            new[] { "Operators", "`{NamespacePath}/{TypeName}/operators.md`" },
        };
        apiWriter.WriteTable(conventionHeaders, conventionRows);

        // Write one namespace page per namespace, ordered so parents precede children
        var resolver = new TypeLinkResolver(rootNamespaces);
        foreach (var namespaceName in allNamespaces)
        {
            WriteNamespacePage(
                factory,
                namespaceName,
                new NamespaceDocContext(allNamespaces, byNamespace, rootNamespaces, namespaceDescriptions, xmlDocs, resolver));
        }
    }

    /// <summary>
    ///     Writes the Markdown summary page for a single namespace, listing its immediate
    ///     child namespaces and the types declared directly in it.
    /// </summary>
    /// <param name="factory">Factory for creating output writers.</param>
    /// <param name="namespaceName">The full namespace name to document.</param>
    /// <param name="ctx">Bundled namespace documentation context shared across all namespace page writes.</param>
    private void WriteNamespacePage(
        IMarkdownWriterFactory factory,
        string namespaceName,
        NamespaceDocContext ctx)
    {
        var folderPath = GetNamespaceFolderPath(namespaceName, ctx.RootNamespaces);
        SplitPath(folderPath, out var subFolder, out var shortName);

        using var nsWriter = factory.CreateMarkdown(subFolder, shortName);
        nsWriter.WriteHeading(1, namespaceName);

        // Emit the namespace summary when one was supplied via the NamespaceDoc convention
        if (ctx.NamespaceDescriptions.TryGetValue(namespaceName, out var nsSummary) &&
            !string.IsNullOrEmpty(nsSummary))
        {
            nsWriter.WriteParagraph(nsSummary);
        }

        // List immediate child namespaces (gradual disclosure — one level at a time)
        var children = GetImmediateChildNamespaces(namespaceName, ctx.AllNamespaces)
            .OrderBy(n => n)
            .ToList();

        if (children.Count > 0)
        {
            var childNsHeaders = new[] { "Namespace", DescriptionColumnHeader };
            var childNsRows = children.Select(child =>
            {
                var childFolderPath = GetNamespaceFolderPath(child, ctx.RootNamespaces);
                SplitPath(childFolderPath, out _, out var childShortName);
                var link = $"{shortName}/{childShortName}.md";
                var childDesc = ctx.NamespaceDescriptions.TryGetValue(child, out var desc) && !string.IsNullOrEmpty(desc)
                    ? desc
                    : NoDescriptionPlaceholder;
                return new[] { $"[{child}]({link})", childDesc };
            });
            nsWriter.WriteTable(childNsHeaders, childNsRows);
        }

        // List types declared directly in this namespace
        if (!ctx.ByNamespace.TryGetValue(namespaceName, out var nsTypes) || nsTypes.Count == 0)
        {
            return;
        }

        var typeHeaders = new[] { "Type", DescriptionColumnHeader };
        var typeRows = nsTypes.Select(t =>
        {
            var typeMemberId = BuildTypeId(t);
            var summary = ctx.XmlDocs.GetSummary(typeMemberId) ?? NoDescriptionPlaceholder;
            var typeDisplayName = StripArity(t.Name);
            var link = $"{shortName}/{FlattenArity(t.Name)}.md";
            return new[] { $"[{typeDisplayName}]({link})", summary };
        });
        nsWriter.WriteTable(typeHeaders, typeRows);

        foreach (var type in nsTypes)
        {
            var typeCtx = new TypePageWriteContext(factory, namespaceName, folderPath, type, ctx.XmlDocs, ctx.Resolver);
            WriteTypePage(typeCtx);
        }
    }

    /// <summary>
    ///     Writes the Markdown page for a single type, including a members table and
    ///     links to complex member pages.
    /// </summary>
    /// <param name="ctx">The type-page context encapsulating factory, namespace, type, and resolver.</param>
    private void WriteTypePage(TypePageWriteContext ctx)
    {
        using var typeWriter = ctx.Factory.CreateMarkdown(ctx.NamespaceFolderPath, FlattenArity(ctx.Type.Name));
        typeWriter.WriteHeading(1, StripArity(ctx.Type.Name));

        // Emit the C# declaration signature so readers can see the type kind, modifiers, and direct inheritance
        var typeSignature = BuildTypeSignature(ctx.Type, ctx.NamespaceName);
        typeWriter.WriteSignature("csharp", typeSignature);

        var typeMemberId = BuildTypeId(ctx.Type);

        // Always emit a summary paragraph — use the placeholder when no doc is present
        var typeSummary = ctx.XmlDocs.GetSummary(typeMemberId);
        typeWriter.WriteParagraph(!string.IsNullOrEmpty(typeSummary) ? typeSummary : NoDescriptionPlaceholder);

        var typeRemarks = ctx.XmlDocs.GetRemarks(typeMemberId);
        if (!string.IsNullOrEmpty(typeRemarks))
        {
            typeWriter.WriteParagraph(typeRemarks);
        }

        // Delegates carry all their useful information in the declaration signature —
        // the compiler-injected Invoke/BeginInvoke/EndInvoke methods and the synthetic
        // (object, IntPtr) constructor are implementation noise that should never appear
        // in public API docs, analogous to how enum backing fields are suppressed.
        if (IsDelegate(ctx.Type))
        {
            return;
        }

        // Collect visible members: constructors first, then alphabetically
        var allMembers = GetVisibleMembers(ctx.Type)
            .OrderBy(m => m.Name == ConstructorMethodName ? 0 : 1)
            .ThenBy(m => m.Name)
            .ToList();

        // Separate operator overloads: all operators share a single operators.md page
        // and are not processed through the per-member collision/overload logic below
        var operatorMethods = allMembers
            .OfType<MethodDefinition>()
            .Where(IsOperator)
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .ThenBy(m => m.Parameters.Count)
            .ThenBy(m => string.Join(",", m.Parameters.Select(p => p.ParameterType.FullName)), StringComparer.Ordinal)
            .ToList();

        var members = allMembers
            .Where(m => !(m is MethodDefinition md && IsOperator(md)))
            .ToList();

        if (members.Count == 0 && operatorMethods.Count == 0)
        {
            return;
        }

        // Build a case-insensitive map of all members by their sanitized file name to detect
        // collisions between members whose names differ only in case (e.g. field "name" and
        // property "Name"). Members sharing a lowercase key are combined onto a single page.
        var caseInsensitiveGroups = new Dictionary<string, List<IMemberDefinition>>(StringComparer.Ordinal);
        foreach (var member in members)
        {
            var lowerKey = GetSanitizedMemberFileName(member, ctx.Type).ToLowerInvariant();
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
            var lowerKey = GetSanitizedMemberFileName(member, ctx.Type).ToLowerInvariant();
            var group = caseInsensitiveGroups[lowerKey];

            if (group.Count == 1)
            {
                ProcessSingleMember(ctx, member, constructorRows, propertyRows, methodRows, fieldRows, eventRows, externalTypes);
            }
            else if (IsPureMethodOverloadGroup(group, ctx.Type))
            {
                if (!writtenLowerKeys.Add(lowerKey))
                {
                    continue;
                }

                ProcessOverloadGroup(ctx, group, constructorRows, methodRows, externalTypes);
            }
            else
            {
                ProcessCollisionMember(ctx, member, group, lowerKey, writtenLowerKeys, constructorRows, propertyRows, methodRows, fieldRows, eventRows, externalTypes);
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

        // Emit Operators section when the type has operator overloads — all operators share
        // a single page to prevent file-name collisions between op_Addition, op_Subtraction, etc.
        if (operatorMethods.Count > 0)
        {
            WriteTypeOperatorsPage(ctx.Factory, ctx.NamespaceName, ctx.NamespaceFolderPath, ctx.Type, operatorMethods, ctx.XmlDocs, ctx.Resolver);
            typeWriter.WriteHeading(2, "Operators");
            typeWriter.WriteTable(
                new[] { "Member", DescriptionColumnHeader },
                new[] { new[] { $"[Operators]({FlattenArity(ctx.Type.Name)}/operators.md)", "Operator overloads" } });
        }

        // Emit Nested Types section when the type has visible nested types — each nested type
        // receives a dedicated page under the containing type's folder so the documentation
        // hierarchy mirrors the C# type hierarchy
        var visibleNestedTypes = GetVisibleNestedTypes(ctx.Type).ToList();
        if (visibleNestedTypes.Count > 0)
        {
            typeWriter.WriteHeading(2, "Nested Types");
            var nestedTypeHeaders = new[] { "Type", DescriptionColumnHeader };
            var nestedTypeRows = visibleNestedTypes.Select(nested =>
            {
                var nestedTypeId = BuildTypeId(nested);
                var nestedSummary = ctx.XmlDocs.GetSummary(nestedTypeId) ?? NoDescriptionPlaceholder;
                var nestedDisplayName = StripArity(nested.Name);
                var nestedLink = $"{FlattenArity(ctx.Type.Name)}/{FlattenArity(nested.Name)}.md";
                return new[] { $"[{nestedDisplayName}]({nestedLink})", nestedSummary };
            });
            typeWriter.WriteTable(nestedTypeHeaders, nestedTypeRows);

            // Recursively write a dedicated page for each visible nested type under the
            // containing type's folder, mirroring the C# type nesting hierarchy
            var nestedFolderPath = $"{ctx.NamespaceFolderPath}/{FlattenArity(ctx.Type.Name)}";
            foreach (var nested in visibleNestedTypes)
            {
                WriteTypePage(new TypePageWriteContext(ctx.Factory, ctx.NamespaceName, nestedFolderPath, nested, ctx.XmlDocs, ctx.Resolver));
            }
        }

        // Emit the External Types section when any non-standard external types were referenced
        WriteExternalTypesSection(typeWriter, externalTypes);
    }

    /// <summary>
    ///     Writes the detailed Markdown page for a single complex member, including
    ///     signature, parameters, returns, exceptions, remarks, and examples.
    /// </summary>
    /// <remarks>
    ///     Method members delegate to <see cref="WriteMethodDocumentation"/>; all other
    ///     member kinds use a direct signature/summary/sections layout. External type
    ///     references found in parameter type cells are accumulated and emitted in a
    ///     trailing section via <see cref="WriteExternalTypesSection"/>.
    /// </remarks>
    /// <param name="ctx">The type-page context containing factory, namespace, type, and resolver.</param>
    /// <param name="member">The member whose page is being written.</param>
    /// <param name="memberId">Pre-computed XML doc member identifier for <paramref name="member"/>.</param>
    private static void WriteMemberPage(
        TypePageWriteContext ctx,
        IMemberDefinition member,
        string memberId)
    {
        var sanitizedName = GetSanitizedMemberFileName(member, ctx.Type);
        var memberCurrentFolder = $"{ctx.NamespaceFolderPath}/{FlattenArity(ctx.Type.Name)}";
        using var memberWriter = ctx.Factory.CreateMarkdown(memberCurrentFolder, sanitizedName);

        var displayName = GetMemberDisplayName(member);
        memberWriter.WriteHeading(1, displayName);

        if (member is MethodDefinition method)
        {
            // Method pages use the resolver for parameter type cells
            var externalTypes = new SortedSet<ExternalTypeInfo>();
            WriteMethodDocumentation(memberWriter, method, memberId, new MethodDocContext(ctx.NamespaceName, ctx.XmlDocs, ctx.Resolver, memberCurrentFolder, externalTypes));
            WriteExternalTypesSection(memberWriter, externalTypes);
            return;
        }

        var signature = BuildMemberSignature(member, ctx.NamespaceName);
        memberWriter.WriteSignature("csharp", signature);

        // Always emit a summary paragraph — use the placeholder when no doc is present
        var summary = ctx.XmlDocs.GetSummary(memberId);
        memberWriter.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : NoDescriptionPlaceholder);

        var returns = ctx.XmlDocs.GetReturns(memberId);
        if (!string.IsNullOrEmpty(returns))
        {
            memberWriter.WriteParagraph($"**Returns:** {returns}");
        }

        // Emit exception table when documented exceptions exist
        var exceptions = ctx.XmlDocs.GetExceptionDetails(memberId);
        if (exceptions.Count > 0)
        {
            var exHeaders = new[] { "Exception", DescriptionColumnHeader };
            var exRows = exceptions.Select(e => new[] { e.Type, e.Description ?? string.Empty });
            memberWriter.WriteTable(exHeaders, exRows);
        }

        var remarks = ctx.XmlDocs.GetRemarks(memberId);
        if (!string.IsNullOrEmpty(remarks))
        {
            memberWriter.WriteParagraph(remarks);
        }

        var example = ctx.XmlDocs.GetExample(memberId);
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
        var overloadCurrentFolder = $"{namespaceFolderPath}/{FlattenArity(type.Name)}";
        using var memberWriter = factory.CreateMarkdown(overloadCurrentFolder, sanitizedName);

        memberWriter.WriteHeading(1, GetMethodGroupName(overloads[0]));

        // Accumulate external types across all overloads on this shared page
        var externalTypes = new SortedSet<ExternalTypeInfo>();
        foreach (var overload in overloads)
        {
            memberWriter.WriteHeading(2, BuildMethodDisplayName(overload));
            WriteMethodDocumentation(memberWriter, overload, BuildMemberId(overload), new MethodDocContext(namespaceName, xmlDocs, resolver, overloadCurrentFolder, externalTypes));
        }

        WriteExternalTypesSection(memberWriter, externalTypes);
    }

    /// <summary>
    ///     Writes the combined operator overloads page for a type, placing all operator
    ///     methods onto a single <c>operators.md</c> page to prevent file-name collisions.
    /// </summary>
    /// <remarks>
    ///     Operator names such as <c>operator +</c>, <c>operator -</c>, and <c>operator *</c>
    ///     would produce unsafe or ambiguous file names when written individually. Grouping
    ///     them onto a single deterministic page resolves the collision and makes the operators
    ///     section a stable navigation target for both human readers and AI agents.
    /// </remarks>
    /// <param name="factory">Factory for creating the output writer.</param>
    /// <param name="namespaceName">The namespace that owns the declaring type; used to simplify type names in signatures.</param>
    /// <param name="namespaceFolderPath">
    ///     The file-system folder path for the namespace (e.g. <c>ApiMark.DotNet.Fixtures</c>).
    ///     Used to construct the operators file's subfolder path.
    /// </param>
    /// <param name="type">The declaring type whose operator overloads are being documented.</param>
    /// <param name="operators">
    ///     The ordered list of operator methods to document. All elements must satisfy
    ///     <see cref="IsOperator"/>. Must contain at least one element.
    /// </param>
    /// <param name="xmlDocs">XML documentation index for summary and detail lookups.</param>
    /// <param name="resolver">Type link resolver used to linkify parameter type cells.</param>
    private static void WriteTypeOperatorsPage(
        IMarkdownWriterFactory factory,
        string namespaceName,
        string namespaceFolderPath,
        TypeDefinition type,
        IReadOnlyList<MethodDefinition> operators,
        XmlDocReader xmlDocs,
        TypeLinkResolver resolver)
    {
        var operatorsCurrentFolder = $"{namespaceFolderPath}/{FlattenArity(type.Name)}";
        using var writer = factory.CreateMarkdown(operatorsCurrentFolder, "operators");
        writer.WriteHeading(1, "Operators");

        var externalTypes = new SortedSet<ExternalTypeInfo>();
        foreach (var op in operators)
        {
            writer.WriteHeading(2, BuildMethodDisplayName(op));
            WriteMethodDocumentation(writer, op, BuildMemberId(op), new MethodDocContext(namespaceName, xmlDocs, resolver, operatorsCurrentFolder, externalTypes));
        }

        WriteExternalTypesSection(writer, externalTypes);
    }

    /// <summary>
    ///     Writes a combined Markdown page for a group of members whose names collide on
    ///     case-insensitive file systems, placing all members on a single page named after
    ///     the shared lowercase key.
    /// </summary>
    /// <remarks>
    ///     This handles the case where a field <c>name</c> and a property <c>Name</c> would
    ///     map to the same file name on case-insensitive file systems. All colliding members
    ///     are documented together under H2 sub-headings that show both the exact display name
    ///     and the member kind (e.g., <c>name (Field)</c>).
    /// </remarks>
    /// <param name="ctx">The type-page context containing factory, namespace, type, and resolver.</param>
    /// <param name="lowerKey">
    ///     The shared lowercase file name key. Used as both the page file name and the H1
    ///     page heading so the combined page has a stable, predictable address.
    /// </param>
    /// <param name="members">
    ///     The ordered list of members whose sanitized file names collide on case-insensitive
    ///     file systems. Must contain at least two elements.
    /// </param>
    private static void WriteCombinedMemberPage(
        TypePageWriteContext ctx,
        string lowerKey,
        IReadOnlyList<IMemberDefinition> members)
    {
        var combinedCurrentFolder = $"{ctx.NamespaceFolderPath}/{FlattenArity(ctx.Type.Name)}";
        using var writer = ctx.Factory.CreateMarkdown(combinedCurrentFolder, lowerKey);

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
                WriteMethodDocumentation(writer, method, memberId, new MethodDocContext(ctx.NamespaceName, ctx.XmlDocs, ctx.Resolver, combinedCurrentFolder, externalTypes));
            }
            else
            {
                WriteNonMethodMemberContent(writer, member, memberId, new MethodDocContext(ctx.NamespaceName, ctx.XmlDocs, ctx.Resolver, combinedCurrentFolder, externalTypes));
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

    /// <summary>
    ///     Processes a single member (no name collisions) by writing its individual detail page
    ///     and appending the appropriate row to one of the per-kind row accumulators.
    /// </summary>
    /// <remarks>
    ///     Called when a member's sanitized file name is unique within its type (no
    ///     case-insensitive collision). Writes the page immediately and adds the table row
    ///     to the correct bucket based on the member's runtime kind.
    /// </remarks>
    /// <param name="ctx">The type-page context containing factory, namespace, type, and resolver.</param>
    /// <param name="member">The single member to document.</param>
    /// <param name="constructorRows">Accumulator for constructor table rows.</param>
    /// <param name="propertyRows">Accumulator for property table rows.</param>
    /// <param name="methodRows">Accumulator for method table rows.</param>
    /// <param name="fieldRows">Accumulator for field table rows.</param>
    /// <param name="eventRows">Accumulator for event table rows.</param>
    /// <param name="externalTypes">External type reference accumulator for the current type page.</param>
    private static void ProcessSingleMember(
        TypePageWriteContext ctx,
        IMemberDefinition member,
        List<string[]> constructorRows,
        List<string[]> propertyRows,
        List<string[]> methodRows,
        List<string[]> fieldRows,
        List<string[]> eventRows,
        SortedSet<ExternalTypeInfo> externalTypes)
    {
        var memberId = BuildMemberId(member);
        var memberSummary = ctx.XmlDocs.GetSummary(memberId) ?? NoDescriptionPlaceholder;
        var memberTypeRef = GetMemberTypeRef(member);
        var memberTypeName = memberTypeRef != null
            ? ctx.Resolver.Linkify(memberTypeRef, ctx.NamespaceFolderPath, ctx.NamespaceName, externalTypes, IsMemberTypeNullableAnnotated(member))
            : string.Empty;
        var memberDisplayName = GetMemberDisplayName(member);
        var sanitizedName = GetSanitizedMemberFileName(member, ctx.Type);
        var memberPageLink = $"{FlattenArity(ctx.Type.Name)}/{sanitizedName}.md";

        if (member is MethodDefinition singleMethod)
        {
            WriteMemberPage(ctx, singleMethod, memberId);
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
            WriteMemberPage(ctx, member, memberId);
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

    /// <summary>
    ///     Processes a pure method overload group (all methods share the same exact file name)
    ///     by writing a single shared overload page and adding one representative table row.
    /// </summary>
    /// <remarks>
    ///     Called on the first encounter of each overload group key. Subsequent members
    ///     in the same group are skipped by the caller via a <c>writtenLowerKeys</c> guard.
    ///     The representative overload is the method with the fewest generic and value
    ///     parameters (deterministic selection).
    /// </remarks>
    /// <param name="ctx">The type-page context containing factory, namespace, type, and resolver.</param>
    /// <param name="group">All members sharing the same lowercase key (all must be MethodDefinition).</param>
    /// <param name="constructorRows">Accumulator for constructor table rows.</param>
    /// <param name="methodRows">Accumulator for method table rows.</param>
    /// <param name="externalTypes">External type reference accumulator for the current type page.</param>
    private static void ProcessOverloadGroup(
        TypePageWriteContext ctx,
        IReadOnlyList<IMemberDefinition> group,
        List<string[]> constructorRows,
        List<string[]> methodRows,
        SortedSet<ExternalTypeInfo> externalTypes)
    {
        var methods = group.Cast<MethodDefinition>().ToList();

        // Ensure deterministic ordering for representative selection and page rendering
        var orderedOverloads = methods
            .OrderBy(m => m.GenericParameters.Count)
            .ThenBy(m => m.Parameters.Count)
            .ThenBy(m => string.Join(",", m.Parameters.Select(p => p.ParameterType.FullName)), StringComparer.Ordinal)
            .ToList();

        var representative = orderedOverloads[0];
        var representativeMemberId = BuildMemberId(representative);
        var representativeSummary = ctx.XmlDocs.GetSummary(representativeMemberId) ?? NoDescriptionPlaceholder;
        var representativeTypeRef = GetMemberTypeRef(representative);
        var representativeTypeName = representativeTypeRef != null
            ? ctx.Resolver.Linkify(representativeTypeRef, ctx.NamespaceFolderPath, ctx.NamespaceName, externalTypes, IsMemberTypeNullableAnnotated(representative))
            : string.Empty;
        var overloadDisplayName = GetMethodGroupDisplayName(representative, orderedOverloads.Count);
        var overloadFileName = GetSanitizedMemberFileName(representative, ctx.Type);
        var memberLink = $"{FlattenArity(ctx.Type.Name)}/{overloadFileName}.md";
        var isConstructorGroup = representative.Name == ConstructorMethodName;

        WriteMethodOverloadPage(ctx.Factory, ctx.NamespaceName, ctx.NamespaceFolderPath, ctx.Type, orderedOverloads, ctx.XmlDocs, ctx.Resolver);

        if (isConstructorGroup)
        {
            constructorRows.Add(new[] { $"[{overloadDisplayName}]({memberLink})", representativeSummary });
        }
        else
        {
            methodRows.Add(new[] { $"[{overloadDisplayName}]({memberLink})", representativeTypeName, representativeSummary });
        }
    }

    /// <summary>
    ///     Processes one member from a case-insensitive collision group, writing the shared
    ///     combined page on first encounter and adding the member's individual row to the
    ///     appropriate per-kind accumulator.
    /// </summary>
    /// <remarks>
    ///     A collision occurs when members whose names differ only in case map to the same
    ///     file name. All such members share a single page named after the lowercase key;
    ///     each still contributes its own row to the sub-table on the containing type page.
    /// </remarks>
    /// <param name="ctx">The type-page context containing factory, namespace, type, and resolver.</param>
    /// <param name="member">The member to process.</param>
    /// <param name="group">All members sharing the same lowercase collision key.</param>
    /// <param name="lowerKey">The shared lowercase file name key.</param>
    /// <param name="writtenLowerKeys">Tracks keys whose combined page has already been written.</param>
    /// <param name="constructorRows">Accumulator for constructor table rows.</param>
    /// <param name="propertyRows">Accumulator for property table rows.</param>
    /// <param name="methodRows">Accumulator for method table rows.</param>
    /// <param name="fieldRows">Accumulator for field table rows.</param>
    /// <param name="eventRows">Accumulator for event table rows.</param>
    /// <param name="externalTypes">External type reference accumulator for the current type page.</param>
    private static void ProcessCollisionMember(
        TypePageWriteContext ctx,
        IMemberDefinition member,
        IReadOnlyList<IMemberDefinition> group,
        string lowerKey,
        HashSet<string> writtenLowerKeys,
        List<string[]> constructorRows,
        List<string[]> propertyRows,
        List<string[]> methodRows,
        List<string[]> fieldRows,
        List<string[]> eventRows,
        SortedSet<ExternalTypeInfo> externalTypes)
    {
        var memberLink = $"{FlattenArity(ctx.Type.Name)}/{lowerKey}.md";

        if (writtenLowerKeys.Add(lowerKey))
        {
            WriteCombinedMemberPage(ctx, lowerKey, group);
        }

        // Every member in the collision group still contributes its own row to the
        // appropriate sub-table, all linking to the shared combined page
        var memberId = BuildMemberId(member);
        var memberSummary = ctx.XmlDocs.GetSummary(memberId) ?? NoDescriptionPlaceholder;
        var memberTypeRef = GetMemberTypeRef(member);
        var memberTypeName = memberTypeRef != null
            ? ctx.Resolver.Linkify(memberTypeRef, ctx.NamespaceFolderPath, ctx.NamespaceName, externalTypes, IsMemberTypeNullableAnnotated(member))
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

    /// <summary>
    ///     Writes the signature, summary, returns, exceptions, remarks, and example sections
    ///     for a single non-method member into <paramref name="writer"/>.
    /// </summary>
    /// <remarks>
    ///     Extracted from <see cref="WriteCombinedMemberPage"/> to reduce its cognitive
    ///     complexity. Covers all member kinds except <see cref="MethodDefinition"/>, which
    ///     is handled by <see cref="WriteMethodDocumentation"/>.
    /// </remarks>
    /// <param name="writer">The Markdown writer for the containing combined page.</param>
    /// <param name="member">The non-method member to document.</param>
    /// <param name="memberId">Pre-computed XML doc member identifier for <paramref name="member"/>.</param>
    /// <param name="ctx">Bundled documentation context providing XmlDocs and namespace name.</param>
    private static void WriteNonMethodMemberContent(
        IMarkdownWriter writer,
        IMemberDefinition member,
        string memberId,
        MethodDocContext ctx)
    {
        var signature = BuildMemberSignature(member, ctx.NamespaceName);
        writer.WriteSignature("csharp", signature);

        var summary = ctx.XmlDocs.GetSummary(memberId);
        writer.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : NoDescriptionPlaceholder);

        var returns = ctx.XmlDocs.GetReturns(memberId);
        if (!string.IsNullOrEmpty(returns))
        {
            writer.WriteParagraph($"**Returns:** {returns}");
        }

        var exceptions = ctx.XmlDocs.GetExceptionDetails(memberId);
        if (exceptions.Count > 0)
        {
            var exHeaders = new[] { "Exception", DescriptionColumnHeader };
            var exRows = exceptions.Select(e => new[] { e.Type, e.Description ?? string.Empty });
            writer.WriteTable(exHeaders, exRows);
        }

        var remarks = ctx.XmlDocs.GetRemarks(memberId);
        if (!string.IsNullOrEmpty(remarks))
        {
            writer.WriteParagraph(remarks);
        }

        var example = ctx.XmlDocs.GetExample(memberId);
        if (!string.IsNullOrEmpty(example))
        {
            writer.WriteCodeBlock("csharp", example);
        }
    }

    /// <summary>
    ///     Writes the complete documentation body for a single method or constructor into
    ///     <paramref name="memberWriter"/>, including signature, summary, parameter table,
    ///     returns, exceptions, remarks, and example sections.
    /// </summary>
    /// <remarks>
    ///     Shared by <see cref="WriteMemberPage"/>, <see cref="WriteMethodOverloadPage"/>,
    ///     <see cref="WriteTypeOperatorsPage"/>, and <see cref="WriteCombinedMemberPage"/> so
    ///     that all method-documentation paths produce identically structured output.
    /// </remarks>
    /// <param name="memberWriter">The Markdown writer to emit content into.</param>
    /// <param name="method">The method whose documentation to write.</param>
    /// <param name="memberId">Pre-computed XML doc member identifier for <paramref name="method"/>.</param>
    /// <param name="ctx">Bundled documentation context shared across all members on the current page.</param>
    private static void WriteMethodDocumentation(
        IMarkdownWriter memberWriter,
        MethodDefinition method,
        string memberId,
        MethodDocContext ctx)
    {
        var signature = BuildMethodSignature(method, ctx.NamespaceName);
        memberWriter.WriteSignature("csharp", signature);

        // Always emit a summary paragraph — use the placeholder when no doc is present
        var summary = ctx.XmlDocs.GetSummary(memberId);
        memberWriter.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : NoDescriptionPlaceholder);

        if (method.HasParameters)
        {
            var paramDocs = ctx.XmlDocs.GetParams(memberId);
            var paramHeaders = new[] { "Parameter", "Type", DescriptionColumnHeader };

            // Linkify parameter types — resolver tracks external types and emits links for intra-assembly types
            var paramRows = method.Parameters.Select(p =>
            {
                var desc = paramDocs.FirstOrDefault(pd => pd.Name == p.Name).Description ?? string.Empty;
                var typeName = ctx.Resolver.Linkify(p.ParameterType, ctx.CurrentFolder, ctx.NamespaceName, ctx.ExternalTypes);
                return new[] { p.Name, typeName, desc };
            });
            memberWriter.WriteTable(paramHeaders, paramRows);
        }

        var returns = ctx.XmlDocs.GetReturns(memberId);
        if (!string.IsNullOrEmpty(returns))
        {
            memberWriter.WriteParagraph($"**Returns:** {returns}");
        }

        var exceptions = ctx.XmlDocs.GetExceptionDetails(memberId);
        if (exceptions.Count > 0)
        {
            var exHeaders = new[] { "Exception", DescriptionColumnHeader };
            var exRows = exceptions.Select(e => new[] { e.Type, e.Description ?? string.Empty });
            memberWriter.WriteTable(exHeaders, exRows);
        }

        var remarks = ctx.XmlDocs.GetRemarks(memberId);
        if (!string.IsNullOrEmpty(remarks))
        {
            memberWriter.WriteParagraph(remarks);
        }

        var example = ctx.XmlDocs.GetExample(memberId);
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
    ///     Returns the nested types of <paramref name="type"/> that satisfy the visibility
    ///     setting in <see cref="_options"/>, ordered by name.
    /// </summary>
    /// <remarks>
    ///     Nested-type visibility is tested with the <c>IsNested*</c> flags rather than the
    ///     top-level <c>IsPublic</c> flag because Cecil assigns separate flags to each
    ///     nested-access level. Ordering by name ensures deterministic output regardless of
    ///     metadata table order.
    /// </remarks>
    /// <param name="type">The declaring type whose nested types are to be filtered.</param>
    /// <returns>
    ///     An ordered enumerable of nested <see cref="TypeDefinition"/> instances that pass
    ///     the current visibility filter.
    /// </returns>
    private IEnumerable<TypeDefinition> GetVisibleNestedTypes(TypeDefinition type)
    {
        return type.NestedTypes
            .Where(t => _options.Visibility switch
            {
                ApiVisibility.Public => t.IsNestedPublic,
                ApiVisibility.PublicAndProtected => t.IsNestedPublic || t.IsNestedFamily || t.IsNestedFamilyOrAssembly,
                ApiVisibility.All => true,
                _ => t.IsNestedPublic,
            })
            .OrderBy(t => t.Name, StringComparer.Ordinal);
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

    /// <summary>Returns <c>true</c> when <paramref name="method"/> is a C# user-defined operator overload.</summary>
    /// <param name="method">The method to test.</param>
    /// <returns><c>true</c> when the method has the special-name flag and a name starting with <c>op_</c>.</returns>
    private static bool IsOperator(MethodDefinition method) =>
        method.IsSpecialName && method.Name.StartsWith("op_", StringComparison.Ordinal);

    /// <summary>Returns <c>true</c> when <paramref name="method"/> is a special-name accessor that is not a constructor or operator.</summary>
    /// <param name="method">The method to test.</param>
    /// <returns><c>true</c> for property getters/setters and event add/remove methods.</returns>
    private static bool IsSpecialNameNonConstructor(MethodDefinition method) =>
        method.IsSpecialName && method.Name != ConstructorMethodName && !IsOperator(method);

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

        // XML doc format uses #ctor for constructors; IL metadata uses .ctor
        var methodName = method.Name == ConstructorMethodName ? "#ctor" : method.Name;

        // XML doc format includes parenthesized parameter list only when parameters exist
        if (!method.HasParameters)
        {
            return $"M:{typeName}.{methodName}";
        }

        // Normalize nested-type separators: Cecil uses '/' in FullName (e.g. Outer/Inner)
        // but XML doc IDs always use '.' (e.g. Outer.Inner)
        var paramList = string.Join(",", method.Parameters.Select(p => p.ParameterType.FullName.Replace('/', '.')));

        // Conversion operators carry a ~ReturnType suffix in the XML doc member ID
        // (e.g. M:Type.op_Implicit(SourceType)~TargetType) that distinguishes overloads
        // with the same source type but different target types
        if (method.Name is "op_Implicit" or "op_Explicit")
        {
            // Normalize nested-type separators: Cecil uses '/' in FullName (e.g. OuterClass/Inner)
            // but XML doc IDs always use '.' (e.g. OuterClass.Inner)
            return $"M:{typeName}.{methodName}({paramList})~{method.ReturnType.FullName.Replace('/', '.')}";
        }

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
            // Malformed delegate — fall back to a void declaration without parameters
            return $"public delegate void {StripArity(type.Name)}()";
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
        if (IsOperator(method))
        {
            return BuildOperatorSignature(method, contextNamespace);
        }

        var returnType = TypeNameSimplifier.Simplify(
            method.ReturnType,
            contextNamespace,
            HasNullableAnnotation(method.MethodReturnType.CustomAttributes));
        var isExtensionMethod = IsExtensionMethod(method);

        // Use the declaring type name for constructors rather than ConstructorMethodName
        var name = method.Name == ConstructorMethodName
            ? StripArity(method.DeclaringType.Name)
            : method.Name;

        var parameters = string.Join(", ", method.Parameters.Select((p, index) =>
        {
            var receiverPrefix = isExtensionMethod && index == 0 ? "this " : string.Empty;
            var paramType = TypeNameSimplifier.Simplify(
                p.ParameterType,
                contextNamespace,
                HasNullableAnnotation(p.CustomAttributes));
            return $"{receiverPrefix}{paramType} {p.Name}";
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
        var typeName = TypeNameSimplifier.Simplify(
            prop.PropertyType,
            contextNamespace,
            HasNullableAnnotation(prop.CustomAttributes));
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
        var typeName = TypeNameSimplifier.Simplify(
            field.FieldType,
            contextNamespace,
            HasNullableAnnotation(field.CustomAttributes));

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
        var typeName = TypeNameSimplifier.Simplify(
            evt.EventType,
            contextNamespace,
            HasNullableAnnotation(evt.CustomAttributes));
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
    ///     Returns <see langword="true"/> when the member's primary type (return type, property
    ///     type, field type, or event type) carries a <c>NullableAttribute(2)</c> annotation on
    ///     its outermost position, indicating a C# 8+ nullable reference type.
    /// </summary>
    /// <remarks>
    ///     Mono.Cecil stores nullable-reference annotations on the containing member (method return
    ///     parameter, property, field, or event) rather than on the <see cref="TypeReference"/>
    ///     itself. This method reads the correct <see cref="ICustomAttributeProvider"/> for each
    ///     member kind and delegates to <see cref="HasNullableAnnotation"/>.
    /// </remarks>
    private static bool IsMemberTypeNullableAnnotated(IMemberDefinition member) => member switch
    {
        MethodDefinition m when m.Name != ConstructorMethodName
            => HasNullableAnnotation(m.MethodReturnType.CustomAttributes),
        PropertyDefinition p => HasNullableAnnotation(p.CustomAttributes),
        FieldDefinition f => HasNullableAnnotation(f.CustomAttributes),
        EventDefinition e => HasNullableAnnotation(e.CustomAttributes),
        _ => false,
    };

    /// <summary>
    ///     Returns <see langword="true"/> when <paramref name="attrs"/> contains a
    ///     <c>System.Runtime.CompilerServices.NullableAttribute</c> whose first (or only)
    ///     byte argument is <c>2</c>, which is the compiler-emitted encoding for a nullable
    ///     reference type annotation (<c>?</c>).
    /// </summary>
    /// <remarks>
    ///     The attribute has two constructor forms: <c>NullableAttribute(byte)</c> for simple
    ///     types and <c>NullableAttribute(byte[])</c> for composite types (arrays, generics).
    ///     In the composite form, byte index 0 represents the outermost type's nullability.
    /// </remarks>
    private static bool HasNullableAnnotation(IEnumerable<CustomAttribute> attrs)
    {
        var nullableAttr = attrs.FirstOrDefault(
            a => a.AttributeType.FullName == "System.Runtime.CompilerServices.NullableAttribute");
        if (nullableAttr == null || nullableAttr.ConstructorArguments.Count == 0)
        {
            return false;
        }

        var arg = nullableAttr.ConstructorArguments[0];

        // Single-byte form: NullableAttribute(byte)
        if (arg.Value is byte b)
        {
            return b == 2;
        }

        // Byte-array form: NullableAttribute(byte[]) — index 0 is the outermost type
        if (arg.Value is CustomAttributeArgument[] arr && arr.Length > 0 && arr[0].Value is byte first)
        {
            return first == 2;
        }

        return false;
    }

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

    private static string GetMethodGroupName(MethodDefinition method)
    {
        if (method.Name == ConstructorMethodName)
        {
            return StripArity(method.DeclaringType.Name);
        }

        if (IsOperator(method))
        {
            return GetOperatorCSharpName(method);
        }

        return method.Name;
    }

    /// <summary>
    ///     Returns the C# source-level name for an operator method, suitable for use as a heading
    ///     and display name in documentation (e.g. <c>operator +</c> or <c>implicit operator double</c>).
    /// </summary>
    /// <param name="method">The operator method. Must satisfy <see cref="IsOperator"/>.</param>
    /// <returns>
    ///     For conversion operators, returns <c>implicit operator {ReturnType}</c> or
    ///     <c>explicit operator {ReturnType}</c>. For all others, returns <c>operator {symbol}</c>.
    /// </returns>
    private static string GetOperatorCSharpName(MethodDefinition method)
    {
        var conversionKeyword = method.Name switch
        {
            "op_Implicit" => "implicit",
            "op_Explicit" => "explicit",
            _ => null,
        };

        if (conversionKeyword != null)
        {
            var returnType = TypeNameSimplifier.Simplify(method.ReturnType, method.DeclaringType.Namespace);
            return $"{conversionKeyword} operator {returnType}";
        }

        return $"operator {GetOperatorSymbol(method.Name)}";
    }

    /// <summary>
    ///     Maps an IL operator method name (e.g. <c>op_Addition</c>) to its C# source-level
    ///     symbol (e.g. <c>+</c>). Returns the IL name unchanged when no mapping is defined.
    /// </summary>
    /// <param name="ilName">The IL operator method name.</param>
    /// <returns>The C# symbol string, or <paramref name="ilName"/> when unmapped.</returns>
    private static string GetOperatorSymbol(string ilName) => ilName switch
    {
        "op_Addition" => "+",
        "op_Subtraction" => "-",
        "op_Multiply" => "*",
        "op_Division" => "/",
        "op_Modulus" => "%",
        "op_BitwiseAnd" => "&",
        "op_BitwiseOr" => "|",
        "op_ExclusiveOr" => "^",
        "op_LeftShift" => "<<",
        "op_RightShift" => ">>",
        "op_Equality" => "==",
        "op_Inequality" => "!=",
        "op_LessThan" => "<",
        "op_GreaterThan" => ">",
        "op_LessThanOrEqual" => "<=",
        "op_GreaterThanOrEqual" => ">=",
        "op_UnaryNegation" => "-",
        "op_UnaryPlus" => "+",
        "op_Increment" => "++",
        "op_Decrement" => "--",
        "op_OnesComplement" => "~",
        "op_LogicalNot" => "!",
        "op_True" => "true",
        "op_False" => "false",
        _ => ilName,
    };

    /// <summary>Builds a C# operator declaration signature for an operator method.</summary>
    /// <param name="method">The operator method. Must satisfy <see cref="IsOperator"/>.</param>
    /// <param name="contextNamespace">Used to simplify type names in parameters and return type.</param>
    /// <returns>
    ///     For conversion operators: <c>public static implicit operator T(U v)</c>.
    ///     For all others: <c>public static T operator +(T left, T right)</c>.
    /// </returns>
    private static string BuildOperatorSignature(MethodDefinition method, string contextNamespace)
    {
        var accessibility = GetAccessibilityKeyword(method);
        var returnType = TypeNameSimplifier.Simplify(
            method.ReturnType,
            contextNamespace,
            HasNullableAnnotation(method.MethodReturnType.CustomAttributes));
        var parameters = string.Join(", ", method.Parameters.Select(p =>
        {
            var paramType = TypeNameSimplifier.Simplify(
                p.ParameterType,
                contextNamespace,
                HasNullableAnnotation(p.CustomAttributes));
            return $"{paramType} {p.Name}";
        }));

        return method.Name switch
        {
            "op_Implicit" => $"{accessibility} static implicit operator {returnType}({parameters})",
            "op_Explicit" => $"{accessibility} static explicit operator {returnType}({parameters})",
            _ => $"{accessibility} static {returnType} operator {GetOperatorSymbol(method.Name)}({parameters})",
        };
    }

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

    /// <summary>
    ///     Converts the IL backtick arity suffix to a plain numeric suffix for file-system-safe names.
    ///     For example, <c>Foo`2</c> becomes <c>Foo2</c>; <c>Foo</c> is unchanged.
    /// </summary>
    /// <param name="name">The raw IL type name that may contain a backtick arity suffix.</param>
    /// <returns>The name with the backtick removed but the arity count preserved.</returns>
    private static string FlattenArity(string name) => TypeNameSimplifier.FlattenArity(name);
}
