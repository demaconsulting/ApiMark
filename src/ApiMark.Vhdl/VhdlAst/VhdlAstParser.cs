using Antlr4.Runtime;
using ApiMark.Vhdl.VhdlAst.Antlr;

namespace ApiMark.Vhdl.VhdlAst;

/// <summary>
///     Parses a VHDL source file into a <see cref="VhdlFileModel"/> using the ANTLR4 vhdl2008 grammar.
/// </summary>
internal static class VhdlAstParser
{
    /// <summary>
    ///     Parses the specified VHDL file and returns a <see cref="VhdlFileModel"/> containing
    ///     all entity, architecture, and package declarations found.
    /// </summary>
    /// <param name="filePath">Absolute path to the .vhd source file.</param>
    /// <returns>A <see cref="VhdlFileModel"/> with all declarations and associated doc comments.</returns>
    internal static VhdlFileModel Parse(string filePath)
    {
        var sourceText = File.ReadAllText(filePath);
        var lines = File.ReadAllLines(filePath);

        var input = new AntlrInputStream(sourceText);
        var lexer = new vhdl2008Lexer(input);
        var stream = new CommonTokenStream(lexer);
        var parser = new vhdl2008Parser(stream);
        var tree = parser.design_file();

        var visitor = new VhdlVisitor(sourceText, lines);
        visitor.Visit(tree);

        return new VhdlFileModel(filePath, visitor.Entities, visitor.Architectures, visitor.Packages);
    }

    /// <summary>
    ///     Visitor that walks the ANTLR4 parse tree to collect VHDL declarations.
    /// </summary>
    private sealed class VhdlVisitor : vhdl2008BaseVisitor<object?>
    {
        private readonly string _sourceText;
        private readonly string[] _lines;

        /// <summary>Gets the entity declarations collected during the tree walk.</summary>
        public List<VhdlEntityDecl> Entities { get; } = [];

        /// <summary>Gets the architecture declarations collected during the tree walk.</summary>
        public List<VhdlArchitectureDecl> Architectures { get; } = [];

        /// <summary>Gets the package declarations collected during the tree walk.</summary>
        public List<VhdlPackageDecl> Packages { get; } = [];

        /// <summary>Initializes the visitor with the raw source text and split lines.</summary>
        /// <param name="sourceText">Full raw text of the file (used for range-based type extraction).</param>
        /// <param name="lines">Source lines array (1-based via <c>lines[lineNum - 1]</c>).</param>
        public VhdlVisitor(string sourceText, string[] lines)
        {
            _sourceText = sourceText;
            _lines = lines;
        }

        /// <inheritdoc/>
        public override object? VisitEntity_declaration(vhdl2008Parser.Entity_declarationContext context)
        {
            // Extract entity name
            var entityName = context.identifier(0).GetText();

            // Extract preceding doc comment
            var entityDoc = ExtractPrecedingDocComment(context.Start.Line);

            // Parse generics
            var generics = new List<VhdlGenericDoc>();
            var genericDecls = context.entity_header()
                ?.generic_clause()
                ?.generic_list()
                ?.interface_list()
                ?.interface_declaration();
            if (genericDecls != null)
            {
                foreach (var iface in genericDecls)
                {
                    var constDecl = iface.interface_object_declaration()?.interface_constant_declaration();
                    if (constDecl == null)
                    {
                        continue;
                    }

                    var identifiers = constDecl.identifier_list()?.identifier();
                    if (identifiers == null)
                    {
                        continue;
                    }

                    var typeName = GetSourceRange(constDecl.subtype_indication());
                    var defaultValue = constDecl.expression() != null
                        ? GetSourceRange(constDecl.expression())
                        : null;

                    // Extract inline trailing comment from the line of the last token of this declaration
                    var inlineDoc = ExtractInlineTrailingComment(constDecl.Stop.Line);

                    foreach (var ident in identifiers)
                    {
                        generics.Add(new VhdlGenericDoc(ident.GetText(), typeName, defaultValue, inlineDoc));
                    }
                }
            }

            // Parse ports
            var ports = new List<VhdlPortDoc>();
            var portDecls = context.entity_header()
                ?.port_clause()
                ?.port_list()
                ?.interface_list()
                ?.interface_declaration();
            if (portDecls != null)
            {
                foreach (var iface in portDecls)
                {
                    var signalDecl = iface.interface_object_declaration()?.interface_signal_declaration();
                    string typeName;
                    string direction;
                    int stopLine;

                    if (signalDecl != null)
                    {
                        var identifiers = signalDecl.identifier_list()?.identifier();
                        if (identifiers == null)
                        {
                            continue;
                        }

                        // Extract direction
                        var modeCtx = signalDecl.mode_rule();
                        direction = "in"; // default per VHDL-2008 §6.5.2
                        if (modeCtx != null)
                        {
                            if (modeCtx.OUT() != null)
                            {
                                direction = "out";
                            }
                            else if (modeCtx.INOUT() != null)
                            {
                                direction = "inout";
                            }
                            else if (modeCtx.BUFFER() != null)
                            {
                                direction = "buffer";
                            }
                            else
                            {
                                direction = "in";
                            }
                        }

                        typeName = GetSourceRange(signalDecl.subtype_indication());
                        stopLine = signalDecl.Stop.Line;

                        var inlineDoc = ExtractInlineTrailingComment(stopLine);
                        foreach (var ident in identifiers)
                        {
                            ports.Add(new VhdlPortDoc(ident.GetText(), direction, typeName, inlineDoc));
                        }
                    }
                    else
                    {
                        // Fallback: ANTLR may parse `name : IN type` as interface_constant_declaration
                        var constDecl = iface.interface_object_declaration()?.interface_constant_declaration();
                        if (constDecl == null)
                        {
                            continue;
                        }

                        var identifiers = constDecl.identifier_list()?.identifier();
                        if (identifiers == null)
                        {
                            continue;
                        }

                        typeName = GetSourceRange(constDecl.subtype_indication());
                        direction = "in";
                        stopLine = constDecl.Stop.Line;

                        var inlineDoc = ExtractInlineTrailingComment(stopLine);
                        foreach (var ident in identifiers)
                        {
                            ports.Add(new VhdlPortDoc(ident.GetText(), direction, typeName, inlineDoc));
                        }
                    }
                }
            }

            Entities.Add(new VhdlEntityDecl(entityName, generics, ports, entityDoc));

            // Do not visit children of entity_declaration to avoid collecting nested declarations
            return null;
        }

