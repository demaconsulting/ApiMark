namespace ApiMark.MSBuild;

using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

/// <summary>
///     MSBuild task that integrates ApiMark documentation generation into any build pipeline.
/// </summary>
/// <remarks>
///     ApiMarkTask fires automatically <c>AfterTargets="Build"</c> for both <c>.csproj</c> SDK-style
///     builds and <c>.vcxproj</c> Visual Studio C++ builds. It translates MSBuild properties into a
///     <c>dotnet ApiMark.Tool.dll &lt;language&gt; [options]</c> child-process invocation and pipes
///     the output and exit code back to the MSBuild build log.
///
///     This task targets <c>netstandard2.0</c> so the same assembly runs under both .NET Framework
///     MSBuild (Visual Studio) and .NET SDK MSBuild (<c>dotnet build</c>). Language generators are
///     intentionally kept out-of-process in <c>ApiMark.Tool</c>, which targets <c>net8.0</c> and may
///     use libraries that do not support <c>netstandard2.0</c>.
/// </remarks>
public sealed class ApiMarkTask : Task
{
    /// <summary>Language identifier for .NET documentation generation.</summary>
    private const string DotNetLanguage = "dotnet";
    /// <summary>Language identifier for C++ documentation generation.</summary>
    private const string CppLanguage = "cpp";
    /// <summary>
    ///     Gets or sets a value indicating whether documentation generation is suppressed.
    /// </summary>
    /// <remarks>
    ///     When <c>true</c>, Execute returns <c>true</c> immediately with no side effects, allowing
    ///     projects to opt out of documentation generation without removing the NuGet package reference.
    ///     Maps to the <c>$(DisableApiMark)</c> MSBuild property.
    /// </remarks>
    public bool DisableApiMark { get; set; }

    /// <summary>
    ///     Gets or sets the explicit documentation language. When <c>null</c> or empty, the language
    ///     is inferred from <see cref="ProjectExtension"/>.
    /// </summary>
    /// <remarks>
    ///     Accepted values: <c>dotnet</c>, <c>cpp</c>. Maps to the <c>$(ApiMarkLanguage)</c> MSBuild
    ///     property. Leave unset to use automatic language inference.
    /// </remarks>
    public string? ApiMarkLanguage { get; set; }

