## DemaConsulting.HelpCards

DemaConsulting.HelpCards is a NuGet package produced by DEMA Consulting. It provides the
built-in help of ApiMarkTool: it turns Markdown help cards embedded in the tool assembly into a
validated catalog, serves `apimark help` from that catalog for people and AI agents, and
supplies the test helpers that prove the cards are valid and that their examples work.

### Purpose

The package was chosen so that the help travels inside the tool assembly and works wherever
the tool is installed, including the copy bundled in the `ApiMark.MSBuild` package, without a
network connection. It is produced by another repository with its own requirements and release
lifecycle, and ApiMark does not change it, so it is classified as an OTS software item.

### Features Used

| Feature | Usage in ApiMark |
| --- | --- |
| `HelpSource.FromAssembly` | Exposes the `Help` folder that the package build targets embed in the tool assembly |
| `HelpCli`, `HelpCliOptions` | Answer the `help` command, and supply the `Footer` pointer for the usage text |
| `HelpCliOptions.GetCatalogConfiguration` | Gives the tests the catalog composition that the tool ships |
| `HelpCatalogBuilder` | `AddSource`, `TagVocabulary`, and `ExpectedRoots` fix the cards, tags, and topics |
| `HelpCardValidator`, `HelpExampleRunner` | Test helpers that validate the cards and run every command example |
| MSBuild targets | Embed `Help/**` as resources of the tool assembly |

### Integration Pattern

`HelpCommand` is the only unit that uses the package in the shipped tool; creating its
`HelpSource` and `HelpCli` performs no I/O and needs no disposal. `Program.Main` calls
`HelpCommand.Default.TryRun` before `Context.Create`, and the package builds the catalog only
when the first argument is `help`. No HelpCards type appears in any public signature.

The tool project references the package directly, because only a project with its own package
reference embeds cards; the test project receives the test helpers through its project
reference to the tool. The `ApiMark.MSBuild` package publishes the tool with its dependencies,
so the HelpCards assembly travels with the bundled tool. The package is a pre-release, so its
exact version is pinned in the project file; upgrading it requires re-running the help tests
and a review of this design.

The consuming unit is `ApiMarkTool-Cli-HelpCommand`.
