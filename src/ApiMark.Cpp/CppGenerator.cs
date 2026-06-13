using ApiMark.Core;
using ApiMark.Cpp.CppAst;
using Microsoft.Extensions.FileSystemGlobbing;

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
    ///     Thrown when a path in <see cref="CppGeneratorOptions.PublicIncludeRoots"/> does not exist on disk.
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
    ///     Enumerates candidate header files under each configured public include root,
    ///     applying <see cref="CppGeneratorOptions.ApiHeaderPatterns"/> with gitignore-style
    ///     semantics to determine which headers appear in the generated documentation.
    /// </summary>
    /// <returns>
    ///     A list of absolute header file paths with recognized C++ header extensions
    ///     (<c>.h</c>, <c>.hpp</c>, <c>.hxx</c>, <c>.h++</c>) that are selected for
    ///     documentation by the configured pattern list.
    /// </returns>
    /// <remarks>
    ///     <para>
    ///         When <see cref="CppGeneratorOptions.ApiHeaderPatterns"/> is empty, pattern
    ///         matching is bypassed and all files with recognized C++ header extensions
    ///         under each root are included.
    ///     </para>
    ///     <para>
    ///         Gitignore-style evaluation: for each candidate file, start with
    ///         <c>included = false</c> and walk the pattern list in order. If a pattern
    ///         starts with <c>!</c>, strip the prefix and if the file matches set
    ///         <c>included = false</c>; otherwise if the file matches set
    ///         <c>included = true</c>. The final value of <c>included</c> determines
    ///         whether the header is forwarded to clang. This allows include/exclude/re-include
    ///         sequences that are not possible with separate include and exclude lists.
    ///     </para>
    ///     <para>
    ///         All patterns (both caller-supplied and the per-root defaults) are evaluated
    ///         relative to the current working directory so that paths like
    ///         <c>"src/include/**"</c> are unambiguous across multiple <c>--includes</c>
    ///         roots. Pattern compilation is delegated to <see cref="CompileHeaderPatterns"/>;
    ///         per-root file enumeration and matching is delegated to <see cref="CollectMatchingFiles"/>.
    ///     </para>
    /// </remarks>
    /// <exception cref="DirectoryNotFoundException">
    ///     Thrown when a configured public include root does not exist on disk.
    /// </exception>
    private List<string> CollectHeaderFiles()
    {
        var headerExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".h", ".hpp", ".hxx", ".h++",
        };

        var cwdAbsolute = Path.GetFullPath(Directory.GetCurrentDirectory());
        var compiledPatterns = CompileHeaderPatterns();

        var headers = new List<string>();
        foreach (var root in _options.PublicIncludeRoots)
        {
            headers.AddRange(CollectMatchingFiles(root, headerExtensions, compiledPatterns, cwdAbsolute));
        }

        // De-duplicate and sort to produce a stable, deterministic header list.
        // Overlapping PublicIncludeRoots can otherwise produce duplicate entries that
        // cause declarations to appear multiple times in the generated output.
        return headers
            .Select(Path.GetFullPath)
            .Distinct(CppEmitter.FileSystemPathComparer)
            .OrderBy(h => h, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    ///     Compiles the configured API header patterns into a list of
    ///     (isExclusion, compiledMatcher) pairs that can be reused for every file evaluation.
    /// </summary>
    /// <remarks>
    ///     Empty exclusion globs (those whose pattern body is whitespace after stripping the
    ///     leading <c>!</c>) are dropped because they would match nothing and only add overhead.
    ///     Matchers are precompiled here so the per-file evaluation loop does not construct
    ///     short-lived <see cref="Matcher"/> objects on every (file × pattern) pair.
    /// </remarks>
    /// <returns>
    ///     An ordered list of (IsExclusion, Matcher) pairs in the same order as
    ///     <see cref="CppGeneratorOptions.ApiHeaderPatterns"/>; exclusion entries have
    ///     <c>IsExclusion = true</c>.
    /// </returns>
    private List<(bool IsExclusion, Matcher Matcher)> CompileHeaderPatterns()
    {
        var compiledPatterns = new List<(bool IsExclusion, Matcher Matcher)>();
        foreach (var pattern in _options.ApiHeaderPatterns)
        {
            if (pattern.StartsWith('!'))
            {
                var exclusionGlob = pattern.Substring(1).Trim();
                if (exclusionGlob.Length > 0)
                {
                    var m = new Matcher();
                    m.AddInclude(exclusionGlob);
                    compiledPatterns.Add((true, m));
                }
            }
            else
            {
                var m = new Matcher();
                m.AddInclude(pattern);
                compiledPatterns.Add((false, m));
            }
        }

        return compiledPatterns;
    }

    /// <summary>
    ///     Enumerates all recognized header files under <paramref name="root"/> and applies
    ///     the precompiled pattern list to select files that match at least one include
    ///     pattern and are not subsequently overridden by an exclusion pattern.
    /// </summary>
    /// <remarks>
    ///     When <paramref name="compiledPatterns"/> is empty every recognized header file
    ///     under <paramref name="root"/> is included (default behavior). Pattern evaluation
    ///     uses gitignore semantics: the last matching rule wins, and files start as excluded.
    ///     Paths are CWD-relative when the file is under the working directory; otherwise
    ///     root-relative paths are used to support absolute include roots and test environments.
    /// </remarks>
    /// <param name="root">The public include root directory to enumerate.</param>
    /// <param name="headerExtensions">The set of recognized header file extensions.</param>
    /// <param name="compiledPatterns">Precompiled inclusion and exclusion patterns.</param>
    /// <param name="cwdAbsolute">The normalized absolute path of the current working directory.</param>
    /// <returns>
    ///     A list of absolute file paths that passed pattern matching; may be empty when no
    ///     files under <paramref name="root"/> satisfy the configured patterns.
    /// </returns>
    /// <exception cref="DirectoryNotFoundException">
    ///     Thrown when <paramref name="root"/> does not exist on disk.
    /// </exception>
    private static List<string> CollectMatchingFiles(
        string root,
        HashSet<string> headerExtensions,
        List<(bool IsExclusion, Matcher Matcher)> compiledPatterns,
        string cwdAbsolute)
    {
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException(
                $"Public include root not found: '{root}'");
        }

        var rootAbsolute = Path.GetFullPath(root);

        var allFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => headerExtensions.Contains(Path.GetExtension(f)))
            .Select(Path.GetFullPath)
            .ToList();

        if (compiledPatterns.Count == 0)
        {
            return allFiles;
        }

        var result = new List<string>();
        foreach (var absoluteFile in allFiles)
        {
            var relFromCwd = Path.GetRelativePath(cwdAbsolute, absoluteFile).Replace('\\', '/');

            var relPath = IsOutsideCwd(relFromCwd)
                ? Path.GetRelativePath(rootAbsolute, absoluteFile).Replace('\\', '/')
                : relFromCwd;

            var included = false;

            foreach (var (isExclusion, matcher) in compiledPatterns)
            {
                if (matcher.Match(relPath).HasMatches)
                {
                    included = !isExclusion;
                }
            }

            if (included)
            {
                result.Add(absoluteFile);
            }
        }

        return result;
    }

    /// <summary>
    ///     Returns <see langword="true"/> when the path returned by
    ///     <see cref="Path.GetRelativePath"/> indicates the file lies outside the base directory.
    /// </summary>
    /// <remarks>
    ///     <see cref="Path.GetRelativePath"/> returns <c>".."</c> or a <c>"../"</c>-prefixed
    ///     path when the file is above the base, and returns the original absolute path unchanged
    ///     when the base and the file are on different drives (Windows). Both cases mean the file
    ///     is outside the base directory. Checking for the exact two-dot segment avoids
    ///     misclassifying filenames that legitimately start with two dots (e.g. <c>..hidden.h</c>).
    /// </remarks>
    /// <param name="relativeFromBase">The forward-slash-normalized relative path to test.</param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="relativeFromBase"/> indicates the file is
    ///     outside the base directory.
    /// </returns>
    private static bool IsOutsideCwd(string relativeFromBase) =>
        relativeFromBase == ".."
        || relativeFromBase.StartsWith("../", StringComparison.Ordinal)
        || Path.IsPathRooted(relativeFromBase);

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
