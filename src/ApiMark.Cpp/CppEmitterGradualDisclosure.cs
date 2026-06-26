using ApiMark.Core;
using ApiMark.Cpp.CppAst;

namespace ApiMark.Cpp;

/// <summary>
///     Gradual-disclosure emitter for C++ API documentation. Writes one file per namespace,
///     type, and member, creating a navigable tree of Markdown pages.
/// </summary>
internal sealed class CppEmitterGradualDisclosure
{
    /// <summary>Column headers for the file-naming and path-convention table on the index page.</summary>
    private static readonly string[] PathConventionHeaders = ["Symbol kind", "Path pattern"];

    /// <summary>Row data for the file-naming and path-convention table on the index page.</summary>
    private static readonly string[][] PathConventionRows =
    [
        ["Namespace", "`{Namespace}.md`"],
        ["Type", "`{Namespace}/{TypeName}.md`"],
        ["Member", "`{Namespace}/{TypeName}/{MemberName}.md`"],
        ["Nested type", "`{Namespace}/{OuterType}/{NestedType}.md`"],
        ["Class-scoped type alias", "`{Namespace}/{TypeName}/{AliasName}.md`"],
        ["Free function", "`{Namespace}/{FunctionName}.md`"],
        ["Enum", "`{Namespace}/{EnumName}.md`"],
        ["Type alias", "`{Namespace}/{AliasName}.md`"],
        ["Operators (class)", "`{Namespace}/{TypeName}/operators.md`"],
        ["Operators (namespace)", "`{Namespace}/operators.md`"],
    ];

    /// <summary>Parent emitter providing options and shared helper methods.</summary>
    private readonly CppEmitter _emitter;

    /// <summary>Namespace declarations collected during parse, sorted by namespace key.</summary>
    private readonly SortedDictionary<string, CppEmitter.NamespaceDeclarations> _namespaceDecls;

    /// <summary>Type link resolver built from the parsed namespace declarations.</summary>
    private readonly CppTypeLinkResolver _cppResolver;

    /// <summary>Initializes a new gradual-disclosure emitter.</summary>
    /// <param name="emitter">Parent emitter providing options and shared helper methods.</param>
    /// <param name="namespaceDecls">Namespace declarations collected during parse, sorted by namespace key.</param>
    /// <param name="cppResolver">Type link resolver built from the parsed namespace declarations.</param>
    internal CppEmitterGradualDisclosure(
        CppEmitter emitter,
        SortedDictionary<string, CppEmitter.NamespaceDeclarations> namespaceDecls,
        CppTypeLinkResolver cppResolver)
    {
        _emitter = emitter;
        _namespaceDecls = namespaceDecls;
        _cppResolver = cppResolver;
    }

    // =========================================================================
    // Entry point
    // =========================================================================

    /// <summary>
    ///     Emits the full gradual-disclosure Markdown tree: one library index page,
    ///     one namespace summary per namespace, one type page per class, and
    ///     one detail page per member.
    /// </summary>
    /// <param name="factory">Factory used to create per-file Markdown writers.</param>
    /// <param name="config">Emission configuration including heading depth and format.</param>
    /// <param name="context">Logging and diagnostic context.</param>
    internal void Emit(IMarkdownWriterFactory factory, EmitConfig config, IContext context)
    {
        EmitGradualDisclosure(factory);
    }

    /// <summary>
    ///     Writes the complete gradual-disclosure tree starting with the API index page,
    ///     then iterating over all namespace declarations.
    /// </summary>
    private void EmitGradualDisclosure(IMarkdownWriterFactory factory)
    {
        // Write the library entrypoint page listing all discovered namespaces
        WriteApiPage(factory, _namespaceDecls);

        // Write one namespace summary page per namespace, one type page per owned class,
        // one detail page per owned free function, and one detail page per owned enum
        foreach (var (nsKey, nsDecls) in _namespaceDecls)
        {
            WriteNamespacePage(factory, nsKey, nsDecls, _cppResolver);
            foreach (var cls in nsDecls.Classes)
            {
                WriteTypePage(new CppTypePageWriteContext(factory, nsKey, nsDecls.DisplayName, cls, _cppResolver));
                WriteNestedTypePages(factory, nsKey, nsDecls.DisplayName, cls, _cppResolver);
            }

            // Partition free functions into regular functions and operator overloads;
            // operator names such as operator+, operator-, and operator<< all sanitize
            // to the same file name so all operators share a single operators.md page
            // instead of producing individual colliding files
            var nsOperatorFunctions = nsDecls.FreeFunctions
                .Where(fn => fn.Name.StartsWith("operator", StringComparison.Ordinal))
                .ToList();
            foreach (var fn in nsDecls.FreeFunctions
                .Where(fn => !fn.Name.StartsWith("operator", StringComparison.Ordinal)))
            {
                WriteFreeFunctionPage(factory, nsKey, nsDecls.DisplayName, fn, _cppResolver);
            }

            if (nsOperatorFunctions.Count > 0)
            {
                WriteNamespaceOperatorsPage(factory, nsKey, nsDecls.DisplayName, nsOperatorFunctions, _cppResolver);
            }

            // Write one enum detail page per owned enum declared in this namespace
            foreach (var en in nsDecls.Enums)
            {
                WriteEnumPage(factory, nsKey, nsDecls.DisplayName, en);
            }

            // Write one type alias page per owned using-alias declared in this namespace
            foreach (var alias in nsDecls.TypeAliases)
            {
                WriteTypeAliasPage(factory, nsKey, nsDecls.DisplayName, alias, _cppResolver);
            }
        }
    }

    // =========================================================================
    // Page writers
    // =========================================================================

