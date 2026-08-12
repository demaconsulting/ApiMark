using System.Reflection;
using ApiMark.Core;
using ApiMark.Cpp;
using ApiMark.DotNet;
using ApiMark.Tool.Cli;
using ApiMark.Tool.SelfTest;
using ApiMark.Vhdl;
using CppApiVisibility = ApiMark.Cpp.ApiVisibility;
using DotNetApiVisibility = ApiMark.DotNet.ApiVisibility;

namespace ApiMark.Tool;

/// <summary>
///     CLI entry point for the ApiMark documentation tool.
/// </summary>
/// <remarks>
///     <para>
///         Dispatch is priority-ordered: version check first (no banner), then banner, then help,
///         then self-validation, then main tool logic. Only the highest-priority matching action
///         is executed per invocation.
///     </para>
///     <para>
///         <see cref="ArgumentException"/> and <see cref="InvalidOperationException"/> from
///         <see cref="Cli.Context.Create"/> are treated as expected errors: their messages are
///         written to stderr and exit code 1 is returned without a stack trace. Any other
///         exception propagated out of <see cref="Main"/> is re-thrown so that the runtime can
///         record it in event logs.
///     </para>
/// </remarks>
internal static class Program
{
    /// <summary>
    ///     Gets the application version string.
    /// </summary>
    /// <remarks>
    ///     The version is read from the <see cref="AssemblyInformationalVersionAttribute"/> via
    ///     reflection on every access. Callers that need the value more than once should store
    ///     the result locally.
    /// </remarks>
    public static string Version
    {
        get
        {
            // Read the informational version from assembly metadata; fall back to AssemblyVersion
            // or a safe default when neither attribute is present (e.g., during unit tests)
            var assembly = typeof(Program).Assembly;
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? assembly.GetName().Version?.ToString()
                   ?? "0.0.0";
        }
    }

