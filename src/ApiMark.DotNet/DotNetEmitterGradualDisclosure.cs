// Copyright (c) DemaConsulting LLC. All rights reserved.
// Licensed under the MIT License.

using ApiMark.Core;
using Mono.Cecil;
using static ApiMark.DotNet.DotNetEmitter;

namespace ApiMark.DotNet;

/// <summary>
///     Writes the complete gradual-disclosure Markdown tree for a .NET assembly:
///     one assembly index page, one namespace summary page per namespace, one type page
///     per visible type, and one detail page per visible member.
/// </summary>
/// <remarks>
///     Created exclusively by <see cref="DotNetEmitter.Emit"/> when the requested output
///     format is not <see cref="OutputFormat.SingleFile"/>.
/// </remarks>
internal sealed class DotNetEmitterGradualDisclosure
{
    /// <summary>Parent emitter providing shared helper methods and the data model.</summary>
    private readonly DotNetEmitter _emitter;

    /// <summary>Pre-parsed assembly data.</summary>
    private readonly DotNetAstModel _model;

    /// <summary>
    ///     Initializes a new <see cref="DotNetEmitterGradualDisclosure"/>.
    /// </summary>
    /// <param name="emitter">Parent emitter providing shared helpers.</param>
    /// <param name="model">Pre-parsed assembly data.</param>
    internal DotNetEmitterGradualDisclosure(DotNetEmitter emitter, DotNetAstModel model)
    {
        _emitter = emitter;
        _model = model;
    }

    /// <summary>Dispatches to <see cref="EmitGradualDisclosure"/>.</summary>
    /// <param name="factory">Factory for creating per-file Markdown writers.</param>
    /// <param name="config">Output configuration (unused for gradual-disclosure format).</param>
    /// <param name="context">Output channel for informational messages (unused for gradual-disclosure format).</param>
    internal void Emit(IMarkdownWriterFactory factory, EmitConfig config, IContext context)
    {
        EmitGradualDisclosure(factory);
    }

    // =========================================================================
    // Gradual-disclosure emitter
    // =========================================================================