    /// <summary>
    ///     Writes the library entrypoint <c>api.md</c> listing all documented namespaces.
    /// </summary>
    /// <param name="factory">Factory for creating the output writer.</param>
    /// <param name="namespaces">All documented namespaces grouped and sorted by key.</param>
    private void WriteApiPage(
        IMarkdownWriterFactory factory,
        SortedDictionary<string, CppEmitter.NamespaceDeclarations> namespaces)
    {
        using var writer = factory.CreateMarkdown("", "api");
        writer.WriteHeading(1, $"{_emitter.Options.LibraryName} API Reference");

        // Emit optional library description paragraph
        if (!string.IsNullOrWhiteSpace(_emitter.Options.Description))
        {
            writer.WriteParagraph(_emitter.Options.Description);
        }

        if (namespaces.Count == 0)
        {
            // Emit a fallback paragraph so api.md is never completely empty
            writer.WriteParagraph("No public API declarations found.");
            return;
        }

        // All-namespaces table — lists every namespace so AI agents get a complete map in one read.
        // Declarations count reflects only declarations directly in each namespace
        var headers = new[] { "Namespace", "Declarations", CppEmitter.DescriptionColumnHeader };
        var rows = namespaces.Select(kv =>
        {
            var nsDecls = kv.Value;
            var declarationCount = nsDecls.Classes.Count + nsDecls.Enums.Count + nsDecls.FreeFunctions.Count + nsDecls.TypeAliases.Count;
            var description = CppEmitter.GetNamespaceDescription(nsDecls);
            return new[] { $"[{nsDecls.DisplayName}]({kv.Key}.md)", declarationCount.ToString(), description };
        });
        writer.WriteTable(headers, rows);

        // Path convention appendix — helps AI agents navigate without a separate resolver
        writer.WriteHeading(2, "File Naming and Path Convention");
        writer.WriteParagraph("Documentation paths are derived deterministically from fully-qualified symbol names. Namespace separators (`::`) are replaced with `.` in file and folder names.");
        writer.WriteTable(PathConventionHeaders, PathConventionRows);
    }

    /// <summary>
    ///     Writes the type page for a single C++ class or struct, including the qualified
    ///     name, an optional template declaration, an optional <c>#include</c> directive,
    ///     summary, base types, and grouped member sub-tables for constructors, methods, and fields.
    /// </summary>
    /// <param name="ctx">The type-page context encapsulating factory, namespace, class, and resolver.</param>
    private void WriteTypePage(CppTypePageWriteContext ctx)
    {
        // Build the template parameter suffix (e.g. "<T>" for template<typename T> class Stack)
        var templateParamDisplay = CppEmitter.BuildTemplateParamDisplay(ctx.Class);
        var displayName = string.IsNullOrEmpty(templateParamDisplay)
            ? ctx.Class.Name
            : $"{ctx.Class.Name}{templateParamDisplay}";

        // Fully-qualified name for the signature comment (e.g. "fixtures::Stack<T>")
        var qualifiedClassName = string.IsNullOrEmpty(ctx.NsDisplayName)
            ? displayName
            : $"{ctx.NsDisplayName}::{displayName}";

        using var writer = ctx.Factory.CreateMarkdown(ctx.NsKey, ctx.Class.Name);
        writer.WriteHeading(1, displayName);
        WriteClassSignatureBlock(writer, ctx.Class, qualifiedClassName);

        // Emit summary from doc comment, or placeholder when no comment is present
        var typeSummary = CppEmitter.GetSummary(ctx.Class.Doc);
        writer.WriteParagraph(!string.IsNullOrEmpty(typeSummary) ? typeSummary : CppEmitter.NoDescriptionPlaceholder);

        // Emit extended details when the doc comment contains a @details or @remarks block
        var typeDetails = CppEmitter.GetDetails(ctx.Class.Doc);
        if (!string.IsNullOrEmpty(typeDetails))
        {
            writer.WriteParagraph(typeDetails);
        }

        // Emit @note as a blockquote when present
        var typeNote = CppEmitter.GetNote(ctx.Class.Doc);
        if (!string.IsNullOrEmpty(typeNote))
        {
            writer.WriteParagraph($"> **Note:** {typeNote}");
        }

        // Emit @code example block when present
        var typeExample = CppEmitter.GetExample(ctx.Class.Doc);
        if (!string.IsNullOrEmpty(typeExample))
        {
            writer.WriteCodeBlock("cpp", typeExample);
        }

        WriteClassBaseTypesParagraph(writer, ctx.Class);

        // Collect visible constructors, methods, and fields; sorted alphabetically for deterministic output
        var visibleCtors = _emitter.GetVisibleConstructors(ctx.Class).OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
        var visibleMethods = _emitter.GetVisibleMethods(ctx.Class).OrderBy(m => m.Name, StringComparer.Ordinal).ToList();
        var visibleFields = _emitter.GetVisibleFields(ctx.Class).OrderBy(f => f.Name, StringComparer.Ordinal).ToList();

        // Accumulate external type references found in type-column cells on this page
        var externalTypes = new SortedSet<CppExternalTypeInfo>();

        // Early exit when the class has no members, no nested classes, and no type aliases —
        // emitting an empty page with only the signature and summary is still valid output
        if (visibleCtors.Count == 0 && visibleMethods.Count == 0 && visibleFields.Count == 0
            && ctx.Class.NestedClasses.Count == 0 && ctx.Class.TypeAliases.Count == 0)
        {
            return;
        }

        // Partition methods into operator overloads and regular methods; operator overloads are
        // grouped onto a single operators.md page to prevent file-name collisions
        var operatorMethods = visibleMethods
            .Where(m => m.Name.StartsWith("operator", StringComparison.Ordinal))
            .ToList();
        var regularMethods = visibleMethods
            .Where(m => !m.Name.StartsWith("operator", StringComparison.Ordinal))
            .ToList();

        // Build a flat list of all visible members for case-insensitive collision detection
        var allMembers = new List<object>(
            visibleCtors.Cast<object>()
                .Concat(regularMethods.Cast<object>())
                .Concat(visibleFields.Cast<object>()));

        // Case-insensitive map: lowercase member name → list of members sharing that lowercase name
        var caseInsensitiveGroups = new Dictionary<string, List<object>>(StringComparer.Ordinal);
        foreach (var member in allMembers)
        {
            var baseName = CppEmitter.GetMemberBaseName(member, ctx.Class.Name);
            var lowerKey = baseName.ToLowerInvariant();
            if (!caseInsensitiveGroups.TryGetValue(lowerKey, out var list))
            {
                list = [];
                caseInsensitiveGroups[lowerKey] = list;
            }

            list.Add(member);
        }

        var writtenKeys = new HashSet<string>(StringComparer.Ordinal);
        var ctorRows = new List<string[]>();
        var methodRows = new List<string[]>();
        var fieldRows = new List<string[]>();

        foreach (var ctor in visibleCtors)
        {
            ProcessClassConstructorMember(ctx, ctor, caseInsensitiveGroups, writtenKeys, ctorRows);
        }

        foreach (var method in regularMethods)
        {
            ProcessClassMethodMember(ctx, method, caseInsensitiveGroups, writtenKeys, methodRows, externalTypes);
        }

        foreach (var field in visibleFields)
        {
            ProcessClassFieldMember(ctx, field, caseInsensitiveGroups, writtenKeys, fieldRows, externalTypes);
        }

        WriteClassMemberTables(writer, ctx, operatorMethods, ctorRows, methodRows, fieldRows, externalTypes);
    }

