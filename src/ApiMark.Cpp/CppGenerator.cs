using ApiMark.Core;
using ApiMark.Cpp.CppAst;

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
    /// <summary>Configuration controlling which headers, roots, visibility, and other parse options to apply.</summary>
    private readonly CppGeneratorOptions _options;

    /// <summary>
    ///     Initializes a new instance of <see cref="CppGenerator"/> with the specified options.
    /// </summary>
    /// <remarks>
    ///     No file system access or parsing occurs at construction time; all I/O is deferred to
    ///     <see cref="Parse"/>.
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
    ///     Parses the configured C++ library headers and returns an emitter ready to produce
    ///     Markdown documentation in the requested format.
    /// </summary>
    /// <remarks>
    ///     Execution steps:
    ///     <list type="number">
    ///       <item>Enumerate candidate header files under each <see cref="CppGeneratorOptions.PublicIncludeRoots"/> entry.</item>
    ///       <item>Run clang with <c>-ast-dump=json</c> on all candidate headers via <see cref="ClangAstParser"/>.</item>
    ///       <item>Log any clang diagnostic errors from system headers via the context output channel.</item>
    ///       <item>Walk the parsed namespaces, applying the ownership and visibility filters.</item>
    ///     </list>
    ///     The caller must subsequently invoke <see cref="IApiEmitter.Emit"/> to write output.
    /// </remarks>
    /// <param name="context">
    ///     Output channel for informational messages. Must not be null. System-header diagnostic
    ///     messages from clang are emitted here via <see cref="IContext.WriteLine"/>.
    /// </param>
    /// <exception cref="DirectoryNotFoundException">
    ///     Thrown when <see cref="CppGeneratorOptions.ApiHeaderPatterns"/> is empty and a
    ///     path in <see cref="CppGeneratorOptions.PublicIncludeRoots"/> does not exist on disk.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when clang cannot be located or exits with an error and produces no JSON output.
    /// </exception>
    public IApiEmitter Parse(IContext context)
    {
        // Collect candidate header files from all configured public include roots
        var headerFiles = CollectHeaderFiles();

        // Run clang -ast-dump=json on all headers and parse the resulting AST
        var result = ClangAstParser.Parse(headerFiles, _options);

        // Throw on errors from the user's public headers; log errors from system headers
        CheckForErrors(result, headerFiles, context);

        // Walk parsed namespaces and group owned declarations by their qualified namespace key
        var namespaceDecls = new SortedDictionary<string, CppEmitter.NamespaceDeclarations>(StringComparer.Ordinal);
        foreach (var ns in result.Namespaces)
        {
            CollectResultNamespace(ns, namespaceDecls);
        }

        // Build the intra-library type map for link resolution.
        // nsKey uses "." separators for file paths; display names use "::" for C++ qualified names.
        // FlattenClass recursively registers a class, its scoped type aliases, and any nested
        // classes so that fully-qualified names like "fixtures::Outer::Inner" resolve correctly.
        static IEnumerable<(string Key, string Value)> FlattenClass(string nsDisplay, string nsPath, CppClass cls)
        {
            var clsDisplay = $"{nsDisplay}::{cls.Name}";
            var clsPath = $"{nsPath}/{cls.Name}";
            return new[] { (Key: clsDisplay, Value: clsPath) }
                .Concat(cls.TypeAliases.Select(alias =>
                    (Key: $"{clsDisplay}::{alias.Name}", Value: $"{clsPath}/{alias.Name}")))
                .Concat(cls.NestedClasses.SelectMany(nested =>
                    FlattenClass(clsDisplay, clsPath, nested)));
        }

        var knownTypes = namespaceDecls.SelectMany(kv =>
        {
            var nsDisplay = kv.Key.Replace(".", "::", StringComparison.Ordinal);
            var nsPath = kv.Key; // preserve dot-separated key to match CreateMarkdown page keys
            return kv.Value.Classes.SelectMany(cls => FlattenClass(nsDisplay, nsPath, cls))
                .Concat(kv.Value.Enums.Select(enm => (Key: $"{nsDisplay}::{enm.Name}", Value: $"{nsPath}/{enm.Name}")))
                .Concat(kv.Value.TypeAliases.Select(a => (Key: $"{nsDisplay}::{a.Name}", Value: $"{nsPath}/{a.Name}")));
        }).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);

        var cppResolver = new CppTypeLinkResolver(knownTypes);

        return new CppEmitter(_options, namespaceDecls, cppResolver);
    }

    // =========================================================================
    // Header file collection
    // =========================================================================

    /// <summary>
    ///     Enumerates candidate header files using <see cref="GlobFileCollector"/> and returns
    ///     a sorted, deduplicated list ready for clang processing.
    /// </summary>
    /// <returns>
    ///     A sorted, deduplicated list of absolute header file paths with recognized C++
    ///     header extensions (<c>.h</c>, <c>.hpp</c>, <c>.hxx</c>, <c>.h++</c>) selected
    ///     by the configured pattern list.
    /// </returns>
    /// <remarks>
    ///     <para>
    ///         When <see cref="CppGeneratorOptions.ApiHeaderPatterns"/> is empty, each
    ///         <see cref="CppGeneratorOptions.PublicIncludeRoots"/> entry is validated to
    ///         exist and a <c>/**/*</c> pattern is synthesized for it. The bare-star final
    ///         segment triggers extension inference in <see cref="GlobFileCollector"/>,
    ///         which restricts results to files with recognized C++ header extensions.
    ///     </para>
    ///     <para>
    ///         When patterns are provided, absolute patterns are forwarded to
    ///         <see cref="GlobFileCollector"/> unchanged. Relative patterns are expanded
    ///         against each <see cref="CppGeneratorOptions.PublicIncludeRoots"/> entry so
    ///         that callers can write root-agnostic patterns such as <c>**/MyHeader.h</c>
    ///         and have them resolved under every configured include root.
    ///     </para>
    /// </remarks>
    /// <exception cref="DirectoryNotFoundException">
    ///     Thrown when <see cref="CppGeneratorOptions.ApiHeaderPatterns"/> is empty and a
    ///     configured public include root does not exist on disk.
    /// </exception>
    private List<string> CollectHeaderFiles()
    {
        var headerExtensions = new[] { ".h", ".hpp", ".hxx", ".h++" };
        var cwd = Path.GetFullPath(Directory.GetCurrentDirectory());

        List<string> patterns;

        if (_options.ApiHeaderPatterns.Count == 0)
        {
            // Default mode: validate each root exists, then synthesize per-root wildcard patterns.
            // The bare-star final segment causes GlobFileCollector to filter by language extensions.
            var missingRoot = _options.PublicIncludeRoots.FirstOrDefault(r => !Directory.Exists(r));
            if (missingRoot is not null)
            {
                throw new DirectoryNotFoundException(
                    $"Public include root not found: '{missingRoot}'");
            }

            patterns = _options.PublicIncludeRoots
                .Select(r => Path.GetFullPath(r) + "/**/*")
                .ToList();
        }
        else
        {
            // Explicit patterns: forward absolute patterns unchanged; expand relative patterns
            // against each include root so root-agnostic globs like "**/MyHeader.h" resolve
            // correctly under every configured root without requiring callers to know the roots.
            patterns = ExpandExplicitPatterns();
        }

        return GlobFileCollector.Collect(patterns, headerExtensions, cwd).ToList();
    }

    /// <summary>
    ///     Expands the explicit <see cref="CppGeneratorOptions.ApiHeaderPatterns"/> into absolute
    ///     glob patterns ready for <see cref="GlobFileCollector.Collect"/>.
    /// </summary>
    /// <remarks>
    ///     Absolute patterns are forwarded unchanged. Relative patterns are resolved against
    ///     every entry in <see cref="CppGeneratorOptions.PublicIncludeRoots"/> so that
    ///     root-agnostic globs such as <c>**/MyHeader.h</c> match under all configured roots
    ///     without requiring callers to hard-code root paths. Exclusion prefixes (<c>!</c>)
    ///     are preserved on the expanded output entries.
    /// </remarks>
    /// <returns>
    ///     An ordered list of absolute-path glob patterns with exclusion prefixes preserved,
    ///     ready to pass directly to <see cref="GlobFileCollector.Collect"/>.
    /// </returns>
    private List<string> ExpandExplicitPatterns()
    {
        var patterns = new List<string>();
        foreach (var pattern in _options.ApiHeaderPatterns)
        {
            var isExclusion = pattern.StartsWith('!');
            var body = isExclusion ? pattern.Substring(1).Trim() : pattern.Trim();

            if (Path.IsPathRooted(body))
            {
                // Absolute pattern — pass through unchanged
                patterns.Add(pattern);
            }
            else
            {
                // Relative pattern — expand against each include root so callers can write
                // root-agnostic patterns without knowing the configured include paths
                patterns.AddRange(
                    _options.PublicIncludeRoots.Select(root =>
                    {
                        var expanded = Path.Join(Path.GetFullPath(root), body);
                        return isExclusion ? "!" + expanded : expanded;
                    }));
            }
        }

        return patterns;
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
    internal static void CheckForErrors(CppCompilationResult result, IReadOnlyList<string> headerFiles, IContext context)
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
    ///     <see cref="CppEmitter.NamespaceDeclarations"/> accumulator, applying the configured
    ///     visibility and deprecated filters.
    /// </summary>
    /// <param name="ns">The namespace declaration to process.</param>
    /// <param name="result">Dictionary that accumulates declarations grouped by namespace key.</param>
    private void CollectResultNamespace(
        CppNamespaceDecl ns,
        SortedDictionary<string, CppEmitter.NamespaceDeclarations> result)
    {
        // Derive the file-path-compatible key (:: -> .) and the display name
        var qualName = ns.QualifiedName;
        var nsKey = string.IsNullOrEmpty(qualName)
            ? CppEmitter.GlobalNamespaceKey
            : qualName.Replace("::", ".", StringComparison.Ordinal);
        var displayName = string.IsNullOrEmpty(qualName) ? CppEmitter.GlobalNamespaceKey : qualName;

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

        // Collect owned type aliases, applying the deprecated filter
        foreach (var alias in ns.TypeAliases)
        {
            if (!_options.IncludeDeprecated && alias.IsDeprecated)
            {
                continue;
            }

            EnsureNamespace(result, nsKey, displayName, ns.Doc);
            result[nsKey].TypeAliases.Add(alias);
        }
    }

    /// <summary>
    ///     Ensures a <see cref="CppEmitter.NamespaceDeclarations"/> entry exists in the result dictionary,
    ///     creating a new one with the supplied display name when the key is absent.
    /// </summary>
    /// <param name="result">The dictionary to update in-place.</param>
    /// <param name="key">The file-path-compatible namespace key.</param>
    /// <param name="displayName">The C++ qualified namespace name used as the Markdown page heading.</param>
    /// <param name="doc">Optional doc comment from the namespace declaration.</param>
    private static void EnsureNamespace(
        SortedDictionary<string, CppEmitter.NamespaceDeclarations> result,
        string key,
        string displayName,
        CppDocComment? doc = null)
    {
        if (!result.ContainsKey(key))
        {
            result[key] = new CppEmitter.NamespaceDeclarations(displayName, doc);
        }
    }
}
