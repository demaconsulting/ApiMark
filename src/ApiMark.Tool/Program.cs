using System.Reflection;
using ApiMark.Core;
using ApiMark.Cpp;
using ApiMark.DotNet;
using ApiMark.Tool.Cli;
using ApiMark.Tool.SelfTest;
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

        try
        {
            // Construct the generator and invoke it with a file-system writer factory
            var generator = CreateGenerator(context);
            var factory = new FileMarkdownWriterFactory(context.Output!);
            generator.Generate(factory, context);
        }
        // Catch all generator construction and execution errors so failures produce
        // clean non-zero exits without an unhandled-exception stack trace
        catch (Exception ex)
        {
            context.WriteError($"Error: {ex.Message}");
        }
    }

    /// <summary>
    ///     Constructs and returns an <see cref="IApiGenerator"/> configured from the parsed context.
    /// </summary>
    /// <param name="context">Fully parsed CLI context.</param>
    /// <returns>A configured generator ready for <c>Generate</c> to be called.</returns>
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

        return context.Language switch
        {
            // Construct a DotNetGenerator from the dotnet-specific options
            "dotnet" => new DotNetGenerator(new DotNetGeneratorOptions
            {
                AssemblyPath = context.Assembly ?? string.Empty,
                XmlDocPath = context.XmlDoc ?? string.Empty,
                Visibility = visibility,
                IncludeObsolete = context.IncludeObsolete,
            }),

            // Construct a CppGenerator from the cpp-specific options; cast visibility via its
            // integer ordinal because ApiMark.Cpp.ApiVisibility mirrors ApiMark.DotNet.ApiVisibility
            // with identical values and the projects must not depend on each other
            "cpp" => new CppGenerator(new CppGeneratorOptions
            {
                LibraryName = cppLibraryName,
                Description = context.LibraryDescription ?? string.Empty,
                PublicIncludeRoots = context.Includes,
                IncludePatterns = context.IncludePatterns,
                ExcludePatterns = context.ExcludePatterns,
                Defines = context.Defines,
                CppStandard = context.CppStandard ?? "c++17",
                Visibility = (CppApiVisibility)(int)visibility,
                IncludeDeprecated = context.IncludeObsolete,
                ClangPath = context.ClangPath,
                AdditionalIncludePaths = context.SearchPaths,
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
        context.WriteLine("  --results <file>           Write validation results to file (.trx or .xml)");
        context.WriteLine("  --depth <#>                Set heading depth for validation output (default: 1)");
        context.WriteLine("  --log <file>               Write all output to log file");
        context.WriteLine("");
        context.WriteLine("Languages:");
        context.WriteLine("  dotnet    Generate API documentation from a .NET assembly");
        context.WriteLine("  cpp       Generate API documentation from C++ headers");
        context.WriteLine("");
        context.WriteLine("dotnet options:");
        context.WriteLine("  --assembly <path>          Path to the .NET assembly (required)");
        context.WriteLine("  --xml-doc <path>           Path to the XML documentation file (required)");
        context.WriteLine("  --output <dir>             Output directory for Markdown files (required)");
        context.WriteLine("  --visibility <value>       Visibility filter: Public, PublicAndProtected, All (default: Public)");
        context.WriteLine("  --include-obsolete         Include obsolete members in generated output");
        context.WriteLine("");
        context.WriteLine("cpp options:");
        context.WriteLine("  --includes <paths>         Comma-separated list of public include directories (required)");
        context.WriteLine("  --output <dir>             Output directory for Markdown files (required)");
        context.WriteLine("  --library-name <name>      Library name used as the top-level heading (default: output directory name)");
        context.WriteLine("  --library-description <d>  Optional description for the library api.md introduction");
        context.WriteLine("  --defines <values>         Comma-separated preprocessor definitions (e.g. MYLIB_API=,NDEBUG)");
        context.WriteLine("  --cpp-standard <std>       C++ language standard passed to Clang (default: c++17)");
        context.WriteLine("  --clang-path <path>        Path to clang executable (default: auto-discovered via PATH / xcrun / vswhere)");
        context.WriteLine("  --search-paths <paths>     Comma-separated compiler-only -I paths (not documented)");
        context.WriteLine("  --include-patterns <p>     Comma-separated glob patterns selecting headers to document");
        context.WriteLine("  --exclude-patterns <p>     Comma-separated glob patterns for headers to exclude");
        context.WriteLine("  --visibility <value>       Visibility filter: Public, PublicAndProtected, All (default: Public)");
        context.WriteLine("  --include-obsolete         Include deprecated members in generated output");
    }
}

