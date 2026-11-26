using DemaConsulting.HelpCards.Integration;
using DemaConsulting.HelpCards.Loading;

namespace ApiMark.Tool.Cli;

/// <summary>
///     Answers the <c>apimark help</c> command from the help cards embedded in the tool assembly.
/// </summary>
/// <remarks>
///     The only unit that references HelpCards. Creating an instance performs no I/O, and the catalog is
///     built only when the first argument is <c>help</c>. The exit codes and the bypass of <see cref="Context"/>
///     are described in the HelpCommand design.
/// </remarks>
internal sealed class HelpCommand
{
    /// <summary>
    ///     The name of the tool as the user types it; the usage lines and the <c>{{tool}}</c> token use it.
    /// </summary>
    internal const string ToolName = "apimark";

    /// <summary>
    ///     Ids of the root cards: the topics that <c>apimark help</c> lists. A missing or extra root card
    ///     fails the catalog.
    /// </summary>
    internal static readonly string[] RootCardIds =
    [
        "getting-started",
        "command-line",
        "dotnet",
        "cpp",
        "vhdl",
        "output",
        "doc-coverage",
        "response-files",
        "msbuild",
        "self-validation",
        "troubleshooting"
    ];

    /// <summary>
    ///     The closed tag vocabulary: every card carries one of these purpose tags, and no other tag.
    /// </summary>
    private static readonly string[] PurposeTags = ["tutorial", "reference", "troubleshooting"];

    /// <summary>
    ///     The cards embedded in the tool assembly; declared before <see cref="Default"/> so that it is
    ///     initialized first. Creating the source reads nothing.
    /// </summary>
    private static readonly HelpSource Cards = HelpSource.FromAssembly(typeof(HelpCommand).Assembly);

    /// <summary>
    ///     The underlying command-line helper.
    /// </summary>
    private readonly HelpCli _cli;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HelpCommand"/> class.
    /// </summary>
    /// <param name="output">The writer for output, or <see langword="null"/> for the console output.</param>
    /// <param name="error">The writer for errors, or <see langword="null"/> for the console error.</param>
    private HelpCommand(TextWriter? output, TextWriter? error) =>
        _cli = HelpCli.Create(CreateOptions(output, error));

    /// <summary>
    ///     Gets the instance used by the tool, which writes to the console.
    /// </summary>
    internal static HelpCommand Default { get; } = new(null, null);

    /// <summary>
    ///     Gets the sentence that ends the usage text and points to <c>apimark help</c>.
    /// </summary>
    public string Footer => _cli.Footer;

    /// <summary>
    ///     Creates the options of the command-line helper; tests build the shipped catalog from
    ///     <see cref="HelpCliOptions.GetCatalogConfiguration"/>.
    /// </summary>
    /// <param name="output">The writer for output, or <see langword="null"/> for the console output.</param>
    /// <param name="error">The writer for errors, or <see langword="null"/> for the console error.</param>
    /// <returns>The options.</returns>
    internal static HelpCliOptions CreateOptions(TextWriter? output = null, TextWriter? error = null) =>
        new()
        {
            ToolName = ToolName,
            Version = Program.Version,
            ConfigureCatalog = (builder, _) => builder
                .AddSource(Cards)
                .TagVocabulary(PurposeTags, PurposeTags)
                .ExpectedRoots(RootCardIds),
            UsageLines = [$"{ToolName} help [topic]", $"{ToolName} help --search <words>"],
            IndexPreamble = $"Start with '{ToolName} help getting-started'.",
            Output = output,
            Error = error
        };

    /// <summary>
    ///     Creates a help command that writes to the given writers.
    /// </summary>
    /// <param name="output">The writer for output.</param>
    /// <param name="error">The writer for errors.</param>
    /// <returns>The new help command.</returns>
    internal static HelpCommand Create(TextWriter output, TextWriter error) => new(output, error);

    /// <summary>
    ///     Runs the help command when the first argument is <c>help</c>.
    /// </summary>
    /// <param name="args">The command-line arguments of the tool.</param>
    /// <param name="exitCode">Receives the exit code when the command was answered.</param>
    /// <returns>
    ///     <see langword="true"/> when the command was answered; <see langword="false"/> when the tool should
    ///     continue with its own option parser.
    /// </returns>
    public bool TryRun(string[] args, out int exitCode) => _cli.TryRun(args, out exitCode);
}