        /// <inheritdoc/>
        public override object? VisitArchitecture_body(vhdl2008Parser.Architecture_bodyContext context)
        {
            var archName = context.identifier(0).GetText();
            var entityName = context.name().GetText();
            var doc = ExtractPrecedingDocComment(context.Start.Line);

            Architectures.Add(new VhdlArchitectureDecl(archName, entityName, doc));

            // Do not visit children of architecture_body
            return null;
        }

        /// <inheritdoc/>
        public override object? VisitPackage_declaration(vhdl2008Parser.Package_declarationContext context)
        {
            var pkgName = context.identifier(0).GetText();
            var doc = ExtractPrecedingDocComment(context.Start.Line);

            var types = new List<VhdlTypeDecl>();
            var constants = new List<VhdlConstantDecl>();
            var components = new List<VhdlComponentDecl>();
            var subprograms = new List<VhdlSubprogramDecl>();

            var items = context.package_declarative_part()?.package_declarative_item();
            if (items != null)
            {
                foreach (var item in items)
                {
                    var fullTypeDecl = item.type_declaration()?.full_type_declaration();
                    if (fullTypeDecl != null)
                    {
                        var typeName = fullTypeDecl.identifier().GetText();
                        var definition = GetSourceRange(fullTypeDecl.type_definition());
                        var typeDoc = ExtractPrecedingDocComment(item.Start.Line);
                        types.Add(new VhdlTypeDecl(typeName, definition, typeDoc));
                        continue;
                    }

                    var subtypeDecl = item.subtype_declaration();
                    if (subtypeDecl != null)
                    {
                        var typeName = subtypeDecl.identifier().GetText();
                        var definition = GetSourceRange(subtypeDecl.subtype_indication());
                        var typeDoc = ExtractPrecedingDocComment(item.Start.Line);
                        types.Add(new VhdlTypeDecl(typeName, definition, typeDoc));
                        continue;
                    }

                    var constDecl = item.constant_declaration();
                    if (constDecl != null)
                    {
                        var constTypeName = GetSourceRange(constDecl.subtype_indication());
                        var value = constDecl.expression() != null ? GetSourceRange(constDecl.expression()) : null;
                        var constDoc = ExtractInlineTrailingComment(constDecl.Stop.Line);
                        var identifiers = constDecl.identifier_list()?.identifier();
                        if (identifiers != null)
                        {
                            foreach (var ident in identifiers)
                            {
                                constants.Add(new VhdlConstantDecl(ident.GetText(), constTypeName, value, constDoc));
                            }
                        }
                        continue;
                    }

                    var compDecl = item.component_declaration();
                    if (compDecl != null)
                    {
                        var compName = compDecl.identifier(0).GetText();
                        var compDoc = ExtractPrecedingDocComment(item.Start.Line);
                        components.Add(new VhdlComponentDecl(compName, compDoc));
                        continue;
                    }

                    var subprogramDecl = item.subprogram_declaration();
                    if (subprogramDecl != null)
                    {
                        var spec = subprogramDecl.subprogram_specification();
                        if (spec != null)
                        {
                            var funcSpec = spec.function_specification();
                            var procSpec = spec.procedure_specification();
                            string subprogramName;
                            VhdlSubprogramKind kind;
                            if (funcSpec != null)
                            {
                                subprogramName = funcSpec.designator().identifier()?.GetText() ?? funcSpec.designator().GetText();
                                kind = VhdlSubprogramKind.Function;
                            }
                            else if (procSpec != null)
                            {
                                subprogramName = procSpec.designator().identifier()?.GetText() ?? procSpec.designator().GetText();
                                kind = VhdlSubprogramKind.Procedure;
                            }
                            else
                            {
                                continue;
                            }

                            var signature = GetSourceRange(spec);
                            var subprogramDoc = ExtractPrecedingDocComment(item.Start.Line);
                            subprograms.Add(new VhdlSubprogramDecl(subprogramName, kind, signature, subprogramDoc));
                        }
                    }
                }
            }

            Packages.Add(new VhdlPackageDecl(pkgName, doc, types, constants, components, subprograms));

            // Do not visit children of package_declaration
            return null;
        }

