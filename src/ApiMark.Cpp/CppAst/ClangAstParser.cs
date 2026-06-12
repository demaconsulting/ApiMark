using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ApiMark.Cpp.CppAst;

/// <summary>
///     Invokes <c>clang -ast-dump=json</c> on a set of C++ header files and converts the
///     resulting JSON AST into a <see cref="CppCompilationResult"/>.
/// </summary>
/// <remarks>
///     The parser discovers the clang executable automatically (PATH, xcrun on macOS, vswhere
///     on Windows) or uses an explicit path from
///     <see cref="CppGeneratorOptions.ClangPath"/>. It walks only nodes whose source file
///     falls under a <see cref="CppGeneratorOptions.PublicIncludeRoots"/> entry so that the
///     hundreds of megabytes of standard-library AST are skipped efficiently. Not thread-safe;
///     each <see cref="Parse"/> call constructs a fresh internal parser instance.
/// </remarks>
internal sealed class ClangAstParser
{
    // =========================================================================
    // Instance state
    // =========================================================================

    /// <summary>Generator options providing include roots, defines, and clang path.</summary>
    private readonly CppGeneratorOptions _options;

    /// <summary>
    ///     The set of normalized absolute paths of header files that were explicitly selected
    ///     as the API surface by the <see cref="Parse"/> caller. Only declarations whose source
    ///     file appears in this set are considered owned by the library; transitively-included
    ///     dependency headers that are under a <see cref="CppGeneratorOptions.PublicIncludeRoots"/>
    ///     entry but were not selected are therefore excluded.
    /// </summary>
    private readonly IReadOnlySet<string> _selectedHeaders;

    /// <summary>
    ///     Tracks the source file currently being walked. The clang JSON AST emits
    ///     <c>loc.file</c> only when the file changes; all subsequent nodes without a
    ///     <c>loc.file</c> field inherit this value.
    /// </summary>
    private string _currentFile = string.Empty;

    /// <summary>
    ///     Accumulates declarations grouped by fully-qualified namespace name. The empty
    ///     string key represents the C++ global (unnamed) namespace.
    /// </summary>
    private readonly Dictionary<string, NamespaceBuilder> _nsBuilders =
        new(StringComparer.Ordinal);

    /// <summary>Private constructor — callers use the static <see cref="Parse"/> entry point.</summary>
    /// <param name="options">Generator options. Must not be null.</param>
    /// <param name="selectedHeaders">
    ///     Normalized absolute paths of the header files selected as the API surface.
    ///     Must not be null. <see cref="IsOwned"/> uses this set to reject declarations
    ///     from transitively-included headers that were not explicitly selected.
    /// </param>
    private ClangAstParser(CppGeneratorOptions options, IReadOnlySet<string> selectedHeaders)
    {
        _options = options;
        _selectedHeaders = selectedHeaders;
    }

    // =========================================================================
    // Public entry point
    // =========================================================================

    /// <summary>
    ///     Runs clang on the supplied header files and returns a structured parse result.
    /// </summary>
    /// <remarks>
    ///     Execution steps:
    ///     <list type="number">
    ///       <item>Resolve the clang executable from <see cref="CppGeneratorOptions.ClangPath"/>
    ///         or automatic discovery.</item>
    ///       <item>Build the argument list from all structured options.</item>
    ///       <item>Start clang, capturing stdout (JSON) and stderr (diagnostics) concurrently.</item>
    ///       <item>Throw when clang exits non-zero and stdout is empty or invalid.</item>
    ///       <item>Walk the <c>TranslationUnitDecl</c> JSON tree, filtering by public include
    ///         root ownership.</item>
    ///       <item>Return merged namespaces and any stderr error lines.</item>
    ///     </list>
    /// </remarks>
    /// <param name="headers">
    ///     Absolute paths of the header files to parse. These paths also define the
    ///     owned-symbol filter: only declarations whose source file normalizes to one of
    ///     these paths are emitted; transitively-included dependency headers that fall under
    ///     a <see cref="CppGeneratorOptions.PublicIncludeRoots"/> entry but are not in this
    ///     list are excluded. Must not be null or empty.
    /// </param>
    /// <param name="options">
    ///     Generator options controlling include paths, defines, C++ standard, and clang
    ///     discovery. Must not be null.
    /// </param>
    /// <returns>A <see cref="CppCompilationResult"/> containing parsed namespaces and any errors.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the clang executable cannot be located, when clang exits non-zero and
    ///     produces no usable JSON, or when the JSON output cannot be parsed.
    /// </exception>
    public static CppCompilationResult Parse(
        IReadOnlyList<string> headers,
        CppGeneratorOptions options)
    {
        // Resolve the clang executable (may be xcrun on macOS)
        var (fileName, prefix) = FindClangExecutable(options.ClangPath);

        // Create a temporary combined header that #includes every header file so that
        // clang processes them as a single translation unit and produces a single JSON
        // object. Passing multiple files directly would produce one JSON object per TU,
        // making the output unsuitable for a single-document JSON parser.
        var tempFile = Path.Join(
            Path.GetTempPath(),
            $"apimark_combined_{Guid.NewGuid():N}.h");
        try
        {
            // Forward slashes in #include paths are portable across all platforms
            var includeLines = headers.Select(h => $"#include \"{h.Replace('\\', '/')}\"");
            File.WriteAllText(tempFile, string.Join(Environment.NewLine, includeLines));

            // Build argument list targeting only the combined temp file
            var args = BuildArguments(prefix, [tempFile], options);

            // Invoke clang and capture stdout (JSON) and stderr (diagnostics) without deadlock
            var (stdout, stderr, exitCode) = RunProcess(fileName, args);

            // Surface a clear error when clang fails and produces no JSON — the empty check
            // guards against the common case where clang prints "command not found" to stderr
            if (exitCode != 0 && string.IsNullOrWhiteSpace(stdout))
            {
                throw new InvalidOperationException(
                    $"clang exited with code {exitCode} and produced no JSON output.\n{stderr.Trim()}");
            }

            // Collect error lines from stderr for the caller to log
            var errors = CollectStderrErrors(stderr);

            // Build the normalized set of selected header paths so that IsOwned() can
            // restrict output to declarations physically defined in those files.
            // Headers that are only transitively included (and therefore not in this set)
            // are excluded even when their paths fall under a PublicIncludeRoot.
            var selectedHeaders = headers
                .Select(Path.GetFullPath)
                .ToHashSet(FileSystemPathComparer);

            // Parse the JSON and walk the AST.
            // Use Utf8JsonReader with an explicit MaxDepth because clang's JSON AST can nest
            // hundreds of levels deep inside standard library template instantiations.
            // JsonDocument.ParseValue reads exactly one JSON object from the reader position,
            // consuming the entire TU in one call.
            var parser = new ClangAstParser(options, selectedHeaders);
            try
            {
                var jsonBytes = System.Text.Encoding.UTF8.GetBytes(stdout);
                var readerOptions = new JsonReaderOptions { MaxDepth = 2048 };
                var reader = new Utf8JsonReader(jsonBytes, readerOptions);
                reader.Read(); // Advance to the first token (StartObject of TranslationUnitDecl)
                using var doc = JsonDocument.ParseValue(ref reader);
                parser.WalkTranslationUnit(doc.RootElement);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"Failed to parse clang JSON AST output: {ex.Message}", ex);
            }

            // Merge namespace builders into immutable records and return
            return new CppCompilationResult(parser.BuildNamespaces(), errors);
        }
        finally
        {
            // Remove the temporary combined header regardless of success or failure
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    // =========================================================================
    // Clang discovery
    // =========================================================================

    /// <summary>The name of the environment variable that overrides automatic clang discovery.</summary>
    internal const string ClangPathEnvVar = "APIMARK_CLANG_PATH";

    /// <summary>
    ///     Resolves the clang executable path and any command prefix needed to invoke it.
    /// </summary>
    /// <remarks>
    ///     Discovery order:
    ///     <list type="number">
    ///       <item>Explicit <paramref name="clangPath"/> from options (must exist on disk).</item>
    ///       <item><c>APIMARK_CLANG_PATH</c> environment variable (must exist on disk).</item>
    ///       <item><c>clang</c> on the system PATH.</item>
    ///       <item>On macOS: <c>xcrun</c> with <c>"clang"</c> as the first argument.</item>
    ///       <item>On Windows: vswhere-located LLVM clang, then
    ///         <c>C:\Program Files\LLVM\bin\clang.exe</c>.</item>
    ///     </list>
    /// </remarks>
    /// <param name="clangPath">
    ///     Optional explicit path to a clang executable. When non-empty, no discovery is performed.
    /// </param>
    /// <returns>
    ///     A tuple of (<c>fileName</c>, <c>prefix</c>) where <c>prefix</c> contains any
    ///     arguments that must be prepended to the clang argument list (e.g. <c>"clang"</c>
    ///     when invoking through <c>xcrun</c>).
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when no clang executable can be located.
    /// </exception>
    private static (string FileName, IReadOnlyList<string> Prefix) FindClangExecutable(
        string? clangPath)
    {
        // Option 1: explicit path supplied by the caller
        if (!string.IsNullOrEmpty(clangPath))
        {
            if (!File.Exists(clangPath))
            {
                throw new InvalidOperationException(
                    $"Clang executable not found at the specified path '{clangPath}'.");
            }

            return (clangPath, []);
        }

        // Option 2: APIMARK_CLANG_PATH environment variable
        var envPath = Environment.GetEnvironmentVariable(ClangPathEnvVar);
        if (!string.IsNullOrEmpty(envPath))
        {
            if (!File.Exists(envPath))
            {
                throw new InvalidOperationException(
                    $"Clang executable not found at the path specified by the " +
                    $"{ClangPathEnvVar} environment variable: '{envPath}'.");
            }

            return (envPath, []);
        }

        // Option 3: clang on the system PATH (all platforms)
        if (TryFindOnPath("clang", out var clangOnPath))
        {
            return (clangOnPath, []);
        }

        // Option 4: macOS — use xcrun so the active Xcode SDK is selected automatically
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return ("xcrun", ["clang"]);
        }

        // Option 5: Windows — query vswhere, then fall back to the default LLVM install location
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var vsClang = FindVsClang();
            if (vsClang != null)
            {
                return (vsClang, []);
            }

            // Default LLVM install directory on Windows
            const string LlvmDefault = @"C:\Program Files\LLVM\bin\clang.exe";
            if (File.Exists(LlvmDefault))
            {
                return (LlvmDefault, []);
            }
        }