    /// <summary>
    ///     Emits the fenced C++ signature block for a class, including the qualified name comment,
    ///     optional template declaration, <c>#include</c> directive, and optional class declaration line.
    /// </summary>
    private void WriteClassSignatureBlock(IMarkdownWriter writer, CppClass cls, string qualifiedClassName)
    {
        var sourceFile = cls.Location?.File;
        if (string.IsNullOrEmpty(sourceFile))
        {
            return;
        }

        var includePath = _emitter.GetIncludePath(sourceFile);
        var sigParts = new List<string> { $"// {qualifiedClassName}" };

        // Prepend the template<...> line when the class is a template so the signature
        // is a valid, copy-pasteable C++ forward-declaration fragment
        var templateDecl = CppEmitter.BuildTemplateDeclaration(cls);
        if (!string.IsNullOrEmpty(templateDecl))
        {
            sigParts.Add(templateDecl);
        }

        sigParts.Add($"#include \"{includePath}\"");

        // Append the class declaration line when the class is marked final or has base types
        if (cls.IsFinal || cls.BaseTypes.Count > 0)
        {
            sigParts.Add(CppEmitter.BuildClassDeclaration(cls));
        }

        writer.WriteSignature("cpp", string.Join("\n", sigParts));
    }

    /// <summary>
    ///     Emits the <c>**Inherits**: …</c> paragraph listing simplified base type names when
    ///     the class has one or more public base types.
    /// </summary>
    private static void WriteClassBaseTypesParagraph(IMarkdownWriter writer, CppClass cls)
    {
        if (cls.BaseTypes.Count == 0)
        {
            return;
        }

        var baseNames = cls.BaseTypes
            .Select(bt => CppEmitter.SimplifyTypeName(bt.Name))
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();
        if (baseNames.Count > 0)
        {
            writer.WriteParagraph($"**Inherits**: {string.Join(", ", baseNames)}");
        }
    }

    /// <summary>
    ///     Processes a single constructor by writing its detail page (or a shared collision page)
    ///     and appending the appropriate row to <paramref name="ctorRows"/>.
    /// </summary>
    private static void ProcessClassConstructorMember(
        CppTypePageWriteContext ctx,
        CppFunction ctor,
        IReadOnlyDictionary<string, List<object>> caseInsensitiveGroups,
        HashSet<string> writtenKeys,
        List<string[]> ctorRows)
    {
        var baseName = CppEmitter.GetMemberBaseName(ctor, ctx.Class.Name);
        var lowerKey = baseName.ToLowerInvariant();
        var group = caseInsensitiveGroups[lowerKey];
        var pageFileName = CppEmitter.SanitizeFileName(group.Count == 1 ? baseName : lowerKey);
        var ctorSummary = CppEmitter.GetSummary(ctor.Doc) ?? CppEmitter.NoDescriptionPlaceholder;

        // Show simplified parameter types in the link text so readers can
        // distinguish overloaded constructors at a glance
        var paramTypes = string.Join(", ", ctor.Parameters.Select(p => CppEmitter.SimplifyTypeName(p.TypeName)));

        if (writtenKeys.Add(lowerKey))
        {
            if (group.Count == 1)
            {
                WriteMemberPage(ctx.Factory, ctx.NsKey, ctx.NsDisplayName, ctx.Class, ctor, pageFileName, ctx.CppResolver);
            }
            else
            {
                CppEmitter.WriteCombinedMemberPage(ctx.Factory, ctx.NsKey, ctx.NsDisplayName, ctx.Class, pageFileName, group, ctx.CppResolver);
            }
        }

        ctorRows.Add(new[] { $"[{ctx.Class.Name}({paramTypes})]({ctx.Class.Name}/{pageFileName}.md)", ctorSummary });
    }

    /// <summary>
    ///     Processes a single regular method by writing its detail page (or a shared collision page)
    ///     and appending the appropriate row to <paramref name="methodRows"/>.
    /// </summary>
    private static void ProcessClassMethodMember(
        CppTypePageWriteContext ctx,
        CppFunction method,
        IReadOnlyDictionary<string, List<object>> caseInsensitiveGroups,
        HashSet<string> writtenKeys,
        List<string[]> methodRows,
        SortedSet<CppExternalTypeInfo> externalTypes)
    {
        var baseName = CppEmitter.GetMemberBaseName(method, ctx.Class.Name);
        var lowerKey = baseName.ToLowerInvariant();
        var group = caseInsensitiveGroups[lowerKey];
        var pageFileName = CppEmitter.SanitizeFileName(group.Count == 1 ? baseName : lowerKey);
        var methodSummary = CppEmitter.GetSummary(method.Doc) ?? CppEmitter.NoDescriptionPlaceholder;

        // Linkify the return type cell using the type link resolver
        var returnType = ctx.CppResolver.Linkify(CppEmitter.SimplifyTypeName(method.ReturnTypeName), ctx.NsKey, externalTypes)!;

        if (writtenKeys.Add(lowerKey))
        {
            if (group.Count == 1)
            {
                WriteMemberPage(ctx.Factory, ctx.NsKey, ctx.NsDisplayName, ctx.Class, method, pageFileName, ctx.CppResolver);
            }
            else
            {
                CppEmitter.WriteCombinedMemberPage(ctx.Factory, ctx.NsKey, ctx.NsDisplayName, ctx.Class, pageFileName, group, ctx.CppResolver);
            }
        }

        // Show simplified parameter types in the link text so readers can
        // distinguish overloaded methods at a glance
        var methodParamTypes = string.Join(", ", method.Parameters.Select(p => CppEmitter.SimplifyTypeName(p.TypeName)));
        methodRows.Add(new[] { $"[{method.Name}({methodParamTypes})]({ctx.Class.Name}/{pageFileName}.md)", returnType, methodSummary });
    }