    /// <summary>
    ///     Gets or sets the MSBuild project file extension, used to infer the language when
    ///     <see cref="ApiMarkLanguage"/> is not set.
    /// </summary>
    /// <remarks>
    ///     Maps to <c>$(MSBuildProjectExtension)</c>. A value of <c>.vcxproj</c> (case-insensitive)
    ///     causes the task to infer the language as <c>cpp</c>; all other extensions infer
    ///     <c>dotnet</c>. Must not be null or empty.
    /// </remarks>
    [Required]
    public string ProjectExtension { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the directory where generated Markdown documentation is written.
    /// </summary>
    /// <remarks>
    ///     Maps to <c>$(ApiMarkOutputDir)</c>. When not set, the tool uses its own default output
    ///     directory. Optional — omit the <c>--output</c> flag when this property is empty.
    /// </remarks>
    public string? ApiMarkOutputDir { get; set; }

    /// <summary>
    ///     Gets or sets the visibility filter forwarded to the tool.
    /// </summary>
    /// <remarks>
    ///     Accepted values: <c>Public</c>, <c>PublicAndProtected</c>, <c>All</c>. Maps to
    ///     <c>$(ApiMarkVisibility)</c>. When not set, the tool applies its own default
    ///     (<c>Public</c>). Optional — omit the <c>--visibility</c> flag when this property is empty.
    /// </remarks>
    public string? ApiMarkVisibility { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether members marked <c>[Obsolete]</c> are included
    ///     in the generated documentation.
    /// </summary>
    /// <remarks>
    ///     When <c>true</c>, the <c>--include-obsolete</c> flag is passed to the tool. Maps to
    ///     <c>$(ApiMarkIncludeObsolete)</c>. Defaults to <c>false</c>.
    /// </remarks>
    public bool ApiMarkIncludeObsolete { get; set; }

    /// <summary>
    ///     Gets or sets the path to the compiled .NET assembly to document.
    /// </summary>
    /// <remarks>
    ///     Used for the <c>dotnet</c> language only. Maps to <c>$(ApiMarkAssemblyPath)</c>; the
    ///     <c>.targets</c> file defaults this to <c>$(TargetPath)</c> when not explicitly set.
    /// </remarks>
    public string? ApiMarkAssemblyPath { get; set; }

    /// <summary>
    ///     Gets or sets the path to the compiler-generated XML documentation file.
    /// </summary>
    /// <remarks>
    ///     Used for the <c>dotnet</c> language only. Maps to <c>$(ApiMarkXmlDocPath)</c>; the
    ///     <c>.targets</c> file defaults this to <c>$(DocumentationFile)</c> when not explicitly
    ///     set. When empty and the language is <c>dotnet</c>, the task skips generation gracefully
    ///     and returns <c>true</c>.
    /// </remarks>
    public string? ApiMarkXmlDocPath { get; set; }

    /// <summary>
    ///     Gets or sets the semicolon-separated list of include directory paths for C++ documentation.
    /// </summary>
    /// <remarks>
    ///     Used for the <c>cpp</c> language only. Maps to <c>$(ApiMarkIncludePaths)</c>.
    ///     Each semicolon-delimited entry is forwarded as an individual <c>--includes</c> flag;
    ///     all entries are passed to Clang as <c>-I</c> paths and serve as the base directories
    ///     for the default header glob when <see cref="ApiMarkApiHeaders"/> is not set.
    /// </remarks>
    public string? ApiMarkIncludePaths { get; set; }

    /// <summary>Gets or sets the library name for the C++ documentation root heading.</summary>
    /// <remarks>
    ///     Used for the <c>cpp</c> language only. Maps to <c>$(ApiMarkLibraryName)</c>; the
    ///     <c>.targets</c> file defaults this to <c>$(MSBuildProjectName)</c>.
    /// </remarks>
    public string? ApiMarkLibraryName { get; set; }

    /// <summary>Gets or sets an optional description for the C++ library.</summary>
    /// <remarks>
    ///     Used for the <c>cpp</c> language only. Maps to <c>$(ApiMarkLibraryDescription)</c>.
    ///     Optional — omitted when empty.
    /// </remarks>
    public string? ApiMarkLibraryDescription { get; set; }

    /// <summary>Gets or sets semicolon-separated preprocessor definitions for the Clang parser.</summary>
    /// <remarks>
    ///     Used for the <c>cpp</c> language only. Maps to <c>$(ApiMarkDefines)</c>.
    ///     Semicolons are converted to commas for the <c>--defines</c> tool argument.
    /// </remarks>
    public string? ApiMarkDefines { get; set; }

    /// <summary>Gets or sets the C++ language standard passed to Clang.</summary>
    /// <remarks>
    ///     Used for the <c>cpp</c> language only. Maps to <c>$(ApiMarkCppStandard)</c>.
    ///     Accepted values: <c>c++14</c>, <c>c++17</c>, <c>c++20</c>, <c>c++23</c>.
    ///     Optional — omitted when empty; the tool defaults to <c>c++17</c>.
    /// </remarks>
    public string? ApiMarkCppStandard { get; set; }

    /// <summary>Gets or sets the path to the clang executable for C++ documentation generation.</summary>
    /// <remarks>
    ///     Used for the <c>cpp</c> language only. Maps to <c>$(ApiMarkClangPath)</c>.
    ///     Optional — when empty, clang is located automatically via PATH, xcrun (macOS), or
    ///     vswhere (Windows).
    /// </remarks>
    public string? ApiMarkClangPath { get; set; }

    /// <summary>Gets or sets the semicolon-separated, order-preserved list of glob and exclusion pattern strings for C++ header selection.</summary>
    /// <remarks>
    ///     Used for the <c>cpp</c> language only. Maps to <c>$(ApiMarkApiHeaders)</c>.
    ///     Entries are forwarded as individual <c>--api-headers</c> flags in the order they appear.
    ///     Entries with a <c>!</c> prefix are exclusion patterns; entries without are include
    ///     patterns. Gitignore semantics apply: the last matching pattern wins, enabling
    ///     include/exclude/re-include sequences.
    ///     Optional — when empty, all headers under <c>$(ApiMarkIncludePaths)</c> with recognized
    ///     C++ header extensions are documented.
    /// </remarks>
    public string? ApiMarkApiHeaders { get; set; }

    /// <summary>
    ///     Gets or sets the output format forwarded to the tool.
    /// </summary>
    /// <remarks>
    ///     Accepted values: <c>gradual</c>, <c>single-file</c>. Maps to <c>$(ApiMarkFormat)</c>.
    ///     When not set, the tool applies its own default (<c>gradual</c>).
    ///     Optional — omit the <c>--format</c> flag when this property is empty.
    /// </remarks>
    public string? ApiMarkFormat { get; set; }

    /// <summary>
    ///     Gets or sets an optional list of named output configurations for multi-format generation.
    /// </summary>
    /// <remarks>
    ///     Maps to the <c>ApiMarkOutput</c> item group. Each item may carry <c>OutputDir</c>,
    ///     <c>Format</c>, and <c>Visibility</c> metadata. When this collection is non-empty,
    ///     the task spawns one child process per item instead of the single-invocation path.
    ///     When empty or absent, the task uses the scalar <see cref="ApiMarkOutputDir"/>,
    ///     <see cref="ApiMarkVisibility"/>, and <see cref="ApiMarkFormat"/> properties.
    /// </remarks>
    public Microsoft.Build.Framework.ITaskItem[]? ApiMarkOutputs { get; set; }

    /// <summary>
    ///     Gets or sets the full path to <c>ApiMark.Tool.dll</c> bundled inside the NuGet package.
    /// </summary>
    /// <remarks>
    ///     Set by the <c>.targets</c> file to the <c>tools/net8.0/ApiMark.Tool.dll</c> location
    ///     inside the installed NuGet package. Must not be overridden by project authors. Must not be
    ///     null or empty.
    /// </remarks>
    [Required]
    public string ToolDllPath { get; set; } = string.Empty;

    /// <summary>
    ///     Determines the documentation language to use, inferring from the project extension when
    ///     <see cref="ApiMarkLanguage"/> is not explicitly set.
    /// </summary>
    /// <returns>
    ///     <c>"dotnet"</c> for SDK-style projects; <c>"cpp"</c> when
    ///     <see cref="ProjectExtension"/> is <c>.vcxproj</c> (case-insensitive); the explicit value
    ///     of <see cref="ApiMarkLanguage"/> when set.
    /// </returns>
    internal string ResolveLanguage()
    {
        // Use the explicitly-configured language if provided
        if (!string.IsNullOrEmpty(ApiMarkLanguage))
        {
            return ApiMarkLanguage!;
        }

        // Infer from project extension: .vcxproj → cpp, all others → dotnet
        if (string.Equals(ProjectExtension, ".vcxproj", StringComparison.OrdinalIgnoreCase))
        {
            return "cpp";
        }

        return DotNetLanguage;
    }

    /// <summary>
    ///     Builds the argument list for <c>ApiMark.Tool.dll</c> based on the resolved language
    ///     and the current property values.
    /// </summary>
    /// <param name="language">
    ///     The resolved language; <c>"dotnet"</c> or <c>"cpp"</c>. Must not be null or empty.
    ///     Throws <see cref="InvalidOperationException"/> for any other value.
    /// </param>
    /// <returns>
    ///     An ordered list of individual arguments to pass to <c>dotnet ApiMark.Tool.dll</c>,
    ///     starting with the language subcommand. Each element is an unquoted value; callers
    ///     must add them via <c>ProcessStartInfo.ArgumentList</c> so that the runtime applies
    ///     correct OS-level quoting.
    /// </returns>
    internal IReadOnlyList<string> BuildArguments(string language)
    {
        var args = new List<string>();

        if (language == DotNetLanguage)
        {
            AppendDotNetArguments(args);
        }
        else if (language == CppLanguage)
        {
            AppendCppArguments(args);
        }
        else
        {
            throw new InvalidOperationException($"Unsupported language: '{language}'.");
        }

        AppendCommonArguments(args);

        return args;
    }

    /// <summary>
    ///     Appends the .NET-specific CLI arguments to <paramref name="args"/>, including the
    ///     language subcommand (<c>dotnet</c>), <c>--assembly</c>, and <c>--xml-doc</c>.
    /// </summary>
    /// <param name="args">The argument list being built by <see cref="BuildArguments"/>.</param>
    private void AppendDotNetArguments(List<string> args)
    {
        // Assembly and XML doc paths are both required for .NET documentation
        args.Add(DotNetLanguage);
        args.Add("--assembly");
        args.Add(ApiMarkAssemblyPath ?? string.Empty);
        args.Add("--xml-doc");
        args.Add(ApiMarkXmlDocPath ?? string.Empty);
    }

    /// <summary>
    ///     Appends all C++ specific CLI arguments to <paramref name="args"/>, including the
    ///     <c>cpp</c> language subcommand and all optional C++ flags
    ///     (<c>--includes</c>, <c>--api-headers</c>, <c>--library-name</c>,
    ///     <c>--library-description</c>, <c>--defines</c>, <c>--cpp-standard</c>,
    ///     <c>--clang-path</c>).
    /// </summary>
    /// <param name="args">The argument list being built by <see cref="BuildArguments"/>.</param>
    private void AppendCppArguments(List<string> args)
    {
        args.Add("cpp");

        // Emit one --includes flag per path entry — each semicolon-delimited entry becomes
        // a separate repeatable --includes argument so paths with spaces are unambiguous
        if (!string.IsNullOrEmpty(ApiMarkIncludePaths))
        {
            // Manually trim each entry — StringSplitOptions.TrimEntries is not
            // available in netstandard2.0 which this assembly targets
            foreach (var entry in ApiMarkIncludePaths!.Split(';').Select(e => e.Trim()))
            {
                if (string.IsNullOrEmpty(entry))
                {
                    continue;
                }

                args.Add("--includes");
                args.Add(entry);
            }
        }

        // Emit one --api-headers flag per pattern entry, order-preserved including ! exclusion patterns
        if (!string.IsNullOrEmpty(ApiMarkApiHeaders))
        {
            // Manually trim each entry — StringSplitOptions.TrimEntries is not
            // available in netstandard2.0 which this assembly targets
            foreach (var entry in ApiMarkApiHeaders!.Split(';').Select(e => e.Trim()))
            {
                if (string.IsNullOrEmpty(entry))
                {
                    continue;
                }

                args.Add("--api-headers");
                args.Add(entry);
            }
        }

        // Library name (defaults to project name via .targets)
        if (!string.IsNullOrEmpty(ApiMarkLibraryName))
        {
            args.Add("--library-name");
            args.Add(ApiMarkLibraryName!);
        }

        // Optional library description
        if (!string.IsNullOrEmpty(ApiMarkLibraryDescription))
        {
            args.Add("--library-description");
            args.Add(ApiMarkLibraryDescription!);
        }

        // Preprocessor defines — semicolons converted to commas
        if (!string.IsNullOrEmpty(ApiMarkDefines))
        {
            var commaDefines = ApiMarkDefines!.Replace(';', ',');
            args.Add("--defines");
            args.Add(commaDefines);
        }

        // C++ standard
        if (!string.IsNullOrEmpty(ApiMarkCppStandard))
        {
            args.Add("--cpp-standard");
            args.Add(ApiMarkCppStandard!);
        }

        // Optional: explicit clang path
        if (!string.IsNullOrEmpty(ApiMarkClangPath))
        {
            args.Add("--clang-path");
            args.Add(ApiMarkClangPath!);
        }
    }

    /// <summary>
    ///     Appends the common output and visibility arguments to <paramref name="args"/>,
    ///     shared by both the .NET and C++ language paths: <c>--output</c>,
    ///     <c>--visibility</c>, and <c>--include-obsolete</c>.
    /// </summary>
    /// <param name="args">The argument list being built by <see cref="BuildArguments"/>.</param>
    private void AppendCommonArguments(List<string> args)
    {
        // Optional: output directory
        if (!string.IsNullOrEmpty(ApiMarkOutputDir))
        {
            args.Add("--output");
            args.Add(ApiMarkOutputDir!);
        }

        // Optional: visibility filter
        if (!string.IsNullOrEmpty(ApiMarkVisibility))
        {
            args.Add("--visibility");
            args.Add(ApiMarkVisibility!);
        }

        // Optional: include obsolete members
        if (ApiMarkIncludeObsolete)
        {
            args.Add("--include-obsolete");
        }

        // Optional: output format
        if (!string.IsNullOrEmpty(ApiMarkFormat))
        {
            args.Add("--format");
            args.Add(ApiMarkFormat!);
        }
    }

    /// <summary>
    ///     Builds the argument list for a named output item from the <c>ApiMarkOutput</c> item group,
    ///     overriding scalar output, visibility, and format values with item metadata.
    /// </summary>
    /// <param name="language">Resolved language string (<c>"dotnet"</c> or <c>"cpp"</c>).</param>
    /// <param name="outputItem">The MSBuild item providing per-output metadata.</param>
    /// <returns>An ordered list of arguments for the child process invocation.</returns>
    internal IReadOnlyList<string> BuildArgumentsForOutput(string language, Microsoft.Build.Framework.ITaskItem outputItem)
    {
        // Override scalar properties for this invocation
        var savedOutputDir = ApiMarkOutputDir;
        var savedVisibility = ApiMarkVisibility;
        var savedFormat = ApiMarkFormat;

        try
        {
            var itemOutputDir = outputItem.GetMetadata("OutputDir");
            var itemVisibility = outputItem.GetMetadata("Visibility");
            var itemFormat = outputItem.GetMetadata("Format");

            if (!string.IsNullOrEmpty(itemOutputDir))
            {
                ApiMarkOutputDir = itemOutputDir;
            }

            if (!string.IsNullOrEmpty(itemVisibility))
            {
                ApiMarkVisibility = itemVisibility;
            }

            if (!string.IsNullOrEmpty(itemFormat))
            {
                ApiMarkFormat = itemFormat;
            }

            return BuildArguments(language);
        }
        finally
        {
            ApiMarkOutputDir = savedOutputDir;
            ApiMarkVisibility = savedVisibility;
            ApiMarkFormat = savedFormat;
        }
    }

    /// <summary>
    ///     MSBuild entry point; spawns the ApiMark.Tool child process and pipes its output to the
    ///     MSBuild build log.
    /// </summary>
    /// <returns>
    ///     <c>true</c> if the tool process exits with code zero or if generation was skipped
    ///     legitimately; <c>false</c> if the tool exits with a non-zero code or cannot be started,
    ///     causing MSBuild to mark the build as failed.
    /// </returns>
    public override bool Execute()
    {
        // Short-circuit immediately when the user has disabled ApiMark — no side effects
        if (DisableApiMark)
        {
            return true;
        }

        // Determine which language generator to invoke
        var language = ResolveLanguage();

        // For .NET projects, skip gracefully when no XML doc path is configured
        if (language == DotNetLanguage && string.IsNullOrEmpty(ApiMarkXmlDocPath))
        {
            Log.LogMessage(MessageImportance.Normal, "Skipping ApiMark: ApiMarkXmlDocPath not set.");
            return true;
        }

        // For C++ projects, skip gracefully when no include paths are configured
        if (language == CppLanguage && string.IsNullOrWhiteSpace(ApiMarkIncludePaths))
        {
            Log.LogMessage(MessageImportance.Normal,
                "Skipping ApiMark: no include paths resolved for C++ documentation generation. " +
                "Ensure ClCompile items have AdditionalIncludeDirectories set, " +
                "or set $(ApiMarkIncludePaths) explicitly in your .vcxproj.");
            return true;
        }

        // Reject unrecognized language values early to prevent silent argument mis-routing
        if (language != DotNetLanguage && language != CppLanguage)
        {
            Log.LogError(
                $"ApiMark: language '{language}' is not supported by the MSBuild task. " +
                "Supported values are 'dotnet' and 'cpp'. " +
                "Use the ApiMark.Tool CLI directly for other languages.");
            return false;
        }

        // Verify the bundled tool DLL exists before attempting to spawn it
        if (!File.Exists(ToolDllPath))
        {
            Log.LogError(
                $"ApiMark.Tool not found at '{ToolDllPath}'. Ensure the NuGet package is installed correctly.");
            return false;
        }

        // Locate the dotnet executable needed to run the tool DLL
        var dotnetExe = ResolveDotNetExe();
        if (dotnetExe is null)
        {
            Log.LogError(
                "Cannot locate the 'dotnet' executable. " +
                "Ensure the .NET SDK is installed and available via DOTNET_HOST_PATH or PATH.");
            return false;
        }

        // When ApiMarkOutputs is populated, spawn one child process per output item
        if (ApiMarkOutputs != null && ApiMarkOutputs.Length > 0)
        {
            var allSucceeded = true;
            foreach (var outputItem in ApiMarkOutputs)
            {
                var itemArgs = BuildArgumentsForOutput(language, outputItem);
                if (!RunToolProcess(dotnetExe, itemArgs))
                {
                    allSucceeded = false;
                }
            }

            return allSucceeded;
        }

        // Single-invocation path: use scalar properties as before
        return RunToolProcess(dotnetExe, BuildArguments(language));
    }

    /// <summary>
    ///     Spawns an ApiMark.Tool child process with the given argument list and pipes
    ///     its output to the MSBuild build log.
    /// </summary>
    /// <param name="dotnetExe">Full path to the <c>dotnet</c> executable.</param>
    /// <param name="toolArgs">Ordered argument list starting with the language subcommand.</param>
    /// <returns><c>true</c> when the process exits with code zero; <c>false</c> otherwise.</returns>
    private bool RunToolProcess(string dotnetExe, IReadOnlyList<string> toolArgs)
    {
        // Configure the child process with redirected I/O so all output feeds the MSBuild log.
        // ArgumentList is used instead of Arguments so that the runtime applies correct
        // OS-level quoting regardless of whether the host is Windows, macOS, or Linux.
        var psi = new ProcessStartInfo
        {
            FileName = dotnetExe,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        // First argument is the tool DLL path, followed by the language-specific arguments
        psi.ArgumentList.Add(ToolDllPath!);
        foreach (var arg in toolArgs)
        {
            psi.ArgumentList.Add(arg);
        }

        // Start the tool process; a null return indicates the OS refused to launch it
        using var process = Process.Start(psi);
        if (process is null)
        {
            Log.LogError("Failed to start ApiMark.Tool process.");
            return false;
        }

        // Route stdout lines as normal informational build messages
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                Log.LogMessage(MessageImportance.Normal, e.Data);
            }
        };

        // Route stderr lines as build errors so MSBuild surfaces them prominently
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                Log.LogError(e.Data);
            }
        };