        /// <summary>
        ///     Extracts the source text for an ANTLR parser rule context, preserving whitespace
        ///     that the grammar's skip rules would otherwise discard.
        /// </summary>
        /// <param name="ctx">The parser rule context whose source range to extract.</param>
        /// <returns>The raw source text spanning from the context's first to last token.</returns>
        private string GetSourceRange(Antlr4.Runtime.ParserRuleContext ctx) =>
            _sourceText[ctx.Start.StartIndex..(ctx.Stop.StopIndex + 1)];

        /// <summary>
        ///     Walks backward from the line immediately preceding <paramref name="declarationLine"/>,
        ///     collecting consecutive lines that begin with <c>--!</c> (after trimming leading whitespace),
        ///     and parses them into a <see cref="VhdlDocComment"/>.
        /// </summary>
        /// <param name="declarationLine">1-based line number of the declaration token.</param>
        /// <returns>Parsed doc comment, or null if no preceding --! lines are found.</returns>
        private VhdlDocComment? ExtractPrecedingDocComment(int declarationLine)
        {
            var commentLines = new List<string>();
            var line = declarationLine - 1;
            while (line >= 1)
            {
                var rawLine = _lines[line - 1]; // 0-indexed array, 1-based line number
                var trimmed = rawLine.TrimStart();
                if (trimmed.StartsWith("--!", StringComparison.Ordinal))
                {
                    var text = trimmed.Length > 3 ? trimmed[3..].TrimStart() : string.Empty;
                    commentLines.Add(text);
                    line--;
                }
                else
                {
                    break;
                }
            }

            commentLines.Reverse();
            return ParseDocCommentLines(commentLines);
        }

        /// <summary>
        ///     Scans the raw source line at <paramref name="portLine"/> for an inline <c>--!</c>
        ///     comment and returns a minimal <see cref="VhdlDocComment"/> from it.
        /// </summary>
        /// <param name="portLine">1-based line number of the declaration token.</param>
        /// <returns>A doc comment whose Summary is the inline text, or null if none found.</returns>
        private VhdlDocComment? ExtractInlineTrailingComment(int portLine)
        {
            if (portLine < 1 || portLine > _lines.Length)
            {
                return null;
            }

            var rawLine = _lines[portLine - 1];
            var idx = rawLine.LastIndexOf("--!", StringComparison.Ordinal);
            if (idx < 0)
            {
                return null;
            }

            var text = rawLine[(idx + 3)..].TrimStart();
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            return new VhdlDocComment(text, null, []);
        }

        /// <summary>
        ///     Parses a list of --! comment lines into a structured <see cref="VhdlDocComment"/>,
        ///     recognizing <c>@brief</c>, <c>@param</c>, and <c>@return</c> tags.
        /// </summary>
        /// <param name="lines">Pre-extracted comment line texts (without the --! prefix).</param>
        /// <returns>A <see cref="VhdlDocComment"/> or null when <paramref name="lines"/> is empty.</returns>
        private static VhdlDocComment? ParseDocCommentLines(List<string> lines)
        {
            if (lines.Count == 0)
            {
                return null;
            }

            string? summary = null;
            string? returns = null;
            var paramDocs = new List<VhdlParamDoc>();
            var bodyLines = new List<string>();

            foreach (var line in lines)
            {
                if (line.StartsWith("@brief ", StringComparison.Ordinal))
                {
                    summary = line["@brief ".Length..].Trim();
                }
                else if (line.StartsWith("@param ", StringComparison.Ordinal))
                {
                    var rest = line["@param ".Length..].Trim();
                    var spaceIdx = rest.IndexOf(' ');
                    if (spaceIdx > 0)
                    {
                        var paramName = rest[..spaceIdx];
                        var paramDesc = rest[(spaceIdx + 1)..].Trim();
                        paramDocs.Add(new VhdlParamDoc(paramName, paramDesc));
                    }
                }
                else if (line.StartsWith("@return ", StringComparison.Ordinal))
                {
                    returns = line["@return ".Length..].Trim();
                }
                else
                {
                    bodyLines.Add(line);
                }
            }

            // If no @brief tag, use the first non-empty body line as the summary
            if (summary == null)
            {
                var firstLine = bodyLines.FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
                if (firstLine != null)
                {
                    summary = firstLine.Trim();
                    bodyLines.Remove(firstLine);
                }
            }

            // Remaining body lines become the details text
            var detailText = string.Join("\n", bodyLines.SkipWhile(l => string.IsNullOrWhiteSpace(l))).Trim();
            string? details = !string.IsNullOrEmpty(detailText) ? detailText : null;

            return new VhdlDocComment(summary, details, paramDocs, returns);
        }
    }
}