        throw new InvalidOperationException(
            "Clang executable not found. Install LLVM clang and ensure 'clang' is on PATH, " +
            $"set the {ClangPathEnvVar} environment variable, " +
            "or set CppGeneratorOptions.ClangPath to the absolute path of the clang executable.");
    }

    /// <summary>
    ///     Searches the system PATH for an executable with the given base name.
    /// </summary>
    /// <param name="executable">
    ///     Base name without extension (e.g. <c>"clang"</c>).
    ///     On Windows, <c>.exe</c> is appended automatically.
    /// </param>
    /// <param name="path">Receives the absolute path when found.</param>
    /// <returns><see langword="true"/> when the executable was found on PATH.</returns>
    private static bool TryFindOnPath(
        string executable,
        [NotNullWhen(true)] out string? path)
    {
        // Append platform-specific extension before searching PATH directories
        var fileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? executable + ".exe"
            : executable;

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            if (string.IsNullOrEmpty(dir))
            {
                continue;
            }

            var candidate = Path.Join(dir, fileName);
            if (File.Exists(candidate))
            {
                path = candidate;
                return true;
            }
        }

        path = null;
        return false;
    }

    /// <summary>
    ///     Queries vswhere for the latest Visual Studio installation that includes the
    ///     LLVM clang component and returns the absolute path to <c>clang.exe</c>.
    /// </summary>
    /// <remarks>
    ///     Uses the vswhere tool shipped with every Visual Studio 2017+ installer.
    ///     Returns <see langword="null"/> when vswhere is absent, the component is not
    ///     installed, or any error occurs.
    /// </remarks>
    /// <returns>Absolute path to the VS-bundled <c>clang.exe</c>, or <see langword="null"/>.</returns>
    private static string? FindVsClang()
    {
        // vswhere is shipped alongside the Visual Studio installer
        var vsWherePath = Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio",
            "Installer",
            "vswhere.exe");

        if (!File.Exists(vsWherePath))
        {
            return null;
        }

        try
        {
            // Query for the latest VS install that ships LLVM clang
            var psi = new ProcessStartInfo
            {
                FileName = vsWherePath,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            psi.ArgumentList.Add("-latest");
            psi.ArgumentList.Add("-requires");
            psi.ArgumentList.Add("Microsoft.VisualStudio.Component.VC.Llvm.Clang");
            psi.ArgumentList.Add("-property");
            psi.ArgumentList.Add("installationPath");

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                return null;
            }

            var installPath = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit();

            if (string.IsNullOrEmpty(installPath))
            {
                return null;
            }

            // The LLVM clang bundled with VS lives at VC\Tools\Llvm\x64\bin\clang.exe
            var candidate = Path.Join(installPath, "VC", "Tools", "Llvm", "x64", "bin", "clang.exe");
            return File.Exists(candidate) ? candidate : null;
        }
        catch
        {
            // Any failure (missing component, process error) returns null to fall through discovery
            return null;
        }
    }

    // =========================================================================
    // Argument building
    // =========================================================================

    /// <summary>
    ///     Constructs the complete ordered argument list to pass to clang (or xcrun).
    /// </summary>
    /// <remarks>
    ///     Order:
    ///     <list type="number">
    ///       <item>Any <paramref name="prefix"/> arguments (e.g. <c>"clang"</c> for xcrun).</item>
    ///       <item>Core AST-dump flags and input-type flags.</item>
    ///       <item>C++ standard flag.</item>
    ///       <item><c>-I</c> flags for public include roots.</item>
    ///       <item><c>-isystem</c> flags for system include paths.</item>
    ///       <item><c>-D</c> flags for preprocessor defines.</item>
    ///       <item>Additional compiler arguments (escape-hatch).</item>
    ///       <item>Header file paths.</item>
    ///     </list>
    /// </remarks>
    /// <param name="prefix">Arguments to prepend before all clang flags (may be empty).</param>
    /// <param name="headers">Absolute paths of the header files to parse.</param>
    /// <param name="options">Generator options providing all structured clang settings.</param>
    /// <returns>The complete ordered argument list.</returns>
    private static IReadOnlyList<string> BuildArguments(
        IReadOnlyList<string> prefix,
        IReadOnlyList<string> headers,
        CppGeneratorOptions options)
    {
        var args = new List<string>(prefix);

        // Core flags: emit JSON AST, parse all comments, syntax-check only, treat input as C++
        args.AddRange(["-Xclang", "-ast-dump=json", "-fparse-all-comments", "-fsyntax-only", "-x", "c++"]);

        // C++ language standard
        args.Add($"-std={options.CppStandard}");

        // All public include roots are passed as -I flags so headers can find each other
        // — all compiler include directories live in PublicIncludeRoots after the redesign
        foreach (var root in options.PublicIncludeRoots)
        {
            args.Add("-I");
            args.Add(root);
        }

        // System include paths — declarations found here are resolved but never documented
        foreach (var path in options.SystemIncludePaths)
        {
            args.Add("-isystem");
            args.Add(path);
        }

        // Preprocessor defines (export macros, feature flags, etc.)
        foreach (var define in options.Defines)
        {
            args.Add("-D");
            args.Add(define);
        }

        // Escape-hatch arguments appended last so they can override structured options
        foreach (var arg in options.AdditionalCompilerArguments)
        {
            args.Add(arg);
        }

        // Header files are listed last, after all flags
        foreach (var header in headers)
        {
            args.Add(header);
        }

        return args;
    }

    // =========================================================================
    // Process execution
    // =========================================================================

    /// <summary>
    ///     Starts a process, reads stdout and stderr concurrently to avoid pipe deadlock,
    ///     and returns the combined output with the exit code.
    /// </summary>
    /// <param name="fileName">Executable to launch.</param>
    /// <param name="arguments">Arguments to pass to the executable.</param>
    /// <returns>A tuple of (stdout, stderr, exitCode).</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the process cannot be started.
    /// </exception>
    private static (string Stdout, string Stderr, int ExitCode) RunProcess(
        string fileName,
        IReadOnlyList<string> arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in arguments)
        {
            psi.ArgumentList.Add(arg);
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process '{fileName}'.");

        // Read stdout and stderr concurrently — reading them sequentially can deadlock
        // when one pipe's buffer fills while the other is not being drained
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        process.WaitForExit();

        return (stdoutTask.Result, stderrTask.Result, process.ExitCode);
    }

    /// <summary>
    ///     Extracts error-class lines from clang's stderr output, used to populate
    ///     <see cref="CppCompilationResult.Errors"/>.
    /// </summary>
    /// <param name="stderr">The raw stderr text captured from clang.</param>
    /// <returns>A list of lines that contain <c>": error:"</c> or <c>": fatal error:"</c>.</returns>
    private static IReadOnlyList<string> CollectStderrErrors(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return [];
        }

        // Only surface lines that carry an explicit error classification so that warnings
        // about system headers do not trigger the caller's error-logging path
        return stderr.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Contains(": error:", StringComparison.Ordinal) ||
                        l.Contains(": fatal error:", StringComparison.Ordinal))
            .ToList();
    }

    // =========================================================================
    // Ownership helper
    // =========================================================================

    /// <summary>
    ///     Returns the <see cref="StringComparison"/> appropriate for file-system path comparisons
    ///     on the current platform.
    /// </summary>
    /// <remarks>
    ///     Linux file systems are case-sensitive, so <see cref="StringComparison.Ordinal"/> is
    ///     used there. Windows and macOS default to case-insensitive file systems, so
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
    ///     used there to avoid incorrectly treating paths that differ only in case as the same
    ///     file. Windows and macOS default to case-insensitive file systems, so
    ///     <see cref="StringComparer.OrdinalIgnoreCase"/> is used on those platforms.
    /// </remarks>
    private static StringComparer FileSystemPathComparer =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
            ? StringComparer.Ordinal
            : StringComparer.OrdinalIgnoreCase;

    /// <summary>
    ///     Determines whether a source file both falls under one of the configured
    ///     <see cref="CppGeneratorOptions.PublicIncludeRoots"/> entries and was explicitly
    ///     selected as part of the API surface by the <see cref="Parse"/> caller.
    /// </summary>
    /// <remarks>
    ///     Both conditions must be satisfied: the file must be rooted under a public include
    ///     root (first check) and its normalized path must appear in <see cref="_selectedHeaders"/>
    ///     (second check). The second check prevents transitively-included dependency headers
    ///     that happen to live under a public include root from having their declarations
    ///     documented when only specific headers were selected by <c>--api-headers</c>.
    /// </remarks>
    /// <param name="sourceFile">
    ///     The source file path from a clang AST node. May be null or empty for built-in
    ///     or synthesized declarations — those are never considered owned.
    /// </param>
    /// <returns>
    ///     <see langword="true"/> when the file is under a configured include root and its
    ///     normalized absolute path appears in the selected-headers set;
    ///     <see langword="false"/> otherwise.
    /// </returns>
    private bool IsOwned(string? sourceFile)
    {
        if (string.IsNullOrEmpty(sourceFile))
        {
            return false;
        }

        // Normalize the source path to resolve relative segments and mixed separators
        var normalized = Path.GetFullPath(sourceFile);

        // Require the file to be under a public include root AND in the selected-headers set.
        // The selected-headers check excludes transitively-included dependency headers that
        // share the same root but were not explicitly chosen by the caller.
        return _options.PublicIncludeRoots.Any(root =>
        {
            // Append the directory separator so "lib" cannot match "libext"
            var normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, '/') + Path.DirectorySeparatorChar;
            return normalized.StartsWith(normalizedRoot, FileSystemPathComparison);
        })
            && _selectedHeaders.Contains(normalized);
    }

    // =========================================================================
    // AST walking
    // =========================================================================

    /// <summary>
    ///     Walks the <c>TranslationUnitDecl</c> root element and dispatches its
    ///     top-level children to the appropriate parse methods.
    /// </summary>
    /// <param name="root">The root JSON element of the clang AST dump.</param>
    private void WalkTranslationUnit(JsonElement root)
    {
        // The global namespace corresponds to an empty qualified name
        if (!root.TryGetProperty("inner", out var inner))
        {
            return;
        }

        WalkNodes(inner, string.Empty);
    }

    /// <summary>
    ///     Iterates an <c>inner</c> array of clang AST nodes, updating the current-file
    ///     tracking state and dispatching each node to the appropriate handler.
    /// </summary>
    /// <param name="inner">The JSON array of child nodes to iterate.</param>
    /// <param name="nsQualName">
    ///     The fully-qualified C++ namespace name of the enclosing scope (empty string for global).
    /// </param>
    private void WalkNodes(JsonElement inner, string nsQualName)
    {
        foreach (var node in inner.EnumerateArray())
        {
            // Refresh the current-file state from the node's location when the file changes.
            // Nodes without a location file field inherit the tracker value from the previous node.
            UpdateCurrentFile(node);

            var kind = GetKind(node);
            switch (kind)
            {
                case "NamespaceDecl":
                    WalkNamespace(node, nsQualName);
                    break;

                case "ClassTemplateDecl":
                    WalkClassTemplate(node, nsQualName);
                    break;

                case "CXXRecordDecl":
                    // Only process complete definitions (not forward declarations)
                    if (node.TryGetProperty("completeDefinition", out var cd) && cd.GetBoolean())
                    {
                        ParseClass(node, nsQualName, null);
                    }

                    break;

                case "EnumDecl":
                    ParseEnum(node, nsQualName);
                    break;

                case "TypeAliasDecl":
                    ParseTypeAlias(node, nsQualName);
                    break;

                case "FunctionDecl":
                case "FunctionTemplateDecl":
                    ParseFreeFunction(node, nsQualName);
                    break;
            }
        }
    }

    /// <summary>
    ///     Processes a <c>NamespaceDecl</c> node by building the qualified namespace name
    ///     and recursing into its children.
    /// </summary>
    /// <param name="node">The <c>NamespaceDecl</c> JSON node.</param>
    /// <param name="parentNsName">
    ///     The qualified name of the enclosing namespace; empty for top-level namespaces.
    /// </param>
    private void WalkNamespace(JsonElement node, string parentNsName)
    {
        var name = GetName(node);
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        // Build fully-qualified name by joining parent and short name with ::
        var qualName = string.IsNullOrEmpty(parentNsName)
            ? name
            : $"{parentNsName}::{name}";

        if (!node.TryGetProperty("inner", out var inner))
        {
            return;
        }

        WalkNodes(inner, qualName);
    }

    /// <summary>
    ///     Processes a <c>ClassTemplateDecl</c> node by extracting the template type
    ///     parameters and delegating the inner <c>CXXRecordDecl</c> to <see cref="ParseClass"/>.
    /// </summary>
    /// <param name="node">The <c>ClassTemplateDecl</c> JSON node.</param>
    /// <param name="nsQualName">The qualified name of the enclosing namespace.</param>
    private void WalkClassTemplate(JsonElement node, string nsQualName)
    {
        if (!node.TryGetProperty("inner", out var inner))
        {
            return;
        }

        // Collect template type parameters from the wrapper before reaching the record
        var templateParams = new List<CppTemplateParam>();
        var parsedDefinition = false;

        foreach (var child in inner.EnumerateArray())
        {
            UpdateCurrentFile(child);
            var kind = GetKind(child);

            if (kind is "TemplateTypeParmDecl" or "NonTypeTemplateParmDecl" or "TemplateTemplateParmDecl")
            {
                // Record each parameter name; skip unnamed/anonymous parameters
                var tpName = GetName(child);
                if (!string.IsNullOrEmpty(tpName))
                {
                    templateParams.Add(new CppTemplateParam(tpName));
                }
            }
            else if (!parsedDefinition && kind == "CXXRecordDecl" &&
                     child.TryGetProperty("completeDefinition", out var cd) && cd.GetBoolean())
            {
                // Process only the primary template definition; skip partial specializations
                ParseClass(child, nsQualName, templateParams);
                parsedDefinition = true;
            }
        }
    }

    // =========================================================================
    // Declaration parsers
    // =========================================================================

    /// <summary>
    ///     Parses a <c>CXXRecordDecl</c> node into a <see cref="CppClass"/> and adds it
    ///     to the appropriate namespace builder.
    /// </summary>
    /// <remarks>
    ///     This is a thin wrapper over <see cref="BuildClass"/> that adds the resulting
    ///     <see cref="CppClass"/> to the namespace accumulator when parsing succeeds.
    /// </remarks>
    /// <param name="node">The <c>CXXRecordDecl</c> JSON node with <c>completeDefinition: true</c>.</param>
    /// <param name="nsQualName">The qualified name of the enclosing namespace.</param>
    /// <param name="templateParams">
    ///     Template parameters extracted from the enclosing <c>ClassTemplateDecl</c>, or
    ///     <see langword="null"/> for non-template classes.
    /// </param>
    private void ParseClass(
        JsonElement node,
        string nsQualName,
        IReadOnlyList<CppTemplateParam>? templateParams)
    {
        // Delegate to BuildClass for all parsing, then register the result in the namespace builder
        var cls = BuildClass(node, nsQualName, templateParams);
        if (cls != null)
        {
            GetNsBuilder(nsQualName).Classes.Add(cls);
        }
    }

    /// <summary>
    ///     Builds a <see cref="CppClass"/> from a <c>CXXRecordDecl</c> JSON node and returns
    ///     it without adding it to any namespace builder.
    /// </summary>
    /// <remarks>
    ///     Separated from <see cref="ParseClass"/> so that nested class declarations found
    ///     inside a class body can be parsed recursively and collected in the parent class's
    ///     <see cref="CppClass.NestedClasses"/> list rather than added to the namespace builder.
    /// </remarks>
    /// <param name="node">The <c>CXXRecordDecl</c> JSON node with <c>completeDefinition: true</c>.</param>
    /// <param name="nsQualName">
    ///     The qualified name of the enclosing scope (namespace or class). Used as context
    ///     when recursing into nested class bodies.
    /// </param>
    /// <param name="templateParams">
    ///     Template parameters extracted from an enclosing <c>ClassTemplateDecl</c>, or
    ///     <see langword="null"/> for non-template classes.
    /// </param>
    /// <returns>
    ///     A populated <see cref="CppClass"/>, or <see langword="null"/> when the node is
    ///     not owned, is compiler-synthesized, or has no valid name.
    /// </returns>
    private CppClass? BuildClass(
        JsonElement node,
        string nsQualName,
        IReadOnlyList<CppTemplateParam>? templateParams)
    {
        // Skip when the class is not owned by a public include root
        if (!IsOwned(_currentFile))
        {
            return null;
        }

        // Skip compiler-synthesized records (anonymous structs, etc.)
        if (node.TryGetProperty("isImplicit", out var impl) && impl.GetBoolean())
        {
            return null;
        }

        var name = GetName(node);
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        // Determine the default access level: struct → public, class → private
        var tagUsed = node.TryGetProperty("tagUsed", out var tu) ? tu.GetString() : "class";
        var currentAccess = tagUsed == "struct"
            ? CppAccessibility.Public
            : CppAccessibility.Private;

        var members = new List<CppFunction>();
        var fields = new List<CppField>();
        var baseTypes = new List<CppBaseType>();
        var nestedClasses = new List<CppClass>();
        var typeAliases = new List<CppTypeAlias>();
        CppDocComment? doc = null;

        // Deprecated if isDeprecated flag is set; DeprecatedAttr in inner also triggers
        var isDeprecated = node.TryGetProperty("isDeprecated", out var dep) && dep.GetBoolean();

        // Final if FinalAttr appears in the inner node array
        var isFinal = false;
        var location = GetCurrentSourceLocation(node);

        // Clang 18+ surfaces base class information in a top-level "bases" array on the
        // CXXRecordDecl node rather than as CXXBaseSpecifier child nodes inside "inner".
        // Earlier versions emit CXXBaseSpecifier nodes in "inner" (handled below).
        // Both paths are retained so the parser works across clang versions.
        var hasTopLevelBases = false;
        if (node.TryGetProperty("bases", out var bases))
        {
            foreach (var baseName in bases.EnumerateArray()
                .Select(e => e.TryGetProperty("type", out var bt) && bt.TryGetProperty("qualType", out var qtEl)
                    ? qtEl.GetString()
                    : null)
                .Where(n => !string.IsNullOrEmpty(n)))
            {
                baseTypes.Add(new CppBaseType(baseName!));
                hasTopLevelBases = true;
            }
        }

        if (node.TryGetProperty("inner", out var inner))
        {
            foreach (var child in inner.EnumerateArray())
            {
                UpdateCurrentFile(child);
                var kind = GetKind(child);

                switch (kind)
                {
                    case "AccessSpecDecl":
                        // Transition the current access level for all following members
                        currentAccess = ParseAccessSpec(child);
                        break;

                    case "CXXConstructorDecl":
                    case "CXXDestructorDecl":
                    case "CXXMethodDecl":
                        {
                            // ParseMethod returns null for destructors and implicit members
                            var method = ParseMethod(child, currentAccess);
                            if (method != null)
                            {
                                members.Add(method);
                            }

                            break;
                        }

                    case "FieldDecl":
                    case "VarDecl":
                        {
                            // VarDecl covers static constexpr members inside the class body
                            var field = ParseField(child, currentAccess);
                            if (field != null)
                            {
                                fields.Add(field);
                            }

                            break;
                        }

                    case "CXXRecordDecl":
                        HandleNestedCxxRecord(child, nsQualName, name, currentAccess, nestedClasses);
                        break;

                    case "ClassTemplateDecl":
                        HandleClassTemplate(child, nsQualName, name, currentAccess, nestedClasses);
                        break;

                    case "TypeAliasDecl":
                        HandleTypeAliasInClass(child, currentAccess, typeAliases);
                        break;

                    case "CXXBaseSpecifier":
                        HandleCxxBaseSpecifier(child, hasTopLevelBases, baseTypes);
                        break;

                    case "FullComment":
                        doc = ParseFullComment(child);
                        break;

                    case "DeprecatedAttr":
                        isDeprecated = true;
                        break;

                    case "FinalAttr":
                        isFinal = true;
                        break;
                }
            }
        }

        return new CppClass(
            name,
            baseTypes,
            templateParams ?? [],
            members,
            fields,
            nestedClasses,
            typeAliases,
            isDeprecated,
            isFinal,
            location,
            doc);
    }

    /// <summary>
    ///     Processes a <c>CXXRecordDecl</c> child node within a class body, recursively
    ///     building a nested class and adding it to <paramref name="nestedClasses"/> when
    ///     the current access level is public and the definition is complete.
    /// </summary>
    /// <param name="child">The <c>CXXRecordDecl</c> JSON child node.</param>
    /// <param name="nsQualName">The qualified name of the enclosing scope (used for recursion context).</param>
    /// <param name="name">The name of the enclosing class (used to build the nested scope name).</param>
    /// <param name="currentAccess">The current access level at the point of this declaration.</param>
    /// <param name="nestedClasses">Accumulator list that receives any successfully parsed nested class.</param>
    private void HandleNestedCxxRecord(
        JsonElement child,
        string nsQualName,
        string name,
        CppAccessibility currentAccess,
        List<CppClass> nestedClasses)
    {
        // Recursively collect public nested classes with complete definitions;
        // implicit and forward declarations are excluded by BuildClass itself
        if (currentAccess == CppAccessibility.Public &&
            child.TryGetProperty("completeDefinition", out var ncd) && ncd.GetBoolean())
        {
            var nestedCls = BuildClass(child, $"{nsQualName}::{name}", null);
            if (nestedCls != null)
            {
                nestedClasses.Add(nestedCls);
            }
        }
    }

    /// <summary>
    ///     Processes a <c>ClassTemplateDecl</c> child node within a class body, extracting
    ///     template parameters and recursively building the primary template class, then
    ///     adding it to <paramref name="nestedClasses"/> when access permits.
    /// </summary>
    /// <remarks>
    ///     Only the primary template definition is processed; specializations (nodes beyond
    ///     the first <c>CXXRecordDecl</c> with <c>completeDefinition</c>) are skipped so
    ///     that template instantiation details do not pollute the API documentation.
    /// </remarks>
    /// <param name="child">The <c>ClassTemplateDecl</c> JSON child node.</param>
    /// <param name="nsQualName">The qualified name of the enclosing scope.</param>
    /// <param name="name">The name of the enclosing class.</param>
    /// <param name="currentAccess">The current access level at the point of this declaration.</param>
    /// <param name="nestedClasses">Accumulator list that receives the parsed nested template class.</param>
    private void HandleClassTemplate(
        JsonElement child,
        string nsQualName,
        string name,
        CppAccessibility currentAccess,
        List<CppClass> nestedClasses)
    {
        // Handle nested template classes; only collect public ones
        if (currentAccess != CppAccessibility.Public ||
            !child.TryGetProperty("inner", out var tmplInner))
        {
            return;
        }

        // Gather template type parameters before reaching the CXXRecordDecl child
        var tmplParams = new List<CppTemplateParam>();
        var tmplParsed = false;
        foreach (var tmplChild in tmplInner.EnumerateArray())
        {
            UpdateCurrentFile(tmplChild);
            var tmplKind = GetKind(tmplChild);
            if (tmplKind is "TemplateTypeParmDecl" or "NonTypeTemplateParmDecl"
                or "TemplateTemplateParmDecl")
            {
                var tpName = GetName(tmplChild);
                if (!string.IsNullOrEmpty(tpName))
                {
                    tmplParams.Add(new CppTemplateParam(tpName));
                }
            }
            else if (!tmplParsed && tmplKind == "CXXRecordDecl" &&
                     tmplChild.TryGetProperty("completeDefinition", out var tcd) &&
                     tcd.GetBoolean())
            {
                // Process only the primary template definition; skip specializations
                var nestedCls = BuildClass(tmplChild, $"{nsQualName}::{name}", tmplParams);
                if (nestedCls != null)
                {
                    nestedClasses.Add(nestedCls);
                }

                tmplParsed = true;
            }
        }
    }

    /// <summary>
    ///     Processes a <c>TypeAliasDecl</c> child node within a class body, collecting the
    ///     alias name, underlying type, doc comment, and deprecation status, then appending
    ///     the result to <paramref name="typeAliases"/> when the current access is public.
    /// </summary>
    /// <param name="child">The <c>TypeAliasDecl</c> JSON child node.</param>
    /// <param name="currentAccess">The current access level at the point of this declaration.</param>
    /// <param name="typeAliases">Accumulator list that receives the parsed type alias.</param>
    private void HandleTypeAliasInClass(
        JsonElement child,
        CppAccessibility currentAccess,
        List<CppTypeAlias> typeAliases)
    {
        // Collect public class-scoped using-aliases and parse their doc comments
        if (currentAccess != CppAccessibility.Public)
        {
            return;
        }

        var aliasName = GetName(child);
        if (string.IsNullOrEmpty(aliasName))
        {
            return;
        }

        // Underlying type lives in child["type"]["qualType"]
        var underlyingType = string.Empty;
        if (child.TryGetProperty("type", out var typeNode) &&
            typeNode.TryGetProperty("qualType", out var qualTypeNode))
        {
            underlyingType = qualTypeNode.GetString() ?? string.Empty;
        }

        var aliasIsDeprecated =
            child.TryGetProperty("isDeprecated", out var aliasDepEl) &&
            aliasDepEl.GetBoolean();
        var aliasLocation = GetCurrentSourceLocation(child);
        CppDocComment? aliasDoc = null;

        if (child.TryGetProperty("inner", out var aliasInner))
        {
            foreach (var aliasChild in aliasInner.EnumerateArray())
            {
                var aliasChildKind = GetKind(aliasChild);
                switch (aliasChildKind)
                {
                    case "FullComment":
                        aliasDoc = ParseFullComment(aliasChild);
                        break;
                    case "DeprecatedAttr":
                        aliasIsDeprecated = true;
                        break;
                }
            }
        }

        typeAliases.Add(new CppTypeAlias(
            aliasName, underlyingType, aliasIsDeprecated, aliasLocation, aliasDoc));
    }

    /// <summary>
    ///     Processes a <c>CXXBaseSpecifier</c> child node for clang versions older than 18,
    ///     appending the base type name to <paramref name="baseTypes"/> when the top-level
    ///     <c>bases</c> array was not already populated.
    /// </summary>
    /// <remarks>
    ///     Clang 18+ surfaces base class information in a top-level <c>bases</c> array on
    ///     the <c>CXXRecordDecl</c> node. This fallback handles older clang versions that
    ///     emit <c>CXXBaseSpecifier</c> nodes inside <c>inner</c>. Skipped when
    ///     <paramref name="hasTopLevelBases"/> is <see langword="true"/> to avoid duplicates.
    /// </remarks>
    /// <param name="child">The <c>CXXBaseSpecifier</c> JSON child node.</param>
    /// <param name="hasTopLevelBases">Whether base types were already collected from the top-level array.</param>
    /// <param name="baseTypes">Accumulator list that receives the parsed base type.</param>
    private static void HandleCxxBaseSpecifier(
        JsonElement child,
        bool hasTopLevelBases,
        List<CppBaseType> baseTypes)
    {
        // Fallback for clang versions older than 18 that emit base specifiers
        // as CXXBaseSpecifier child nodes in "inner" rather than in a top-level
        // "bases" array. Skipped when base types were already collected from the
        // top-level "bases" array to avoid duplicates across clang versions.
        if (!hasTopLevelBases &&
            child.TryGetProperty("type", out var bt) &&
            bt.TryGetProperty("qualType", out var qtEl))
        {
            var baseName = qtEl.GetString();
            if (!string.IsNullOrEmpty(baseName))
            {
                baseTypes.Add(new CppBaseType(baseName));
            }
        }
    }

    /// <summary>
    ///     Parses a <c>FunctionDecl</c> or <c>FunctionTemplateDecl</c> node into a
    ///     <see cref="CppFunction"/> and adds it to the appropriate namespace builder.
    /// </summary>
    /// <remarks>
    ///     For <c>FunctionTemplateDecl</c>, the method unwraps the inner <c>FunctionDecl</c>
    ///     and processes it directly; template parameters are not tracked for free functions.
    /// </remarks>
    /// <param name="node">The <c>FunctionDecl</c> or <c>FunctionTemplateDecl</c> JSON node.</param>
    /// <param name="nsQualName">The qualified name of the enclosing namespace.</param>
    private void ParseFreeFunction(JsonElement node, string nsQualName)
    {
        // Unwrap FunctionTemplateDecl: find its inner FunctionDecl and re-enter
        if (GetKind(node) == "FunctionTemplateDecl")
        {
            if (!node.TryGetProperty("inner", out var templateInner))
            {
                return;
            }

            foreach (var child in templateInner.EnumerateArray())
            {
                UpdateCurrentFile(child);
                if (GetKind(child) == "FunctionDecl")
                {
                    ParseFreeFunction(child, nsQualName);
                    return; // Process only the primary definition
                }
            }

            return;
        }

        // Skip when not owned by a public include root
        if (!IsOwned(_currentFile))
        {
            return;
        }

        // Skip compiler-synthesized functions
        if (node.TryGetProperty("isImplicit", out var impl) && impl.GetBoolean())
        {
            return;
        }

        var name = GetName(node);
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        var qualType = GetQualType(node);
        var returnTypeName = ExtractReturnType(qualType);

        var isVariadic = node.TryGetProperty("variadic", out var v) && v.GetBoolean();
        var isDeprecated = node.TryGetProperty("isDeprecated", out var dep) && dep.GetBoolean();
        var isDeleted = node.TryGetProperty("explicitlyDeleted", out var del) && del.GetBoolean();
        var location = GetCurrentSourceLocation(node);
        var parameters = new List<CppParameter>();
        CppDocComment? doc = null;

        if (node.TryGetProperty("inner", out var inner))
        {
            foreach (var child in inner.EnumerateArray())
            {
                var childKind = GetKind(child);
                switch (childKind)
                {
                    case "ParmVarDecl":
                        parameters.Add(ParseParameter(child));
                        break;
                    case "FullComment":
                        doc = ParseFullComment(child);
                        break;
                    case "DeprecatedAttr":
                        isDeprecated = true;
                        break;
                }
            }
        }

        var fn = new CppFunction(
            name,
            returnTypeName,
            parameters,
            CppAccessibility.Public,
            false,
            false,
            false,
            isVariadic,
            isDeprecated,
            isDeleted,
            location,
            doc);

        GetNsBuilder(nsQualName).FreeFunctions.Add(fn);
    }

    /// <summary>
    ///     Parses an <c>EnumDecl</c> node into a <see cref="CppEnum"/> and adds it to
    ///     the appropriate namespace builder.
    /// </summary>
    /// <param name="node">The <c>EnumDecl</c> JSON node.</param>
    /// <param name="nsQualName">The qualified name of the enclosing namespace.</param>
    private void ParseEnum(JsonElement node, string nsQualName)
    {
        // Skip when not owned by a public include root
        if (!IsOwned(_currentFile))
        {
            return;
        }

        var name = GetName(node);
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        var isDeprecated = node.TryGetProperty("isDeprecated", out var dep) && dep.GetBoolean();
        var location = GetCurrentSourceLocation(node);
        var values = new List<CppEnumValue>();
        CppDocComment? doc = null;

        if (node.TryGetProperty("inner", out var inner))
        {
            foreach (var child in inner.EnumerateArray())
            {
                var childKind = GetKind(child);
                switch (childKind)
                {
                    case "EnumConstantDecl":
                        var valueName = GetName(child);
                        if (!string.IsNullOrEmpty(valueName))
                        {
                            // Each enumerator may carry its own doc comment inside its inner array
                            CppDocComment? valueDoc = null;
                            if (child.TryGetProperty("inner", out var valueInner))
                            {
                                var fullCommentNode = valueInner.EnumerateArray()
                                    .Where(vc => GetKind(vc) == "FullComment")
                                    .Select(vc => ParseFullComment(vc))
                                    .FirstOrDefault();
                                valueDoc = fullCommentNode;
                            }

                            values.Add(new CppEnumValue(valueName, valueDoc));
                        }

                        break;
                    case "FullComment":
                        doc = ParseFullComment(child);
                        break;
                    case "DeprecatedAttr":
                        isDeprecated = true;
                        break;
                }
            }
        }

        var cppEnum = new CppEnum(name, values, isDeprecated, location, doc);
        GetNsBuilder(nsQualName).Enums.Add(cppEnum);
    }

    /// <summary>
    ///     Parses a <c>TypeAliasDecl</c> node (a <c>using X = Y</c> declaration) into a
    ///     <see cref="CppTypeAlias"/> and adds it to the appropriate namespace builder.
    /// </summary>
    /// <param name="node">The <c>TypeAliasDecl</c> JSON node.</param>
    /// <param name="nsQualName">The qualified name of the enclosing namespace.</param>
    private void ParseTypeAlias(JsonElement node, string nsQualName)
    {
        // Skip when not owned by a public include root
        if (!IsOwned(_currentFile))
        {
            return;
        }

        var name = GetName(node);
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        // The underlying type is in node["type"]["qualType"]
        var underlyingType = string.Empty;
        if (node.TryGetProperty("type", out var typeNode) &&
            typeNode.TryGetProperty("qualType", out var qualTypeNode))
        {
            underlyingType = qualTypeNode.GetString() ?? string.Empty;
        }

        var isDeprecated = node.TryGetProperty("isDeprecated", out var dep) && dep.GetBoolean();
        var location = GetCurrentSourceLocation(node);
        CppDocComment? doc = null;

        if (node.TryGetProperty("inner", out var inner))
        {
            foreach (var child in inner.EnumerateArray())
            {
                var childKind = GetKind(child);
                switch (childKind)
                {
                    case "FullComment":
                        doc = ParseFullComment(child);
                        break;
                    case "DeprecatedAttr":
                        isDeprecated = true;
                        break;
                }
            }
        }

        var alias = new CppTypeAlias(name, underlyingType, isDeprecated, location, doc);
        GetNsBuilder(nsQualName).TypeAliases.Add(alias);
    }

    /// <summary>
    ///     Parses a <c>CXXMethodDecl</c>, <c>CXXConstructorDecl</c>, or
    ///     <c>CXXDestructorDecl</c> node into a <see cref="CppFunction"/>.
    /// </summary>
    /// <remarks>
    ///     Returns <see langword="null"/> for destructors (not documented) and for
    ///     compiler-synthesized implicit members.
    /// </remarks>
    /// <param name="node">The method JSON node.</param>
    /// <param name="accessibility">The current access level at the point of declaration.</param>
    /// <returns>
    ///     A populated <see cref="CppFunction"/>, or <see langword="null"/> for destructors
    ///     and implicit members.
    /// </returns>
    private CppFunction? ParseMethod(JsonElement node, CppAccessibility accessibility)
    {
        var kind = GetKind(node);

        // Destructors carry no useful documentation surface — skip them
        if (kind == "CXXDestructorDecl")
        {
            return null;
        }

        // Compiler-synthesized copy/move constructors and assignment operators are not documented
        if (node.TryGetProperty("isImplicit", out var impl) && impl.GetBoolean())
        {
            return null;
        }

        var name = GetName(node);
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        var isConstructor = kind == "CXXConstructorDecl";

        // Constructors have no meaningful return type — represent as void
        string returnTypeName;
        if (isConstructor)
        {
            returnTypeName = "void";
        }
        else
        {
            // Method type.qualType is "ReturnType (ArgType1, ArgType2)"
            var qualType = GetQualType(node);
            returnTypeName = ExtractReturnType(qualType);
        }

        var isStatic = node.TryGetProperty("storageClass", out var sc) && sc.GetString() == "static";
        var isVirtual = node.TryGetProperty("isVirtual", out var iv) && iv.GetBoolean();
        var isVariadic = node.TryGetProperty("variadic", out var varNode) && varNode.GetBoolean();
        var isDeprecated = node.TryGetProperty("isDeprecated", out var dep) && dep.GetBoolean();
        var isDeleted = node.TryGetProperty("explicitlyDeleted", out var del) && del.GetBoolean();

        // Source location: record the file and line from the node's own loc field
        var location = GetCurrentSourceLocation(node);
        var parameters = new List<CppParameter>();
        CppDocComment? doc = null;

        if (node.TryGetProperty("inner", out var inner))
        {
            foreach (var child in inner.EnumerateArray())
            {
                var childKind = GetKind(child);
                switch (childKind)
                {
                    case "ParmVarDecl":
                        parameters.Add(ParseParameter(child));
                        break;
                    case "FullComment":
                        doc = ParseFullComment(child);
                        break;
                    case "DeprecatedAttr":
                        isDeprecated = true;
                        break;
                }
            }
        }

        return new CppFunction(
            name,
            returnTypeName,
            parameters,
            accessibility,
            isStatic,
            isVirtual,
            isConstructor,
            isVariadic,
            isDeprecated,
            isDeleted,
            location,
            doc);
    }

    /// <summary>
    ///     Parses a <c>FieldDecl</c> or <c>VarDecl</c> node into a <see cref="CppField"/>.
    /// </summary>
    /// <remarks>
    ///     <c>VarDecl</c> inside a class body represents a <c>static constexpr</c> or
    ///     <c>static inline</c> member variable, which is documented identically to a
    ///     regular field.
    /// </remarks>
    /// <param name="node">The field JSON node.</param>
    /// <param name="accessibility">The current access level at the point of declaration.</param>
    /// <returns>A populated <see cref="CppField"/>, or <see langword="null"/> for implicit members.</returns>
    private static CppField? ParseField(JsonElement node, CppAccessibility accessibility)
    {
        // Skip compiler-synthesized fields
        if (node.TryGetProperty("isImplicit", out var impl) && impl.GetBoolean())
        {
            return null;
        }

        var name = GetName(node);
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        // VarDecl inside a class body is always a static member
        var kind = GetKind(node);
        var isStatic = kind == "VarDecl" ||
                       (node.TryGetProperty("storageClass", out var sc) && sc.GetString() == "static");

        var typeName = node.TryGetProperty("type", out var t) &&
                       t.TryGetProperty("qualType", out var qt)
            ? qt.GetString() ?? string.Empty
            : string.Empty;

        var isDeprecated = node.TryGetProperty("isDeprecated", out var dep) && dep.GetBoolean();
        CppDocComment? doc = null;

        if (node.TryGetProperty("inner", out var inner))
        {
            foreach (var child in inner.EnumerateArray())
            {
                var childKind = GetKind(child);
                switch (childKind)
                {
                    case "FullComment":
                        doc = ParseFullComment(child);
                        break;
                    case "DeprecatedAttr":
                        isDeprecated = true;
                        break;
                }
            }
        }

        return new CppField(name, typeName, accessibility, isStatic, isDeprecated, null, doc);
    }

    /// <summary>
    ///     Parses a <c>ParmVarDecl</c> node into a <see cref="CppParameter"/>.
    /// </summary>
    /// <param name="node">The <c>ParmVarDecl</c> JSON node.</param>
    /// <returns>A <see cref="CppParameter"/> with the parameter name, type, and optional default value.</returns>
    private static CppParameter ParseParameter(JsonElement node)
    {
        var name = GetName(node) ?? string.Empty;
        var typeName = node.TryGetProperty("type", out var t) &&
                       t.TryGetProperty("qualType", out var qt)
            ? qt.GetString() ?? string.Empty
            : string.Empty;

        // When "init" is present the parameter has a default argument; extract a display string
        // from the first child expression node
        string? defaultValue = null;
        if (node.TryGetProperty("init", out _) &&
            node.TryGetProperty("inner", out var innerEl) &&
            innerEl.GetArrayLength() > 0)
        {
            defaultValue = ExtractDefaultValue(innerEl[0]);
        }

        return new CppParameter(name, typeName, defaultValue);
    }

    /// <summary>
    ///     Recursively extracts a display string for a default-argument expression node.
    ///     Handles integer, floating-point, boolean, string, and nullptr literals, as well
    ///     as implicit cast wrappers that the clang AST inserts around some literals.
    ///     Returns <see langword="null"/> when the expression is too complex to represent
    ///     as a simple display string.
    /// </summary>
    /// <param name="node">The root expression node from the <c>inner</c> array of a <c>ParmVarDecl</c>.</param>
    /// <returns>A display string for the default value, or <see langword="null"/>.</returns>
    private static string? ExtractDefaultValue(JsonElement node)
    {
        var kind = node.TryGetProperty("kind", out var k) ? k.GetString() : null;

        return kind switch
        {
            // Numeric literals carry their value as a JSON string
            "IntegerLiteral" or "FloatingLiteral" =>
                node.TryGetProperty("value", out var v) ? v.GetString() : null,

            // Boolean literals carry their value as a JSON boolean, not a string
            "CXXBoolLiteralExpr" =>
                node.TryGetProperty("value", out var bv)
                    ? bv.ValueKind switch
                    {
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        _ => bv.GetString(), // fallback for unexpected encoding
                    }
                    : null,

            // String literals carry their value (already includes surrounding quotes)
            "StringLiteral" =>
                node.TryGetProperty("value", out var sv) ? sv.GetString() : null,

            // nullptr literal
            "CXXNullPtrLiteralExpr" => "nullptr",

            // Named constant or enum value — use the referenced declaration name
            "DeclRefExpr" =>
                node.TryGetProperty("referencedDecl", out var rd) &&
                rd.TryGetProperty("name", out var rn) ? rn.GetString() : null,

            // Implicit casts and other wrapper expressions — recurse into first child
            "ImplicitCastExpr" or "CStyleCastExpr" or "CXXStaticCastExpr"
                or "CXXFunctionalCastExpr" or "MaterializeTemporaryExpr"
                or "ExprWithCleanups" or "CXXBindTemporaryExpr" =>
                node.TryGetProperty("inner", out var inner) && inner.GetArrayLength() > 0
                    ? ExtractDefaultValue(inner[0])
                    : null,

            // Unary operator (e.g. -1) — reconstruct from operator token and child
            "UnaryOperator" =>
                node.TryGetProperty("opcode", out var op) &&
                node.TryGetProperty("inner", out var uInner) && uInner.GetArrayLength() > 0
                    ? $"{op.GetString()}{ExtractDefaultValue(uInner[0])}"
                    : null,

            // All other expressions are too complex to display simply
            _ => null,
        };
    }

    // =========================================================================
    // Access specifier helper
    // =========================================================================

    /// <summary>
    ///     Reads the <c>access</c> field of an <c>AccessSpecDecl</c> node and converts it
    ///     to the corresponding <see cref="CppAccessibility"/> value.
    /// </summary>
    /// <param name="node">The <c>AccessSpecDecl</c> JSON node.</param>
    /// <returns>
    ///     <see cref="CppAccessibility.Public"/>, <see cref="CppAccessibility.Protected"/>,
    ///     or <see cref="CppAccessibility.Private"/>.
    /// </returns>
    private static CppAccessibility ParseAccessSpec(JsonElement node)
    {
        var access = node.TryGetProperty("access", out var acc) ? acc.GetString() : null;
        return access switch
        {
            "public" => CppAccessibility.Public,
            "protected" => CppAccessibility.Protected,
            _ => CppAccessibility.Private,
        };
    }

    // =========================================================================
    // Doc comment extraction
    // =========================================================================

    /// <summary>
    ///     Converts a <c>FullComment</c> JSON node into a <see cref="CppDocComment"/>.
    /// </summary>
    /// <remarks>
    ///     Recognizes <c>@brief</c> (or first plain paragraph) as the summary,
    ///     <c>@details</c> / <c>@remarks</c> as extended details, <c>@param</c> as parameter
    ///     docs, and <c>@return</c> / <c>@returns</c> as the return description. Text is
    ///     collected recursively from nested <c>TextComment</c> nodes.
    /// </remarks>
    /// <param name="node">The <c>FullComment</c> JSON node.</param>
    /// <returns>
    ///     A populated <see cref="CppDocComment"/>, or <see langword="null"/> when no
    ///     meaningful content is found.
    /// </returns>
    private static CppDocComment? ParseFullComment(JsonElement node)
    {
        if (!node.TryGetProperty("inner", out var inner))
        {
            return null;
        }

        string? summary = null;
        string? details = null;
        string? returns = null;
        string? note = null;
        string? example = null;
        var paramDocs = new List<CppParamDoc>();

        foreach (var child in inner.EnumerateArray())
        {
            var kind = child.TryGetProperty("kind", out var k) ? k.GetString() : null;

            switch (kind)
            {
                case "ParagraphComment":
                    // A plain paragraph without a preceding command becomes the summary when
                    // no explicit @brief has been found yet
                    if (summary == null)
                    {
                        var text = CollectText(child);
                        if (!string.IsNullOrEmpty(text))
                        {
                            summary = NormalizeSingleLine(text);
                        }
                    }

                    break;

                case "BlockCommandComment":
                    HandleBlockCommandComment(child, ref summary, ref details, ref returns, ref note);
                    break;

                case "ParamCommandComment":
                    HandleParamCommandComment(child, paramDocs);
                    break;

                case "VerbatimBlockComment":
                    var blockText = CollectVerbatimBlockText(child);
                    if (!string.IsNullOrEmpty(blockText))
                    {
                        example = blockText;
                    }

                    break;
            }
        }

        // Return null when the comment block carried no extractable documentation
        if (summary == null && details == null && returns == null && note == null && example == null && paramDocs.Count == 0)
        {
            return null;
        }

        return new CppDocComment(summary, details, paramDocs, returns, note, example);
    }

    /// <summary>
    ///     Processes a single <c>BlockCommandComment</c> child node, updating the
    ///     <paramref name="summary"/>, <paramref name="details"/>, <paramref name="returns"/>,
    ///     and <paramref name="note"/> accumulators based on the command name.
    /// </summary>
    /// <remarks>
    ///     Recognized commands: <c>@brief</c> (overrides plain paragraph summary),
    ///     <c>@details</c> / <c>@remarks</c> (extended description),
    ///     <c>@return</c> / <c>@returns</c> (return description), and
    ///     <c>@note</c> (blockquote note). Unknown commands are silently ignored.
    /// </remarks>
    /// <param name="child">The <c>BlockCommandComment</c> JSON node to process.</param>
    /// <param name="summary">Current summary accumulator; updated when <c>@brief</c> is found.</param>
    /// <param name="details">Current details accumulator; updated when <c>@details</c>/<c>@remarks</c> is found.</param>
    /// <param name="returns">Current returns accumulator; updated when <c>@return</c>/<c>@returns</c> is found.</param>
    /// <param name="note">Current note accumulator; updated when <c>@note</c> is found.</param>
    private static void HandleBlockCommandComment(
        JsonElement child,
        ref string? summary,
        ref string? details,
        ref string? returns,
        ref string? note)
    {
        var cmdName = child.TryGetProperty("name", out var cn) ? cn.GetString() : null;
        var cmdText = CollectText(child);

        if (string.Equals(cmdName, "brief", StringComparison.OrdinalIgnoreCase))
        {
            // @brief overrides any plain paragraph already captured as summary
            if (!string.IsNullOrEmpty(cmdText))
            {
                summary = NormalizeSingleLine(cmdText);
            }
        }
        else if (string.Equals(cmdName, "details", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(cmdName, "remarks", StringComparison.OrdinalIgnoreCase))
        {
            // Preserve internal structure for @details so multi-sentence text reads well
            if (!string.IsNullOrEmpty(cmdText))
            {
                details = cmdText.Trim();
            }
        }
        else if ((string.Equals(cmdName, "return", StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(cmdName, "returns", StringComparison.OrdinalIgnoreCase)) &&
                 !string.IsNullOrEmpty(cmdText))
        {
            returns = NormalizeSingleLine(cmdText);
        }
        else if (string.Equals(cmdName, "note", StringComparison.OrdinalIgnoreCase) &&
                 !string.IsNullOrEmpty(cmdText))
        {
            note = NormalizeSingleLine(cmdText);
        }
    }

    /// <summary>
    ///     Processes a single <c>ParamCommandComment</c> child node, appending a new
    ///     <see cref="CppParamDoc"/> entry to <paramref name="paramDocs"/> when both a
    ///     parameter name and description text are present.
    /// </summary>
    /// <param name="child">The <c>ParamCommandComment</c> JSON node to process.</param>
    /// <param name="paramDocs">The accumulator list to append to when a valid param is found.</param>
    private static void HandleParamCommandComment(
        JsonElement child,
        List<CppParamDoc> paramDocs)
    {
        // @param name Description — the param name comes from the "param" property
        var paramName = child.TryGetProperty("param", out var pn) ? pn.GetString() : null;
        var paramText = CollectText(child);
        if (!string.IsNullOrEmpty(paramName) && !string.IsNullOrEmpty(paramText))
        {
            paramDocs.Add(new CppParamDoc(paramName!, NormalizeSingleLine(paramText)));
        }
    }

    /// <summary>
    ///     Recursively collects all <c>TextComment</c> leaf node text values from a node
    ///     subtree and joins them with a single space.
    /// </summary>
    /// <param name="node">The JSON node whose subtree to harvest for text.</param>
    /// <returns>
    ///     A joined string of all text node values, or <see langword="null"/> when none
    ///     are found.
    /// </returns>
    private static string? CollectText(JsonElement node)
    {
        var parts = new List<string>();
        CollectTextNodes(node, parts);
        if (parts.Count == 0)
        {
            return null;
        }

        return string.Join(" ", parts.Select(p => p.Trim()).Where(p => !string.IsNullOrEmpty(p)));
    }

    /// <summary>
    ///     Depth-first recursion that appends every <c>TextComment</c> text value to
    ///     <paramref name="parts"/>, descending into all non-text child nodes.
    /// </summary>
    /// <param name="node">The current JSON node to inspect.</param>
    /// <param name="parts">The list to append text values to.</param>
    private static void CollectTextNodes(JsonElement node, List<string> parts)
    {
        if (!node.TryGetProperty("inner", out var inner))
        {
            return;
        }

        foreach (var child in inner.EnumerateArray())
        {
            var kind = child.TryGetProperty("kind", out var k) ? k.GetString() : null;
            if (kind == "TextComment")
            {
                // Append the raw text — leading/trailing whitespace is trimmed by the caller
                if (child.TryGetProperty("text", out var textEl))
                {
                    parts.Add(textEl.GetString() ?? string.Empty);
                }
            }
            else
            {
                // Descend into any non-text node to find nested TextComments
                CollectTextNodes(child, parts);
            }
        }
    }

    /// <summary>
    ///     Collects the source lines from a <c>VerbatimBlockComment</c> node (produced by
    ///     Doxygen <c>@code</c>/<c>@endcode</c> blocks) and joins them into a single string.
    /// </summary>
    /// <param name="node">The <c>VerbatimBlockComment</c> JSON node.</param>
    /// <returns>
    ///     The joined code text, trimmed of leading/trailing blank lines, or
    ///     <see langword="null"/> when the block contains no lines.
    /// </returns>
    private static string? CollectVerbatimBlockText(JsonElement node)
    {
        if (!node.TryGetProperty("inner", out var inner))
        {
            return null;
        }

        var lines = new List<string>();
        foreach (var child in inner.EnumerateArray())
        {
            var kind = child.TryGetProperty("kind", out var k) ? k.GetString() : null;
            if (kind == "VerbatimBlockLineComment" &&
                child.TryGetProperty("text", out var textEl))
            {
                lines.Add(textEl.GetString() ?? string.Empty);
            }
        }

        if (lines.Count == 0)
        {
            return null;
        }

        return string.Join("\n", lines).Trim();
    }

    /// <summary>
    ///     Collapses a multi-line string into a single space-separated line by splitting on
    ///     line-break and tab characters and rejoining with single spaces.
    /// </summary>
    /// <param name="text">The text to normalize.</param>
    /// <returns>A single-line string with internal whitespace runs collapsed.</returns>
    private static string NormalizeSingleLine(string text)
    {
        return string.Join(
            " ",
            text.Split(['\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s)));
    }

    // =========================================================================
    // Type extraction helpers
    // =========================================================================

    /// <summary>
    ///     Extracts the return type from a clang function <c>type.qualType</c> string of the
    ///     form <c>"ReturnType (ArgType1, ArgType2)"</c>.
    /// </summary>
    /// <remarks>
    ///     Scans the string from the right to find the outermost opening parenthesis that
    ///     matches the trailing closing parenthesis. Everything to the left of that
    ///     parenthesis (trimmed) is the return type. This correctly handles templates in the
    ///     return type such as <c>"std::pair&lt;int,int&gt; (int)"</c>.
    /// </remarks>
    /// <param name="qualType">The <c>type.qualType</c> value from a function AST node.</param>
    /// <returns>
    ///     The extracted return type string, or the full <paramref name="qualType"/> as a
    ///     fallback when the expected format is not found.
    /// </returns>
    private static string ExtractReturnType(string qualType)
    {
        if (string.IsNullOrEmpty(qualType))
        {
            return qualType;
        }

        // Scan from the right, tracking parenthesis depth to find the outermost '('
        var depth = 0;
        for (var i = qualType.Length - 1; i >= 0; i--)
        {
            if (qualType[i] == ')')
            {
                depth++;
            }
            else if (qualType[i] == '(')
            {
                depth--;
                if (depth == 0)
                {
                    // Everything before the opening parenthesis (with trailing space trimmed)
                    return qualType[..i].TrimEnd();
                }
            }
        }

        // Fallback: return the whole string if the expected format was not found
        return qualType;
    }

    // =========================================================================
    // Location helpers
    // =========================================================================

    /// <summary>
    ///     Updates <see cref="_currentFile"/> from the <c>loc.file</c> (or
    ///     <c>loc.spellingLoc.file</c> for macro expansions) field of a node when present.
    /// </summary>
    /// <remarks>
    ///     The clang JSON AST omits <c>loc.file</c> on nodes that reside in the same file as
    ///     the previous node. This method must be called in document-order for each node so
    ///     that <see cref="_currentFile"/> always reflects the correct file.
    /// </remarks>
    /// <param name="node">The JSON node whose <c>loc</c> property to inspect.</param>
    private void UpdateCurrentFile(JsonElement node)
    {
        if (!node.TryGetProperty("loc", out var loc))
        {
            return;
        }

        // Direct file field — the common case
        if (loc.TryGetProperty("file", out var fileEl))
        {
            _currentFile = fileEl.GetString() ?? _currentFile;
            return;
        }

        // spellingLoc.file — used when the declaration is at a macro call site
        if (loc.TryGetProperty("spellingLoc", out var spellingLoc) &&
            spellingLoc.TryGetProperty("file", out var spellingFile))
        {
            _currentFile = spellingFile.GetString() ?? _currentFile;
        }
    }

    /// <summary>
    ///     Builds a <see cref="CppSourceLocation"/> for the current node using
    ///     <see cref="_currentFile"/> and the line number from the node's <c>loc</c>
    ///     property when available.
    /// </summary>
    /// <param name="node">The JSON node to extract the line number from.</param>
    /// <returns>
    ///     A <see cref="CppSourceLocation"/>, or <see langword="null"/> when
    ///     <see cref="_currentFile"/> is empty.
    /// </returns>
    private CppSourceLocation? GetCurrentSourceLocation(JsonElement node)
    {
        if (string.IsNullOrEmpty(_currentFile))
        {
            return null;
        }

        var line = 0;
        if (node.TryGetProperty("loc", out var loc))
        {
            if (loc.TryGetProperty("line", out var lineEl))
            {
                line = lineEl.GetInt32();
            }
            else if (loc.TryGetProperty("spellingLoc", out var spelling) &&
                     spelling.TryGetProperty("line", out var spLineEl))
            {
                line = spLineEl.GetInt32();
            }
        }

        return new CppSourceLocation(_currentFile, line);
    }

    // =========================================================================
    // JSON helper utilities
    // =========================================================================

    /// <summary>
    ///     Returns the <c>kind</c> string from a JSON AST node, or an empty string
    ///     when the property is absent.
    /// </summary>
    /// <param name="node">The JSON element to inspect.</param>
    /// <returns>The kind string, or an empty string.</returns>
    private static string GetKind(JsonElement node)
    {
        return node.TryGetProperty("kind", out var k) ? k.GetString() ?? string.Empty : string.Empty;
    }

    /// <summary>
    ///     Returns the <c>name</c> string from a JSON AST node, or <see langword="null"/>
    ///     when absent.
    /// </summary>
    /// <param name="node">The JSON element to inspect.</param>
    /// <returns>The name string, or <see langword="null"/>.</returns>
    private static string? GetName(JsonElement node)
    {
        return node.TryGetProperty("name", out var n) ? n.GetString() : null;
    }

    /// <summary>
    ///     Returns the <c>type.qualType</c> string from a JSON AST node, or an empty
    ///     string when absent.
    /// </summary>
    /// <param name="node">The JSON element to inspect.</param>
    /// <returns>The qualified type string, or an empty string.</returns>
    private static string GetQualType(JsonElement node)
    {
        return node.TryGetProperty("type", out var t) &&
               t.TryGetProperty("qualType", out var qt)
            ? qt.GetString() ?? string.Empty
            : string.Empty;
    }

    // =========================================================================
    // Namespace builder management
    // =========================================================================

    /// <summary>
    ///     Returns the <see cref="NamespaceBuilder"/> for the given qualified name,
    ///     creating a new one when one does not yet exist.
    /// </summary>
    /// <param name="qualName">
    ///     The fully-qualified namespace name (e.g. <c>"mylib::rendering"</c>),
    ///     or an empty string for the global namespace.
    /// </param>
    /// <returns>The existing or newly created <see cref="NamespaceBuilder"/>.</returns>
    private NamespaceBuilder GetNsBuilder(string qualName)
    {
        if (!_nsBuilders.TryGetValue(qualName, out var builder))
        {
            builder = new NamespaceBuilder(qualName);
            _nsBuilders[qualName] = builder;
        }

        return builder;
    }

    /// <summary>
    ///     Converts all accumulated <see cref="NamespaceBuilder"/> instances into immutable
    ///     <see cref="CppNamespaceDecl"/> records.
    /// </summary>
    /// <returns>
    ///     A list of all namespace declarations that contain at least one owned declaration.
    /// </returns>
    private IReadOnlyList<CppNamespaceDecl> BuildNamespaces()
    {
        return _nsBuilders.Values
            .Select(b => b.Build())
            .ToList();
    }

    // =========================================================================
    // Inner namespace builder
    // =========================================================================

    /// <summary>
    ///     Mutable accumulator for the declarations contributed to a single C++ namespace
    ///     before they are frozen into a <see cref="CppNamespaceDecl"/>.
    /// </summary>
    private sealed class NamespaceBuilder
    {
        /// <summary>Initializes a new builder for the given qualified namespace name.</summary>
        /// <param name="qualifiedName">
        ///     The fully-qualified C++ namespace name, or an empty string for the global namespace.
        /// </param>
        public NamespaceBuilder(string qualifiedName)
        {
            QualifiedName = qualifiedName;
        }

        /// <summary>Gets the fully-qualified C++ namespace name.</summary>
        public string QualifiedName { get; }

        /// <summary>Gets the mutable list of classes being accumulated.</summary>
        public List<CppClass> Classes { get; } = [];

        /// <summary>Gets the mutable list of free functions being accumulated.</summary>
        public List<CppFunction> FreeFunctions { get; } = [];

        /// <summary>Gets the mutable list of enums being accumulated.</summary>
        public List<CppEnum> Enums { get; } = [];

        /// <summary>Gets the mutable list of type aliases being accumulated.</summary>
        public List<CppTypeAlias> TypeAliases { get; } = [];

        /// <summary>
        ///     Builds an immutable <see cref="CppNamespaceDecl"/> from the accumulated
        ///     declarations.
        /// </summary>
        /// <returns>A frozen <see cref="CppNamespaceDecl"/>.</returns>
        public CppNamespaceDecl Build()
        {
            // Namespace-level doc comments are not reliably exposed in the clang JSON
            // AST dump for plain namespace declarations, so Doc is always null here
            return new CppNamespaceDecl(QualifiedName, Classes, FreeFunctions, Enums, TypeAliases, Doc: null);
        }
    }
}