        // Begin asynchronous reads and then wait; the no-timeout overload of WaitForExit
        // guarantees all DataReceived events are drained before returning
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        // A non-zero exit code means the tool encountered an error during generation
        if (process.ExitCode != 0)
        {
            Log.LogError($"ApiMark.Tool exited with code {process.ExitCode}.");
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Locates the <c>dotnet</c> executable by checking <c>DOTNET_HOST_PATH</c> first and then
    ///     searching each directory listed in the <c>PATH</c> environment variable.
    /// </summary>
    /// <returns>
    ///     The full path to the <c>dotnet</c> executable, or <c>null</c> if it cannot be found in
    ///     either <c>DOTNET_HOST_PATH</c> or <c>PATH</c>.
    /// </returns>
    private static string? ResolveDotNetExe()
    {
        // DOTNET_HOST_PATH is set by the .NET SDK on all platforms and points directly to the
        // dotnet host executable; prefer it over PATH for reliability in build environments
        var hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrEmpty(hostPath) && File.Exists(hostPath))
        {
            return hostPath;
        }

        // Fall back to a PATH search; on Windows the executable has the .exe extension
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
        var exeName = isWindows ? "dotnet.exe" : "dotnet";

        foreach (var dir in pathVar.Split(Path.PathSeparator))
        {
            // Skip empty entries that result from consecutive path separators
            if (string.IsNullOrEmpty(dir))
            {
                continue;
            }

            // exeName is a known literal ("dotnet" or "dotnet.exe") and never starts with a
            // separator, so Path.Join is correct and performs simple safe concatenation.
            var candidate = Path.Join(dir, exeName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }


}