    /// <summary>
    ///     Processes a single field by writing its detail page (or a shared collision page)
    ///     and appending the appropriate row to <paramref name="fieldRows"/>.
    /// </summary>
    private static void ProcessClassFieldMember(
        CppTypePageWriteContext ctx,
        CppField field,
        IReadOnlyDictionary<string, List<object>> caseInsensitiveGroups,
        HashSet<string> writtenKeys,
        List<string[]> fieldRows,
        SortedSet<CppExternalTypeInfo> externalTypes)
    {
        var baseName = CppEmitter.GetMemberBaseName(field, ctx.Class.Name);
        var lowerKey = baseName.ToLowerInvariant();
        var group = caseInsensitiveGroups[lowerKey];
        var pageFileName = CppEmitter.SanitizeFileName(group.Count == 1 ? baseName : lowerKey);
        var fieldSummary = CppEmitter.GetSummary(field.Doc) ?? CppEmitter.NoDescriptionPlaceholder;

        // Linkify the field type cell using the type link resolver
        var typeName = ctx.CppResolver.Linkify(CppEmitter.SimplifyTypeName(field.TypeName), ctx.NsKey, externalTypes)!;

        if (writtenKeys.Add(lowerKey))
        {
            if (group.Count == 1)
            {
                WriteMemberPage(ctx.Factory, ctx.NsKey, ctx.NsDisplayName, ctx.Class, field, pageFileName, ctx.CppResolver);
            }
            else
            {
                CppEmitter.WriteCombinedMemberPage(ctx.Factory, ctx.NsKey, ctx.NsDisplayName, ctx.Class, pageFileName, group, ctx.CppResolver);
            }
        }

        fieldRows.Add(new[] { $"[{field.Name}]({ctx.Class.Name}/{pageFileName}.md)", typeName, fieldSummary });
    }

    /// <summary>
    ///     Emits all member sub-tables (Constructors, Methods, Fields, Operators, Nested Classes,
    ///     and Type Aliases) onto the type page in canonical section order.
    /// </summary>
    private void WriteClassMemberTables(
        IMarkdownWriter writer,
        CppTypePageWriteContext ctx,
        IReadOnlyList<CppFunction> operatorMethods,
        List<string[]> ctorRows,
        List<string[]> methodRows,
        List<string[]> fieldRows,
        SortedSet<CppExternalTypeInfo> externalTypes)
    {
        // Emit grouped sub-tables in the canonical order: Constructors, Methods, Fields
        if (ctorRows.Count > 0)
        {
            writer.WriteHeading(2, "Constructors");
            writer.WriteTable(new[] { "Constructor", CppEmitter.DescriptionColumnHeader }, ctorRows);
        }

        if (methodRows.Count > 0)
        {
            writer.WriteHeading(2, "Methods");
            writer.WriteTable(new[] { "Member", "Returns", CppEmitter.DescriptionColumnHeader }, methodRows);
        }

        if (fieldRows.Count > 0)
        {
            writer.WriteHeading(2, "Fields");
            writer.WriteTable(new[] { "Member", "Type", CppEmitter.DescriptionColumnHeader }, fieldRows);
        }

        // Emit Operators section when the class has operator overloads — all operators share
        // a single page to prevent file-name collisions between operator+, operator-, etc.
        if (operatorMethods.Count > 0)
        {
            WriteClassOperatorsPage(ctx.Factory, ctx.NsKey, ctx.NsDisplayName, ctx.Class, operatorMethods, ctx.CppResolver);
            writer.WriteHeading(2, "Operators");
            writer.WriteTable(
                new[] { "Operators", CppEmitter.DescriptionColumnHeader },
                new[] { new[] { $"[operators]({ctx.Class.Name}/operators.md)", "Operator overloads" } });
        }

        // Emit Nested Classes table so readers can discover inner types without browsing the header
        if (ctx.Class.NestedClasses.Count > 0)
        {
            writer.WriteHeading(2, "Nested Classes");
            var nestedHeaders = new[] { "Type", CppEmitter.DescriptionColumnHeader };
            var nestedRows = ctx.Class.NestedClasses
                .OrderBy(n => n.Name, StringComparer.Ordinal)
                .Select(nested =>
                {
                    var summary = CppEmitter.GetSummary(nested.Doc) ?? CppEmitter.NoDescriptionPlaceholder;
                    return new[] { $"[{nested.Name}]({ctx.Class.Name}/{nested.Name}.md)", summary };
                });
            writer.WriteTable(nestedHeaders, nestedRows);
        }

        // Emit Type Aliases table for public class-scoped using-aliases
        if (ctx.Class.TypeAliases.Count > 0)
        {
            writer.WriteHeading(2, "Type Aliases");
            var aliasHeaders = new[] { "Alias", "Underlying Type", CppEmitter.DescriptionColumnHeader };
            var aliasRows = ctx.Class.TypeAliases
                .OrderBy(a => a.Name, StringComparer.Ordinal)
                .Select(alias =>
                {
                    var summary = CppEmitter.GetSummary(alias.Doc) ?? CppEmitter.NoDescriptionPlaceholder;
                    var underlying = ctx.CppResolver.Linkify(CppEmitter.SimplifyTypeName(alias.UnderlyingTypeName), ctx.NsKey, externalTypes)!;
                    return new[] { $"[{alias.Name}]({ctx.Class.Name}/{alias.Name}.md)", underlying, summary };
                });
            writer.WriteTable(aliasHeaders, aliasRows);
        }

        CppEmitter.WriteExternalTypesSection(writer, externalTypes);
    }

    /// <summary>
    ///     Recursively writes type alias pages and nested class pages for a class.
    /// </summary>
    private void WriteNestedTypePages(
        IMarkdownWriterFactory factory,
        string parentKey,
        string parentDisplayName,
        CppClass cls,
        CppTypeLinkResolver cppResolver)
    {
        // The class folder and display name form the base for all children of this class
        var clsKey = $"{parentKey}/{cls.Name}";
        var clsDisplayName = $"{parentDisplayName}::{cls.Name}";

        // Write one type alias page per public class-scoped using-alias
        foreach (var alias in cls.TypeAliases)
        {
            WriteTypeAliasPage(factory, clsKey, clsDisplayName, alias, cppResolver);
        }

        // Write one type page per public nested class and recurse into its own children
        foreach (var nested in cls.NestedClasses)
        {
            WriteTypePage(new CppTypePageWriteContext(factory, clsKey, clsDisplayName, nested, cppResolver));
            WriteNestedTypePages(factory, clsKey, clsDisplayName, nested, cppResolver);
        }
    }