    /// <summary>
    ///     Application entry point.
    /// </summary>
    /// <param name="args">Command-line arguments from the host environment.</param>
    /// <returns>0 on success; non-zero on error.</returns>
    /// <exception cref="Exception">
    ///     Thrown when an unexpected error occurs; re-thrown after writing to stderr.
    /// </exception>
    public static int Main(string[] args)
    {
        try
        {
            // Create context from command-line arguments; argument parsing failures throw here
            using var context = Context.Create(args);

            // Run the program logic and return the exit code
            Run(context);
            return context.ExitCode;
        }
        catch (ArgumentException ex)
        {
            // Print expected argument exceptions and return error code
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            // Print expected operation exceptions (e.g., log file open failure) and return error code
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            // Print unexpected exceptions and re-throw to generate event logs
            Console.Error.WriteLine($"Unexpected error: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    ///     Runs the program logic based on the provided context.
    /// </summary>
    /// <param name="context">The context containing command-line arguments and program state.</param>
    /// <remarks>
    ///     Dispatch is priority-ordered: version → banner → help → validate → main tool logic.
    ///     The version flag short-circuits before the banner so that <c>--version</c> output is
    ///     undecorated by the application header.
    /// </remarks>
    public static void Run(Context context)
    {
        // Priority 1: Version query — short-circuits before banner
        if (context.Version)
        {
            context.WriteLine(Version);
            return;
        }

        // Print application banner for all remaining paths
        PrintBanner(context);

        // Priority 2: Help
        if (context.Help)
        {
            PrintHelp(context);
            return;
        }

        // Priority 3: Self-Validation
        if (context.Validate)
        {
            Validation.Run(context);
            return;
        }

        // Priority 4: Main tool functionality
        RunToolLogic(context);
    }

    /// <summary>
    ///     Runs the main tool logic — validates required options, constructs the generator, and generates output.
    /// </summary>
    /// <param name="context">The context containing parsed options and program state.</param>
    private static void RunToolLogic(Context context)
    {
        // Require a language subcommand before validating language-specific options
        if (string.IsNullOrEmpty(context.Language))
        {
            context.WriteError("Error: No language subcommand specified.");
            PrintHelp(context);
            return;
        }

        // Require --output for every language subcommand
        if (string.IsNullOrEmpty(context.Output))
        {
            context.WriteError("Error: --output is required.");
            PrintHelp(context);
            return;
        }

        // Validate dotnet-specific required options before constructing the generator
        if (context.Language == "dotnet" && string.IsNullOrEmpty(context.Assembly))
        {
            context.WriteError("Error: --assembly is required for the dotnet subcommand.");
            PrintHelp(context);
            return;
        }

        if (context.Language == "dotnet" && string.IsNullOrEmpty(context.XmlDoc))
        {
            context.WriteError("Error: --xml-doc is required for the dotnet subcommand.");
            PrintHelp(context);
            return;
        }

        // Validate cpp-specific required options before constructing the generator.
        // Whitespace-only entries in the Includes array are treated as absent for this check.
        if (context.Language == "cpp" && !context.Includes.Any(s => !string.IsNullOrWhiteSpace(s)))
        {
            context.WriteError("Error: --includes is required for the cpp subcommand.");
            PrintHelp(context);
            return;
        }

        // Validate vhdl-specific required options: at least one non-empty, non-exclusion --source
        // pattern must be provided so the generator has something to scan.
        if (context.Language == "vhdl" &&
            !context.Sources.Any(s => !s.StartsWith('!') && !string.IsNullOrWhiteSpace(s)))
        {
            context.WriteError("Error: at least one non-exclusion --source pattern is required for the vhdl subcommand.");
            PrintHelp(context);
            return;
        }

        // Enforce the single-file format depth constraint: the single-file emitter writes member
        // headings at depth+3 (assembly at depth, namespace at depth+1, type at depth+2, member
        // at depth+3), so depth > 3 would produce H7+ headings which CommonMark does not support.
        // This check lives here (not in Context) because it is a format-specific, cross-argument
        // constraint discoverable only after both --format and --depth are known.
        if (context.Format == OutputFormat.SingleFile && context.HeadingDepth > 3)
        {
            context.WriteError(
                $"--depth must be 1-3 for single-file output " +
                $"(member headings would exceed H6 at depth {context.HeadingDepth}).");
            return;
        }

        // --enforce-docs is validated per-subcommand in CreateGenerator, then applied uniformly
        // below via IDocumentationCoverageCapable for whichever language generator is active.

        try
        {
            // Construct the generator and parse symbols first; documentation coverage (when
            // requested) must be checked before Emit, which disposes the parsed assembly
            var generator = CreateGenerator(context);
            var emitter = generator.Parse(context);

            if (!string.IsNullOrEmpty(context.EnforceDocs) && generator is IDocumentationCoverageCapable coverageCapable)
            {
                ReportDocumentationCoverage(context, coverageCapable.CheckDocumentationCoverage(context.EnforceDocs));
            }
            else if (!string.IsNullOrEmpty(context.EnforceDocs))
            {
                // Defensive fallback: unreachable today since dotnet, cpp, and vhdl all
                // implement IDocumentationCoverageCapable, but keeps a future 4th language
                // generator that does not implement the interface from failing outright.
                context.WriteLine("Note: --enforce-docs is not supported for this generator; ignoring.");
            }

            var factory = new FileMarkdownWriterFactory(context.Output!);
            var emitConfig = new EmitConfig
            {
                Format = context.Format,
                HeadingDepth = context.HeadingDepth,
            };
            emitter.Emit(factory, emitConfig, context);
        }
        // Catch all generator construction and execution errors so failures produce
        // clean non-zero exits without an unhandled-exception stack trace
        catch (Exception ex)
        {
            context.WriteError($"Error: {ex.Message}");
        }
    }

    /// <summary>
    ///     Writes the documentation-coverage scan results to the context output stream, and
    ///     signals a build failure via <see cref="Context.WriteError"/> when the configured
    ///     severity is <c>Error</c> and at least one violation was found.
    /// </summary>
    /// <param name="context">The CLI context used to write output and, potentially, an error.</param>
    /// <param name="result">The documentation coverage scan result.</param>
    /// <exception cref="ArgumentException">
    ///     Thrown when <see cref="Context.EnforceDocsSeverity"/> is not a recognized value.
    /// </exception>
    private static void ReportDocumentationCoverage(Context context, DocumentationCoverageResult result)
    {
        if (!Enum.TryParse<EnforcementSeverity>(context.EnforceDocsSeverity, ignoreCase: true, out var severity))
        {
            throw new ArgumentException(
                $"Invalid --enforce-docs-severity value '{context.EnforceDocsSeverity}'. " +
                $"Valid values are: {string.Join(", ", Enum.GetNames<EnforcementSeverity>())}.");
        }

        context.WriteLine("Documentation coverage check:");
        foreach (var item in result.UndocumentedItems)
        {
            context.WriteLine($"  [Undocumented] {item.Kind}: {item.DisplayName}");
        }

        context.WriteLine($"Documentation coverage: {result.UndocumentedCount} undocumented of {result.CheckedCount} checked.");

        // Only a single summary line goes through WriteError (which sets ExitCode = 1) — per-item
        // WriteError calls would be disproportionately noisy for what is, in essence, one failure
        if (result.HasViolations && severity == EnforcementSeverity.Error)
        {
            context.WriteError(
                $"Error: {result.UndocumentedCount} undocumented API item(s) found (--enforce-docs-severity Error).");
        }
    }

    /// <summary>Severity applied when documentation-coverage enforcement finds undocumented items.</summary>
    private enum EnforcementSeverity
    {
        /// <summary>Undocumented items are reported but do not fail the build.</summary>
        Warning,

        /// <summary>Undocumented items are reported and cause the build to fail.</summary>
        Error,
    }

    /// <summary>
    ///     Constructs and returns an <see cref="IApiGenerator"/> configured from the parsed context.
    /// </summary>
    /// <param name="context">Fully parsed CLI context.</param>
    /// <returns>A configured generator ready for <c>Parse</c> to be called.</returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when <see cref="Context.Visibility"/> is not a recognized
    ///     <see cref="DotNetApiVisibility"/> value.
    /// </exception>
    /// <exception cref="NotSupportedException">
    ///     Thrown when <see cref="Context.Language"/> identifies an unrecognized or
    ///     not-yet-implemented language subcommand.
    /// </exception>
    private static IApiGenerator CreateGenerator(Context context)
    {
        // Parse the visibility string case-insensitively; reject unknown values early
        if (!Enum.TryParse<DotNetApiVisibility>(context.Visibility, ignoreCase: true, out var visibility))
        {
            throw new ArgumentException(
                $"Invalid visibility value '{context.Visibility}'. " +
                $"Valid values are: {string.Join(", ", Enum.GetNames<DotNetApiVisibility>())}.");
        }

        // Resolve the cpp library name: the explicit --library-name flag takes precedence,
        // falling back to the output directory name or a safe default when neither is set.
        // Trailing path separators are trimmed first because Path.GetFileName returns an
        // empty string when the path ends with a separator (e.g. "docs/api/").
        var outputTrimmed = context.Output?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var defaultLibraryName = !string.IsNullOrEmpty(outputTrimmed) ? Path.GetFileName(outputTrimmed) : "Library";

        // Guard against root-only output paths where Path.GetFileName returns empty —
        // for example when the output path resolves to a drive root after separator trimming
        if (string.IsNullOrEmpty(defaultLibraryName))
        {
            defaultLibraryName = "Library";
        }

        var cppLibraryName = !string.IsNullOrEmpty(context.LibraryName) ? context.LibraryName : defaultLibraryName;

        // Parse the optional --enforce-docs enforcement tier; absent/empty means enforcement is
        // disabled (each generator's own EnforceDocsVisibility option stays null). Validated
        // per-subcommand here so an invalid value fails fast, before Parse runs, for all three
        // languages uniformly.
        DotNetApiVisibility? enforceDocsVisibility = null;
        CppApiVisibility? cppEnforceDocsVisibility = null;
        string? vhdlEnforceDocsVisibility = null;
        if (!string.IsNullOrEmpty(context.EnforceDocs))
        {
            switch (context.Language)
            {
                case "dotnet":
                    if (!Enum.TryParse<DotNetApiVisibility>(context.EnforceDocs, ignoreCase: true, out var parsedDotNetTier))
                    {
                        throw new ArgumentException(
                            $"Invalid --enforce-docs value '{context.EnforceDocs}'. " +
                            $"Valid values are: {string.Join(", ", Enum.GetNames<DotNetApiVisibility>())}.");
                    }

                    enforceDocsVisibility = parsedDotNetTier;
                    break;

                case "cpp":
                    if (!Enum.TryParse<CppApiVisibility>(context.EnforceDocs, ignoreCase: true, out var parsedCppTier))
                    {
                        throw new ArgumentException(
                            $"Invalid --enforce-docs value '{context.EnforceDocs}'. " +
                            $"Valid values are: {string.Join(", ", Enum.GetNames<CppApiVisibility>())}.");
                    }

                    cppEnforceDocsVisibility = parsedCppTier;
                    break;

                case "vhdl":
                    // VHDL has no visibility/accessibility concept: Public, PublicAndProtected,
                    // and All are all accepted (using the same vocabulary as dotnet/cpp purely
                    // for a consistent CLI experience and error message) but behave identically —
                    // see ApiMark.Vhdl.DocumentationCoverageChecker. The parsed enum value itself
                    // is discarded; only the raw string is forwarded to VhdlGeneratorOptions.
                    if (!Enum.TryParse<DotNetApiVisibility>(context.EnforceDocs, ignoreCase: true, out _))
                    {
                        throw new ArgumentException(
                            $"Invalid --enforce-docs value '{context.EnforceDocs}'. " +
                            $"Valid values are: {string.Join(", ", Enum.GetNames<DotNetApiVisibility>())}.");
                    }

                    vhdlEnforceDocsVisibility = context.EnforceDocs;
                    break;
            }
        }

        return context.Language switch
        {
            // Construct a DotNetGenerator from the dotnet-specific options
            "dotnet" => new DotNetGenerator(new DotNetGeneratorOptions
            {
                AssemblyPath = context.Assembly ?? string.Empty,
                XmlDocPath = context.XmlDoc ?? string.Empty,
                Visibility = visibility,
                IncludeObsolete = context.IncludeObsolete,
                ExcludePatterns = context.Excludes,
                EnforceDocsVisibility = enforceDocsVisibility,
            }),

            // Construct a CppGenerator from the cpp-specific options; cast visibility via its
            // integer ordinal because ApiMark.Cpp.ApiVisibility mirrors ApiMark.DotNet.ApiVisibility
            // with identical values and the projects must not depend on each other
            "cpp" => new CppGenerator(new CppGeneratorOptions
            {
                LibraryName = cppLibraryName,
                Description = context.LibraryDescription ?? string.Empty,
                PublicIncludeRoots = context.Includes,
                ApiHeaderPatterns = context.ApiHeaders,
                Defines = context.Defines,
                CppStandard = context.CppStandard ?? "c++17",
                Visibility = (CppApiVisibility)(int)visibility,
                IncludeDeprecated = context.IncludeObsolete,
                ClangPath = context.ClangPath,
                EnforceDocsVisibility = cppEnforceDocsVisibility,
            }),

            // Construct a VhdlGenerator from the vhdl-specific options
            "vhdl" => new VhdlGenerator(new VhdlGeneratorOptions
            {
                LibraryName = !string.IsNullOrEmpty(context.LibraryName) ? context.LibraryName : defaultLibraryName,
                Description = context.LibraryDescription ?? string.Empty,
                Sources = new List<string>(context.Sources),
                EnforceDocsVisibility = vhdlEnforceDocsVisibility,
            }),

            // Any other token is an unrecognized subcommand
            _ => throw new NotSupportedException(
                $"Unrecognized language subcommand '{context.Language}'."),
        };
    }

    /// <summary>
    ///     Prints the application banner to the context output stream.
    /// </summary>
    /// <param name="context">The context for output.</param>
    private static void PrintBanner(Context context)
    {
        context.WriteLine($"ApiMark.Tool version {Version}");
        context.WriteLine("Copyright (c) DEMA Consulting");
        context.WriteLine("");
    }

    /// <summary>
    ///     Prints usage and option information to the context output stream.
    /// </summary>
    /// <param name="context">The context for output.</param>
    private static void PrintHelp(Context context)
    {
        context.WriteLine("Usage: apimark [options] [language [language-options]]");
        context.WriteLine("");
        context.WriteLine("Options:");
        context.WriteLine("  -v, --version              Display version information");
        context.WriteLine("  -?, -h, --help             Display this help message");
        context.WriteLine("  --silent                   Suppress console output");
        context.WriteLine("  --validate                 Run self-validation tests");
        context.WriteLine("  --results, --result <file>  Write validation results to file (.trx or .xml)");
        context.WriteLine("  --depth <#>                Set the top-level heading depth for generated Markdown output (default: 1)");
        context.WriteLine("  --format <value>           Output format: gradual (default) or single-file");
        context.WriteLine("  --log <file>               Write all output to log file");
        context.WriteLine("");
        context.WriteLine("Languages:");
        context.WriteLine("  dotnet    Generate API documentation from a .NET assembly");
        context.WriteLine("  cpp       Generate API documentation from C++ headers");
        context.WriteLine("  vhdl      Generate API documentation from VHDL source files");
        context.WriteLine("");
        context.WriteLine("dotnet options:");
        context.WriteLine("  --assembly <path>          Path to the .NET assembly (required)");
        context.WriteLine("  --xml-doc <path>           Path to the XML documentation file (required)");
        context.WriteLine("  --output <dir>             Output directory for Markdown files (required)");
        context.WriteLine("  --visibility <value>       Visibility filter: Public, PublicAndProtected, All (default: Public)");
        context.WriteLine("  --include-obsolete         Include obsolete members in generated output");
        context.WriteLine("  --exclude <pattern>        Exclude namespaces/types matching a wildcard pattern (repeatable)");
        context.WriteLine("  --enforce-docs <value>     Enforce XML doc <summary> coverage at a visibility tier: Public, PublicAndProtected, All (default: disabled)");
        context.WriteLine("  --enforce-docs-severity <v> Severity when --enforce-docs finds violations: Warning, Error (default: Warning)");
        context.WriteLine("");
        context.WriteLine("cpp options:");
        context.WriteLine("  --includes <path>          Include directory for clang -I (repeatable, required)");
        context.WriteLine("  --api-headers <pattern>    Glob pattern for documented headers, supports ! exclusions (repeatable, ordered)");
        context.WriteLine("  --output <dir>             Output directory for Markdown files (required)");
        context.WriteLine("  --library-name <name>      Library name used as the top-level heading (default: output directory name)");
        context.WriteLine("  --library-description <d>  Optional description for the library api.md introduction");
        context.WriteLine("  --defines <values>         Comma-separated preprocessor definitions (e.g. MYLIB_API=,NDEBUG)");
        context.WriteLine("  --cpp-standard <std>       C++ language standard passed to Clang (default: c++17)");
        context.WriteLine("  --clang-path <path>        Path to clang executable (default: auto-discovered via PATH / xcrun / vswhere)");
        context.WriteLine("  --visibility <value>       Visibility filter: Public, PublicAndProtected, All (default: Public)");
        context.WriteLine("  --include-obsolete         Include deprecated members in generated output");
        context.WriteLine("");
        context.WriteLine("vhdl options:");
        context.WriteLine("  --source <glob>            VHDL source glob pattern (repeatable; prefix with ! to exclude)");
        context.WriteLine("  --output <dir>             Output directory for Markdown files (required)");
        context.WriteLine("  --library-name <name>      Library name used as the top-level heading (default: output directory name)");
        context.WriteLine("  --library-description <d>  Optional description for the library api.md introduction");
    }
}

