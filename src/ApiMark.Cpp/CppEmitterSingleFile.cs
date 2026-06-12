using ApiMark.Core;
using ApiMark.Cpp.CppAst;

namespace ApiMark.Cpp;

/// <summary>
///     Single-file emitter for C++ API documentation. Writes all documentation into a
///     single <c>api.md</c> file using heading levels offset by <see cref="EmitConfig.HeadingDepth"/>.
/// </summary>
internal sealed class CppEmitterSingleFile
{
    /// <summary>Parent emitter providing options and shared helper methods.</summary>
    private readonly CppEmitter _emitter;

    /// <summary>Namespace declarations collected during parse, sorted by namespace key.</summary>
    private readonly SortedDictionary<string, CppEmitter.NamespaceDeclarations> _namespaceDecls;

    /// <summary>Initializes a new single-file emitter.</summary>
    /// <param name="emitter">Parent emitter providing options and shared helper methods.</param>
    /// <param name="namespaceDecls">Namespace declarations collected during parse, sorted by namespace key.</param>
    /// <param name="cppResolver">Type link resolver; unused in single-file mode as type links are omitted to prevent anchor collisions.</param>
    internal CppEmitterSingleFile(
        CppEmitter emitter,
        SortedDictionary<string, CppEmitter.NamespaceDeclarations> namespaceDecls,
        CppTypeLinkResolver cppResolver)
    {
        _emitter = emitter;
        _namespaceDecls = namespaceDecls;

        // cppResolver is not used in single-file mode — type links are omitted
        // to prevent anchor collisions when all members share a single file.
        _ = cppResolver;
    }

    // =========================================================================
    // Entry point
    // =========================================================================

    /// <summary>
    ///     Emits all documentation content into a single <c>api.md</c> file using
    ///     heading levels offset by <see cref="EmitConfig.HeadingDepth"/>.
    /// </summary>
    /// <param name="factory">Factory used to create the single Markdown writer.</param>
    /// <param name="config">Emission configuration including heading depth and format.</param>
    /// <param name="context">Logging and diagnostic context.</param>
    /// <remarks>
    ///     Structure: H{depth} library title, H{depth+1} namespace, H{depth+2} type/function/enum
    ///     (with signature and member bullet list), H{depth+3} individual class members.
    ///     The convention appendix is omitted — it describes multi-file layout.
    /// </remarks>
    internal void Emit(IMarkdownWriterFactory factory, EmitConfig config, IContext context)
    {
        EmitSingleFile(factory, config);
    }

    // =========================================================================
    // Single-file emitter methods
    // =========================================================================

    /// <summary>
    ///     Writes all content into a single <c>api.md</c> file.
    /// </summary>
    private void EmitSingleFile(IMarkdownWriterFactory factory, EmitConfig config)
    {
        var depth = config.HeadingDepth;

        // All output goes into the single api.md file
        using var writer = factory.CreateMarkdown("", "api");
        writer.WriteHeading(depth, $"{_emitter.Options.LibraryName} API Reference");

        // Emit optional library description paragraph
        if (!string.IsNullOrWhiteSpace(_emitter.Options.Description))
        {
            writer.WriteParagraph(_emitter.Options.Description);
        }

        foreach (var (_, nsDecls) in _namespaceDecls)
        {
            // H{depth+1} namespace heading
            var nsDisplay = nsDecls.DisplayName;
            writer.WriteHeading(depth + 1, nsDisplay);

            // Optional namespace summary from the doc comment
            var nsSummary = CppEmitter.GetSummary(nsDecls.Doc);
            if (!string.IsNullOrEmpty(nsSummary))
            {
                writer.WriteParagraph(nsSummary);
            }

            // Emit each class as an H{depth+2} section
            foreach (var cls in nsDecls.Classes)
            {
                WriteSingleFileClassSection(writer, depth, nsDisplay, cls);
            }

            // Emit each non-operator free function as an H{depth+2} section
            foreach (var fn in nsDecls.FreeFunctions
                .Where(fn => !fn.Name.StartsWith("operator", StringComparison.Ordinal))
                .OrderBy(fn => fn.Name, StringComparer.Ordinal))
            {
                WriteSingleFileFreeFunctionSection(writer, depth, nsDisplay, fn);
            }

            // Emit each enum as an H{depth+2} section
            foreach (var en in nsDecls.Enums.OrderBy(e => e.Name, StringComparer.Ordinal))
            {
                WriteSingleFileEnumSection(writer, depth, en);
            }
        }
    }

