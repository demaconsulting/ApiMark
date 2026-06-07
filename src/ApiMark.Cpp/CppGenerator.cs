using System.Diagnostics;
using System.Runtime.InteropServices;
using ApiMark.Core;
using CppAst;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace ApiMark.Cpp;

/// <summary>Generates Markdown API documentation from C++ headers using CppAst.Net.</summary>
/// <remarks>
///     Implements <see cref="IApiGenerator"/> for C++ libraries. Parses public header files via
///     CppAst.Net (libclang), applies a file-provenance ownership filter based on
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
    ///       <item>Build CppAst parser options from all configured paths, defines, and compiler flags.</item>
    ///       <item>Parse all candidate headers via <see cref="CppParser.ParseFiles"/>.</item>
    ///       <item>Surface any Clang parse errors as an <see cref="InvalidOperationException"/>.</item>
    ///       <item>Walk the resulting AST, applying the ownership filter to each declaration.</item>
    ///       <item>Write the library entrypoint, namespace summaries, type pages, and member detail pages.</item>
    ///     </list>
    /// </remarks>
    /// <param name="factory">
    ///     Factory for creating per-file Markdown writers. Must not be null. The generator produces
    ///     the fixed entrypoint via <c>factory.CreateMarkdown("", "api")</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="factory"/> is null.</exception>
    /// <exception cref="DirectoryNotFoundException">
    ///     Thrown when a path in <see cref="CppGeneratorOptions.PublicIncludeRoots"/> does not exist on disk.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when CppAst reports parse errors in the public headers.
    /// </exception>
    public void Generate(IMarkdownWriterFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        // Collect candidate header files from all configured public include roots
        var headerFiles = CollectHeaderFiles();

        // Build CppAst parser options from configured paths, defines, and compiler flags
        var parserOptions = BuildParserOptions();

        // Parse all collected headers in a single CppAst invocation; this resolves cross-header
        // type references and produces a unified, fully-resolved AST
        var compilation = CppParser.ParseFiles(headerFiles, parserOptions);

        // Surface any parse errors before attempting to walk the AST, so callers receive a
        // clear failure rather than silently generating incomplete or incorrect documentation
        CheckForErrors(compilation);

        // Walk the AST and group owned declarations by their qualified namespace key
        var namespaceDecls = new SortedDictionary<string, NamespaceDeclarations>(StringComparer.Ordinal);
        CollectGlobalDeclarations(compilation, namespaceDecls);
        foreach (var ns in compilation.Namespaces)
        {
            CollectNamespaceDeclarations(ns, namespaceDecls);
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

            foreach (var fn in nsDecls.FreeFunctions)
            {
                WriteFreeFunctionPage(factory, nsKey, nsDecls.DisplayName, fn);
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
            // neither is set, all discovered headers are forwarded to CppAst without filtering
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
                // relative paths which are then converted back to absolute for CppAst
                var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(root)));
                var matchedAbsolute = new HashSet<string>(
                    result.Files.Select(f => Path.GetFullPath(Path.Combine(root, f.Path))),
                    StringComparer.OrdinalIgnoreCase);

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
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(h => h, StringComparer.Ordinal)
            .ToList();
    }

    // =========================================================================
    // Parser option construction
    // =========================================================================

    /// <summary>
    ///     Builds a <see cref="CppParserOptions"/> instance from the configured generator options.
    /// </summary>
    /// <returns>
    ///     A fully configured <see cref="CppParserOptions"/> ready for
    ///     <see cref="CppParser.ParseFiles"/>.
    /// </returns>
    private CppParserOptions BuildParserOptions()
    {
        var options = new CppParserOptions();

        // Public include roots are added as -I paths so public headers can find each other
        foreach (var root in _options.PublicIncludeRoots)
        {
            options.IncludeFolders.Add(root);
        }

        // Additional include directories for third-party or internal headers referenced
        // by public headers but not part of the documented API
        foreach (var path in _options.AdditionalIncludePaths)
        {
            options.IncludeFolders.Add(path);
        }

        // System include paths for toolchain and SDK headers; declarations found under
        // these paths are resolved but never documented
        foreach (var path in _options.SystemIncludePaths)
        {
            options.SystemIncludeFolders.Add(path);
        }

        // Preprocessor defines — export macros must be defined as empty strings so the
        // parser sees them as no-ops rather than type annotations
        foreach (var define in _options.Defines)
        {
            options.Defines.Add(define);
        }

        // Set the C++ language standard; placed before additional arguments so it can
        // be overridden by an explicit -std flag in AdditionalCompilerArguments
        options.AdditionalArguments.Add($"-std={_options.CppStandard}");

        // Append escape-hatch arguments last so they can override any earlier option
        foreach (var arg in _options.AdditionalCompilerArguments)
        {
            options.AdditionalArguments.Add(arg);
        }

        // CppParserOptions defaults TargetSystem to "windows", producing a target triple of
        // "x86_64-pc-windows-" on every platform. This causes libclang to search Windows
        // system headers regardless of the actual OS, so <string> and other C++ stdlib
        // headers are never found on macOS or Linux. Set the correct triple unless the
        // caller has already supplied an explicit --target= in AdditionalCompilerArguments.
        if (!_options.AdditionalCompilerArguments.Any(a => a.StartsWith("--target=", StringComparison.Ordinal)))
        {
            ConfigurePlatformTarget(options);
        }

        // On non-Windows platforms the NuGet-bundled libclang does not automatically
        // locate system headers. Ask the host clang for its full include search list
        // (via `clang -v`) and add every directory in the exact order clang reports,
        // which guarantees the correct C++ stdlib → resource → C system header ordering.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var dir in GetClangSystemIncludeDirs())
            {
                options.SystemIncludeFolders.Add(dir);
            }
        }

        return options;
    }

    /// <summary>
    ///     Sets the <see cref="CppParserOptions"/> target triple to match the current runtime
    ///     platform and architecture, overriding the CppAst default of <c>x86_64-pc-windows-</c>.
    /// </summary>
    /// <remarks>
    ///     CppAst's <c>CppParserOptions</c> defaults <c>TargetSystem</c> to <c>"windows"</c>
    ///     regardless of the host OS. Without correction, libclang uses Windows header search
    ///     paths on all platforms and cannot locate C++ standard library headers on macOS or
    ///     Linux.
    /// </remarks>
    /// <param name="options">The parser options to configure in-place.</param>
    private static void ConfigurePlatformTarget(CppParserOptions options)
    {
        options.TargetCpu = RuntimeInformation.OSArchitecture switch
        {
            Architecture.X64 => CppTargetCpu.X86_64,
            Architecture.Arm64 => CppTargetCpu.ARM64,
            Architecture.X86 => CppTargetCpu.X86,
            Architecture.Arm => CppTargetCpu.ARM,
            _ => CppTargetCpu.X86_64
        };
        options.TargetCpuSub = string.Empty;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            options.TargetVendor = "apple";
            options.TargetSystem = "macos";
            options.TargetAbi = string.Empty;
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            options.TargetVendor = "unknown";
            options.TargetSystem = "linux";
            options.TargetAbi = "gnu";
        }
        else
        {
            // Windows — preserve the CppAst default values
            options.TargetVendor = "pc";
            options.TargetSystem = "windows";
            options.TargetAbi = string.Empty;
        }
    }

    /// <summary>
    ///     Returns the system include directories that the host Clang toolchain uses for C++
    ///     header parsing, in the exact order Clang itself searches them.
    /// </summary>
    /// <remarks>
    ///     Runs <c>clang -v -x c++ -E /dev/null</c> and parses the <c>#include &lt;...&gt; search
    ///     starts here</c> block from stderr. This is the authoritative source of include paths and
    ///     their ordering — no manual path reconstruction is required. Framework directories reported
    ///     by Clang are skipped because CppAst handles frameworks separately.
    /// </remarks>
    /// <returns>
    ///     An ordered sequence of absolute directory paths to add as system include folders.
    ///     Returns an empty sequence when the host Clang toolchain cannot be located or produces
    ///     no usable output.
    /// </returns>
    private static IEnumerable<string> GetClangSystemIncludeDirs()
    {
        // On macOS, clang is accessed via xcrun to pick up the active Xcode/CommandLineTools toolchain.
        var clangExe = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? RunCommand("xcrun", "--find", "clang")
            : "clang";

        if (string.IsNullOrEmpty(clangExe))
        {
            yield break;
        }

        var verbose = RunCommandStderr(clangExe, "-v", "-x", "c++", "-E", "/dev/null");
        if (string.IsNullOrEmpty(verbose))
        {
            yield break;
        }

        var inList = false;
        foreach (var line in verbose.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed == "#include <...> search starts here:")
            {
                inList = true;
                continue;
            }

            if (trimmed == "End of search list.")
            {
                break;
            }

            if (inList && !trimmed.Contains("(framework directory)") && Directory.Exists(trimmed))
            {
                yield return trimmed;
            }
        }
    }

    /// <summary>
    ///     Runs an external command and returns its trimmed standard output, or
    ///     <see langword="null"/> when the command fails or is not available.
    /// </summary>
    /// <param name="fileName">The executable to run.</param>
    /// <param name="arguments">Arguments to pass to the executable.</param>
    /// <returns>Trimmed stdout on success, or <see langword="null"/> on any failure.</returns>
    private static string? RunCommand(string fileName, params string[] arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var arg in arguments)
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = Process.Start(psi);
            if (process == null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            return process.ExitCode == 0 && !string.IsNullOrEmpty(output) ? output : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    ///     Runs an external command and returns its trimmed standard error output, or
    ///     <see langword="null"/> when the command is not available. Unlike
    ///     <see cref="RunCommand"/>, a non-zero exit code does not suppress the output because
    ///     some tools (including <c>clang -v</c>) write useful diagnostic output to stderr
    ///     while still returning a non-zero code when given a null input file.
    /// </summary>
    /// <param name="fileName">The executable to run.</param>
    /// <param name="arguments">Arguments to pass to the executable.</param>
    /// <returns>Trimmed stderr on success, or <see langword="null"/> on any failure.</returns>
    private static string? RunCommandStderr(string fileName, params string[] arguments)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var arg in arguments)
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = Process.Start(psi);
            if (process == null)
            {
                return null;
            }

            var output = process.StandardError.ReadToEnd().Trim();
            process.WaitForExit();
            return string.IsNullOrEmpty(output) ? null : output;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // =========================================================================
    // Error checking
    // =========================================================================

    /// <summary>
    ///     Inspects the compilation diagnostics and throws when any parse errors are present.
    /// </summary>
    /// <remarks>
    ///     Parse errors in public headers mean the generated documentation would be incomplete
    ///     or incorrect, so they are surfaced immediately rather than silently ignored.
    /// </remarks>
    /// <param name="compilation">The CppAst compilation result to inspect.</param>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when one or more parse errors are present in the diagnostics.
    /// </exception>
    private static void CheckForErrors(CppCompilation compilation)
    {
        if (!compilation.HasErrors)
        {
            return;
        }

        // Collect all error messages to produce a comprehensive failure report with context
        var errors = compilation.Diagnostics.Messages
            .Where(m => m.Type == CppLogMessageType.Error)
            .Select(m => m.Text)
            .ToList();

        throw new InvalidOperationException(
            $"C++ header parsing failed with {errors.Count} error(s):\n{string.Join("\n", errors)}");
    }

    // =========================================================================
    // AST walking and declaration collection
    // =========================================================================

    /// <summary>
    ///     Collects owned classes and free functions declared in the C++ global (unnamed) namespace
    ///     directly on the compilation object.
    /// </summary>
    /// <param name="compilation">The compilation whose top-level declarations to inspect.</param>
    /// <param name="result">Dictionary that accumulates declarations grouped by namespace key.</param>
    private void CollectGlobalDeclarations(
        CppCompilation compilation,
        SortedDictionary<string, NamespaceDeclarations> result)
    {
        // Global-namespace classes — owned when their source file falls under a public include root
        foreach (var cls in compilation.Classes)
        {
            if (!IsOwned(cls.SourceFile))
            {
                continue;
            }

            // Skip deprecated classes unless the caller has opted into seeing them
            if (!_options.IncludeDeprecated && IsDeprecated(cls))
            {
                continue;
            }

            EnsureNamespace(result, GlobalNamespaceKey, GlobalNamespaceKey);
            result[GlobalNamespaceKey].Classes.Add(cls);
        }

        // Global-namespace free functions — owned when their source file falls under a root
        foreach (var fn in compilation.Functions)
        {
            if (!IsOwned(fn.SourceFile))
            {
                continue;
            }

            // Skip deprecated free functions unless the caller has opted into seeing them
            if (!_options.IncludeDeprecated && IsDeprecated(fn))
            {
                continue;
            }

            EnsureNamespace(result, GlobalNamespaceKey, GlobalNamespaceKey);
            result[GlobalNamespaceKey].FreeFunctions.Add(fn);
        }

        // Global-namespace enums — owned when their source file falls under a root
        foreach (var en in compilation.Enums)
        {
            if (!IsOwned(en.SourceFile))
            {
                continue;
            }

            // Skip deprecated enums unless the caller has opted into seeing them
            if (!_options.IncludeDeprecated && IsDeprecated(en))
            {
                continue;
            }

            EnsureNamespace(result, GlobalNamespaceKey, GlobalNamespaceKey);
            result[GlobalNamespaceKey].Enums.Add(en);
        }
    }

    /// <summary>
    ///     Recursively collects owned classes and free functions from a namespace and all its
    ///     nested child namespaces.
    /// </summary>
    /// <param name="ns">The namespace to inspect.</param>
    /// <param name="result">Dictionary that accumulates declarations grouped by namespace key.</param>
    private void CollectNamespaceDeclarations(
        CppNamespace ns,
        SortedDictionary<string, NamespaceDeclarations> result)
    {
        // Build the fully-qualified C++ namespace name from the parent name and this namespace's
        // short name, then derive the file-path-compatible key by replacing '::' with '.'
        var qualifiedName = GetNamespaceName(ns);
        var nsKey = qualifiedName.Replace("::", ".", StringComparison.Ordinal);

        // Collect owned classes declared directly in this namespace
        foreach (var cls in ns.Classes)
        {
            if (!IsOwned(cls.SourceFile))
            {
                continue;
            }

            // Skip deprecated classes unless the caller has opted into seeing them
            if (!_options.IncludeDeprecated && IsDeprecated(cls))
            {
                continue;
            }

            EnsureNamespace(result, nsKey, qualifiedName);
            result[nsKey].Classes.Add(cls);
        }

        // Collect owned free functions declared directly in this namespace
        foreach (var fn in ns.Functions)
        {
            if (!IsOwned(fn.SourceFile))
            {
                continue;
            }

            // Skip deprecated free functions unless the caller has opted into seeing them
            if (!_options.IncludeDeprecated && IsDeprecated(fn))
            {
                continue;
            }

            EnsureNamespace(result, nsKey, qualifiedName);
            result[nsKey].FreeFunctions.Add(fn);
        }

        // Collect owned enums declared directly in this namespace
        foreach (var en in ns.Enums)
        {
            if (!IsOwned(en.SourceFile))
            {
                continue;
            }

            // Skip deprecated enums unless the caller has opted into seeing them
            if (!_options.IncludeDeprecated && IsDeprecated(en))
            {
                continue;
            }

            EnsureNamespace(result, nsKey, qualifiedName);
            result[nsKey].Enums.Add(en);
        }

        // Recurse into nested namespaces so deeply-nested declarations are captured
        foreach (var nested in ns.Namespaces)
        {
            CollectNamespaceDeclarations(nested, result);
        }

        // Register this namespace object in the entry so its doc comment is available
        // for GetNamespaceDescription; deduplication guard handles namespaces that span
        // multiple translation units, where the same qualified name may be visited more
        // than once with distinct CppNamespace objects
        if (result.TryGetValue(nsKey, out var nsEntry) && !nsEntry.Namespaces.Contains(ns))
        {
            nsEntry.Namespaces.Add(ns);
        }
    }

    /// <summary>
    ///     Ensures a <see cref="NamespaceDeclarations"/> entry exists in the result dictionary,
    ///     creating a new one with the supplied display name when the key is absent.
    /// </summary>
    /// <param name="result">The dictionary to update in-place.</param>
    /// <param name="key">The file-path-compatible namespace key.</param>
    /// <param name="displayName">The C++ qualified namespace name used as the Markdown page heading.</param>
    private static void EnsureNamespace(
        SortedDictionary<string, NamespaceDeclarations> result,
        string key,
        string displayName)
    {
        if (!result.ContainsKey(key))
        {
            result[key] = new NamespaceDeclarations(displayName);
        }
    }

    /// <summary>
    ///     Returns the fully-qualified C++ name of a namespace by combining its parent name
    ///     and short name with the <c>::</c> separator.
    /// </summary>
    /// <remarks>
    ///     <see cref="CppNamespace"/> exposes only its short <c>Name</c> and the
    ///     <c>FullParentName</c> of its enclosing scope. This helper reassembles the
    ///     fully-qualified name that is needed for display and dictionary keys.
    /// </remarks>
    /// <param name="ns">The namespace whose qualified name to compute.</param>
    /// <returns>
    ///     The fully-qualified namespace name (e.g. <c>mylib::rendering</c>).
    ///     Returns only <see cref="CppNamespace.Name"/> when <c>FullParentName</c> is empty
    ///     (i.e., the namespace is at global scope).
    /// </returns>
    private static string GetNamespaceName(CppNamespace ns)
    {
        return string.IsNullOrEmpty(ns.FullParentName)
            ? ns.Name
            : $"{ns.FullParentName}::{ns.Name}";
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
    ///     Determines whether a source file falls under one of the configured
    ///     <see cref="CppGeneratorOptions.PublicIncludeRoots"/> and therefore belongs to the
    ///     documented public API.
    /// </summary>
    /// <param name="sourceFile">
    ///     The source file path from a CppAst declaration span. May be null or empty for
    ///     built-in or synthesized declarations — those are never considered owned.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when the file is under a configured include root;
    ///     <see langword="false"/> otherwise.
    /// </returns>
    private bool IsOwned(string? sourceFile)
    {
        if (string.IsNullOrEmpty(sourceFile))
        {
            return false;
        }

        // Normalize the source path before comparing to avoid false mismatches caused by
        // mixed separators, relative path segments, or symlinks
        var normalized = Path.GetFullPath(sourceFile);

        return _options.PublicIncludeRoots.Any(root =>
        {
            // Append a directory separator to the root so "lib" does not match "libext"
            var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, '/') +
                                 Path.DirectorySeparatorChar;
            return normalized.StartsWith(normalizedRoot, FileSystemPathComparison);
        });
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

        // All-namespaces table — lists every namespace so AI agents get a complete map in one
        // read; Declarations count reflects only declarations directly in each namespace
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
                    var summary = GetSummary(cls) ?? NoDescriptionPlaceholder;
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
                    var summary = GetSummary(en) ?? NoDescriptionPlaceholder;
                    return new[] { $"[{en.Name}]({nsKey}/{en.Name}.md)", summary };
                });
            writer.WriteTable(enumHeaders, enumRows);
        }

        // Free-function table — one row per owned free function, sorted alphabetically
        if (nsDecls.FreeFunctions.Count > 0)
        {
            writer.WriteHeading(2, "Functions");
            var fnHeaders = new[] { "Function", "Returns", DescriptionColumnHeader };
            var fnRows = nsDecls.FreeFunctions
                .OrderBy(f => f.Name, StringComparer.Ordinal)
                .Select(fn =>
                {
                    var summary = GetSummary(fn) ?? NoDescriptionPlaceholder;
                    var returnType = SimplifyTypeName(fn.ReturnType.GetDisplayName());
                    return new[] { $"[{fn.Name}]({nsKey}/{fn.Name}.md)", returnType, summary };
                });
            writer.WriteTable(fnHeaders, fnRows);
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
        var sourceFile = cls.SourceFile;
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
            writer.WriteSignature("cpp", string.Join("\n", sigParts));
        }

        // Emit summary from doc comment, or placeholder when no comment is present
        var typeSummary = GetSummary(cls);
        writer.WriteParagraph(!string.IsNullOrEmpty(typeSummary) ? typeSummary : NoDescriptionPlaceholder);

        // Emit extended details when the doc comment contains a @details or @remarks block
        var typeDetails = GetDetails(cls);
        if (!string.IsNullOrEmpty(typeDetails))
        {
            writer.WriteParagraph(typeDetails);
        }

        // Emit base type names so readers know the inheritance chain without reading the header
        if (cls.BaseTypes.Count > 0)
        {
            var baseNames = cls.BaseTypes
                .Select(bt => SimplifyTypeName(bt.Type.GetDisplayName()))
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

        // Build a flat list of all visible members for case-insensitive collision detection.
        // Constructors, methods, and fields are merged so that cross-kind name collisions
        // (e.g. method Name() and field name) are detected as a single group.
        var allMembers = new List<CppElement>(
            visibleCtors.Cast<CppElement>()
                .Concat(visibleMethods)
                .Concat(visibleFields.Cast<CppElement>()));

        // Case-insensitive map: lowercase member name → list of members sharing that lowercase name.
        // Members sharing a key are combined onto a single page named after the lowercase key.
        var caseInsensitiveGroups = new Dictionary<string, List<CppElement>>(StringComparer.Ordinal);
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
            // when there is a collision, the page uses the lowercase key so it is stable
            var pageFileName = group.Count == 1 ? baseName : lowerKey;
            var ctorSummary = GetSummary(ctor) ?? NoDescriptionPlaceholder;

            // Show simplified parameter types in the link text so readers can
            // distinguish overloaded constructors at a glance
            var paramTypes = string.Join(
                ", ",
                ctor.Parameters.Select(p => SimplifyTypeName(p.Type.GetDisplayName())));

            if (writtenKeys.Add(lowerKey))
            {
                if (group.Count == 1)
                {
                    WriteMemberPage(factory, nsKey, nsDisplayName, cls, ctor, baseName);
                }
                else
                {
                    WriteCombinedMemberPage(factory, nsKey, nsDisplayName, cls, lowerKey, group);
                }
            }

            ctorRows.Add(new[] { $"[{cls.Name}({paramTypes})]({cls.Name}/{pageFileName}.md)", ctorSummary });
        }

        // Process methods — emit method rows after writing each member's detail page
        foreach (var method in visibleMethods)
        {
            var baseName = GetMemberBaseName(method, cls.Name);
            var lowerKey = baseName.ToLowerInvariant();
            var group = caseInsensitiveGroups[lowerKey];
            var pageFileName = group.Count == 1 ? baseName : lowerKey;
            var methodSummary = GetSummary(method) ?? NoDescriptionPlaceholder;
            var returnType = SimplifyTypeName(method.ReturnType.GetDisplayName());

            if (writtenKeys.Add(lowerKey))
            {
                if (group.Count == 1)
                {
                    WriteMemberPage(factory, nsKey, nsDisplayName, cls, method, baseName);
                }
                else
                {
                    WriteCombinedMemberPage(factory, nsKey, nsDisplayName, cls, lowerKey, group);
                }
            }

            // Show simplified parameter types in the link text so readers can
            // distinguish overloaded methods at a glance
            var methodParamTypes = string.Join(
                ", ",
                method.Parameters.Select(p => SimplifyTypeName(p.Type.GetDisplayName())));
            methodRows.Add(new[] { $"[{method.Name}({methodParamTypes})]({cls.Name}/{pageFileName}.md)", returnType, methodSummary });
        }

        // Process fields — emit field rows after writing each member's detail page
        foreach (var field in visibleFields)
        {
            var baseName = GetMemberBaseName(field, cls.Name);
            var lowerKey = baseName.ToLowerInvariant();
            var group = caseInsensitiveGroups[lowerKey];
            var pageFileName = group.Count == 1 ? baseName : lowerKey;
            var fieldSummary = GetSummary(field) ?? NoDescriptionPlaceholder;
            var typeName = SimplifyTypeName(field.Type.GetDisplayName());

            if (writtenKeys.Add(lowerKey))
            {
                if (group.Count == 1)
                {
                    WriteMemberPage(factory, nsKey, nsDisplayName, cls, field, baseName);
                }
                else
                {
                    WriteCombinedMemberPage(factory, nsKey, nsDisplayName, cls, lowerKey, group);
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
        using var writer = factory.CreateMarkdown(nsKey, fn.Name);
        writer.WriteHeading(1, fn.Name);

        // Emit the fully-qualified name as a comment followed by the optional #include
        // directive and C++ signature so that an AI reader has all context needed to
        // use the function without browsing the header tree
        var qualifiedName = string.IsNullOrEmpty(nsDisplayName)
            ? fn.Name
            : $"{nsDisplayName}::{fn.Name}";
        var signature = BuildMethodSignature(fn);
        if (!string.IsNullOrEmpty(fn.SourceFile))
        {
            var includePath = GetIncludePath(fn.SourceFile);
            writer.WriteSignature("cpp", $"// {qualifiedName}\n#include <{includePath}>\n{signature}");
        }
        else
        {
            writer.WriteSignature("cpp", $"// {qualifiedName}\n{signature}");
        }

        // Emit summary from doc comment or placeholder when no comment is present
        var summary = GetSummary(fn);
        writer.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : NoDescriptionPlaceholder);

        // Emit extended details when the doc comment contains a @details or @remarks block
        var details = GetDetails(fn);
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
                new[] { p.Name, SimplifyTypeName(p.Type.GetDisplayName()), GetParamDescription(fn, p.Name) ?? string.Empty });
            writer.WriteTable(paramHeaders, paramRows);
        }

        // Emit return description when the function is not void
        var returnTypeName = SimplifyTypeName(fn.ReturnType.GetDisplayName());
        if (!string.Equals(returnTypeName, "void", StringComparison.Ordinal))
        {
            writer.WriteHeading(4, "Returns");
            var returnDescription = GetReturnDescription(fn);
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
        CppElement member,
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
        var summary = GetSummary(method);
        writer.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : NoDescriptionPlaceholder);

        // Emit extended details when the doc comment contains a @details or @remarks block
        var details = GetDetails(method);
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
                new[] { p.Name, SimplifyTypeName(p.Type.GetDisplayName()), GetParamDescription(method, p.Name) ?? string.Empty });
            writer.WriteTable(paramHeaders, paramRows);
        }

        // Emit return description when the method is not void and is not a constructor;
        // constructors have no return type so the return section would be meaningless
        if (!method.IsConstructor)
        {
            var returnTypeName = SimplifyTypeName(method.ReturnType.GetDisplayName());
            if (!string.Equals(returnTypeName, "void", StringComparison.Ordinal))
            {
                writer.WriteHeading(4, "Returns");
                var returnDescription = GetReturnDescription(method);
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
        var signature = $"{SimplifyTypeName(field.Type.GetDisplayName())} {field.Name};";
        writer.WriteSignature("cpp", $"// {qualifiedName}\n{signature}");

        // Emit summary from doc comment or placeholder
        var summary = GetSummary(field);
        writer.WriteParagraph(!string.IsNullOrEmpty(summary) ? summary : NoDescriptionPlaceholder);

        // Emit extended details when the doc comment contains a @details or @remarks block
        var details = GetDetails(field);
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
        if (!string.IsNullOrEmpty(cppEnum.SourceFile))
        {
            var includePath = GetIncludePath(cppEnum.SourceFile);
            writer.WriteSignature("cpp", $"// {qualifiedName}\n#include <{includePath}>");
        }
        else
        {
            writer.WriteSignature("cpp", $"// {qualifiedName}");
        }

        // Emit summary from doc comment or placeholder
        var enumSummary = GetSummary(cppEnum);
        writer.WriteParagraph(!string.IsNullOrEmpty(enumSummary) ? enumSummary : NoDescriptionPlaceholder);

        // Emit extended details when the doc comment contains a @details or @remarks block
        var enumDetails = GetDetails(cppEnum);
        if (!string.IsNullOrEmpty(enumDetails))
        {
            writer.WriteParagraph(enumDetails);
        }

        // Emit a values table so readers can see all valid values and their meanings
        if (cppEnum.Items.Count > 0)
        {
            writer.WriteHeading(3, "Values");
            var headers = new[] { "Value", DescriptionColumnHeader };
            var rows = cppEnum.Items.Select(item =>
            {
                var itemSummary = GetSummary(item) ?? NoDescriptionPlaceholder;
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
        return cls.Constructors
            .Where(c => IsVisibleMember(c.Visibility))
            .Where(c => _options.IncludeDeprecated || !IsDeprecated(c));
    }

    /// <summary>
    ///     Returns the visible methods of a class, filtered by the configured
    ///     <see cref="CppGeneratorOptions.Visibility"/> and <see cref="CppGeneratorOptions.IncludeDeprecated"/>.
    /// </summary>
    /// <param name="cls">The class whose methods to filter.</param>
    /// <returns>Methods that pass both the visibility and deprecated filters.</returns>
    private IEnumerable<CppFunction> GetVisibleMethods(CppClass cls)
    {
        return cls.Functions
            .Where(m => IsVisibleMember(m.Visibility))
            .Where(m => _options.IncludeDeprecated || !IsDeprecated(m));
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
            .Where(f => IsVisibleMember(f.Visibility))
            .Where(f => _options.IncludeDeprecated || !IsDeprecated(f));
    }

    /// <summary>
    ///     Determines whether a class member with the given visibility should appear in the
    ///     generated output based on <see cref="CppGeneratorOptions.Visibility"/>.
    /// </summary>
    /// <param name="visibility">The CppAst visibility of the member access specifier.</param>
    /// <returns><see langword="true"/> when the member should be included.</returns>
    private bool IsVisibleMember(CppVisibility visibility)
    {
        return _options.Visibility switch
        {
            ApiVisibility.Public => visibility == CppVisibility.Public,
            ApiVisibility.PublicAndProtected => visibility is CppVisibility.Public or CppVisibility.Protected,
            ApiVisibility.All => true,

            // Default to public-only for any unrecognized future enum value
            _ => visibility == CppVisibility.Public,
        };
    }

    /// <summary>
    ///     Determines whether a declaration carries a <c>[[deprecated]]</c> attribute.
    /// </summary>
    /// <remarks>
    ///     CppAst exposes <c>Attributes</c> on concrete declaration types (<see cref="CppFunction"/>,
    ///     <see cref="CppField"/>, <see cref="CppClass"/>) rather than on the shared base class, so
    ///     this method uses pattern matching to obtain the attribute list.
    /// </remarks>
    /// <param name="element">The CppAst element to inspect.</param>
    /// <returns>
    ///     <see langword="true"/> when the element has a <c>deprecated</c> attribute;
    ///     <see langword="false"/> otherwise.
    /// </returns>
    private static bool IsDeprecated(CppElement element)
    {
        // Obtain the attribute list from the concrete type; CppDeclaration does not expose it
        List<CppAttribute>? attributes = element switch
        {
            CppFunction fn => fn.Attributes,
            CppField field => field.Attributes,
            CppClass cls => cls.Attributes,
            CppEnum en => en.Attributes,
            _ => null,
        };

        // Match [[deprecated]] and compiler-specific deprecated spellings
        return attributes?.Any(a =>
            string.Equals(a.Name, "deprecated", StringComparison.OrdinalIgnoreCase)) ?? false;
    }

    // =========================================================================
    // Comment extraction helpers
    // =========================================================================

    /// <summary>
    ///     Extracts the brief summary text from a CppAst element's Doxygen comment.
    /// </summary>
    /// <remarks>
    ///     Prefers the <c>@brief</c> block command when present; falls back to the first
    ///     plain paragraph. Returns a single normalized line — newlines within the brief
    ///     block are collapsed to spaces. Returns <see langword="null"/> when the element
    ///     has no doc comment.
    /// </remarks>
    /// <param name="element">The element whose comment to extract.</param>
    /// <returns>The trimmed single-line summary, or <see langword="null"/> when none is present.</returns>
    private static string? GetSummary(CppElement element)
    {
        var comment = (element as ICppDeclaration)?.Comment;
        if (comment == null)
        {
            return null;
        }

        // Prefer @brief command; fall back to first plain paragraph
        var briefBlock = comment.Children?
            .OfType<CppCommentBlockCommand>()
            .FirstOrDefault(c => string.Equals(c.CommandName, "brief", StringComparison.OrdinalIgnoreCase));
        if (briefBlock != null)
        {
            var text = briefBlock.ChildrenToString().Trim();
            return string.IsNullOrEmpty(text) ? null : NormalizeSingleLine(text);
        }

        var para = comment.Children?.OfType<CppCommentParagraph>().FirstOrDefault();
        if (para != null)
        {
            var text = para.ChildrenToString().Trim();
            return string.IsNullOrEmpty(text) ? null : NormalizeSingleLine(text);
        }

        return null;
    }

    /// <summary>
    ///     Extracts the extended details text from a CppAst element's <c>@details</c> or
    ///     <c>@remarks</c> Doxygen command.
    /// </summary>
    /// <remarks>
    ///     Unlike <see cref="GetSummary"/>, the details text is returned as-is (trimmed but
    ///     not collapsed to a single line) so that intentional multi-line content is preserved.
    /// </remarks>
    /// <param name="element">The element whose comment to extract.</param>
    /// <returns>The trimmed details text, or <see langword="null"/> when none is present.</returns>
    private static string? GetDetails(CppElement element)
    {
        var comment = (element as ICppDeclaration)?.Comment;
        var detailsBlock = comment?.Children?
            .OfType<CppCommentBlockCommand>()
            .FirstOrDefault(c =>
                string.Equals(c.CommandName, "details", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.CommandName, "remarks", StringComparison.OrdinalIgnoreCase));
        if (detailsBlock == null)
        {
            return null;
        }

        var text = detailsBlock.ChildrenToString().Trim();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>
    ///     Extracts the description for a named parameter from a Doxygen <c>@param</c> command.
    /// </summary>
    /// <param name="element">The function or method whose comment to inspect.</param>
    /// <param name="paramName">The exact parameter name to look up.</param>
    /// <returns>The trimmed single-line description, or <see langword="null"/> when no matching <c>@param</c> exists.</returns>
    private static string? GetParamDescription(CppElement element, string paramName)
    {
        var comment = (element as ICppDeclaration)?.Comment;
        var paramCmd = comment?.Children?
            .OfType<CppCommentParamCommand>()
            .FirstOrDefault(c => string.Equals(c.ParamName, paramName, StringComparison.Ordinal));
        if (paramCmd == null)
        {
            return null;
        }

        var text = paramCmd.ChildrenToString().Trim();
        return string.IsNullOrEmpty(text) ? null : NormalizeSingleLine(text);
    }

    /// <summary>
    ///     Extracts the return description from a Doxygen <c>@return</c> or <c>@returns</c> command.
    /// </summary>
    /// <param name="element">The function or method whose comment to inspect.</param>
    /// <returns>The trimmed single-line return description, or <see langword="null"/> when none is present.</returns>
    private static string? GetReturnDescription(CppElement element)
    {
        var comment = (element as ICppDeclaration)?.Comment;
        var returnCmd = comment?.Children?
            .OfType<CppCommentBlockCommand>()
            .FirstOrDefault(c =>
                string.Equals(c.CommandName, "return", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.CommandName, "returns", StringComparison.OrdinalIgnoreCase));
        if (returnCmd == null)
        {
            return null;
        }

        var text = returnCmd.ChildrenToString().Trim();
        return string.IsNullOrEmpty(text) ? null : NormalizeSingleLine(text);
    }

    /// <summary>
    ///     Collapses a multi-line string into a single space-separated line.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>A single-line string with internal whitespace runs collapsed to a single space.</returns>
    private static string NormalizeSingleLine(string text)
    {
        return string.Join(
            " ",
            text.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s)));
    }

    /// <summary>
    ///     Simplifies a C++ type display name by replacing verbose internal STL names
    ///     with their idiomatic user-facing spellings.
    /// </summary>
    /// <param name="typeName">The raw display name from <c>CppType.GetDisplayName()</c>.</param>
    /// <returns>A more readable type name string.</returns>
    private static string SimplifyTypeName(string typeName)
    {
        // std::string is a typedef for std::basic_string<char>; replace the internal name
        return typeName
            .Replace("basic_string<char, char_traits<char>, allocator<char>>", "string")
            .Replace("basic_string<char,char_traits<char>,allocator<char>>", "string")
            .Replace("basic_string", "string");
    }

    /// <summary>
    ///     Derives a one-line description for a namespace from the doc comment placed
    ///     directly on the <c>namespace</c> keyword, used as the description in <c>api.md</c>.
    /// </summary>
    /// <remarks>
    ///     Iterates all collected <see cref="CppNamespace"/> objects for the namespace key
    ///     and returns the first non-empty summary found on any of them. Falls back to
    ///     <see cref="NoDescriptionPlaceholder"/> when none of the namespace objects carry
    ///     a doc comment.
    /// </remarks>
    /// <param name="nsDecls">The namespace declarations to inspect.</param>
    /// <returns>A short description, or <see cref="NoDescriptionPlaceholder"/> when none is found.</returns>
    private static string GetNamespaceDescription(NamespaceDeclarations nsDecls)
    {
        return nsDecls.Namespaces
                   .Select(GetSummary)
                   .FirstOrDefault(s => !string.IsNullOrEmpty(s))
               ?? NoDescriptionPlaceholder;
    }

    // =========================================================================
    // Signature builders
    // =========================================================================

    /// <summary>
    ///     Builds a C++ method or constructor signature string suitable for display in a
    ///     fenced code block, including storage qualifiers, virtual qualifiers, parameter
    ///     list, const qualifier, and variadic indicator.
    /// </summary>
    /// <remarks>
    ///     Constructors and destructors omit the return type. Static and virtual are mutually
    ///     exclusive prefixes. Pure virtual methods append <c> = 0</c>. Const methods append
    ///     <c> const</c>. Variadic functions append <c>...</c> to the parameter list.
    /// </remarks>
    /// <param name="fn">The method or constructor to produce a signature for.</param>
    /// <returns>
    ///     A C++ declaration string such as <c>static string GetGreeting(const string&amp; name)</c>
    ///     or <c>Circle(double radius)</c> for constructors.
    /// </returns>
    private static string BuildMethodSignature(CppFunction fn)
    {
        var sb = new System.Text.StringBuilder();

        // Constructors and destructors have no return type or storage qualifier prefix
        if (!fn.IsConstructor && !fn.IsDestructor)
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

            sb.Append(SimplifyTypeName(fn.ReturnType.GetDisplayName()));
            sb.Append(' ');
        }

        sb.Append(fn.Name);
        sb.Append('(');

        // Build the parameter list; append "..." for variadic functions
        var paramParts = fn.Parameters
            .Select(p => $"{SimplifyTypeName(p.Type.GetDisplayName())} {p.Name}")
            .ToList();
        if (fn.Flags.HasFlag(CppFunctionFlags.Variadic))
        {
            paramParts.Add("...");
        }

        sb.Append(string.Join(", ", paramParts));
        sb.Append(')');

        // Const qualifier appears after the closing parenthesis
        if (fn.IsConst)
        {
            sb.Append(" const");
        }

        // Pure virtual marker signals to callers that the method must be overridden
        if (fn.IsPureVirtual)
        {
            sb.Append(" = 0");
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
        // NormalClass indicates a non-template type; skip template inspection entirely
        if (cls.TemplateKind == CppTemplateKind.NormalClass || cls.TemplateParameters.Count == 0)
        {
            return string.Empty;
        }

        // Collect the name of each type parameter; non-type parameters may not cast cleanly
        var paramNames = cls.TemplateParameters
            .OfType<CppTemplateParameterType>()
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
        // NormalClass indicates a non-template type; no declaration is needed
        if (cls.TemplateKind == CppTemplateKind.NormalClass || cls.TemplateParameters.Count == 0)
        {
            return string.Empty;
        }

        var paramNames = cls.TemplateParameters
            .OfType<CppTemplateParameterType>()
            .Select(tp => tp.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        if (paramNames.Count == 0)
        {
            return string.Empty;
        }

        var typedParams = string.Join(", ", paramNames.Select(n => $"typename {n}"));
        return $"template<{typedParams}>";
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
        IReadOnlyList<CppElement> members)
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
                        fn.Parameters.Select(p => SimplifyTypeName(p.Type.GetDisplayName())));
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
    ///     member's own <c>Name</c> property.
    /// </returns>
    private static string GetMemberBaseName(CppElement member, string className) => member switch
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
        ///     Initializes a new instance with the given display name.
        /// </summary>
        /// <param name="displayName">
        ///     The C++ qualified namespace name (e.g. <c>mylib::rendering</c>),
        ///     used as the Markdown page heading.
        /// </param>
        public NamespaceDeclarations(string displayName)
        {
            DisplayName = displayName;
        }

        /// <summary>Gets the C++ qualified namespace name used as the Markdown page heading.</summary>
        public string DisplayName { get; }

        /// <summary>Gets the list of owned classes and structs declared in this namespace.</summary>
        public List<CppClass> Classes { get; } = [];

        /// <summary>Gets the list of owned enums declared in this namespace.</summary>
        public List<CppEnum> Enums { get; } = [];

        /// <summary>Gets the list of owned free functions declared in this namespace.</summary>
        public List<CppFunction> FreeFunctions { get; } = [];

        /// <summary>Gets the list of <see cref="CppNamespace"/> objects that contribute to this namespace entry.</summary>
        /// <remarks>
        ///     A C++ namespace may be opened in multiple translation units; each occurrence is
        ///     a distinct <see cref="CppNamespace"/> instance. This list collects them so that
        ///     <c>GetNamespaceDescription</c> can search for a doc comment on any of them.
        /// </remarks>
        public List<CppNamespace> Namespaces { get; } = [];
    }
}