    /// <summary>
    ///     Writes the combined operator overloads page for a class.
    /// </summary>
    private void WriteClassOperatorsPage(
        IMarkdownWriterFactory factory,
        string nsKey,
        string nsDisplayName,
        CppClass cls,
        IReadOnlyList<CppFunction> operators,
        CppTypeLinkResolver cppResolver)
    {
        var opsCurrentFolder = $"{nsKey}/{cls.Name}";
        using var writer = factory.CreateMarkdown(opsCurrentFolder, "operators");
        writer.WriteHeading(1, "Operators");
        // Emit the qualified class name comment and #include directive from the first operator
        // that has source location information — gives readers context without browsing headers
        var qualifiedClassName = string.IsNullOrEmpty(nsDisplayName)
            ? cls.Name
            : $"{nsDisplayName}::{cls.Name}";
        var firstWithLocation = operators.FirstOrDefault(op => op.Location != null);
        if (firstWithLocation != null)
        {
            var includePath = _emitter.GetIncludePath(firstWithLocation.Location!.File);
            writer.WriteSignature("cpp", $"// {qualifiedClassName}\n#include \"{includePath}\"");
        }

        writer.WriteParagraph($"Operator overloads for {cls.Name}.");

        var externalTypes = new SortedSet<CppExternalTypeInfo>();

        // Emit an H2 section for each operator so readers can locate a specific overload quickly
        foreach (var op in operators)
        {
            var paramTypes = string.Join(", ", op.Parameters.Select(p => CppEmitter.SimplifyTypeName(p.TypeName)));
            writer.WriteHeading(2, $"{op.Name}({paramTypes})");
            WriteFunctionContent(writer, op, new CppFunctionWriteContext(nsDisplayName, cls.Name, cppResolver, opsCurrentFolder, externalTypes, 3));
        }

        CppEmitter.WriteExternalTypesSection(writer, externalTypes);
    }

    /// <summary>
    ///     Writes the combined operator overloads page for a namespace.
    /// </summary>
    private void WriteNamespaceOperatorsPage(
        IMarkdownWriterFactory factory,
        string nsKey,
        string nsDisplayName,
        IReadOnlyList<CppFunction> operators,
        CppTypeLinkResolver cppResolver)
    {
        var opsCurrentFolder = nsKey;
        using var writer = factory.CreateMarkdown(nsKey, "operators");
        writer.WriteHeading(1, "Operators");
        // Emit the qualified name comment and #include directive from the first operator that
        // has source location information so readers know which header to include
        var firstWithLocation = operators.FirstOrDefault(op => op.Location != null);
        if (firstWithLocation != null)
        {
            var qualifiedName = string.IsNullOrEmpty(nsDisplayName)
                ? firstWithLocation.Name
                : $"{nsDisplayName}::{firstWithLocation.Name}";
            var includePath = _emitter.GetIncludePath(firstWithLocation.Location!.File);
            writer.WriteSignature("cpp", $"// {qualifiedName}\n#include \"{includePath}\"");
        }

        var displayNs = string.IsNullOrEmpty(nsDisplayName) ? CppEmitter.GlobalNamespaceKey : nsDisplayName;
        writer.WriteParagraph($"Operator overloads in the {displayNs} namespace.");

        var externalTypes = new SortedSet<CppExternalTypeInfo>();

        // Emit an H2 section for each operator so readers can locate a specific overload quickly
        foreach (var op in operators)
        {
            var paramTypes = string.Join(", ", op.Parameters.Select(p => CppEmitter.SimplifyTypeName(p.TypeName)));
            writer.WriteHeading(2, $"{op.Name}({paramTypes})");
            WriteFreeFunctionContent(writer, nsDisplayName, op, cppResolver, opsCurrentFolder, externalTypes, parametersHeadingLevel: 3);
        }

        CppEmitter.WriteExternalTypesSection(writer, externalTypes);
    }

    /// <summary>
    ///     Writes the detail page for a single namespace-level free function.
    /// </summary>
    private void WriteFreeFunctionPage(
        IMarkdownWriterFactory factory,
        string nsKey,
        string nsDisplayName,
        CppFunction fn,
        CppTypeLinkResolver cppResolver)
    {
        var fnCurrentFolder = nsKey;
        using var writer = factory.CreateMarkdown(nsKey, CppEmitter.SanitizeFileName(fn.Name));
        writer.WriteHeading(1, fn.Name);
        var externalTypes = new SortedSet<CppExternalTypeInfo>();
        WriteFreeFunctionContent(writer, nsDisplayName, fn, cppResolver, fnCurrentFolder, externalTypes);
        CppEmitter.WriteExternalTypesSection(writer, externalTypes);
    }

    /// <summary>
    ///     Writes the body content for a namespace-level free function page without the heading.
    /// </summary>
    private void WriteFreeFunctionContent(
        IMarkdownWriter writer,
        string nsDisplayName,
        CppFunction fn,
        CppTypeLinkResolver cppResolver,
        string currentFolder,
        ISet<CppExternalTypeInfo> externalTypes,
        int parametersHeadingLevel = 2)
    {
        // Emit the fully-qualified name as a comment followed by the optional #include
        // directive and C++ signature so that an AI reader has all context needed to
        // use the function without browsing the header tree
        var qualifiedName = string.IsNullOrEmpty(nsDisplayName)
            ? fn.Name
            : $"{nsDisplayName}::{fn.Name}";
        var signature = CppEmitter.BuildMethodSignature(fn);
        if (fn.Location != null)
        {
            var includePath = _emitter.GetIncludePath(fn.Location.File);
            writer.WriteSignature("cpp", $"// {qualifiedName}\n#include \"{includePath}\"\n{signature}");
        }
        else
        {
            writer.WriteSignature("cpp", $"// {qualifiedName}\n{signature}");
        }

        // Emit summary from doc comment or placeholder when no comment is present
        var summary = CppEmitter.GetSummary(fn.Doc);
        writer.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : CppEmitter.NoDescriptionPlaceholder);

        // Emit extended details when the doc comment contains a @details or @remarks block
        var details = CppEmitter.GetDetails(fn.Doc);
        if (!string.IsNullOrEmpty(details))
        {
            writer.WriteParagraph(details);
        }

