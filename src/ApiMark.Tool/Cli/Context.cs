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
    ///     Gets the wildcard exclude patterns for the .NET language subcommand.
    ///     Contains patterns collected from repeated <c>--exclude</c> invocations; each pattern
    ///     may contain <c>*</c> as a wildcard and is matched against full namespace and type names.
    /// </summary>
    public string[] Excludes { get; private init; } = [];

    /// <summary>
    ///     Gets the referenced assembly DLL paths for the .NET language subcommand, used to
    ///     resolve cross-assembly <c>&lt;inheritdoc /&gt;</c> targets. Contains paths collected
    ///     from repeated <c>--reference-paths</c> invocations. Defaults to an empty array — no
    ///     cross-assembly resolution unless explicitly supplied.
    /// </summary>
    public string[] ReferencePaths { get; private init; } = [];

    /// <summary>
    ///     Gets the documentation-coverage enforcement visibility tier for the .NET language
    ///     subcommand. Valid values are <c>Public</c>, <c>PublicAndProtected</c>, and <c>All</c>.
    ///     Defaults to <see langword="null"/> — enforcement is disabled unless this option is
    ///     explicitly supplied.
    /// </summary>
    public string? EnforceDocs { get; private init; }

    /// <summary>
    ///     Gets the severity applied when documentation-coverage enforcement finds undocumented
    ///     items. Valid values are <c>Warning</c> and <c>Error</c>. Defaults to <c>Warning</c>.
    /// </summary>
    public string EnforceDocsSeverity { get; private init; } = "Warning";

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
            Excludes = [.. parser.Excludes],
            ReferencePaths = [.. parser.ReferencePaths],
            EnforceDocs = parser.EnforceDocs,
            EnforceDocsSeverity = parser.EnforceDocsSeverity,
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
        ///     Gets the wildcard exclude patterns for the .NET language subcommand.
        ///     Accumulated by repeated <c>--exclude</c> invocations; each invocation appends
        ///     one <c>*</c>-wildcard pattern matched against full namespace and type names.
        /// </summary>
        public List<string> Excludes { get; } = new List<string>();

        /// <summary>
        ///     Gets the referenced assembly DLL paths for the .NET language subcommand.
        ///     Accumulated by repeated <c>--reference-paths</c> invocations; each invocation
        ///     appends one path used to resolve cross-assembly <c>&lt;inheritdoc /&gt;</c> targets.
        /// </summary>
        public List<string> ReferencePaths { get; } = new List<string>();

        /// <summary>
        ///     Gets the documentation-coverage enforcement visibility tier value.
        ///     <see langword="null"/> when <c>--enforce-docs</c> was not supplied (enforcement disabled).
        /// </summary>
        public string? EnforceDocs { get; private set; }

        /// <summary>
        ///     Gets the documentation-coverage enforcement severity value.
        /// </summary>
        public string EnforceDocsSeverity { get; private set; } = "Warning";

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

            // Expand any "@<file>" response-file tokens into their constituent arguments before
            // the rest of parsing runs, so ParseArgument/its callers are unaware of the
            // substitution.
            args = ExpandResponseFileArguments(args);

            int i = 0;
            while (i < args.Length)
            {
                var arg = args[i++];
                i = ParseArgument(arg, args, i);
            }
        }

        /// <summary>
        ///     Expands any <c>@&lt;file&gt;</c> response-file tokens in <paramref name="args"/> into
        ///     their constituent arguments, one per non-blank line of the referenced file.
        /// </summary>
        /// <remarks>
        ///     This is a single, non-recursive expansion pass: a line read from a response file is
        ///     never itself re-checked for a leading <c>@</c>. Tokens that do not start with
        ///     <c>@</c> pass through unchanged. Blank/whitespace-only lines in a response file are
        ///     skipped so authors can use blank lines for readability without producing empty
        ///     arguments. This convention exists so that MSBuild-driven invocations (e.g. a large,
        ///     harvested <c>ApiMarkReferencePaths</c> list) can avoid operating-system command-line
        ///     length limits by writing arguments to a file and passing a single <c>@&lt;file&gt;</c>
        ///     token instead.
        ///     A legitimate argument value that itself needs to start with a literal <c>@</c>
        ///     (for example a library description or defines value) can be written as
        ///     <c>@@rest</c>: a leading <c>@@</c> is treated as an escape for a literal leading
        ///     <c>@</c> and is passed through as <c>@rest</c> without response-file expansion,
        ///     rather than being misinterpreted as a response-file token. The escape and
        ///     response-file grammars are not fully orthogonal in one narrow corner case: a
        ///     response file whose own file name starts with <c>@</c> (e.g. a file literally
        ///     named <c>@config.rsp</c>) cannot be referenced via this convention, since the
        ///     token that would name it (<c>@@config.rsp</c>) is instead interpreted as the
        ///     escaped literal value <c>@config.rsp</c>. This is considered acceptable: such a
        ///     file name is exceedingly unlikely in practice.
        /// </remarks>
        /// <param name="args">The raw, unexpanded command-line arguments.</param>
        /// <returns>The arguments with any response-file tokens expanded in place.</returns>
        /// <exception cref="ArgumentException">
        ///     Thrown when a response-file token references a file that does not exist or cannot be
        ///     read, naming the offending path.
        /// </exception>
        private static string[] ExpandResponseFileArguments(string[] args)
        {
            List<string>? expanded = null;
            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                if (arg.StartsWith("@@", StringComparison.Ordinal))
                {
                    // Escaped literal: "@@rest" means a literal argument "@rest", not a
                    // response-file token. Strip exactly one leading '@'.
                    expanded ??= new List<string>(args[..i]);
                    expanded.Add(arg[1..]);
                }
                else if (arg.Length > 1 && arg[0] == '@')
                {
                    // Once a response-file token is seen, switch to building an explicit list
                    // (rather than mutating/returning the original array), copying every prior
                    // pass-through argument first.
                    expanded ??= new List<string>(args[..i]);

                    var responseFilePath = arg[1..];
                    string[] lines;
                    try
                    {
                        lines = File.ReadAllLines(responseFilePath);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        throw new ArgumentException(
                            $"Unable to read response file '{responseFilePath}': {ex.Message}", ex);
                    }

                    expanded.AddRange(lines.Where(line => !string.IsNullOrWhiteSpace(line)));
                }
                else
                {
                    expanded?.Add(arg);
                }
            }

            return expanded?.ToArray() ?? args;
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

                case "--exclude":
                    {
                        // Append each --exclude invocation as one wildcard pattern
                        // — repeated --exclude flags accumulate the full list
                        var pattern = GetRequiredStringArgument(arg, args, index, "a wildcard pattern argument");
                        Excludes.Add(pattern);
                        return index + 1;
                    }

                case "--reference-paths":
                    {
                        // Append each --reference-paths invocation as one referenced assembly
                        // DLL path — repeated --reference-paths flags accumulate the full list.
                        // A blank value is silently skipped rather than added: MSBuild's
                        // semicolon-splitting of the ApiMarkReferencePaths property can realistically
                        // produce an empty segment (e.g. a leading/trailing/double semicolon), and an
                        // empty path would otherwise make ExternalXmlDocResolver probe paths relative
                        // to the current working directory, potentially picking up an unrelated .xml
                        // file as if it were external documentation.
                        var path = GetRequiredStringArgument(arg, args, index, "a reference assembly path argument");
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            ReferencePaths.Add(path);
                        }

                        return index + 1;
                    }

                case "--enforce-docs":
                    EnforceDocs = GetRequiredStringArgument(arg, args, index, "an enforcement visibility value argument");
                    return index + 1;

                case "--enforce-docs-severity":
                    EnforceDocsSeverity = GetRequiredStringArgument(arg, args, index, "an enforcement severity value argument");
                    return index + 1;

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