    /// <summary>
    ///     Emits the full gradual-disclosure Markdown tree: one assembly index page,
    ///     one namespace summary per namespace, one type page per visible type, and
    ///     one detail page per visible member.
    /// </summary>
    private void EmitGradualDisclosure(IMarkdownWriterFactory factory)
    {
        // Write the top-level assembly index page with the assembly name as title
        using var apiWriter = factory.CreateMarkdown("", "api");

        apiWriter.WriteHeading(1, _model.Assembly.Name.Name + " API Reference");

        // Emit the assembly description when the AssemblyDescriptionAttribute is present
        var assemblyDescription = _model.Assembly.CustomAttributes
            .FirstOrDefault(a => a.AttributeType.FullName == "System.Reflection.AssemblyDescriptionAttribute")
            ?.ConstructorArguments.FirstOrDefault().Value as string;
        if (!string.IsNullOrWhiteSpace(assemblyDescription))
        {
            apiWriter.WriteParagraph(assemblyDescription);
        }

        // All-namespaces table — lists every namespace so AI agents get a complete map in one
        // read; counts reflect only the types declared directly in each namespace (not children)
        var nsHeaders = new[] { "Namespace", "Types", DotNetEmitter.DescriptionColumnHeader };
        var nsRows = _model.AllNamespaces.Select(nsName =>
        {
            var folderPath = GetNamespaceFolderPath(nsName, _model.RootNamespaces);
            var link = $"{folderPath}.md";
            var typeCount = _model.ByNamespace.TryGetValue(nsName, out var nsTypes) ? nsTypes.Count : 0;
            var description = _model.NamespaceDescriptions.TryGetValue(nsName, out var desc) && !string.IsNullOrEmpty(desc)
                ? desc
                : DotNetEmitter.NoDescriptionPlaceholder;
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
        foreach (var namespaceName in _model.AllNamespaces)
        {
            WriteNamespacePage(
                factory,
                namespaceName,
                new NamespaceDocContext(
                    _model.AllNamespaces,
                    _model.ByNamespace,
                    _model.RootNamespaces,
                    _model.NamespaceDescriptions,
                    _model.XmlDocs,
                    _model.Resolver));
        }
    }

    // =========================================================================
    // Namespace page writer
    // =========================================================================

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
            var childNsHeaders = new[] { "Namespace", DotNetEmitter.DescriptionColumnHeader };
            var childNsRows = children.Select(child =>
            {
                var childFolderPath = GetNamespaceFolderPath(child, ctx.RootNamespaces);
                SplitPath(childFolderPath, out _, out var childShortName);
                var link = $"{shortName}/{childShortName}.md";
                var childDesc = ctx.NamespaceDescriptions.TryGetValue(child, out var desc) && !string.IsNullOrEmpty(desc)
                    ? desc
                    : DotNetEmitter.NoDescriptionPlaceholder;
                return new[] { $"[{child}]({link})", childDesc };
            });
            nsWriter.WriteTable(childNsHeaders, childNsRows);
        }

        // List types declared directly in this namespace
        if (!ctx.ByNamespace.TryGetValue(namespaceName, out var nsTypes) || nsTypes.Count == 0)
        {
            return;
        }

        var typeHeaders = new[] { "Type", DotNetEmitter.DescriptionColumnHeader };
        var typeRows = nsTypes.Select(t =>
        {
            var typeMemberId = BuildTypeId(t);
            var summary = ctx.XmlDocs.GetSummary(typeMemberId) ?? DotNetEmitter.NoDescriptionPlaceholder;
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

    // =========================================================================
    // Type page writer
    // =========================================================================

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
        typeWriter.WriteParagraph(!string.IsNullOrEmpty(typeSummary) ? typeSummary : DotNetEmitter.NoDescriptionPlaceholder);

        var typeRemarks = ctx.XmlDocs.GetRemarks(typeMemberId);
        if (!string.IsNullOrEmpty(typeRemarks))
        {
            typeWriter.WriteParagraph(typeRemarks);
        }

        // Emit structured example blocks for this type
        foreach (var (isCode, content) in ctx.XmlDocs.GetExampleParts(typeMemberId))
        {
            if (isCode)
            {
                typeWriter.WriteCodeBlock("csharp", content);
            }
            else
            {
                typeWriter.WriteParagraph(content);
            }
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
        var allMembers = _emitter.GetVisibleMembers(ctx.Type)
            .OrderBy(m => m.Name == DotNetEmitter.ConstructorMethodName ? 0 : 1)
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
            typeWriter.WriteTable(new[] { "Member", DotNetEmitter.DescriptionColumnHeader }, constructorRows);
        }

        if (propertyRows.Count > 0)
        {
            typeWriter.WriteHeading(2, "Properties");
            typeWriter.WriteTable(new[] { "Member", "Type", DotNetEmitter.DescriptionColumnHeader }, propertyRows);
        }

        if (methodRows.Count > 0)
        {
            typeWriter.WriteHeading(2, "Methods");

            // Use "Returns" instead of "Type" for the method type column — more accurate for return values
            typeWriter.WriteTable(new[] { "Member", "Returns", DotNetEmitter.DescriptionColumnHeader }, methodRows);
        }

        if (fieldRows.Count > 0)
        {
            typeWriter.WriteHeading(2, "Fields");
            typeWriter.WriteTable(new[] { "Member", "Type", DotNetEmitter.DescriptionColumnHeader }, fieldRows);
        }

        if (eventRows.Count > 0)
        {
            typeWriter.WriteHeading(2, "Events");
            typeWriter.WriteTable(new[] { "Member", "Type", DotNetEmitter.DescriptionColumnHeader }, eventRows);
        }

        // Emit Operators section when the type has operator overloads — all operators share
        // a single page to prevent file-name collisions between op_Addition, op_Subtraction, etc.
        if (operatorMethods.Count > 0)
        {
            WriteTypeOperatorsPage(ctx.Factory, ctx.NamespaceName, ctx.NamespaceFolderPath, ctx.Type, operatorMethods, ctx.XmlDocs, ctx.Resolver);
            typeWriter.WriteHeading(2, "Operators");
            typeWriter.WriteTable(
                new[] { "Member", DotNetEmitter.DescriptionColumnHeader },
                new[] { new[] { $"[Operators]({FlattenArity(ctx.Type.Name)}/operators.md)", "Operator overloads" } });
        }

        // Emit Nested Types section when the type has visible nested types — each nested type
        // receives a dedicated page under the containing type's folder so the documentation
        // hierarchy mirrors the C# type hierarchy
        var visibleNestedTypes = _emitter.GetVisibleNestedTypes(ctx.Type).ToList();
        if (visibleNestedTypes.Count > 0)
        {
            typeWriter.WriteHeading(2, "Nested Types");
            var nestedTypeHeaders = new[] { "Type", DotNetEmitter.DescriptionColumnHeader };
            var nestedTypeRows = visibleNestedTypes.Select(nested =>
            {
                var nestedTypeId = BuildTypeId(nested);
                var nestedSummary = ctx.XmlDocs.GetSummary(nestedTypeId) ?? DotNetEmitter.NoDescriptionPlaceholder;
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

    // =========================================================================
    // Member page writers
    // =========================================================================

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

        WriteNonMethodMemberContent(memberWriter, member, memberId, new MethodDocContext(ctx.NamespaceName, ctx.XmlDocs, ctx.Resolver, memberCurrentFolder, new SortedSet<ExternalTypeInfo>()));
    }

    /// <summary>
    ///     Writes a single consolidated Markdown page for a pure method overload group — all methods
    ///     sharing the same base name and therefore the same sanitized file name.
    /// </summary>
    /// <remarks>
    ///     Each overload is distinguished by an H2 heading showing its full parameter signature via
    ///     <see cref="DotNetEmitter.BuildMethodDisplayName"/>. External type references are accumulated
    ///     across all overloads and emitted as a shared External Types section at the bottom of the page.
    /// </remarks>
    /// <param name="factory">Factory for creating the output Markdown writer.</param>
    /// <param name="namespaceName">Full namespace name of the declaring type; used to simplify type names.</param>
    /// <param name="namespaceFolderPath">File-system folder path for the namespace.</param>
    /// <param name="type">The type definition that declares the overload group.</param>
    /// <param name="overloads">Ordered list of overload methods (at least one element).</param>
    /// <param name="xmlDocs">Documentation index for per-overload member-ID lookups.</param>
    /// <param name="resolver">Type link resolver for table cell link generation.</param>
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
    ///     <see cref="DotNetEmitter.IsOperator"/>. Must contain at least one element.
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

    // =========================================================================
    // Member processing helpers
    // =========================================================================

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
        MethodDefinition m when m.Name == DotNetEmitter.ConstructorMethodName => "Constructor",
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
        var memberSummary = ctx.XmlDocs.GetSummary(memberId) ?? DotNetEmitter.NoDescriptionPlaceholder;
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
            var isConstructor = singleMethod.Name == DotNetEmitter.ConstructorMethodName;
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
        var representativeSummary = ctx.XmlDocs.GetSummary(representativeMemberId) ?? DotNetEmitter.NoDescriptionPlaceholder;
        var representativeTypeRef = GetMemberTypeRef(representative);
        var representativeTypeName = representativeTypeRef != null
            ? ctx.Resolver.Linkify(representativeTypeRef, ctx.NamespaceFolderPath, ctx.NamespaceName, externalTypes, IsMemberTypeNullableAnnotated(representative))
            : string.Empty;
        var overloadDisplayName = GetMethodGroupDisplayName(representative, orderedOverloads.Count);
        var overloadFileName = GetSanitizedMemberFileName(representative, ctx.Type);
        var memberLink = $"{FlattenArity(ctx.Type.Name)}/{overloadFileName}.md";
        var isConstructorGroup = representative.Name == DotNetEmitter.ConstructorMethodName;

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
        var memberSummary = ctx.XmlDocs.GetSummary(memberId) ?? DotNetEmitter.NoDescriptionPlaceholder;
        var memberTypeRef = GetMemberTypeRef(member);
        var memberTypeName = memberTypeRef != null
            ? ctx.Resolver.Linkify(memberTypeRef, ctx.NamespaceFolderPath, ctx.NamespaceName, externalTypes, IsMemberTypeNullableAnnotated(member))
            : string.Empty;
        var memberDisplayName = GetMemberDisplayName(member);

        switch (member)
        {
            case MethodDefinition m:
                var isConstructor = m.Name == DotNetEmitter.ConstructorMethodName;
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
        writer.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : DotNetEmitter.NoDescriptionPlaceholder);

        var returns = ctx.XmlDocs.GetReturns(memberId);
        if (!string.IsNullOrEmpty(returns))
        {
            writer.WriteParagraph($"**Returns:** {returns}");
        }

        var exceptions = ctx.XmlDocs.GetExceptionDetails(memberId);
        if (exceptions.Count > 0)
        {
            var exHeaders = new[] { "Exception", DotNetEmitter.DescriptionColumnHeader };
            var exRows = exceptions.Select(e => new[] { e.Type, e.Description ?? string.Empty });
            writer.WriteTable(exHeaders, exRows);
        }

        var remarks = ctx.XmlDocs.GetRemarks(memberId);
        if (!string.IsNullOrEmpty(remarks))
        {
            writer.WriteParagraph(remarks);
        }

        foreach (var (isCode, content) in ctx.XmlDocs.GetExampleParts(memberId))
        {
            if (isCode)
            {
                writer.WriteCodeBlock("csharp", content);
            }
            else
            {
                writer.WriteParagraph(content);
            }
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
        memberWriter.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : DotNetEmitter.NoDescriptionPlaceholder);

        if (method.HasParameters)
        {
            var paramDocs = ctx.XmlDocs.GetParams(memberId);
            var paramHeaders = new[] { "Parameter", "Type", DotNetEmitter.DescriptionColumnHeader };

            // Linkify parameter types — resolver tracks external types and emits links for intra-assembly types
            var paramRows = method.Parameters.Select(p =>
            {
                var desc = paramDocs.FirstOrDefault(pd => pd.Name == p.Name).Description ?? NoDescriptionPlaceholder;
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
            var exHeaders = new[] { "Exception", DotNetEmitter.DescriptionColumnHeader };
            var exRows = exceptions.Select(e => new[] { e.Type, e.Description ?? string.Empty });
            memberWriter.WriteTable(exHeaders, exRows);
        }

        var remarks = ctx.XmlDocs.GetRemarks(memberId);
        if (!string.IsNullOrEmpty(remarks))
        {
            memberWriter.WriteParagraph(remarks);
        }

        foreach (var (isCode, content) in ctx.XmlDocs.GetExampleParts(memberId))
        {
            if (isCode)
            {
                memberWriter.WriteCodeBlock("csharp", content);
            }
            else
            {
                memberWriter.WriteParagraph(content);
            }
        }
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
}