    /// <summary>
    ///     Emits an H{depth+2} section for a single C++ class, including a member
    ///     bullet list and H{depth+3} sections for each visible member, followed by
    ///     peer H{depth+2} sections for any nested classes.
    /// </summary>
    private void WriteSingleFileClassSection(
        IMarkdownWriter writer,
        int depth,
        string nsDisplay,
        CppClass cls,
        string? parentClassName = null)
    {
        writer.WriteHeading(depth + 2, cls.Name);

        // Note the parent class for nested types so readers have context
        if (!string.IsNullOrEmpty(parentClassName))
        {
            writer.WriteParagraph($"Nested type of `{parentClassName}`.");
        }

        // Emit the class signature block when a source location is available
        var sourceFile = cls.Location?.File;
        if (!string.IsNullOrEmpty(sourceFile))
        {
            var includePath = _emitter.GetIncludePath(sourceFile);
            var qualifiedName = string.IsNullOrEmpty(nsDisplay) ? cls.Name : $"{nsDisplay}::{cls.Name}";
            var sigParts = new List<string> { $"// {qualifiedName}" };
            var templateDecl = CppEmitter.BuildTemplateDeclaration(cls);
            if (!string.IsNullOrEmpty(templateDecl))
            {
                sigParts.Add(templateDecl);
            }

            sigParts.Add($"#include \"{includePath}\"");
            writer.WriteSignature("cpp", string.Join("\n", sigParts));
        }

        // Summary (always emitted)
        var typeSummary = CppEmitter.GetSummary(cls.Doc);
        writer.WriteParagraph(!string.IsNullOrEmpty(typeSummary) ? typeSummary : CppEmitter.NoDescriptionPlaceholder);

        var typeDetails = CppEmitter.GetDetails(cls.Doc);
        if (!string.IsNullOrEmpty(typeDetails))
        {
            writer.WriteParagraph(typeDetails);
        }

        // Emit @note as a blockquote when present
        var typeNote = CppEmitter.GetNote(cls.Doc);
        if (!string.IsNullOrEmpty(typeNote))
        {
            writer.WriteParagraph($"> **Note:** {typeNote}");
        }

        // Emit @code example block when present
        var typeExample = CppEmitter.GetExample(cls.Doc);
        if (!string.IsNullOrEmpty(typeExample))
        {
            writer.WriteCodeBlock("cpp", typeExample);
        }

        // Collect visible members — include operators so they appear alongside other members
        var visibleCtors = _emitter.GetVisibleConstructors(cls).OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
        var visibleMethods = _emitter.GetVisibleMethods(cls)
            .OrderBy(m => m.Name, StringComparer.Ordinal).ToList();
        var visibleFields = _emitter.GetVisibleFields(cls).OrderBy(f => f.Name, StringComparer.Ordinal).ToList();

        var allMembers = visibleCtors.Cast<object>()
            .Concat(visibleMethods.Cast<object>())
            .Concat(visibleFields.Cast<object>())
            .ToList();

        if (allMembers.Count > 0)
        {
            // Compact bullet list (no anchor links — names can collide across types in single file)
            var bulletLines = allMembers.Select(member =>
            {
                var (displayName, memberSummary) = GetMemberDisplayAndSummary(member, cls.Name);
                return $"- **{displayName}**: {memberSummary}";
            });
            writer.WriteParagraph(string.Join("\n", bulletLines));

            // Emit each member as an H{depth+3} section
            foreach (var member in allMembers)
            {
                WriteSingleFileMemberSection(writer, depth, nsDisplay, member, cls.Name);
            }
        }

        // Emit nested classes as peer H{depth+2} sections with a parent-context note
        foreach (var nested in cls.NestedClasses.OrderBy(n => n.Name, StringComparer.Ordinal))
        {
            WriteSingleFileClassSection(writer, depth, nsDisplay, nested, cls.Name);
        }
    }

