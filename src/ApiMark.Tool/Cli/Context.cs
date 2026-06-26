using ApiMark.Core;

namespace ApiMark.Tool.Cli;

/// <summary>
///     Context class that handles command-line arguments and program output.
/// </summary>
internal sealed class Context : IContext, IDisposable
{
    /// <summary>
    ///     Log file stream writer (if logging is enabled).
    /// </summary>
    private StreamWriter? _logWriter;

    /// <summary>
    ///     Indicates whether errors have been reported.
    /// </summary>
    private bool _hasErrors;

    /// <summary>
    ///     Gets a value indicating whether the version flag was specified.
    /// </summary>
    public bool Version { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether the help flag was specified.
    /// </summary>
    public bool Help { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether the silent flag was specified.
    /// </summary>
    public bool Silent { get; private init; }

    /// <summary>
    ///     Gets a value indicating whether the validate flag was specified.
    /// </summary>
    public bool Validate { get; private init; }

    /// <summary>
    ///     Gets the validation results file path.
    /// </summary>
    public string? ResultsFile { get; private init; }

    /// <summary>
    ///     Gets the output format for generated Markdown documentation.
    ///     <see cref="OutputFormat.GradualDisclosure"/> produces one page per type (default);
    ///     <see cref="OutputFormat.SingleFile"/> writes all content to a single <c>api.md</c>.
    /// </summary>
    public OutputFormat Format { get; private init; } = OutputFormat.GradualDisclosure;

    /// <summary>
    ///     Gets the heading depth for markdown output (default is 1, valid range 1–6).
    /// </summary>
    public int HeadingDepth { get; private init; } = 1;

    /// <summary>
    ///     Gets the language subcommand (<c>dotnet</c>, <c>cpp</c>, or null if not given).
    /// </summary>
    public string? Language { get; private init; }

    /// <summary>
    ///     Gets the path to the .NET assembly to document.
    /// </summary>
    public string? Assembly { get; private init; }

    /// <summary>
    ///     Gets the path to the XML documentation file alongside the assembly.
    /// </summary>
    public string? XmlDoc { get; private init; }

    /// <summary>
    ///     Gets the include directory paths for the C++ language subcommand.
    ///     Contains plain directory paths collected from repeated <c>--includes</c> invocations;
    ///     all entries are passed to Clang as <c>-I</c> paths.
    /// </summary>
    public string[] Includes { get; private init; } = [];

    /// <summary>
    ///     Gets the ordered list of glob and exclusion pattern strings for the C++ language subcommand.
    ///     Collected from repeated <c>--api-headers</c> invocations; entries with a <c>!</c>
    ///     prefix are exclusion patterns. Order is significant — gitignore semantics apply
    ///     (last matching pattern wins).
    /// </summary>
    public string[] ApiHeaders { get; private init; } = [];

    /// <summary>
    ///     Gets the output directory for generated Markdown files.
    /// </summary>
    public string? Output { get; private init; }

    /// <summary>
    ///     Gets the visibility filter applied to generated documentation.
    ///     Valid values are <c>Public</c>, <c>PublicAndProtected</c>, and <c>All</c>.
    ///     Defaults to <c>Public</c>.
    /// </summary>
    public string Visibility { get; private init; } = "Public";

    /// <summary>
    ///     Gets a value indicating whether members marked <c>[Obsolete]</c> are included
    ///     in generated output. Defaults to <see langword="false"/>.
    /// </summary>
    public bool IncludeObsolete { get; private init; }

    /// <summary>
    ///     Gets the library name used as the top-level heading in C++ documentation.
    ///     Optional — when <see langword="null"/>, the tool defaults to the output directory name.
    /// </summary>
    public string? LibraryName { get; private init; }

    /// <summary>
    ///     Gets an optional description for the C++ library, emitted as an introductory
    ///     paragraph in <c>api.md</c>. Optional — omitted when <see langword="null"/>.
    /// </summary>
    public string? LibraryDescription { get; private init; }

    /// <summary>
    ///     Gets the preprocessor symbol definitions passed to Clang for C++ documentation.
    ///     Each entry is in the form <c>"NAME"</c> or <c>"NAME=value"</c>.
    /// </summary>
    public string[] Defines { get; private init; } = [];

    /// <summary>
    ///     Gets the C++ language standard passed to Clang (e.g. <c>"c++17"</c>, <c>"c++20"</c>).
    ///     Optional — when <see langword="null"/>, the tool defaults to <c>c++17</c>.
    /// </summary>
    public string? CppStandard { get; private init; }

    /// <summary>
    ///     Gets the path to the clang executable, overriding automatic discovery.
    ///     Optional — when null, clang is located via PATH, xcrun (macOS), or vswhere (Windows).
    /// </summary>
    public string? ClangPath { get; private init; }

    /// <summary>
    ///     Gets the ordered list of glob and exclusion pattern strings for the VHDL language subcommand.
    ///     Collected from repeated <c>--source</c> invocations; entries with a <c>!</c>
    ///     prefix are exclusion patterns. Order is significant — gitignore semantics apply
    ///     (last matching pattern wins).
    /// </summary>
    public string[] Sources { get; private init; } = [];

    /// <summary>
    ///     Gets the proposed exit code for the application (0 for success, 1 for errors).
    /// </summary>
    public int ExitCode => _hasErrors ? 1 : 0;

    /// <summary>
    ///     Private constructor — use <see cref="Create"/> factory method instead.
    /// </summary>
    private Context()
    {
    }

    /// <summary>
    ///     Creates a <see cref="Context"/> instance from command-line arguments.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>A new <see cref="Context"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when arguments are invalid.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the specified log file cannot be opened.</exception>
    public static Context Create(string[] args)
    {
        // Validate input
        ArgumentNullException.ThrowIfNull(args);

        var parser = new ArgumentParser();
        parser.ParseArguments(args);

        var result = new Context
        {
            Version = parser.Version,
            Help = parser.Help,
            Silent = parser.Silent,
            Validate = parser.Validate,
            ResultsFile = parser.ResultsFile,
            Format = parser.Format,
            HeadingDepth = parser.HeadingDepth,
            Language = parser.Language,
            Assembly = parser.Assembly,
            XmlDoc = parser.XmlDoc,
            Includes = [.. parser.Includes],
            ApiHeaders = [.. parser.ApiHeaders],
            Output = parser.Output,
            Visibility = parser.Visibility,
            IncludeObsolete = parser.IncludeObsolete,
            LibraryName = parser.LibraryName,
            LibraryDescription = parser.LibraryDescription,
            Defines = parser.Defines,
            CppStandard = parser.CppStandard,
            ClangPath = parser.ClangPath,
            Sources = [.. parser.Sources],
        };

        // Open log file if specified
        if (parser.LogFile != null)
        {
            result.OpenLogFile(parser.LogFile);
        }

        return result;
    }

    /// <summary>
    ///     Opens the log file for writing.
    /// </summary>
    /// <param name="logFile">Log file path.</param>
    private void OpenLogFile(string logFile)
    {
        try
        {
            // Open with AutoFlush enabled so log entries are immediately written to disk
            // even if the application terminates unexpectedly before Dispose is called
            _logWriter = new StreamWriter(logFile, append: false) { AutoFlush = true };
        }
        // Generic catch is justified here to wrap any file system exception with context.
        // Expected exceptions include IOException, UnauthorizedAccessException, ArgumentException,
        // NotSupportedException, and other file system-related exceptions.
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to open log file '{logFile}': {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Writes a line of output to the console and log file (if logging is enabled).
    /// </summary>
    /// <param name="message">The message to write.</param>
    /// <remarks>
    ///     Output is written to stdout. When <see cref="Silent"/> is <c>true</c>, stdout output is
    ///     suppressed, but the message is still written to the log file when one is open.
    /// </remarks>
    public void WriteLine(string message)
    {
        // Write to console unless silent mode is enabled
        if (!Silent)
        {
            Console.WriteLine(message);
        }

        // Write to log file if logging is enabled
        _logWriter?.WriteLine(message);
    }

    /// <summary>
    ///     Writes an error message to the error console and log file (if logging is enabled).
    /// </summary>
    /// <param name="message">The error message to write.</param>
    /// <remarks>
    ///     <c>_hasErrors</c> is set to <c>true</c> unconditionally, so <see cref="ExitCode"/> will
    ///     return 1 regardless of whether <see cref="Silent"/> suppresses the console output.
    ///     Stderr output is suppressed when <see cref="Silent"/> is <c>true</c>, but the message
    ///     is still written to the log file when one is open.
    /// </remarks>
    public void WriteError(string message)
    {
        // Mark that we have encountered errors
        _hasErrors = true;

        // Write to error console unless silent mode is enabled
        if (!Silent)
        {
            var previousColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(message);
            Console.ForegroundColor = previousColor;
        }

        // Write to log file if logging is enabled
        _logWriter?.WriteLine(message);
    }

    /// <summary>
    ///     Disposes resources used by the <see cref="Context"/>.
    /// </summary>
    public void Dispose()
    {
        // Close and dispose the log file writer if it exists
        _logWriter?.Dispose();
        _logWriter = null;
    }

    /// <summary>
    ///     Helper class for parsing command-line arguments.
    /// </summary>
    private sealed class ArgumentParser
    {
        /// <summary>
        ///     Gets a value indicating whether the version flag was specified.
        /// </summary>
        public bool Version { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the help flag was specified.
        /// </summary>
        public bool Help { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the silent flag was specified.
        /// </summary>
        public bool Silent { get; private set; }

        /// <summary>
        ///     Gets a value indicating whether the validate flag was specified.
        /// </summary>
        public bool Validate { get; private set; }

        /// <summary>
        ///     Gets the log file path.
        /// </summary>
        public string? LogFile { get; private set; }

        /// <summary>
        ///     Gets the validation results file path.
        /// </summary>
        public string? ResultsFile { get; private set; }

        /// <summary>
        ///     Gets the output format for generated Markdown documentation.
        ///     Defaults to <see cref="OutputFormat.GradualDisclosure"/>.
        /// </summary>
        public OutputFormat Format { get; private set; } = OutputFormat.GradualDisclosure;

        /// <summary>
        ///     Gets the heading depth for markdown output.
        /// </summary>
        public int HeadingDepth { get; private set; } = 1;

        /// <summary>
        ///     Gets the language subcommand (<c>dotnet</c>, <c>cpp</c>, or null if not specified).
        /// </summary>
        public string? Language { get; private set; }

        /// <summary>
        ///     Gets the path to the .NET assembly.
        /// </summary>
        public string? Assembly { get; private set; }

        /// <summary>
        ///     Gets the path to the XML documentation file.
        /// </summary>
        public string? XmlDoc { get; private set; }

        /// <summary>
        ///     Gets the include directory paths for the C++ language subcommand.
        ///     Accumulated by repeated <c>--includes</c> invocations; each invocation appends
        ///     one plain directory path.
        /// </summary>
        public List<string> Includes { get; } = new List<string>();

        /// <summary>
        ///     Gets the ordered list of glob and exclusion pattern strings for the C++ language subcommand.
        ///     Accumulated by repeated <c>--api-headers</c> invocations; each invocation appends
        ///     one pattern (with or without a leading <c>!</c>). Order is preserved for
        ///     gitignore-style last-match-wins evaluation.
        /// </summary>
        public List<string> ApiHeaders { get; } = new List<string>();

        /// <summary>
        ///     Gets the output directory.
        /// </summary>
        public string? Output { get; private set; }

        /// <summary>
        ///     Gets the visibility filter value.
        /// </summary>
        public string Visibility { get; private set; } = "Public";

        /// <summary>
        ///     Gets a value indicating whether to include obsolete members.
        /// </summary>
        public bool IncludeObsolete { get; private set; }

        /// <summary>
        ///     Gets the library name for the C++ documentation root heading.
        ///     Optional — when <see langword="null"/>, the tool derives it from the output directory.
        /// </summary>
        public string? LibraryName { get; private set; }

        /// <summary>
        ///     Gets an optional description for the C++ library introduction.
        ///     Optional — omitted when <see langword="null"/>.
        /// </summary>
        public string? LibraryDescription { get; private set; }

        /// <summary>
        ///     Gets the preprocessor definitions parsed from the <c>--defines</c> comma-separated list.
        /// </summary>
        public string[] Defines { get; private set; } = [];

        /// <summary>
        ///     Gets the C++ language standard passed to Clang.
        ///     Optional — when <see langword="null"/>, the tool defaults to <c>c++17</c>.
        /// </summary>
        public string? CppStandard { get; private set; }

        /// <summary>
        ///     Gets the path to the clang executable, overriding automatic discovery.
        ///     Optional — when <see langword="null"/>, clang is located via PATH, xcrun (macOS),
        ///     or vswhere (Windows).
        /// </summary>
        public string? ClangPath { get; private set; }

        /// <summary>
        ///     Gets the VHDL source glob patterns from repeated --source flags.
        /// </summary>
        public List<string> Sources { get; } = new List<string>();

        /// <summary>
        ///     Parses command-line arguments using a single-pass strategy.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        /// <remarks>
        ///     Standard flags (<c>-v</c>, <c>--version</c>, <c>--help</c>, <c>--silent</c>, etc.) are
        ///     valid anywhere in the argument list. The language subcommand is the first positional
        ///     non-flag token. Language-specific options may appear anywhere after the language is found.
        /// </remarks>
        public void ParseArguments(string[] args)
        {
            // Validate input
            ArgumentNullException.ThrowIfNull(args);

            int i = 0;
            while (i < args.Length)
            {
                var arg = args[i++];
                i = ParseArgument(arg, args, i);
            }
        }

        /// <summary>
        ///     Parses a single argument and returns the updated index.
        /// </summary>
        /// <param name="arg">Argument to parse.</param>
        /// <param name="args">All arguments.</param>
        /// <param name="index">Current index (pointing past <paramref name="arg"/>).</param>
        /// <returns>Updated index after consuming any value token for this argument.</returns>
        private int ParseArgument(string arg, string[] args, int index)
        {
            switch (arg)
            {
                case "-v":
                case "--version":
                    Version = true;
                    return index;

                case "-?":
                case "-h":
                case "--help":
                    Help = true;
                    return index;

                case "--silent":
                    Silent = true;
                    return index;

                case "--validate":
                    Validate = true;
                    return index;

                case "--log":
                    LogFile = GetRequiredStringArgument(arg, args, index, "a filename argument");
                    return index + 1;

                case "--results":
                case "--result":
                    ResultsFile = GetRequiredStringArgument(arg, args, index, "a results filename argument");
                    return index + 1;

                case "--depth":
                    // Context validates the first-principles range (1–6: valid ATX heading levels in Markdown).
                    // Format-specific constraints (e.g. single-file requires depth ≤ 3 to keep member headings
                    // at H6 or above) are enforced by the program layer after all arguments are known, not here.
                    HeadingDepth = GetRequiredIntArgument(arg, args, index, "a heading depth argument", 1, 6);
                    return index + 1;

                case "--format":
                    {
                        var formatValue = GetRequiredStringArgument(arg, args, index, "a format value argument");
                        Format = formatValue.ToLowerInvariant() switch
                        {
                            "gradual" => OutputFormat.GradualDisclosure,
                            "single-file" => OutputFormat.SingleFile,
                            _ => throw new ArgumentException(
                                $"'{arg}' value must be 'gradual' or 'single-file', got '{formatValue}'.",
                                nameof(args)),
                        };

                        return index + 1;
                    }

                // Language-specific options — accepted anywhere; validated at run time if needed
                case "--assembly":
                    Assembly = GetRequiredStringArgument(arg, args, index, "a file path argument");
                    return index + 1;

                case "--xml-doc":
                    XmlDoc = GetRequiredStringArgument(arg, args, index, "a file path argument");
                    return index + 1;

                case "--includes":
                    {
                        // Append each --includes invocation as one plain directory path
                        // — repeated --includes flags accumulate the full list
                        var path = GetRequiredStringArgument(arg, args, index, "a directory path argument");
                        Includes.Add(path);
                        return index + 1;
                    }

                case "--api-headers":
                    {
                        // Append each --api-headers invocation as one pattern string
                        // — may start with '!' for exclusion; order is preserved for gitignore evaluation
                        var pattern = GetRequiredStringArgument(arg, args, index, "a glob pattern argument");
                        ApiHeaders.Add(pattern);
                        return index + 1;
                    }

                case "--output":
                    Output = GetRequiredStringArgument(arg, args, index, "a directory path argument");
                    return index + 1;

                case "--visibility":
                    Visibility = GetRequiredStringArgument(arg, args, index, "a visibility value argument");
                    return index + 1;

                case "--include-obsolete":
                    IncludeObsolete = true;
                    return index;

                case "--library-name":
                    LibraryName = GetRequiredStringArgument(arg, args, index, "a library name argument");
                    return index + 1;

                case "--library-description":
                    LibraryDescription = GetRequiredStringArgument(arg, args, index, "a description argument");
                    return index + 1;

                case "--defines":
                    Defines = GetRequiredStringArgument(arg, args, index, "a comma-separated defines argument")
                        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    return index + 1;

                case "--cpp-standard":
                    CppStandard = GetRequiredStringArgument(arg, args, index, "a C++ standard argument");
                    return index + 1;

                case "--clang-path":
                    ClangPath = GetRequiredStringArgument(arg, args, index, "a clang executable path argument");
                    return index + 1;

                case "--source":
                    {
                        var pattern = GetRequiredStringArgument(arg, args, index, "a glob pattern argument");
                        Sources.Add(pattern);
                        return index + 1;
                    }

                default:
                    // First positional non-flag token is the language subcommand
                    if (!arg.StartsWith('-') && Language == null)
                    {
                        Language = arg;
                        return index;
                    }

                    throw new ArgumentException($"Unsupported argument '{arg}'", nameof(args));
            }
        }

        /// <summary>
        ///     Gets a required string argument value from the argument array.
        /// </summary>
        /// <param name="arg">Argument name (used in error messages).</param>
        /// <param name="args">All arguments.</param>
        /// <param name="index">Index of the value token.</param>
        /// <param name="description">Description of what is required (used in error messages).</param>
        /// <returns>The string value at <paramref name="index"/>.</returns>
        private static string GetRequiredStringArgument(string arg, string[] args, int index, string description)
        {
            if (index >= args.Length)
            {
                throw new ArgumentException($"{arg} requires {description}", nameof(args));
            }

            var value = args[index];
            if (value.StartsWith('-'))
            {
                throw new ArgumentException($"{arg} requires {description} but got another flag '{value}'", nameof(args));
            }

            return value;
        }

        /// <summary>
        ///     Gets a required integer argument value from the argument array.
        /// </summary>
        /// <param name="arg">Argument name (used in error messages).</param>
        /// <param name="args">All arguments.</param>
        /// <param name="index">Index of the value token.</param>
        /// <param name="description">Description of what is required (used in error messages).</param>
        /// <param name="min">Minimum valid value (inclusive).</param>
        /// <param name="max">Maximum valid value (inclusive).</param>
        /// <returns>The integer value at <paramref name="index"/> in [<paramref name="min"/>, <paramref name="max"/>].</returns>
        private static int GetRequiredIntArgument(string arg, string[] args, int index, string description, int min = 1, int max = int.MaxValue)
        {
            var value = GetRequiredStringArgument(arg, args, index, description);
            if (!int.TryParse(value, out var result) || result < min || result > max)
            {
                throw new ArgumentException($"{arg} requires an integer between {min} and {max} for {description}", nameof(args));
            }

            return result;
        }
    }
}
