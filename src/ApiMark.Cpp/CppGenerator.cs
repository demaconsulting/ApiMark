using System.Runtime.InteropServices;
using ApiMark.Core;
using ApiMark.Cpp.CppAst;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace ApiMark.Cpp;

/// <summary>Generates Markdown API documentation from C++ headers using clang.</summary>
/// <remarks>
///     Implements <see cref="IApiGenerator"/> for C++ libraries. Enumerates public header
///     files, invokes clang with <c>-ast-dump=json</c> to obtain a fully resolved C++ AST,
///     applies a file-provenance ownership filter based on
///     <see cref="CppGeneratorOptions.PublicIncludeRoots"/>, and writes a gradual-disclosure
///     Markdown tree through <see cref="IMarkdownWriterFactory"/>. The output structure mirrors
///     <c>DotNetGenerator</c>: a library entrypoint, per-namespace summaries, per-type pages,
///     and per-member detail pages for every visible member. Not thread-safe; construct and use
///     one instance per generation run.
/// </remarks>
public sealed class CppGenerator : IApiGenerator
{
    /// <summary>Column header label used in all generated Markdown tables for the description column.</summary>
    private const string DescriptionColumnHeader = "Description";

    /// <summary>Placeholder emitted in description cells when no doc comment summary is available.</summary>
    private const string NoDescriptionPlaceholder = "*No description provided.*";

    /// <summary>Reserved namespace key and heading used for declarations in the C++ global namespace.</summary>
    private const string GlobalNamespaceKey = "global";

    /// <summary>Configuration controlling which headers, roots, visibility, and other parse options to apply.</summary>
    private readonly CppGeneratorOptions _options;