        // Emit @note as a blockquote when present
        var note = CppEmitter.GetNote(fn.Doc);
        if (!string.IsNullOrEmpty(note))
        {
            writer.WriteParagraph($"> **Note:** {note}");
        }

        // Emit @code example block when present
        var example = CppEmitter.GetExample(fn.Doc);
        if (!string.IsNullOrEmpty(example))
        {
            writer.WriteCodeBlock("cpp", example);
        }

        // Emit parameter table when the function has at least one parameter
        if (fn.Parameters.Count > 0)
        {
            writer.WriteHeading(parametersHeadingLevel, "Parameters");
            var paramHeaders = new[] { "Parameter", "Type", CppEmitter.DescriptionColumnHeader };

            // Linkify parameter type cells; resolver tracks external types encountered
            var paramRows = fn.Parameters.Select(p =>
                new[] { p.Name, cppResolver.Linkify(CppEmitter.SimplifyTypeName(p.TypeName), currentFolder, externalTypes)!, CppEmitter.GetParamDescription(fn.Doc, p.Name) ?? CppEmitter.NoDescriptionPlaceholder });
            writer.WriteTable(paramHeaders, paramRows);
        }

        // Emit return description when the function is not void
        var returnTypeName = CppEmitter.SimplifyTypeName(fn.ReturnTypeName);
        if (!string.Equals(returnTypeName, "void", StringComparison.Ordinal))
        {
            // Always linkify/track the return type even when a doc description is present
            var linkedReturnType = cppResolver.Linkify(returnTypeName, currentFolder, externalTypes)!;
            writer.WriteHeading(parametersHeadingLevel, "Returns");
            var returnDescription = CppEmitter.GetReturnDescription(fn.Doc);
            writer.WriteParagraph(!string.IsNullOrEmpty(returnDescription) ? returnDescription : linkedReturnType);
        }

    }

    /// <summary>
    ///     Writes the detail page for a single class member (method or field).
    /// </summary>
    private static void WriteMemberPage(
        IMarkdownWriterFactory factory,
        string nsKey,
        string nsDisplayName,
        CppClass cls,
        object member,
        string fileName,
        CppTypeLinkResolver cppResolver)
    {
        var memberCurrentFolder = $"{nsKey}/{cls.Name}";
        using var memberWriter = factory.CreateMarkdown(memberCurrentFolder, fileName);

        var externalTypes = new SortedSet<CppExternalTypeInfo>();

        // Dispatch to the appropriate page writer based on the concrete member type
        switch (member)
        {
            case CppFunction method:
                WriteFunctionPage(memberWriter, nsDisplayName, cls.Name, method, cppResolver, memberCurrentFolder, externalTypes);
                break;

            case CppField field:
                WriteFieldPage(memberWriter, nsDisplayName, cls.Name, field);
                break;
        }

        CppEmitter.WriteExternalTypesSection(memberWriter, externalTypes);
    }

    /// <summary>
    ///     Writes the detail content for a method member page.
    /// </summary>
    private static void WriteFunctionPage(
        IMarkdownWriter writer,
        string nsDisplayName,
        string className,
        CppFunction method,
        CppTypeLinkResolver cppResolver,
        string currentFolder,
        ISet<CppExternalTypeInfo> externalTypes)
    {
        writer.WriteHeading(1, $"{className}.{method.Name}");
        WriteFunctionContent(writer, method, new CppFunctionWriteContext(nsDisplayName, className, cppResolver, currentFolder, externalTypes));
    }

    /// <summary>
    ///     Writes the body content for a method member page without the heading.
    ///     Also called from <see cref="CppEmitter.WriteCombinedMemberPage"/>.
    /// </summary>
    internal static void WriteFunctionContent(
        IMarkdownWriter writer,
        CppFunction method,
        CppFunctionWriteContext ctx)
    {
        // Emit the fully-qualified name as a comment followed by the C++ signature so that
        // an AI reader has the namespace and class context needed to call the member correctly
        var qualifiedName = string.IsNullOrEmpty(ctx.NsDisplayName)
            ? $"{ctx.ClassName}::{method.Name}"
            : $"{ctx.NsDisplayName}::{ctx.ClassName}::{method.Name}";
        var signature = CppEmitter.BuildMethodSignature(method);
        writer.WriteSignature("cpp", $"// {qualifiedName}\n{signature}");

        // Emit summary from doc comment or placeholder
        var summary = CppEmitter.GetSummary(method.Doc);
        writer.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : CppEmitter.NoDescriptionPlaceholder);

        // Emit extended details when the doc comment contains a @details or @remarks block
        var details = CppEmitter.GetDetails(method.Doc);
        if (!string.IsNullOrEmpty(details))
        {
            writer.WriteParagraph(details);
        }

        // Emit @note as a blockquote when present
        var note = CppEmitter.GetNote(method.Doc);
        if (!string.IsNullOrEmpty(note))
        {
            writer.WriteParagraph($"> **Note:** {note}");
        }

        // Emit @code example block when present
        var example = CppEmitter.GetExample(method.Doc);
        if (!string.IsNullOrEmpty(example))
        {
            writer.WriteCodeBlock("cpp", example);
        }

        // Emit parameter table when the method has at least one parameter
        if (method.Parameters.Count > 0)
        {
            writer.WriteHeading(ctx.ParametersHeadingLevel, "Parameters");
            var paramHeaders = new[] { "Parameter", "Type", CppEmitter.DescriptionColumnHeader };

            // Linkify parameter type cells; resolver tracks external types encountered
            var paramRows = method.Parameters.Select(p =>
                new[] { p.Name, ctx.CppResolver.Linkify(CppEmitter.SimplifyTypeName(p.TypeName), ctx.CurrentFolder, ctx.ExternalTypes)!, CppEmitter.GetParamDescription(method.Doc, p.Name) ?? CppEmitter.NoDescriptionPlaceholder });
            writer.WriteTable(paramHeaders, paramRows);
        }

        // Emit return description when the method is not void and is not a constructor;
        // constructors have no return type so the return section would be meaningless
        if (!method.IsConstructor)
        {
            var returnTypeName = CppEmitter.SimplifyTypeName(method.ReturnTypeName);
            if (!string.Equals(returnTypeName, "void", StringComparison.Ordinal))
            {
                // Always linkify/track the return type even when a doc description is present
                var linkedReturnType = ctx.CppResolver.Linkify(returnTypeName, ctx.CurrentFolder, ctx.ExternalTypes)!;
                writer.WriteHeading(ctx.ParametersHeadingLevel, "Returns");
                var returnDescription = CppEmitter.GetReturnDescription(method.Doc);
                writer.WriteParagraph(!string.IsNullOrEmpty(returnDescription) ? returnDescription : linkedReturnType);
            }
        }
    }

    /// <summary>
    ///     Writes the detail content for a field member page.
    /// </summary>
    private static void WriteFieldPage(
        IMarkdownWriter writer,
        string nsDisplayName,
        string className,
        CppField field)
    {
        writer.WriteHeading(1, $"{className}.{field.Name}");
        WriteFieldContent(writer, nsDisplayName, className, field);
    }

    /// <summary>
    ///     Writes the body content for a field member page without the heading.
    ///     Also called from <see cref="CppEmitter.WriteCombinedMemberPage"/>.
    /// </summary>
    internal static void WriteFieldContent(
        IMarkdownWriter writer,
        string nsDisplayName,
        string className,
        CppField field)
    {
        // Build the fully-qualified name comment so readers know the exact symbol to reference
        var qualifiedName = string.IsNullOrEmpty(nsDisplayName)
            ? $"{className}::{field.Name}"
            : $"{nsDisplayName}::{className}::{field.Name}";
        var signature = $"{CppEmitter.SimplifyTypeName(field.TypeName)} {field.Name};";
        writer.WriteSignature("cpp", $"// {qualifiedName}\n{signature}");

        // Emit summary from doc comment or placeholder
        var summary = CppEmitter.GetSummary(field.Doc);
        writer.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : CppEmitter.NoDescriptionPlaceholder);

        // Emit extended details when the doc comment contains a @details or @remarks block
        var details = CppEmitter.GetDetails(field.Doc);
        if (!string.IsNullOrEmpty(details))
        {
            writer.WriteParagraph(details);
        }

        // Emit @note as a blockquote when present
        var note = CppEmitter.GetNote(field.Doc);
        if (!string.IsNullOrEmpty(note))
        {
            writer.WriteParagraph($"> **Note:** {note}");
        }

        // Emit @code example block when present
        var example = CppEmitter.GetExample(field.Doc);
        if (!string.IsNullOrEmpty(example))
        {
            writer.WriteCodeBlock("cpp", example);
        }

    }

    /// <summary>
    ///     Writes the detail page for a single C++ enum.
    /// </summary>
    private void WriteEnumPage(
        IMarkdownWriterFactory factory,
        string nsKey,
        string nsDisplayName,
        CppEnum cppEnum)
    {
        using var writer = factory.CreateMarkdown(nsKey, cppEnum.Name);
        writer.WriteHeading(1, cppEnum.Name);

        // Emit the fully-qualified name comment and optional #include directive so readers
        // have everything needed to use the type without browsing the header tree
        var qualifiedName = string.IsNullOrEmpty(nsDisplayName)
            ? cppEnum.Name
            : $"{nsDisplayName}::{cppEnum.Name}";
        if (cppEnum.Location != null)
        {
            var includePath = _emitter.GetIncludePath(cppEnum.Location.File);
            writer.WriteSignature("cpp", $"// {qualifiedName}\n#include \"{includePath}\"");
        }
        else
        {
            writer.WriteSignature("cpp", $"// {qualifiedName}");
        }

        // Emit summary from doc comment or placeholder
        var enumSummary = CppEmitter.GetSummary(cppEnum.Doc);
        writer.WriteParagraph(!string.IsNullOrEmpty(enumSummary) ? enumSummary : CppEmitter.NoDescriptionPlaceholder);

        // Emit extended details when the doc comment contains a @details or @remarks block
        var enumDetails = CppEmitter.GetDetails(cppEnum.Doc);
        if (!string.IsNullOrEmpty(enumDetails))
        {
            writer.WriteParagraph(enumDetails);
        }

        // Emit @note as a blockquote when present
        var enumNote = CppEmitter.GetNote(cppEnum.Doc);
        if (!string.IsNullOrEmpty(enumNote))
        {
            writer.WriteParagraph($"> **Note:** {enumNote}");
        }

        // Emit @code example block when present
        var enumExample = CppEmitter.GetExample(cppEnum.Doc);
        if (!string.IsNullOrEmpty(enumExample))
        {
            writer.WriteCodeBlock("cpp", enumExample);
        }

        // Emit a values table so readers can see all valid values and their meanings
        if (cppEnum.Values.Count > 0)
        {
            writer.WriteHeading(2, "Values");
            var headers = new[] { "Value", CppEmitter.DescriptionColumnHeader };
            var rows = cppEnum.Values.Select(item =>
            {
                var itemSummary = CppEmitter.GetSummary(item.Doc) ?? CppEmitter.NoDescriptionPlaceholder;
                return new[] { item.Name, itemSummary };
            });
            writer.WriteTable(headers, rows);
        }

    }

    /// <summary>
    ///     Writes a documentation page for a <c>using</c> type alias declaration.
    /// </summary>
    private void WriteTypeAliasPage(
        IMarkdownWriterFactory factory,
        string nsKey,
        string nsDisplayName,
        CppTypeAlias alias,
        CppTypeLinkResolver cppResolver)
    {
        using var writer = factory.CreateMarkdown(nsKey, alias.Name);
        writer.WriteHeading(1, alias.Name);

        // Accumulate external type references found in the underlying type on this page
        var externalTypes = new SortedSet<CppExternalTypeInfo>();

        // Emit the fully-qualified name comment, optional #include, and the using declaration
        // so readers have everything needed to use the alias without browsing the header tree
        var qualifiedName = string.IsNullOrEmpty(nsDisplayName)
            ? alias.Name
            : $"{nsDisplayName}::{alias.Name}";
        var simplifiedUnderlying = CppEmitter.SimplifyTypeName(alias.UnderlyingTypeName);
        var declaration = $"using {alias.Name} = {simplifiedUnderlying}";
        if (alias.Location != null)
        {
            var includePath = _emitter.GetIncludePath(alias.Location.File);
            writer.WriteSignature("cpp", $"// {qualifiedName}\n#include \"{includePath}\"\n{declaration}");
        }
        else
        {
            writer.WriteSignature("cpp", $"// {qualifiedName}\n{declaration}");
        }

        // Emit summary from doc comment or placeholder
        var summary = CppEmitter.GetSummary(alias.Doc);
        writer.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : CppEmitter.NoDescriptionPlaceholder);

        // Emit extended details when the doc comment contains a @details or @remarks block
        var details = CppEmitter.GetDetails(alias.Doc);
        if (!string.IsNullOrEmpty(details))
        {
            writer.WriteParagraph(details);
        }

        // Emit @note as a blockquote when present
        var aliasNote = CppEmitter.GetNote(alias.Doc);
        if (!string.IsNullOrEmpty(aliasNote))
        {
            writer.WriteParagraph($"> **Note:** {aliasNote}");
        }

        // Emit @code example block when present
        var aliasExample = CppEmitter.GetExample(alias.Doc);
        if (!string.IsNullOrEmpty(aliasExample))
        {
            writer.WriteCodeBlock("cpp", aliasExample);
        }

        // Resolve the underlying type to track any non-std external type references
        cppResolver.Linkify(simplifiedUnderlying, nsKey, externalTypes);
        CppEmitter.WriteExternalTypesSection(writer, externalTypes);
    }

    /// <summary>
    ///     Writes the summary page for a single namespace, listing its owned types,
    ///     enums, and free functions.
    /// </summary>
    private static void WriteNamespacePage(
        IMarkdownWriterFactory factory,
        string nsKey,
        CppEmitter.NamespaceDeclarations nsDecls,
        CppTypeLinkResolver cppResolver)
    {
        using var writer = factory.CreateMarkdown("", nsKey);
        writer.WriteHeading(1, $"{nsDecls.DisplayName} Namespace");

        // Accumulate external type references found in type-column cells on this page
        var externalTypes = new SortedSet<CppExternalTypeInfo>();

        // Type table — one row per owned class or struct, sorted alphabetically
        if (nsDecls.Classes.Count > 0)
        {
            writer.WriteHeading(2, "Types");
            var typeHeaders = new[] { "Type", CppEmitter.DescriptionColumnHeader };
            var typeRows = nsDecls.Classes
                .OrderBy(c => c.Name, StringComparer.Ordinal)
                .Select(cls =>
                {
                    var summary = CppEmitter.GetSummary(cls.Doc) ?? CppEmitter.NoDescriptionPlaceholder;
                    return new[] { $"[{cls.Name}]({nsKey}/{cls.Name}.md)", summary };
                });
            writer.WriteTable(typeHeaders, typeRows);
        }

        // Enum table — one row per owned enum, sorted alphabetically
        if (nsDecls.Enums.Count > 0)
        {
            writer.WriteHeading(2, "Enums");
            var enumHeaders = new[] { "Enum", CppEmitter.DescriptionColumnHeader };
            var enumRows = nsDecls.Enums
                .OrderBy(e => e.Name, StringComparer.Ordinal)
                .Select(en =>
                {
                    var summary = CppEmitter.GetSummary(en.Doc) ?? CppEmitter.NoDescriptionPlaceholder;
                    return new[] { $"[{en.Name}]({nsKey}/{en.Name}.md)", summary };
                });
            writer.WriteTable(enumHeaders, enumRows);
        }

        // Type alias table — one row per owned using-alias, sorted alphabetically
        if (nsDecls.TypeAliases.Count > 0)
        {
            writer.WriteHeading(2, "Type Aliases");
            var aliasHeaders = new[] { "Alias", "Underlying Type", CppEmitter.DescriptionColumnHeader };
            var aliasRows = nsDecls.TypeAliases
                .OrderBy(a => a.Name, StringComparer.Ordinal)
                .Select(alias =>
                {
                    var summary = CppEmitter.GetSummary(alias.Doc) ?? CppEmitter.NoDescriptionPlaceholder;
                    var underlying = cppResolver.Linkify(CppEmitter.SimplifyTypeName(alias.UnderlyingTypeName), nsKey, externalTypes)!;
                    return new[] { $"[{alias.Name}]({nsKey}/{alias.Name}.md)", underlying, summary };
                });
            writer.WriteTable(aliasHeaders, aliasRows);
        }

        // Partition free functions into regular functions and operator overloads; operator names
        // such as operator+, operator-, and operator<< all sanitize to the same file name and
        // must be grouped onto a single operators.md page to avoid file-name collisions
        var regularFreeFunctions = nsDecls.FreeFunctions
            .Where(fn => !fn.Name.StartsWith("operator", StringComparison.Ordinal))
            .ToList();
        var operatorFreeFunctions = nsDecls.FreeFunctions
            .Where(fn => fn.Name.StartsWith("operator", StringComparison.Ordinal))
            .ToList();

        // Regular free-function table — one row per owned free function, sorted alphabetically
        if (regularFreeFunctions.Count > 0)
        {
            writer.WriteHeading(2, "Functions");
            var fnHeaders = new[] { "Function", "Returns", CppEmitter.DescriptionColumnHeader };
            var fnRows = regularFreeFunctions
                .OrderBy(f => f.Name, StringComparer.Ordinal)
                .Select(fn =>
                {
                    var summary = CppEmitter.GetSummary(fn.Doc) ?? CppEmitter.NoDescriptionPlaceholder;

                    // Linkify the return type cell; namespace page folder is "" (root)
                    var returnType = cppResolver.Linkify(
                        CppEmitter.SimplifyTypeName(fn.ReturnTypeName), string.Empty, externalTypes)!;
                    var safeName = CppEmitter.SanitizeFileName(fn.Name);
                    return new[] { $"[{fn.Name}]({nsKey}/{safeName}.md)", returnType, summary };
                });
            writer.WriteTable(fnHeaders, fnRows);
        }

        // Operators section — a single row linking to the shared operators.md page so readers
        // can navigate to all operator overloads without hitting file-name collision pages
        if (operatorFreeFunctions.Count > 0)
        {
            writer.WriteHeading(2, "Operators");
            writer.WriteTable(
                new[] { "Operators", CppEmitter.DescriptionColumnHeader },
                new[] { new[] { $"[operators]({nsKey}/operators.md)", "Operator overloads" } });
        }

        CppEmitter.WriteExternalTypesSection(writer, externalTypes);
    }
}
