using System.Runtime.InteropServices;
using ApiMark.Core;
using ApiMark.Cpp.CppAst;

namespace ApiMark.Cpp;

/// <summary>
///     Emitter that holds pre-parsed C++ namespace declarations and can write the Markdown
///     documentation tree in either gradual-disclosure or single-file format.
/// </summary>
/// <remarks>
///     Created exclusively by <see cref="CppGenerator.Parse"/>.
/// </remarks>
internal sealed class CppEmitter : IApiEmitter
{
    /// <summary>Column header label used in all generated Markdown tables for the description column.</summary>
    internal const string DescriptionColumnHeader = "Description";

    /// <summary>Placeholder emitted in description cells when no doc comment summary is available.</summary>
    internal const string NoDescriptionPlaceholder = "*No description provided.*";

    /// <summary>Reserved namespace key and heading used for declarations in the C++ global namespace.</summary>
    internal const string GlobalNamespaceKey = "global";

    /// <summary>Configuration controlling which headers, roots, visibility, and other parse options to apply.</summary>
    private readonly CppGeneratorOptions _options;

    /// <summary>Namespace declarations collected during parse, sorted by namespace key.</summary>
    private readonly SortedDictionary<string, NamespaceDeclarations> _namespaceDecls;

    /// <summary>Type link resolver built from the parsed namespace declarations.</summary>
    private readonly CppTypeLinkResolver _cppResolver;

    /// <summary>
    ///     Initializes a new <see cref="CppEmitter"/> with pre-parsed namespace data.
    /// </summary>
    /// <param name="options">Generator configuration options.</param>
    /// <param name="namespaceDecls">Namespace declarations collected during parse.</param>
    /// <param name="cppResolver">Type link resolver built from the parsed namespace declarations.</param>
    internal CppEmitter(
        CppGeneratorOptions options,
        SortedDictionary<string, NamespaceDeclarations> namespaceDecls,
        CppTypeLinkResolver cppResolver)
    {
        _options = options;
        _namespaceDecls = namespaceDecls;
        _cppResolver = cppResolver;
    }

    /// <summary>Gets the generator configuration options.</summary>
    internal CppGeneratorOptions Options => _options;

    /// <summary>
    ///     Emits the complete Markdown documentation tree in the format specified by
    ///     <paramref name="config"/>.
    /// </summary>
    /// <param name="factory">Factory for creating per-file Markdown writers.</param>
    /// <param name="config">Output configuration (format and heading depth).</param>
    /// <param name="context">Output channel for informational and error messages.</param>
    public void Emit(IMarkdownWriterFactory factory, EmitConfig config, IContext context)
    {
        ArgumentNullException.ThrowIfNull(factory);

        // Dispatch to the appropriate emitter based on the requested output format
        if (config.Format == OutputFormat.SingleFile)
        {
            new CppEmitterSingleFile(this, _namespaceDecls, _cppResolver).Emit(factory, config, context);
        }
        else
        {
            new CppEmitterGradualDisclosure(this, _namespaceDecls, _cppResolver).Emit(factory, config, context);
        }
    }

    // =========================================================================
    // Ownership and include-path helpers
    // =========================================================================

    /// <summary>
    ///     Gets the <see cref="StringComparison"/> appropriate for file-system path comparisons
    ///     on the current platform.
    /// </summary>
    /// <remarks>
    ///     Linux file systems are case-sensitive, so <see cref="StringComparison.Ordinal"/> is
    ///     used there to avoid incorrectly matching paths that differ only in case. Windows and
    ///     macOS default to case-insensitive file systems, so
    ///     <see cref="StringComparison.OrdinalIgnoreCase"/> is used on those platforms.
    /// </remarks>
    internal static StringComparison FileSystemPathComparison =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