    /// <summary>
    ///     Emits an H{depth+2} section for a single free function.
    /// </summary>
    private static void WriteSingleFileFreeFunctionSection(
        IMarkdownWriter writer,
        int depth,
        string nsDisplay,
        CppFunction fn)
    {
        // Show parameter types in the heading so overloads are distinguishable
        var paramTypes = string.Join(", ", fn.Parameters.Select(p => CppEmitter.SimplifyTypeName(p.TypeName)));
        writer.WriteHeading(depth + 2, $"{fn.Name}({paramTypes})");

        // Signature as fenced code block
        writer.WriteSignature("cpp", BuildFunctionSignature(fn, nsDisplay));

        var summary = CppEmitter.GetSummary(fn.Doc);
        writer.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : CppEmitter.NoDescriptionPlaceholder);

        var details = CppEmitter.GetDetails(fn.Doc);
        if (!string.IsNullOrEmpty(details))
        {
            writer.WriteParagraph(details);
        }

        // Parameters table (if any)
        if (fn.Parameters.Count > 0)
        {
            WriteSingleFileParametersTable(writer, fn);
        }

    }

    /// <summary>
    ///     Emits an H{depth+2} section for a single C++ enum.
    /// </summary>
    private static void WriteSingleFileEnumSection(IMarkdownWriter writer, int depth, CppEnum en)
    {
        writer.WriteHeading(depth + 2, en.Name);

        var summary = CppEmitter.GetSummary(en.Doc);
        writer.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : CppEmitter.NoDescriptionPlaceholder);

        if (en.Values.Count > 0)
        {
            var enumHeaders = new[] { "Value", CppEmitter.DescriptionColumnHeader };
            var enumRows = en.Values.Select(item =>
            {
                var itemSummary = CppEmitter.GetSummary(item.Doc) ?? CppEmitter.NoDescriptionPlaceholder;
                return new[] { item.Name, itemSummary };
            });
            writer.WriteTable(enumHeaders, enumRows);
        }
    }

    /// <summary>
    ///     Emits an H{depth+3} section for a single class member (constructor, method, or field).
    /// </summary>
    private static void WriteSingleFileMemberSection(
        IMarkdownWriter writer,
        int depth,
        string nsDisplay,
        object member,
        string className)
    {
        var (displayName, memberSummary) = GetMemberDisplayAndSummary(member, className);
        writer.WriteHeading(depth + 3, displayName);

        switch (member)
        {
            case CppFunction fn:
                {
                    // Build fully-qualified comment + unqualified signature — matches gradual page style
                    var qualifiedName = string.IsNullOrEmpty(nsDisplay)
                        ? $"{className}::{fn.Name}"
                        : $"{nsDisplay}::{className}::{fn.Name}";
                    var fnSig = CppEmitter.BuildMethodSignature(fn);
                    writer.WriteSignature("cpp", $"// {qualifiedName}\n{fnSig}");

                    writer.WriteParagraph(!string.IsNullOrEmpty(memberSummary) ? memberSummary : CppEmitter.NoDescriptionPlaceholder);

                    if (fn.Parameters.Count > 0)
                    {
                        WriteSingleFileParametersTable(writer, fn);
                    }

                    // Returns section for non-void non-constructor methods
                    if (!fn.IsConstructor)
                    {
                        var returnTypeName = CppEmitter.SimplifyTypeName(fn.ReturnTypeName);
                        if (!string.Equals(returnTypeName, "void", StringComparison.Ordinal))
                        {
                            var returnDescription = CppEmitter.GetReturnDescription(fn.Doc);
                            writer.WriteParagraph(
                                $"**Returns:** {(!string.IsNullOrEmpty(returnDescription) ? returnDescription : returnTypeName)}");
                        }
                    }

                    // Emit @code example block when present
                    var fnExample = CppEmitter.GetExample(fn.Doc);
                    if (!string.IsNullOrEmpty(fnExample))
                    {
                        writer.WriteCodeBlock("cpp", fnExample);
                    }

                    break;
                }

            case CppField field:
                {
                    // Build fully-qualified comment + field declaration — matches gradual page style
                    var qualifiedFieldName = string.IsNullOrEmpty(nsDisplay)
                        ? $"{className}::{field.Name}"
                        : $"{nsDisplay}::{className}::{field.Name}";
                    var fieldSig = $"{CppEmitter.SimplifyTypeName(field.TypeName)} {field.Name};";
                    writer.WriteSignature("cpp", $"// {qualifiedFieldName}\n{fieldSig}");

                    writer.WriteParagraph(!string.IsNullOrEmpty(memberSummary) ? memberSummary : CppEmitter.NoDescriptionPlaceholder);

                    // Emit @code example block when present
                    var fieldExample = CppEmitter.GetExample(field.Doc);
                    if (!string.IsNullOrEmpty(fieldExample))
                    {
                        writer.WriteCodeBlock("cpp", fieldExample);
                    }

                    break;
                }
        }
    }

    /// <summary>
    ///     Writes a parameters table for a function when it has at least one parameter.
    /// </summary>
    private static void WriteSingleFileParametersTable(IMarkdownWriter writer, CppFunction fn)
    {
        var paramHeaders = new[] { "Parameter", "Type", CppEmitter.DescriptionColumnHeader };
        var paramRows = fn.Parameters.Select(p =>
        {
            var paramSummary = CppEmitter.GetParamDescription(fn.Doc, p.Name) ?? string.Empty;
            return new[] { p.Name, CppEmitter.SimplifyTypeName(p.TypeName), paramSummary };
        });
        writer.WriteTable(paramHeaders, paramRows);
    }

    /// <summary>
    ///     Returns a one-line function signature string suitable for a fenced code block.
    /// </summary>
    private static string BuildFunctionSignature(CppFunction fn, string nsDisplay)
    {
        var paramList = string.Join(", ", fn.Parameters.Select(p =>
            $"{CppEmitter.SimplifyTypeName(p.TypeName)} {p.Name}"));
        var qualifiedName = string.IsNullOrEmpty(nsDisplay)
            ? fn.Name
            : $"{nsDisplay}::{fn.Name}";
        return $"{CppEmitter.SimplifyTypeName(fn.ReturnTypeName)} {qualifiedName}({paramList})";
    }

    /// <summary>
    ///     Returns the display name and one-line summary for a class member.
    /// </summary>
    private static (string DisplayName, string Summary) GetMemberDisplayAndSummary(object member, string className)
    {
        switch (member)
        {
            case CppFunction fn:
                var paramTypes = string.Join(", ", fn.Parameters.Select(p => CppEmitter.SimplifyTypeName(p.TypeName)));
                var baseName = string.Equals(fn.Name, className, StringComparison.Ordinal)
                    ? className  // constructor display: same as class name
                    : fn.Name;
                return ($"{baseName}({paramTypes})", CppEmitter.GetSummary(fn.Doc) ?? CppEmitter.NoDescriptionPlaceholder);

            case CppField field:
                return (field.Name, CppEmitter.GetSummary(field.Doc) ?? CppEmitter.NoDescriptionPlaceholder);

            default:
                return (string.Empty, CppEmitter.NoDescriptionPlaceholder);
        }
    }
}