    /// <summary>
    ///     Initializes a new instance of <see cref="CppGenerator"/> with the specified options.
    /// </summary>
    /// <remarks>
    ///     No file system access or parsing occurs at construction time; all I/O is deferred to
    ///     <see cref="Generate"/>.
    /// </remarks>
    /// <param name="options">
    ///     The generator configuration options. Must not be null.
    ///     <see cref="CppGeneratorOptions.LibraryName"/> must be non-empty and
    ///     <see cref="CppGeneratorOptions.PublicIncludeRoots"/> must contain at least one entry.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="options"/>.<see cref="CppGeneratorOptions.LibraryName"/> is null or whitespace.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when <paramref name="options"/>.<see cref="CppGeneratorOptions.PublicIncludeRoots"/> is null or empty.
    /// </exception>
    public CppGenerator(CppGeneratorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.LibraryName))
        {
            throw new ArgumentException("LibraryName must not be null or empty.", nameof(options));
        }

        if (options.PublicIncludeRoots == null || options.PublicIncludeRoots.Count == 0)
        {
            throw new ArgumentException("PublicIncludeRoots must contain at least one entry.", nameof(options));
        }

        _options = options;
    }

    /// <summary>
    ///     Generates the full Markdown API documentation tree for the configured C++ library.
    /// </summary>
    /// <remarks>
    ///     Execution steps:
    ///     <list type="number">
    ///       <item>Enumerate candidate header files under each <see cref="CppGeneratorOptions.PublicIncludeRoots"/> entry.</item>
    ///       <item>Run clang with <c>-ast-dump=json</c> on all candidate headers via <see cref="ClangAstParser"/>.</item>
    ///       <item>Log any clang diagnostic errors from system headers via the context output channel.</item>
    ///       <item>Walk the parsed namespaces, applying the ownership and visibility filters.</item>
    ///       <item>Write the library entrypoint, namespace summaries, type pages, and member detail pages.</item>
    ///     </list>
    /// </remarks>
    /// <param name="factory">
    ///     Factory for creating per-file Markdown writers. Must not be null. The generator produces
    ///     the fixed entrypoint via <c>factory.CreateMarkdown("", "api")</c>.
    /// </param>
    /// <param name="context">
    ///     Output channel for informational messages. Must not be null. System-header diagnostic
    ///     messages from clang are emitted here via <see cref="IContext.WriteLine"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
    /// <exception cref="DirectoryNotFoundException">
    ///     Thrown when a path in <see cref="CppGeneratorOptions.PublicIncludeRoots"/> does not exist on disk.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when clang cannot be located or exits with an error and produces no JSON output.
    /// </exception>
    public void Generate(IMarkdownWriterFactory factory, IContext context)
    {
        ArgumentNullException.ThrowIfNull(factory);

        // Collect candidate header files from all configured public include roots
        var headerFiles = CollectHeaderFiles();

        // Run clang -ast-dump=json on all headers and parse the resulting AST
        var result = ClangAstParser.Parse(headerFiles, _options);

        // Throw on errors from the user's public headers; log errors from system headers
        CheckForErrors(result, headerFiles, context);

        // Walk parsed namespaces and group owned declarations by their qualified namespace key
        var namespaceDecls = new SortedDictionary<string, NamespaceDeclarations>(StringComparer.Ordinal);
        foreach (var ns in result.Namespaces)
        {
            CollectResultNamespace(ns, namespaceDecls);
        }

        // Write the library entrypoint page listing all discovered namespaces
        WriteApiPage(factory, namespaceDecls);

        // Write one namespace summary page per namespace, one type page per owned class,
        // one detail page per owned free function, and one detail page per owned enum
        foreach (var (nsKey, nsDecls) in namespaceDecls)
        {
            WriteNamespacePage(factory, nsKey, nsDecls);
            foreach (var cls in nsDecls.Classes)
            {
                WriteTypePage(factory, nsKey, nsDecls.DisplayName, cls);
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
                WriteFreeFunctionPage(factory, nsKey, nsDecls.DisplayName, fn);
            }

            if (nsOperatorFunctions.Count > 0)
            {
                WriteNamespaceOperatorsPage(factory, nsKey, nsDecls.DisplayName, nsOperatorFunctions);
            }

            // Write one enum detail page per owned enum declared in this namespace
            foreach (var en in nsDecls.Enums)
            {
                WriteEnumPage(factory, nsKey, nsDecls.DisplayName, en);
            }
        }
    }

    // =========================================================================
    // Header file collection
    // =========================================================================

    /// <summary>
    ///     Enumerates candidate header files under each configured public include root,
    ///     applying <see cref="CppGeneratorOptions.IncludePatterns"/> and
    ///     <see cref="CppGeneratorOptions.ExcludePatterns"/> when present.
    /// </summary>
    /// <returns>
    ///     A list of absolute header file paths with recognized C++ header extensions
    ///     (<c>.h</c>, <c>.hpp</c>, <c>.hxx</c>, <c>.h++</c>) that satisfy the configured
    ///     include/exclude glob patterns.
    /// </returns>
    /// <remarks>
    ///     When <see cref="CppGeneratorOptions.IncludePatterns"/> is empty all recognized
    ///     headers pass the include step. When <see cref="CppGeneratorOptions.ExcludePatterns"/>
    ///     is empty no files are removed. Patterns are evaluated relative to each root directory
    ///     using <see cref="Matcher"/> from <c>Microsoft.Extensions.FileSystemGlobbing</c>.
    /// </remarks>
    /// <exception cref="DirectoryNotFoundException">
    ///     Thrown when a configured public include root does not exist on disk.
    /// </exception>
    private List<string> CollectHeaderFiles()
    {
        // Recognized C++ header file extensions
        var headerExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".h", ".hpp", ".hxx", ".h++",
        };

        var headers = new List<string>();
        foreach (var root in _options.PublicIncludeRoots)
        {
            // Fail early when a configured root is absent rather than silently producing empty output
            if (!Directory.Exists(root))
            {
                throw new DirectoryNotFoundException(
                    $"Public include root not found: '{root}'");
            }

            // Enumerate all files recursively and retain only recognized header extensions
            var allFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                .Where(f => headerExtensions.Contains(Path.GetExtension(f)))
                .ToList();

            // Apply include/exclude glob patterns when at least one is configured; when
            // neither is set, all discovered headers are forwarded to clang without filtering
            if (_options.IncludePatterns.Count > 0 || _options.ExcludePatterns.Count > 0)
            {
                // Build a Matcher whose patterns are relative to the root directory
                var matcher = new Matcher();

                if (_options.IncludePatterns.Count > 0)
                {
                    // Caller-supplied include patterns define the set of accepted headers
                    foreach (var pattern in _options.IncludePatterns)
                    {
                        matcher.AddInclude(pattern);
                    }
                }
                else
                {
                    // No include patterns: accept every file so that ExcludePatterns alone
                    // can narrow the set without requiring an explicit catch-all
                    matcher.AddInclude("**/*");
                }

                foreach (var pattern in _options.ExcludePatterns)
                {
                    matcher.AddExclude(pattern);
                }

                // Execute the glob matcher against the root; result.Files contains matched
                // relative paths which are then converted back to absolute for clang
                var matchResult = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(root)));
                var matchedAbsolute = new HashSet<string>(
                    matchResult.Files.Select(f => Path.GetFullPath(Path.Combine(root, f.Path))),
                    FileSystemPathComparer);

                // Intersect the extension-filtered list with the glob-matched set so that
                // files with non-header extensions are never forwarded even if a glob matches them
                headers.AddRange(allFiles.Where(f => matchedAbsolute.Contains(Path.GetFullPath(f))));
            }
            else
            {
                // No patterns configured: include all discovered header files under this root
                headers.AddRange(allFiles);
            }
        }

        // De-duplicate and sort to produce a stable, deterministic header list.
        // Overlapping PublicIncludeRoots can otherwise produce duplicate entries that
        // cause declarations to appear multiple times in the generated output.
        return headers
            .Select(Path.GetFullPath)
            .Distinct(FileSystemPathComparer)
            .OrderBy(h => h, StringComparer.Ordinal)
            .ToList();
    }

    // =========================================================================
    // Error checking
    // =========================================================================

    /// <summary>
    ///     Throws <see cref="InvalidOperationException"/> when clang reported error-class diagnostics
    ///     in the user's public headers; logs errors from system or third-party headers via
    ///     <paramref name="context"/> without halting generation.
    /// </summary>
    /// <remarks>
    ///     clang may report errors from system or third-party headers (e.g., unrecognized compiler
    ///     builtins) while still producing a valid and complete AST for the public headers being
    ///     documented. Only errors whose path starts with a known public header file are treated as
    ///     hard failures to avoid false positives from the compiler's own standard library headers.
    /// </remarks>
    /// <param name="result">The compilation result whose <see cref="CppCompilationResult.Errors"/> to check.</param>
    /// <param name="headerFiles">The list of public header files passed to clang; used to identify user errors.</param>
    /// <param name="context">Output channel for informational system-header diagnostic messages.</param>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when <paramref name="result"/> contains error-class diagnostics from one or more
    ///     of the user's public header files.
    /// </exception>
    private static void CheckForErrors(CppCompilationResult result, IReadOnlyList<string> headerFiles, IContext context)
    {
        if (result.Errors.Count == 0)
        {
            return;
        }

        // Normalize header paths to forward slashes because clang emits diagnostics with '/'
        // even on Windows, while Directory.GetFiles returns native '\' paths.
        var normalizedHeaders = headerFiles
            .Select(h => Path.GetFullPath(h).Replace('\\', '/'))
            .ToList();
        var userErrors = result.Errors
            .Where(e => normalizedHeaders.Any(h => e.Contains(h, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        var systemErrors = result.Errors.Except(userErrors).ToList();

        foreach (var error in systemErrors)
        {
            context.WriteLine($"[CppGenerator] clang: {error}");
        }

        if (userErrors.Count > 0)
        {
            var message = string.Join(Environment.NewLine, userErrors.Select(e => $"[CppGenerator] clang: {e}"));
            throw new InvalidOperationException(
                $"clang reported errors in public headers during AST generation:{Environment.NewLine}{message}");
        }
    }

    // =========================================================================
    // AST result collection
    // =========================================================================

    /// <summary>
    ///     Maps a parsed <see cref="CppNamespaceDecl"/> into the generator's internal
    ///     <see cref="NamespaceDeclarations"/> accumulator, applying the configured
    ///     visibility and deprecated filters.
    /// </summary>
    /// <param name="ns">The namespace declaration to process.</param>
    /// <param name="result">Dictionary that accumulates declarations grouped by namespace key.</param>
    private void CollectResultNamespace(
        CppNamespaceDecl ns,
        SortedDictionary<string, NamespaceDeclarations> result)
    {
        // Derive the file-path-compatible key (:: → .) and the display name
        var qualName = ns.QualifiedName;
        var nsKey = string.IsNullOrEmpty(qualName)
            ? GlobalNamespaceKey
            : qualName.Replace("::", ".", StringComparison.Ordinal);
        var displayName = string.IsNullOrEmpty(qualName) ? GlobalNamespaceKey : qualName;

        // Collect owned classes, applying the deprecated filter
        foreach (var cls in ns.Classes)
        {
            if (!_options.IncludeDeprecated && cls.IsDeprecated)
            {
                continue;
            }

            EnsureNamespace(result, nsKey, displayName, ns.Doc);
            result[nsKey].Classes.Add(cls);
        }

        // Collect owned free functions, applying the deprecated filter
        foreach (var fn in ns.FreeFunctions)
        {
            if (!_options.IncludeDeprecated && fn.IsDeprecated)
            {
                continue;
            }

            EnsureNamespace(result, nsKey, displayName, ns.Doc);
            result[nsKey].FreeFunctions.Add(fn);
        }

        // Collect owned enums, applying the deprecated filter
        foreach (var en in ns.Enums)
        {
            if (!_options.IncludeDeprecated && en.IsDeprecated)
            {
                continue;
            }

            EnsureNamespace(result, nsKey, displayName, ns.Doc);
            result[nsKey].Enums.Add(en);
        }
    }

    /// <summary>
    ///     Ensures a <see cref="NamespaceDeclarations"/> entry exists in the result dictionary,
    ///     creating a new one with the supplied display name when the key is absent.
    /// </summary>
    /// <param name="result">The dictionary to update in-place.</param>
    /// <param name="key">The file-path-compatible namespace key.</param>
    /// <param name="displayName">The C++ qualified namespace name used as the Markdown page heading.</param>
    /// <param name="doc">Optional doc comment from the namespace declaration.</param>
    private static void EnsureNamespace(
        SortedDictionary<string, NamespaceDeclarations> result,
        string key,
        string displayName,
        CppDocComment? doc = null)
    {
        if (!result.ContainsKey(key))
        {
            result[key] = new NamespaceDeclarations(displayName, doc);
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
    private static StringComparison FileSystemPathComparison =>
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
    private static StringComparer FileSystemPathComparer =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;

    /// <summary>
    ///     Sanitizes a C++ declaration name for use as a file-system file name by replacing any
    ///     characters that are invalid in file names on Windows or Unix with an underscore.
    /// </summary>
    /// <remarks>
    ///     C++ operator names (e.g. <c>operator*</c>, <c>operator&lt;&lt;</c>) and conversion
    ///     operators can contain characters such as <c>*</c>, <c>&lt;</c>, <c>&gt;</c>, and
    ///     <c>:</c> that are forbidden in Windows file names. Replacing them with <c>_</c> produces
    ///     a stable, platform-safe file name while retaining human readability for non-operator names.
    /// </remarks>
    /// <param name="name">The C++ declaration name to sanitize. Must not be null.</param>
    /// <returns>
    ///     A copy of <paramref name="name"/> with every character from
    ///     <see cref="Path.GetInvalidFileNameChars"/> replaced by <c>_</c>.
    /// </returns>
    private static string SanitizeFileName(string name)
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
    private string GetIncludePath(string sourceFile)
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
    // Markdown page writers
    // =========================================================================

    /// <summary>
    ///     Writes the library entrypoint <c>api.md</c> listing all documented namespaces.
    /// </summary>
    /// <param name="factory">Factory for creating the output writer.</param>
    /// <param name="namespaces">All documented namespaces grouped and sorted by key.</param>
    private void WriteApiPage(
        IMarkdownWriterFactory factory,
        SortedDictionary<string, NamespaceDeclarations> namespaces)
    {
        using var writer = factory.CreateMarkdown("", "api");
        writer.WriteHeading(1, $"{_options.LibraryName} API Reference");

        // Emit optional library description paragraph
        if (!string.IsNullOrWhiteSpace(_options.Description))
        {
            writer.WriteParagraph(_options.Description);
        }

        if (namespaces.Count == 0)
        {
            // Emit a fallback paragraph so api.md is never completely empty
            writer.WriteParagraph("No public API declarations found.");
            return;
        }

        // All-namespaces table — lists every namespace so AI agents get a complete map in one read.
        // Declarations count reflects only declarations directly in each namespace
        var headers = new[] { "Namespace", "Declarations", DescriptionColumnHeader };
        var rows = namespaces.Select(kv =>
        {
            var nsDecls = kv.Value;
            var declarationCount = nsDecls.Classes.Count + nsDecls.Enums.Count + nsDecls.FreeFunctions.Count;
            var description = GetNamespaceDescription(nsDecls);
            return new[] { $"[{nsDecls.DisplayName}]({kv.Key}.md)", declarationCount.ToString(), description };
        });
        writer.WriteTable(headers, rows);

        // Path convention appendix — helps AI agents navigate without a separate resolver
        writer.WriteHeading(2, "File Naming and Path Convention");
        writer.WriteParagraph("Documentation paths are derived deterministically from fully-qualified symbol names. Namespace separators (`::`) are replaced with `.` in file and folder names.");
        writer.WriteTable(
            new[] { "Symbol kind", "Path pattern" },
            new[]
            {
                new[] { "Namespace", "`{Namespace}.md`" },
                new[] { "Type", "`{Namespace}/{TypeName}.md`" },
                new[] { "Member", "`{Namespace}/{TypeName}/{MemberName}.md`" },
                new[] { "Free function", "`{Namespace}/{FunctionName}.md`" },
                new[] { "Enum", "`{Namespace}/{EnumName}.md`" },
                new[] { "Operators (class)", "`{Namespace}/{TypeName}/operators.md`" },
                new[] { "Operators (namespace)", "`{Namespace}/operators.md`" },
            });
    }

    /// <summary>
    ///     Writes the summary page for a single namespace, listing its owned types,
    ///     enums, and free functions.
    /// </summary>
    /// <param name="factory">Factory for creating the output writer.</param>
    /// <param name="nsKey">The file-path-compatible namespace key used as the output file name.</param>
    /// <param name="nsDecls">The declarations that belong to this namespace.</param>
    private static void WriteNamespacePage(
        IMarkdownWriterFactory factory,
        string nsKey,
        NamespaceDeclarations nsDecls)
    {
        using var writer = factory.CreateMarkdown("", nsKey);
        writer.WriteHeading(1, $"{nsDecls.DisplayName} Namespace");

        // Type table — one row per owned class or struct, sorted alphabetically
        if (nsDecls.Classes.Count > 0)
        {
            writer.WriteHeading(2, "Types");
            var typeHeaders = new[] { "Type", DescriptionColumnHeader };
            var typeRows = nsDecls.Classes
                .OrderBy(c => c.Name, StringComparer.Ordinal)
                .Select(cls =>
                {
                    var summary = GetSummary(cls.Doc) ?? NoDescriptionPlaceholder;
                    return new[] { $"[{cls.Name}]({nsKey}/{cls.Name}.md)", summary };
                });
            writer.WriteTable(typeHeaders, typeRows);
        }

        // Enum table — one row per owned enum, sorted alphabetically
        if (nsDecls.Enums.Count > 0)
        {
            writer.WriteHeading(2, "Enums");
            var enumHeaders = new[] { "Enum", DescriptionColumnHeader };
            var enumRows = nsDecls.Enums
                .OrderBy(e => e.Name, StringComparer.Ordinal)
                .Select(en =>
                {
                    var summary = GetSummary(en.Doc) ?? NoDescriptionPlaceholder;
                    return new[] { $"[{en.Name}]({nsKey}/{en.Name}.md)", summary };
                });
            writer.WriteTable(enumHeaders, enumRows);
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
            var fnHeaders = new[] { "Function", "Returns", DescriptionColumnHeader };
            var fnRows = regularFreeFunctions
                .OrderBy(f => f.Name, StringComparer.Ordinal)
                .Select(fn =>
                {
                    var summary = GetSummary(fn.Doc) ?? NoDescriptionPlaceholder;
                    var returnType = SimplifyTypeName(fn.ReturnTypeName);
                    var safeName = SanitizeFileName(fn.Name);
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
                new[] { "Operators", DescriptionColumnHeader },
                new[] { new[] { $"[operators]({nsKey}/operators.md)", "Operator overloads" } });
        }
    }

    /// <summary>
    ///     Writes the type page for a single C++ class or struct, including the qualified
    ///     name, an optional template declaration, an optional <c>#include</c> directive,
    ///     summary, base types, and grouped member sub-tables for constructors, methods, and fields.
    /// </summary>
    /// <param name="factory">Factory for creating output writers.</param>
    /// <param name="nsKey">
    ///     The file-path-compatible namespace key; used as the subfolder for this type's page.
    /// </param>
    /// <param name="nsDisplayName">
    ///     The C++ qualified namespace name (e.g. <c>fixtures</c>) used to build the
    ///     fully-qualified type name shown in the signature comment.
    /// </param>
    /// <param name="cls">The C++ class or struct to document.</param>
    private void WriteTypePage(
        IMarkdownWriterFactory factory,
        string nsKey,
        string nsDisplayName,
        CppClass cls)
    {
        // Build the template parameter suffix (e.g. "<T>" for template<typename T> class Stack)
        var templateParamDisplay = BuildTemplateParamDisplay(cls);
        var displayName = string.IsNullOrEmpty(templateParamDisplay)
            ? cls.Name
            : $"{cls.Name}{templateParamDisplay}";

        // Fully-qualified name for the signature comment (e.g. "fixtures::Stack<T>")
        var qualifiedClassName = string.IsNullOrEmpty(nsDisplayName)
            ? displayName
            : $"{nsDisplayName}::{displayName}";

        using var writer = factory.CreateMarkdown(nsKey, cls.Name);
        writer.WriteHeading(1, displayName);

        // Emit the qualified name comment, optional template declaration, and #include directive
        // so readers have everything needed to use the type without browsing the header tree
        var sourceFile = cls.Location?.File;
        if (!string.IsNullOrEmpty(sourceFile))
        {
            var includePath = GetIncludePath(sourceFile);
            var sigParts = new List<string> { $"// {qualifiedClassName}" };

            // Prepend the template<...> line when the class is a template so the signature
            // is a valid, copy-pasteable C++ forward-declaration fragment
            var templateDecl = BuildTemplateDeclaration(cls);
            if (!string.IsNullOrEmpty(templateDecl))
            {
                sigParts.Add(templateDecl);
            }

            sigParts.Add($"#include <{includePath}>");

            // When the class is marked final, append the class declaration line so readers
            // can see at a glance that the class cannot be used as a base class
            if (cls.IsFinal)
            {
                sigParts.Add(BuildClassDeclaration(cls));
            }

            writer.WriteSignature("cpp", string.Join("\n", sigParts));
        }

        // Emit summary from doc comment, or placeholder when no comment is present
        var typeSummary = GetSummary(cls.Doc);
        writer.WriteParagraph(!string.IsNullOrEmpty(typeSummary) ? typeSummary : NoDescriptionPlaceholder);

        // Emit extended details when the doc comment contains a @details or @remarks block
        var typeDetails = GetDetails(cls.Doc);
        if (!string.IsNullOrEmpty(typeDetails))
        {
            writer.WriteParagraph(typeDetails);
        }

        // Emit base type names so readers know the inheritance chain without reading the header
        if (cls.BaseTypes.Count > 0)
        {
            var baseNames = cls.BaseTypes
                .Select(bt => SimplifyTypeName(bt.Name))
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList();
            if (baseNames.Count > 0)
            {
                writer.WriteParagraph($"**Inherits**: {string.Join(", ", baseNames)}");
            }
        }

        // Collect visible constructors, methods, and fields; sorted alphabetically for deterministic output
        var visibleCtors = GetVisibleConstructors(cls).OrderBy(c => c.Name, StringComparer.Ordinal).ToList();
        var visibleMethods = GetVisibleMethods(cls).OrderBy(m => m.Name, StringComparer.Ordinal).ToList();
        var visibleFields = GetVisibleFields(cls).OrderBy(f => f.Name, StringComparer.Ordinal).ToList();

        if (visibleCtors.Count == 0 && visibleMethods.Count == 0 && visibleFields.Count == 0)
        {
            return;
        }

        // Partition methods into operator overloads and regular methods; operator overloads are
        // grouped onto a single operators.md page to prevent file-name collisions — operator+,
        // operator-, and operator* would all sanitize to the same safe file name
        var operatorMethods = visibleMethods
            .Where(m => m.Name.StartsWith("operator", StringComparison.Ordinal))
            .ToList();
        var regularMethods = visibleMethods
            .Where(m => !m.Name.StartsWith("operator", StringComparison.Ordinal))
            .ToList();

        // Build a flat list of all visible members for case-insensitive collision detection.
        // Constructors, regular methods, and fields are merged so that cross-kind name collisions
        // (e.g. method Name() and field name) are detected as a single group.
        // Operator methods are excluded here because they all share a single operators.md page.
        var allMembers = new List<object>(
            visibleCtors.Cast<object>()
                .Concat(regularMethods.Cast<object>())
                .Concat(visibleFields.Cast<object>()));

        // Case-insensitive map: lowercase member name → list of members sharing that lowercase name.
        // Members sharing a key are combined onto a single page named after the lowercase key.
        var caseInsensitiveGroups = new Dictionary<string, List<object>>(StringComparer.Ordinal);
        foreach (var member in allMembers)
        {
            var baseName = GetMemberBaseName(member, cls.Name);
            var lowerKey = baseName.ToLowerInvariant();
            if (!caseInsensitiveGroups.TryGetValue(lowerKey, out var list))
            {
                list = [];
                caseInsensitiveGroups[lowerKey] = list;
            }

            list.Add(member);
        }

        // Track which lowercase keys have had their page written so collision groups
        // are documented exactly once while their table rows are still emitted individually
        var writtenKeys = new HashSet<string>(StringComparer.Ordinal);

        var ctorRows = new List<string[]>();
        var methodRows = new List<string[]>();
        var fieldRows = new List<string[]>();

        // Process constructors — emitted first because instantiation is the first thing
        // a consumer needs to understand about a type
        foreach (var ctor in visibleCtors)
        {
            var baseName = GetMemberBaseName(ctor, cls.Name);
            var lowerKey = baseName.ToLowerInvariant();
            var group = caseInsensitiveGroups[lowerKey];

            // When the lowercase key is unique, the page uses the original member name;
            // when there is a collision, the page uses the lowercase key so it is stable.
            // Sanitize for use as a file name (operator names can contain *, <, >, : etc.)
            var pageFileName = SanitizeFileName(group.Count == 1 ? baseName : lowerKey);
            var ctorSummary = GetSummary(ctor.Doc) ?? NoDescriptionPlaceholder;

            // Show simplified parameter types in the link text so readers can
            // distinguish overloaded constructors at a glance
            var paramTypes = string.Join(
                ", ",
                ctor.Parameters.Select(p => SimplifyTypeName(p.TypeName)));

            if (writtenKeys.Add(lowerKey))
            {
                if (group.Count == 1)
                {
                    WriteMemberPage(factory, nsKey, nsDisplayName, cls, ctor, pageFileName);
                }
                else
                {
                    WriteCombinedMemberPage(factory, nsKey, nsDisplayName, cls, pageFileName, group);
                }
            }

            ctorRows.Add(new[] { $"[{cls.Name}({paramTypes})]({cls.Name}/{pageFileName}.md)", ctorSummary });
        }

        // Process methods — emit method rows after writing each member's detail page
        foreach (var method in regularMethods)
        {
            var baseName = GetMemberBaseName(method, cls.Name);
            var lowerKey = baseName.ToLowerInvariant();
            var group = caseInsensitiveGroups[lowerKey];
            var pageFileName = SanitizeFileName(group.Count == 1 ? baseName : lowerKey);
            var methodSummary = GetSummary(method.Doc) ?? NoDescriptionPlaceholder;
            var returnType = SimplifyTypeName(method.ReturnTypeName);

            if (writtenKeys.Add(lowerKey))
            {
                if (group.Count == 1)
                {
                    WriteMemberPage(factory, nsKey, nsDisplayName, cls, method, pageFileName);
                }
                else
                {
                    WriteCombinedMemberPage(factory, nsKey, nsDisplayName, cls, pageFileName, group);
                }
            }

            // Show simplified parameter types in the link text so readers can
            // distinguish overloaded methods at a glance
            var methodParamTypes = string.Join(
                ", ",
                method.Parameters.Select(p => SimplifyTypeName(p.TypeName)));
            methodRows.Add(new[] { $"[{method.Name}({methodParamTypes})]({cls.Name}/{pageFileName}.md)", returnType, methodSummary });
        }

        // Process fields — emit field rows after writing each member's detail page
        foreach (var field in visibleFields)
        {
            var baseName = GetMemberBaseName(field, cls.Name);
            var lowerKey = baseName.ToLowerInvariant();
            var group = caseInsensitiveGroups[lowerKey];
            var pageFileName = SanitizeFileName(group.Count == 1 ? baseName : lowerKey);
            var fieldSummary = GetSummary(field.Doc) ?? NoDescriptionPlaceholder;
            var typeName = SimplifyTypeName(field.TypeName);

            if (writtenKeys.Add(lowerKey))
            {
                if (group.Count == 1)
                {
                    WriteMemberPage(factory, nsKey, nsDisplayName, cls, field, pageFileName);
                }
                else
                {
                    WriteCombinedMemberPage(factory, nsKey, nsDisplayName, cls, pageFileName, group);
                }
            }

            fieldRows.Add(new[] { $"[{field.Name}]({cls.Name}/{pageFileName}.md)", typeName, fieldSummary });
        }

        // Emit grouped sub-tables in the canonical order: Constructors, Methods, Fields.
        // Each section is only emitted when at least one member of that kind is present.
        if (ctorRows.Count > 0)
        {
            writer.WriteHeading(3, "Constructors");
            writer.WriteTable(new[] { "Constructor", DescriptionColumnHeader }, ctorRows);
        }

        if (methodRows.Count > 0)
        {
            writer.WriteHeading(3, "Methods");
            writer.WriteTable(new[] { "Member", "Returns", DescriptionColumnHeader }, methodRows);
        }

        if (fieldRows.Count > 0)
        {
            writer.WriteHeading(3, "Fields");
            writer.WriteTable(new[] { "Member", "Type", DescriptionColumnHeader }, fieldRows);
        }

        // Emit Operators section when the class has operator overloads — all operators share
        // a single page to prevent file-name collisions between operator+, operator-, etc.
        if (operatorMethods.Count > 0)
        {
            WriteClassOperatorsPage(factory, nsKey, nsDisplayName, cls, operatorMethods);
            writer.WriteHeading(3, "Operators");
            writer.WriteTable(
                new[] { "Operators", DescriptionColumnHeader },
                new[] { new[] { $"[operators]({cls.Name}/operators.md)", "Operator overloads" } });
        }
    }

    /// <summary>
    ///     Writes the combined operator overloads page for a class, placing all operator
    ///     methods onto a single <c>operators.md</c> page to prevent file-name collisions.
    /// </summary>
    /// <remarks>
    ///     Operator names such as <c>operator+</c>, <c>operator-</c>, and <c>operator*</c>
    ///     all sanitize to the same file name when individual pages are used. Grouping them
    ///     onto a single deterministic page resolves the collision and makes the operators
    ///     page a stable navigation target for both human readers and AI agents.
    /// </remarks>
    /// <param name="factory">Factory for creating the output writer.</param>
    /// <param name="nsKey">The namespace key used as the parent folder for this type's directory.</param>
    /// <param name="nsDisplayName">
    ///     The C++ qualified namespace name forwarded to <see cref="WriteFunctionContent"/>
    ///     so it can emit fully-qualified signature comments.
    /// </param>
    /// <param name="cls">The declaring class whose operator overloads are being documented.</param>
    /// <param name="operators">
    ///     The ordered list of operator methods to document. All elements must have names
    ///     starting with <c>"operator"</c>. Must contain at least one element.
    /// </param>
    private void WriteClassOperatorsPage(
        IMarkdownWriterFactory factory,
        string nsKey,
        string nsDisplayName,
        CppClass cls,
        IReadOnlyList<CppFunction> operators)
    {
        using var writer = factory.CreateMarkdown($"{nsKey}/{cls.Name}", "operators");
        writer.WriteHeading(1, "operators");

        // Emit the qualified class name comment and #include directive from the first operator
        // that has source location information — gives readers context without browsing headers
        var qualifiedClassName = string.IsNullOrEmpty(nsDisplayName)
            ? cls.Name
            : $"{nsDisplayName}::{cls.Name}";
        var firstWithLocation = operators.FirstOrDefault(op => op.Location != null);
        if (firstWithLocation != null)
        {
            var includePath = GetIncludePath(firstWithLocation.Location!.File);
            writer.WriteSignature("cpp", $"// {qualifiedClassName}\n#include <{includePath}>");
        }

        writer.WriteParagraph($"Operator overloads for {cls.Name}.");

        // Emit an H2 section for each operator so readers can locate a specific overload quickly
        foreach (var op in operators)
        {
            var paramTypes = string.Join(", ", op.Parameters.Select(p => SimplifyTypeName(p.TypeName)));
            writer.WriteHeading(2, $"{op.Name}({paramTypes})");
            WriteFunctionContent(writer, nsDisplayName, cls.Name, op);
        }
    }

    /// <summary>
    ///     Writes the combined operator overloads page for a namespace, placing all
    ///     namespace-level operator free functions onto a single <c>operators.md</c> page
    ///     to prevent file-name collisions.
    /// </summary>
    /// <remarks>
    ///     Namespace-level operators such as <c>operator&lt;&lt;</c>, <c>operator+</c>, and
    ///     <c>operator-</c> all sanitize to the same file name when individual pages are used.
    ///     Grouping them onto a single deterministic page resolves the collision and makes the
    ///     operators page a stable navigation target for both human readers and AI agents.
    /// </remarks>
    /// <param name="factory">Factory for creating the output writer.</param>
    /// <param name="nsKey">The file-path-compatible namespace key used as the output folder.</param>
    /// <param name="nsDisplayName">
    ///     The C++ qualified namespace name used to build fully-qualified function names
    ///     shown in signature comments. Pass an empty string for the global namespace.
    /// </param>
    /// <param name="operators">
    ///     The ordered list of namespace-level operator free functions to document. All
    ///     elements must have names starting with <c>"operator"</c>. Must contain at least
    ///     one element.
    /// </param>
    private void WriteNamespaceOperatorsPage(
        IMarkdownWriterFactory factory,
        string nsKey,
        string nsDisplayName,
        IReadOnlyList<CppFunction> operators)
    {
        using var writer = factory.CreateMarkdown(nsKey, "operators");
        writer.WriteHeading(1, "operators");

        // Emit the qualified name comment and #include directive from the first operator that
        // has source location information so readers know which header to include
        var firstWithLocation = operators.FirstOrDefault(op => op.Location != null);
        if (firstWithLocation != null)
        {
            var qualifiedName = string.IsNullOrEmpty(nsDisplayName)
                ? firstWithLocation.Name
                : $"{nsDisplayName}::{firstWithLocation.Name}";
            var includePath = GetIncludePath(firstWithLocation.Location!.File);
            writer.WriteSignature("cpp", $"// {qualifiedName}\n#include <{includePath}>");
        }

        var displayNs = string.IsNullOrEmpty(nsDisplayName) ? GlobalNamespaceKey : nsDisplayName;
        writer.WriteParagraph($"Operator overloads in the {displayNs} namespace.");

        // Emit an H2 section for each operator so readers can locate a specific overload quickly
        foreach (var op in operators)
        {
            var paramTypes = string.Join(", ", op.Parameters.Select(p => SimplifyTypeName(p.TypeName)));
            writer.WriteHeading(2, $"{op.Name}({paramTypes})");
            WriteFreeFunctionContent(writer, nsDisplayName, op);
        }
    }

    /// <summary>
    ///     Writes the detail page for a single namespace-level free function.
    /// </summary>
    /// <remarks>
    ///     Free functions are not members of any class, so the page is placed directly
    ///     under the namespace key folder rather than inside a per-type subdirectory.
    ///     The layout mirrors the class member page but uses only the function name as
    ///     both the folder-relative file name and the heading.
    /// </remarks>
    /// <param name="factory">Factory for creating the output writer.</param>
    /// <param name="nsKey">The namespace key used as the subfolder for this function's page.</param>
    /// <param name="nsDisplayName">
    ///     The C++ qualified namespace name used to build the fully-qualified function
    ///     name shown as the first line of the signature comment.
    /// </param>
    /// <param name="fn">The free function declaration to document.</param>
    private void WriteFreeFunctionPage(
        IMarkdownWriterFactory factory,
        string nsKey,
        string nsDisplayName,
        CppFunction fn)
    {
        using var writer = factory.CreateMarkdown(nsKey, SanitizeFileName(fn.Name));
        writer.WriteHeading(1, fn.Name);
        WriteFreeFunctionContent(writer, nsDisplayName, fn);
    }

    /// <summary>
    ///     Writes the body content for a namespace-level free function page without the
    ///     heading, including the fully-qualified signature comment, C++ signature, summary,
    ///     parameter table, and return type. Shared by both individual function pages and
    ///     the combined namespace operators page.
    /// </summary>
    /// <remarks>
    ///     Separated from <see cref="WriteFreeFunctionPage"/> so that
    ///     <see cref="WriteNamespaceOperatorsPage"/> can write an operator heading of its
    ///     own before delegating to this method for the content body, mirroring the pattern
    ///     of <see cref="WriteFunctionContent"/> being separated from
    ///     <see cref="WriteFunctionPage"/> for class members.
    /// </remarks>
    /// <param name="writer">The Markdown writer to emit content into.</param>
    /// <param name="nsDisplayName">
    ///     The C++ qualified namespace name used to build the fully-qualified function name
    ///     emitted as the first comment line in the signature block. Pass an empty string
    ///     for global-namespace functions.
    /// </param>
    /// <param name="fn">The free function declaration to document.</param>
    private void WriteFreeFunctionContent(
        IMarkdownWriter writer,
        string nsDisplayName,
        CppFunction fn)
    {
        // Emit the fully-qualified name as a comment followed by the optional #include
        // directive and C++ signature so that an AI reader has all context needed to
        // use the function without browsing the header tree
        var qualifiedName = string.IsNullOrEmpty(nsDisplayName)
            ? fn.Name
            : $"{nsDisplayName}::{fn.Name}";
        var signature = BuildMethodSignature(fn);
        if (fn.Location != null)
        {
            var includePath = GetIncludePath(fn.Location.File);
            writer.WriteSignature("cpp", $"// {qualifiedName}\n#include <{includePath}>\n{signature}");
        }
        else
        {
            writer.WriteSignature("cpp", $"// {qualifiedName}\n{signature}");
        }

        // Emit summary from doc comment or placeholder when no comment is present
        var summary = GetSummary(fn.Doc);
        writer.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : NoDescriptionPlaceholder);

        // Emit extended details when the doc comment contains a @details or @remarks block
        var details = GetDetails(fn.Doc);
        if (!string.IsNullOrEmpty(details))
        {
            writer.WriteParagraph(details);
        }

        // Emit parameter table when the function has at least one parameter
        if (fn.Parameters.Count > 0)
        {
            writer.WriteHeading(4, "Parameters");
            var paramHeaders = new[] { "Parameter", "Type", DescriptionColumnHeader };
            var paramRows = fn.Parameters.Select(p =>
                new[] { p.Name, SimplifyTypeName(p.TypeName), GetParamDescription(fn.Doc, p.Name) ?? string.Empty });
            writer.WriteTable(paramHeaders, paramRows);
        }

        // Emit return description when the function is not void
        var returnTypeName = SimplifyTypeName(fn.ReturnTypeName);
        if (!string.Equals(returnTypeName, "void", StringComparison.Ordinal))
        {
            writer.WriteHeading(4, "Returns");
            var returnDescription = GetReturnDescription(fn.Doc);
            writer.WriteParagraph(!string.IsNullOrEmpty(returnDescription) ? returnDescription : returnTypeName);
        }
    }

    /// <summary>
    ///     Writes the detail page for a single class member (method or field).
    /// </summary>
    /// <param name="factory">Factory for creating the output writer.</param>
    /// <param name="nsKey">The namespace key used as the parent folder for this type's directory.</param>
    /// <param name="nsDisplayName">
    ///     The C++ qualified namespace name forwarded to the function-page writer so it
    ///     can emit a fully-qualified signature comment.
    /// </param>
    /// <param name="cls">The declaring class.</param>
    /// <param name="member">
    ///     The member declaration to document. Must be a <see cref="CppFunction"/> (method)
    ///     or a <see cref="CppField"/>.
    /// </param>
    /// <param name="fileName">The unique file name (without extension) allocated for this member.</param>
    private static void WriteMemberPage(
        IMarkdownWriterFactory factory,
        string nsKey,
        string nsDisplayName,
        CppClass cls,
        object member,
        string fileName)
    {
        using var memberWriter = factory.CreateMarkdown($"{nsKey}/{cls.Name}", fileName);

        // Dispatch to the appropriate page writer based on the concrete member type
        switch (member)
        {
            case CppFunction method:
                WriteFunctionPage(memberWriter, nsDisplayName, cls.Name, method);
                break;

            case CppField field:
                WriteFieldPage(memberWriter, nsDisplayName, cls.Name, field);
                break;
        }
    }

    /// <summary>
    ///     Writes the detail content for a method member page, including the fully-qualified
    ///     signature comment, C++ signature, summary, parameter table, and return type.
    /// </summary>
    /// <param name="writer">The Markdown writer for this page.</param>
    /// <param name="nsDisplayName">
    ///     The C++ qualified namespace name used to build the fully-qualified member name
    ///     emitted as the first comment line in the signature block.
    /// </param>
    /// <param name="className">The declaring class name, used in the page heading.</param>
    /// <param name="method">The method declaration to document.</param>
    private static void WriteFunctionPage(
        IMarkdownWriter writer,
        string nsDisplayName,
        string className,
        CppFunction method)
    {
        writer.WriteHeading(3, $"{className}.{method.Name}");
        WriteFunctionContent(writer, nsDisplayName, className, method);
    }

    /// <summary>
    ///     Writes the body content for a method member page without the heading, including
    ///     the fully-qualified signature comment, C++ signature, summary, parameter table,
    ///     and return type. Shared by both individual member pages and combined union pages.
    /// </summary>
    /// <remarks>
    ///     Separated from <see cref="WriteFunctionPage"/> so that combined pages can write an
    ///     H4 heading of their own before delegating to this method for the content body.
    /// </remarks>
    /// <param name="writer">The Markdown writer to emit content into.</param>
    /// <param name="nsDisplayName">
    ///     The C++ qualified namespace name used to build the fully-qualified member name
    ///     emitted as the first comment line in the signature block.
    /// </param>
    /// <param name="className">The declaring class name.</param>
    /// <param name="method">The method declaration to document.</param>
    private static void WriteFunctionContent(
        IMarkdownWriter writer,
        string nsDisplayName,
        string className,
        CppFunction method)
    {
        // Emit the fully-qualified name as a comment followed by the C++ signature so that
        // an AI reader has the namespace and class context needed to call the member correctly
        var qualifiedName = string.IsNullOrEmpty(nsDisplayName)
            ? $"{className}::{method.Name}"
            : $"{nsDisplayName}::{className}::{method.Name}";
        var signature = BuildMethodSignature(method);
        writer.WriteSignature("cpp", $"// {qualifiedName}\n{signature}");

        // Emit summary from doc comment or placeholder
        var summary = GetSummary(method.Doc);
        writer.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : NoDescriptionPlaceholder);

        // Emit extended details when the doc comment contains a @details or @remarks block
        var details = GetDetails(method.Doc);
        if (!string.IsNullOrEmpty(details))
        {
            writer.WriteParagraph(details);
        }

        // Emit parameter table when the method has at least one parameter
        if (method.Parameters.Count > 0)
        {
            writer.WriteHeading(4, "Parameters");
            var paramHeaders = new[] { "Parameter", "Type", DescriptionColumnHeader };
            var paramRows = method.Parameters.Select(p =>
                new[] { p.Name, SimplifyTypeName(p.TypeName), GetParamDescription(method.Doc, p.Name) ?? string.Empty });
            writer.WriteTable(paramHeaders, paramRows);
        }

        // Emit return description when the method is not void and is not a constructor;
        // constructors have no return type so the return section would be meaningless
        if (!method.IsConstructor)
        {
            var returnTypeName = SimplifyTypeName(method.ReturnTypeName);
            if (!string.Equals(returnTypeName, "void", StringComparison.Ordinal))
            {
                writer.WriteHeading(4, "Returns");
                var returnDescription = GetReturnDescription(method.Doc);
                writer.WriteParagraph(!string.IsNullOrEmpty(returnDescription) ? returnDescription : returnTypeName);
            }
        }
    }

    /// <summary>
    ///     Writes the detail content for a field member page, including the fully-qualified
    ///     name comment, C++ declaration as a signature, and the doc comment summary.
    /// </summary>
    /// <param name="writer">The Markdown writer for this page.</param>
    /// <param name="nsDisplayName">
    ///     The C++ qualified namespace name forwarded to <see cref="WriteFieldContent"/> so it
    ///     can emit a fully-qualified signature comment. Pass an empty string for global-namespace fields.
    /// </param>
    /// <param name="className">The declaring class name, used in the page heading.</param>
    /// <param name="field">The field declaration to document.</param>
    private static void WriteFieldPage(
        IMarkdownWriter writer,
        string nsDisplayName,
        string className,
        CppField field)
    {
        writer.WriteHeading(3, $"{className}.{field.Name}");
        WriteFieldContent(writer, nsDisplayName, className, field);
    }

    /// <summary>
    ///     Writes the body content for a field member page without the heading, including the
    ///     fully-qualified name comment, C++ declaration as a fenced code block, and the doc
    ///     comment summary and extended details. Shared by both individual member pages and
    ///     combined union pages.
    /// </summary>
    /// <remarks>
    ///     Separated from <see cref="WriteFieldPage"/> so that combined pages can write an
    ///     H4 heading of their own before delegating to this method for the content body.
    /// </remarks>
    /// <param name="writer">The Markdown writer to emit content into.</param>
    /// <param name="nsDisplayName">
    ///     The C++ qualified namespace name used to build the fully-qualified member name
    ///     emitted as the first comment line in the signature block. Pass an empty string for
    ///     global-namespace fields.
    /// </param>
    /// <param name="className">The declaring class name used to build the qualified name.</param>
    /// <param name="field">The field declaration to document.</param>
    private static void WriteFieldContent(
        IMarkdownWriter writer,
        string nsDisplayName,
        string className,
        CppField field)
    {
        // Build the fully-qualified name comment so readers know the exact symbol to reference
        var qualifiedName = string.IsNullOrEmpty(nsDisplayName)
            ? $"{className}::{field.Name}"
            : $"{nsDisplayName}::{className}::{field.Name}";
        var signature = $"{SimplifyTypeName(field.TypeName)} {field.Name};";
        writer.WriteSignature("cpp", $"// {qualifiedName}\n{signature}");

        // Emit summary from doc comment or placeholder
        var summary = GetSummary(field.Doc);
        writer.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : NoDescriptionPlaceholder);

        // Emit extended details when the doc comment contains a @details or @remarks block
        var details = GetDetails(field.Doc);
        if (!string.IsNullOrEmpty(details))
        {
            writer.WriteParagraph(details);
        }
    }

    /// <summary>
    ///     Writes the detail page for a single C++ enum, including the qualified name,
    ///     a summary, and a table of all enum values with their descriptions.
    /// </summary>
    /// <param name="factory">Factory for creating the output writer.</param>
    /// <param name="nsKey">The namespace key used as the subfolder for this enum's page.</param>
    /// <param name="nsDisplayName">
    ///     The C++ qualified namespace name used to build the fully-qualified enum name
    ///     shown in the signature comment.
    /// </param>
    /// <param name="cppEnum">The C++ enum to document.</param>
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
            var includePath = GetIncludePath(cppEnum.Location.File);
            writer.WriteSignature("cpp", $"// {qualifiedName}\n#include <{includePath}>");
        }
        else
        {
            writer.WriteSignature("cpp", $"// {qualifiedName}");
        }

        // Emit summary from doc comment or placeholder
        var enumSummary = GetSummary(cppEnum.Doc);
        writer.WriteParagraph(!string.IsNullOrEmpty(enumSummary) ? enumSummary : NoDescriptionPlaceholder);

        // Emit extended details when the doc comment contains a @details or @remarks block
        var enumDetails = GetDetails(cppEnum.Doc);
        if (!string.IsNullOrEmpty(enumDetails))
        {
            writer.WriteParagraph(enumDetails);
        }

        // Emit a values table so readers can see all valid values and their meanings
        if (cppEnum.Values.Count > 0)
        {
            writer.WriteHeading(3, "Values");
            var headers = new[] { "Value", DescriptionColumnHeader };
            var rows = cppEnum.Values.Select(item =>
            {
                var itemSummary = GetSummary(item.Doc) ?? NoDescriptionPlaceholder;
                return new[] { item.Name, itemSummary };
            });
            writer.WriteTable(headers, rows);
        }
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
    private IEnumerable<CppFunction> GetVisibleConstructors(CppClass cls)
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
    private IEnumerable<CppFunction> GetVisibleMethods(CppClass cls)
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
    private IEnumerable<CppField> GetVisibleFields(CppClass cls)
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
    private bool IsVisibleMember(CppAccessibility accessibility)
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
    private static string? GetSummary(CppDocComment? doc) => doc?.Summary;

    /// <summary>
    ///     Returns the extended details text from a <see cref="CppDocComment"/>.
    /// </summary>
    /// <param name="doc">The doc comment to inspect. May be null.</param>
    /// <returns>The details string, or <see langword="null"/> when absent.</returns>
    private static string? GetDetails(CppDocComment? doc) => doc?.Details;

    /// <summary>
    ///     Looks up the description for a named parameter in a <see cref="CppDocComment"/>.
    /// </summary>
    /// <param name="doc">The doc comment containing the <c>@param</c> entries. May be null.</param>
    /// <param name="paramName">The exact parameter name to look up.</param>
    /// <returns>The trimmed description, or <see langword="null"/> when no matching entry exists.</returns>
    private static string? GetParamDescription(CppDocComment? doc, string paramName)
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
    private static string? GetReturnDescription(CppDocComment? doc) => doc?.Returns;

    /// <summary>
    ///     Derives a one-line description for a namespace from its doc comment, used as the
    ///     description in <c>api.md</c>.
    /// </summary>
    /// <param name="nsDecls">The namespace declarations to inspect.</param>
    /// <returns>A short description, or <see cref="NoDescriptionPlaceholder"/> when none is found.</returns>
    private static string GetNamespaceDescription(NamespaceDeclarations nsDecls)
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
    private static string BuildMethodSignature(CppFunction fn)
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
            .Select(p => $"{SimplifyTypeName(p.TypeName)} {p.Name}")
            .ToList();
        if (fn.IsVariadic)
        {
            paramParts.Add("...");
        }

        sb.Append(string.Join(", ", paramParts));
        sb.Append(')');

        return sb.ToString();
    }

    /// <summary>
    ///     Builds the class declaration line for a <see cref="CppClass"/> for display in the
    ///     signature block of its type page, appending <c>final</c> when the class is marked
    ///     final and base class names when inheritance is present.
    /// </summary>
    /// <remarks>
    ///     Called only when <see cref="CppClass.IsFinal"/> is true so that the declaration
    ///     fragment makes the <c>final</c> constraint immediately visible to readers without
    ///     them needing to open the header file.
    /// </remarks>
    /// <param name="cls">The C++ class to produce a declaration line for.</param>
    /// <returns>
    ///     A C++ declaration string such as <c>class FinalClass final</c> or
    ///     <c>class FinalClass final : public Shape</c>.
    /// </returns>
    private static string BuildClassDeclaration(CppClass cls)
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
    private static string BuildTemplateParamDisplay(CppClass cls)
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
    private static string BuildTemplateDeclaration(CppClass cls)
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
    private static string SimplifyTypeName(string typeName)
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
    ///     All colliding members are documented together under H4 sub-headings that show
    ///     both the exact display name and the member kind (e.g., <c>Name (Method)</c>).
    /// </remarks>
    /// <param name="factory">Factory for creating the output writer.</param>
    /// <param name="nsKey">The namespace key used as the parent folder for this type's directory.</param>
    /// <param name="nsDisplayName">
    ///     The C++ qualified namespace name forwarded to <see cref="WriteFunctionContent"/>
    ///     so it can emit fully-qualified signature comments.
    /// </param>
    /// <param name="cls">The declaring class.</param>
    /// <param name="lowerKey">
    ///     The shared lowercase file name key. Used as both the page file name and the H3
    ///     page heading so the combined page has a stable, predictable address.
    /// </param>
    /// <param name="members">
    ///     The ordered list of members whose base names collide on case-insensitive file
    ///     systems. Elements must be <see cref="CppFunction"/> or <see cref="CppField"/>.
    ///     Must contain at least two elements.
    /// </param>
    private static void WriteCombinedMemberPage(
        IMarkdownWriterFactory factory,
        string nsKey,
        string nsDisplayName,
        CppClass cls,
        string lowerKey,
        IReadOnlyList<object> members)
    {
        using var writer = factory.CreateMarkdown($"{nsKey}/{cls.Name}", lowerKey);

        // The shared lowercase key serves as the page heading so every member in the group
        // can be found at the same predictable path regardless of filesystem case-sensitivity
        writer.WriteHeading(3, lowerKey);

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
                    writer.WriteHeading(4, $"{fn.Name}({fnParamTypes}) ({fnKind})");
                    WriteFunctionContent(writer, nsDisplayName, cls.Name, fn);
                    break;

                case CppField field:
                    writer.WriteHeading(4, $"{field.Name} (Field)");
                    WriteFieldContent(writer, nsDisplayName, cls.Name, field);
                    break;
            }
        }
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
    private static string GetMemberBaseName(object member, string className) => member switch
    {
        CppFunction fn when fn.IsConstructor => className,
        CppFunction fn => fn.Name,
        CppField field => field.Name,
        _ => className,
    };

    // =========================================================================
    // Inner data model
    // =========================================================================

    /// <summary>
    ///     Accumulates the owned C++ declarations that belong to a single namespace,
    ///     ready for Markdown output.
    /// </summary>
    private sealed class NamespaceDeclarations
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

        /// <summary>Gets the list of owned free functions declared in this namespace.</summary>
        public List<CppFunction> FreeFunctions { get; } = [];
    }
}