    /// <summary>
    ///     Returns the <see cref="StringComparer"/> appropriate for file-system path comparisons
    ///     on the current platform.
    /// </summary>
    /// <remarks>
    ///     Linux file systems are case-sensitive, so <see cref="StringComparer.Ordinal"/> is
    ///     used there to avoid incorrectly treating paths that differ only in case as duplicates.
    ///     Windows and macOS default to case-insensitive file systems, so
    ///     <see cref="StringComparer.OrdinalIgnoreCase"/> is used on those platforms.
    /// </remarks>
    internal static StringComparer FileSystemPathComparer =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;

    /// <summary>
    ///     Sanitizes a C++ declaration name for use as a file-system file name by replacing any
    ///     characters that are invalid in file names on Windows or Unix with an underscore.
    /// </summary>
    /// <remarks>
    ///     Non-operator C++ declaration names — class names, type aliases, free function names,
    ///     and field names — may contain characters such as <c>&lt;</c>, <c>&gt;</c>, and <c>:</c>
    ///     (from template specializations or qualified names) that are forbidden in Windows file
    ///     names. Operator names (e.g. <c>operator*</c>, <c>operator&lt;&lt;</c>) are
    ///     <em>never</em> passed to this method — they are partitioned upstream and written to a
    ///     shared <c>operators.md</c> page, so file-name collisions between operators never arise.
    ///     Note: <see cref="Path.GetInvalidFileNameChars"/> is OS-dependent; on Linux it returns
    ///     only <c>\0</c> and <c>/</c>, so this method has no effect on most characters on that
    ///     platform.
    /// </remarks>
    /// <param name="name">The C++ declaration name to sanitize. Must not be null.</param>
    /// <returns>
    ///     A copy of <paramref name="name"/> with every character from
    ///     <see cref="Path.GetInvalidFileNameChars"/> replaced by <c>_</c>.
    /// </returns>
    internal static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalidChars, chars[i]) >= 0)
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }

    /// <summary>
    ///     Derives the canonical <c>#include</c> path for a declaration from its source file,
    ///     relative to the longest matching public include root.
    /// </summary>
    /// <remarks>
    ///     The longest-root rule ensures that the most specific root wins when multiple roots
    ///     overlap (e.g. both <c>include/</c> and <c>include/mylib/</c> are configured).
    /// </remarks>
    /// <param name="sourceFile">
    ///     The absolute or relative source file path. Must not be null or empty.
    /// </param>
    /// <returns>
    ///     A forward-slash-separated relative path suitable for a <c>#include</c> directive
    ///     (e.g. <c>mylib/renderer.h</c>). Falls back to the normalized file path when no root matches.
    /// </returns>
    internal string GetIncludePath(string sourceFile)
    {
        var normalized = Path.GetFullPath(sourceFile);

        // Select the longest matching root so the most specific prefix wins
        var matchingRoot = _options.PublicIncludeRoots
            .Select(root => Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, '/'))
            .Where(root => normalized.StartsWith(
                root + Path.DirectorySeparatorChar, FileSystemPathComparison))
            .OrderByDescending(root => root.Length, Comparer<int>.Default)
            .FirstOrDefault();

        if (matchingRoot == null)
        {
            return normalized.Replace('\\', '/');
        }

        // Strip the root prefix (plus its trailing separator) and normalize to forward slashes
        var relativePath = normalized[(matchingRoot.Length + 1)..];
        return relativePath.Replace('\\', '/');
    }

    // =========================================================================
    // Visibility filtering
    // =========================================================================

    /// <summary>
    ///     Returns the visible constructors of a class, filtered by the configured
    ///     <see cref="CppGeneratorOptions.Visibility"/> and <see cref="CppGeneratorOptions.IncludeDeprecated"/>.
    /// </summary>
    /// <param name="cls">The class whose constructors to filter.</param>
    /// <returns>Constructors that pass both the visibility and deprecated filters.</returns>
    internal IEnumerable<CppFunction> GetVisibleConstructors(CppClass cls)
    {
        return cls.Members
            .Where(c => c.IsConstructor && IsVisibleMember(c.Accessibility))
            .Where(c => _options.IncludeDeprecated || !c.IsDeprecated);
    }

    /// <summary>
    ///     Returns the visible methods of a class, filtered by the configured
    ///     <see cref="CppGeneratorOptions.Visibility"/> and <see cref="CppGeneratorOptions.IncludeDeprecated"/>.
    /// </summary>
    /// <param name="cls">The class whose methods to filter.</param>
    /// <returns>Methods that pass both the visibility and deprecated filters.</returns>
    internal IEnumerable<CppFunction> GetVisibleMethods(CppClass cls)
    {
        return cls.Members
            .Where(m => !m.IsConstructor && IsVisibleMember(m.Accessibility))
            .Where(m => _options.IncludeDeprecated || !m.IsDeprecated);
    }

    /// <summary>
    ///     Returns the visible fields of a class, filtered by the configured
    ///     <see cref="CppGeneratorOptions.Visibility"/> and <see cref="CppGeneratorOptions.IncludeDeprecated"/>.
    /// </summary>
    /// <param name="cls">The class whose fields to filter.</param>
    /// <returns>Fields that pass both the visibility and deprecated filters.</returns>
    internal IEnumerable<CppField> GetVisibleFields(CppClass cls)
    {
        return cls.Fields
            .Where(f => IsVisibleMember(f.Accessibility))
            .Where(f => _options.IncludeDeprecated || !f.IsDeprecated);
    }

    /// <summary>
    ///     Determines whether a class member with the given accessibility should appear in the
    ///     generated output based on <see cref="CppGeneratorOptions.Visibility"/>.
    /// </summary>
    /// <param name="accessibility">The accessibility of the member.</param>
    /// <returns><see langword="true"/> when the member should be included.</returns>
    internal bool IsVisibleMember(CppAccessibility accessibility)
    {
        return _options.Visibility switch
        {
            ApiVisibility.Public => accessibility == CppAccessibility.Public,
            ApiVisibility.PublicAndProtected => accessibility is CppAccessibility.Public or CppAccessibility.Protected,
            ApiVisibility.All => true,

            // Default to public-only for any unrecognized future enum value
            _ => accessibility == CppAccessibility.Public,
        };
    }

    // =========================================================================
    // Comment extraction helpers
    // =========================================================================

    /// <summary>
    ///     Returns the brief summary text from a <see cref="CppDocComment"/>.
    /// </summary>
    /// <param name="doc">The doc comment to inspect. May be null.</param>
    /// <returns>The summary string, or <see langword="null"/> when <paramref name="doc"/> is null.</returns>
    internal static string? GetSummary(CppDocComment? doc) => doc?.Summary;

    /// <summary>
    ///     Returns the extended details text from a <see cref="CppDocComment"/>.
    /// </summary>
    /// <param name="doc">The doc comment to inspect. May be null.</param>
    /// <returns>The details string, or <see langword="null"/> when absent.</returns>
    internal static string? GetDetails(CppDocComment? doc) => doc?.Details;

    /// <summary>
    ///     Extracts the <c>@note</c> text from a <see cref="CppDocComment"/>, or returns
    ///     <see langword="null"/> when no note is present.
    /// </summary>
    /// <param name="doc">The doc comment to inspect. May be null.</param>
    /// <returns>The note string, or <see langword="null"/> when absent.</returns>
    internal static string? GetNote(CppDocComment? doc) => doc?.Note;

    /// <summary>Gets the example code from a doc comment, or <see langword="null"/> when absent.</summary>
    /// <param name="doc">The doc comment to read, or <see langword="null"/>.</param>
    /// <returns>The example code string, or <see langword="null"/>.</returns>
    internal static string? GetExample(CppDocComment? doc) => doc?.Example;

    /// <summary>
    ///     Looks up the description for a named parameter in a <see cref="CppDocComment"/>.
    /// </summary>
    /// <param name="doc">The doc comment containing the <c>@param</c> entries. May be null.</param>
    /// <param name="paramName">The exact parameter name to look up.</param>
    /// <returns>The trimmed description, or <see langword="null"/> when no matching entry exists.</returns>
    internal static string? GetParamDescription(CppDocComment? doc, string paramName)
    {
        return doc?.Params
            .FirstOrDefault(p => string.Equals(p.Name, paramName, StringComparison.Ordinal))
            ?.Description;
    }

    /// <summary>
    ///     Returns the return description from a <see cref="CppDocComment"/>.
    /// </summary>
    /// <param name="doc">The doc comment to inspect. May be null.</param>
    /// <returns>The return description, or <see langword="null"/> when absent.</returns>
    internal static string? GetReturnDescription(CppDocComment? doc) => doc?.Returns;

    /// <summary>
    ///     Derives a one-line description for a namespace from its doc comment, used as the
    ///     description in <c>api.md</c>.
    /// </summary>
    /// <param name="nsDecls">The namespace declarations to inspect.</param>
    /// <returns>A short description, or <see cref="NoDescriptionPlaceholder"/> when none is found.</returns>
    internal static string GetNamespaceDescription(NamespaceDeclarations nsDecls)
    {
        return GetSummary(nsDecls.Doc) ?? NoDescriptionPlaceholder;
    }

    // =========================================================================
    // Signature builders
    // =========================================================================

    /// <summary>
    ///     Builds a C++ method or constructor signature string suitable for display in a
    ///     fenced code block, including storage qualifiers, virtual qualifiers, parameter
    ///     list, and variadic indicator.
    /// </summary>
    /// <remarks>
    ///     Constructors omit the return type. The <c>static</c> and <c>virtual</c> prefixes
    ///     are mutually exclusive; <c>static</c> takes priority. Variadic functions append
    ///     <c>...</c> to the parameter list.
    /// </remarks>
    /// <param name="fn">The method or constructor to produce a signature for.</param>
    /// <returns>
    ///     A C++ declaration string such as <c>static string GetGreeting(const string&amp; name)</c>
    ///     or <c>Circle(double radius)</c> for constructors.
    /// </returns>
    internal static string BuildMethodSignature(CppFunction fn)
    {
        var sb = new System.Text.StringBuilder();

        // Constructors have no return type or storage qualifier prefix
        if (!fn.IsConstructor)
        {
            // static and virtual are mutually exclusive — static takes priority
            if (fn.IsStatic)
            {
                sb.Append("static ");
            }
            else if (fn.IsVirtual)
            {
                sb.Append("virtual ");
            }

            sb.Append(SimplifyTypeName(fn.ReturnTypeName));
            sb.Append(' ');
        }

        sb.Append(fn.Name);
        sb.Append('(');

        // Build the parameter list; append "..." for variadic functions
        var paramParts = fn.Parameters
            .Select(p => p.DefaultValue != null
                ? $"{SimplifyTypeName(p.TypeName)} {p.Name} = {p.DefaultValue}"
                : $"{SimplifyTypeName(p.TypeName)} {p.Name}")
            .ToList();
        if (fn.IsVariadic)
        {
            paramParts.Add("...");
        }

        sb.Append(string.Join(", ", paramParts));
        sb.Append(')');

        if (fn.IsDeleted)
        {
            sb.Append(" = delete");
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Builds the class declaration line for a <see cref="CppClass"/> for display in the
    ///     signature block of its type page, appending <c>final</c> when the class is marked
    ///     final and base class names when inheritance is present.
    /// </summary>
    /// <remarks>
    ///     Used when <see cref="CppClass.IsFinal"/> is true or when the class has direct base
    ///     types so that the declaration fragment makes the constraint and inheritance chain
    ///     immediately visible to readers without them needing to open the header file.
    /// </remarks>
    /// <param name="cls">The C++ class to produce a declaration line for.</param>
    /// <returns>
    ///     A C++ declaration string such as <c>class FinalClass final</c>,
    ///     <c>class FinalClass final : public Shape</c>, or
    ///     <c>class Circle : public Shape</c>.
    /// </returns>
    internal static string BuildClassDeclaration(CppClass cls)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("class ");
        sb.Append(cls.Name);

        // Append the final specifier so readers can see at a glance that the class
        // cannot be used as a base class
        if (cls.IsFinal)
        {
            sb.Append(" final");
        }

        // Append base class names in the declaration when inheritance is present
        // so the inheritance chain is visible without opening the header
        if (cls.BaseTypes.Count > 0)
        {
            var baseNames = cls.BaseTypes
                .Select(bt => SimplifyTypeName(bt.Name))
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();
            if (baseNames.Count > 0)
            {
                sb.Append(" : ");
                sb.Append(string.Join(", ", baseNames.Select(n => $"public {n}")));
            }
        }

        return sb.ToString();
    }

    /// <summary>
    ///     Builds the template parameter display suffix for a class (e.g. <c>&lt;T&gt;</c>
    ///     for <c>template&lt;typename T&gt; class Stack</c>).
    /// </summary>
    /// <param name="cls">The class to inspect for template parameters.</param>
    /// <returns>
    ///     A string of the form <c>&lt;T, U&gt;</c> when the class has template parameters,
    ///     or an empty string when the class is not a template or has no named parameters.
    /// </returns>
    internal static string BuildTemplateParamDisplay(CppClass cls)
    {
        if (cls.TemplateParams.Count == 0)
        {
            return string.Empty;
        }

        // Collect the name of each parameter; skip unnamed parameters
        var paramNames = cls.TemplateParams
            .Select(tp => tp.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        return paramNames.Count > 0 ? $"<{string.Join(", ", paramNames)}>" : string.Empty;
    }

    /// <summary>
    ///     Builds the full <c>template&lt;typename T&gt;</c> declaration line for a class,
    ///     suitable for inclusion before the <c>#include</c> line in the signature block.
    /// </summary>
    /// <param name="cls">The class to inspect for template parameters.</param>
    /// <returns>
    ///     A string such as <c>template&lt;typename T, typename U&gt;</c> when the class has
    ///     template parameters, or an empty string for non-template classes.
    /// </returns>
    internal static string BuildTemplateDeclaration(CppClass cls)
    {
        if (cls.TemplateParams.Count == 0)
        {
            return string.Empty;
        }

        var paramNames = cls.TemplateParams
            .Select(tp => tp.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        if (paramNames.Count == 0)
        {
            return string.Empty;
        }

        // Each type parameter is prefixed with "typename" to form a valid template declaration
        var typedParams = string.Join(", ", paramNames.Select(n => $"typename {n}"));
        return $"template<{typedParams}>";
    }

    /// <summary>
    ///     Simplifies a C++ type display name by replacing verbose internal STL names
    ///     with their idiomatic user-facing spellings.
    /// </summary>
    /// <param name="typeName">The raw display name from the clang AST <c>type.qualType</c>.</param>
    /// <returns>A more readable type name string.</returns>
    internal static string SimplifyTypeName(string typeName)
    {
        // std::string is a typedef for std::basic_string<char>; replace the internal name
        return typeName
            .Replace("basic_string<char, char_traits<char>, allocator<char>>", "string")
            .Replace("basic_string<char,char_traits<char>,allocator<char>>", "string")
            .Replace("basic_string", "string");
    }

    // =========================================================================
    // File name disambiguation
    // =========================================================================

    /// <summary>
    ///     Writes a combined Markdown page for a group of C++ members whose names collide on
    ///     case-insensitive file systems, placing all members on a single page named after
    ///     the shared lowercase key.
    /// </summary>
    /// <remarks>
    ///     This handles the case where a method <c>Name()</c> and a field <c>name</c> would
    ///     map to the same file name on case-insensitive file systems.
    ///     All colliding members are documented together under H2 sub-headings that show
    ///     both the exact display name and the member kind (e.g., <c>Name (Method)</c>).
    /// </remarks>
    /// <param name="factory">Factory for creating the output writer.</param>
    /// <param name="nsKey">The namespace key used as the parent folder for this type's directory.</param>
    /// <param name="nsDisplayName">
    ///     The C++ qualified namespace name forwarded to <see cref="CppEmitterGradualDisclosure.WriteFunctionContent"/>
    ///     so it can emit fully-qualified signature comments.
    /// </param>
    /// <param name="cls">The declaring class.</param>
    /// <param name="lowerKey">
    ///     The shared lowercase file name key. Used as both the page file name and the H1
    ///     page heading so the combined page has a stable, predictable address.
    /// </param>
    /// <param name="members">
    ///     The ordered list of members whose base names collide on case-insensitive file
    ///     systems. Elements must be <see cref="CppFunction"/> or <see cref="CppField"/>.
    ///     Must contain at least two elements.
    /// </param>
    /// <param name="cppResolver">Type link resolver used to linkify parameter type cells.</param>
    internal static void WriteCombinedMemberPage(
        IMarkdownWriterFactory factory,
        string nsKey,
        string nsDisplayName,
        CppClass cls,
        string lowerKey,
        IReadOnlyList<object> members,
        CppTypeLinkResolver cppResolver)
    {
        var combinedCurrentFolder = $"{nsKey}/{cls.Name}";
        using var writer = factory.CreateMarkdown(combinedCurrentFolder, lowerKey);

        // The shared lowercase key serves as the page heading so every member in the group
        // can be found at the same predictable path regardless of filesystem case-sensitivity
        writer.WriteHeading(1, lowerKey);

        // Accumulate external types across all members on this shared page
        var externalTypes = new SortedSet<CppExternalTypeInfo>();

        foreach (var member in members)
        {
            switch (member)
            {
                case CppFunction fn:
                    // Constructors and methods both use WriteFunctionContent but need distinct labels.
                    // Include parameter types in the heading so overloaded functions are distinguishable.
                    var fnKind = fn.IsConstructor ? "Constructor" : "Method";
                    var fnParamTypes = string.Join(
                        ", ",
                        fn.Parameters.Select(p => SimplifyTypeName(p.TypeName)));
                    writer.WriteHeading(2, $"{fn.Name}({fnParamTypes}) ({fnKind})");
                    CppEmitterGradualDisclosure.WriteFunctionContent(writer, fn, new CppFunctionWriteContext(nsDisplayName, cls.Name, cppResolver, combinedCurrentFolder, externalTypes, 3));
                    break;

                case CppField field:
                    writer.WriteHeading(2, $"{field.Name} (Field)");
                    CppEmitterGradualDisclosure.WriteFieldContent(writer, nsDisplayName, cls.Name, field);
                    break;
            }
        }

        WriteExternalTypesSection(writer, externalTypes);
    }

    /// <summary>
    ///     Returns the base name used to compute the file name for a class member, applying
    ///     the convention that constructors use the declaring class name.
    /// </summary>
    /// <remarks>
    ///     This mirrors the DotNet generator's <c>BuildMethodFileName</c> pattern: constructors
    ///     are identified by the class name in the file tree rather than by a generic token,
    ///     so they receive pages at <c>{ClassName}/{ClassName}.md</c>.
    /// </remarks>
    /// <param name="member">The member whose base name to compute.</param>
    /// <param name="className">The name of the declaring class, used for constructors.</param>
    /// <returns>
    ///     The class name when <paramref name="member"/> is a constructor; otherwise the
    ///     member's own name.
    /// </returns>
    internal static string GetMemberBaseName(object member, string className) => member switch
    {
        CppFunction fn when fn.IsConstructor => className,
        CppFunction fn => fn.Name,
        CppField field => field.Name,
        _ => className,
    };

    /// <summary>
    ///     Writes the "External Types" section at the end of a generated Markdown page,
    ///     listing all non-standard C++ types referenced in table cells on that page.
    /// </summary>
    /// <remarks>
    ///     The section is emitted only when <paramref name="externalTypes"/> is non-empty.
    ///     Rows are sorted alphabetically by type string because <see cref="SortedSet{T}"/>
    ///     preserves the order defined by <see cref="CppExternalTypeInfo.CompareTo"/>.
    /// </remarks>
    /// <param name="writer">The Markdown writer for the current page.</param>
    /// <param name="externalTypes">
    ///     The set of external types accumulated during table row generation. May be empty.
    /// </param>
    internal static void WriteExternalTypesSection(IMarkdownWriter writer, SortedSet<CppExternalTypeInfo> externalTypes)
    {
        if (externalTypes.Count == 0)
        {
            return;
        }

        writer.WriteHeading(2, "External Types");
        writer.WriteTable(
            ["Type", "Namespace"],
            externalTypes.Select(t => new[] { t.TypeString, t.Namespace }));
    }

    // =========================================================================
    // Inner data model
    // =========================================================================

    /// <summary>
    ///     Accumulates the owned C++ declarations that belong to a single namespace,
    ///     ready for Markdown output.
    /// </summary>
    internal sealed class NamespaceDeclarations
    {
        /// <summary>
        ///     Initializes a new instance with the given display name and optional doc comment.
        /// </summary>
        /// <param name="displayName">
        ///     The C++ qualified namespace name (e.g. <c>mylib::rendering</c>),
        ///     used as the Markdown page heading.
        /// </param>
        /// <param name="doc">
        ///     The doc comment attached to the namespace declaration, or <see langword="null"/>
        ///     when no namespace-level comment is available.
        /// </param>
        public NamespaceDeclarations(string displayName, CppDocComment? doc = null)
        {
            DisplayName = displayName;
            Doc = doc;
        }

        /// <summary>Gets the C++ qualified namespace name used as the Markdown page heading.</summary>
        public string DisplayName { get; }

        /// <summary>Gets the doc comment attached to the namespace declaration, if any.</summary>
        public CppDocComment? Doc { get; }

        /// <summary>Gets the list of owned classes and structs declared in this namespace.</summary>
        public List<CppClass> Classes { get; } = [];

        /// <summary>Gets the list of owned enums declared in this namespace.</summary>
        public List<CppEnum> Enums { get; } = [];

        /// <summary>Gets the list of owned <c>using</c> type aliases declared in this namespace.</summary>
        public List<CppTypeAlias> TypeAliases { get; } = [];

        /// <summary>Gets the list of owned free functions declared in this namespace.</summary>
        public List<CppFunction> FreeFunctions { get; } = [];
    }
}

/// <summary>
///     Bundles the per-type-page writing context that is constant across all member pages
///     generated for a single C++ class. Used to reduce parameter counts on the
///     helper methods that emit individual member pages and table rows.
/// </summary>
internal sealed record CppTypePageWriteContext(
    IMarkdownWriterFactory Factory,
    string NsKey,
    string NsDisplayName,
    CppClass Class,
    CppTypeLinkResolver CppResolver);

/// <summary>
///     Bundles the per-function documentation writing context passed to
///     <see cref="CppEmitterGradualDisclosure.WriteFunctionContent"/> so that callers do not need to thread five
///     constant parameters through each call site.
/// </summary>
internal sealed record CppFunctionWriteContext(
    string NsDisplayName,
    string ClassName,
    CppTypeLinkResolver CppResolver,
    string CurrentFolder,
    ISet<CppExternalTypeInfo> ExternalTypes,
    int ParametersHeadingLevel = 2);
