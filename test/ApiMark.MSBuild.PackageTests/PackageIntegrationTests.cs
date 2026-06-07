using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using Xunit;

namespace ApiMark.MSBuild.PackageTests;

/// <summary>
///     Package-level integration tests that verify the <c>DemaConsulting.ApiMark.MSBuild</c> NuGet
///     package produces correct documentation when consumed by a real <c>dotnet build</c> invocation.
/// </summary>
/// <remarks>
///     These tests exercise the full package install path: the <c>.targets</c> file is auto-imported
///     by NuGet, <c>UsingTask</c> loads the task assembly from <c>tasks/netstandard2.0/</c>, and the
///     task spawns <c>ApiMark.Tool.dll</c> from <c>tools/net8.0/</c>. This is the only test layer
///     that catches wrong paths inside the <c>.nupkg</c>.
///
///     The tests require the package to be pre-built by the build script. They are skipped gracefully
///     when no <c>.nupkg</c> is found so local developers who have not run the pack step are not
///     blocked. In CI the build script runs <c>dotnet pack</c> and sets
///     <c>APIMARK_TEST_PACKAGES_DIR</c> before invoking <c>dotnet test</c>.
/// </remarks>
public class PackageIntegrationTests
{
    /// <summary>
    ///     Validates that a .NET project referencing the <c>DemaConsulting.ApiMark.MSBuild</c> NuGet
    ///     package generates <c>api.md</c> automatically when <c>dotnet build</c> runs.
    /// </summary>
    /// <remarks>
    ///     This test exercises the complete package-consumption path: NuGet restore, <c>.targets</c>
    ///     auto-import, task assembly loading, and out-of-process tool spawn. It is skipped when the
    ///     packed <c>.nupkg</c> is not present in the local packages directory.
    /// </remarks>
    [Fact]
    public void ApiMarkMsbuild_NuGetPackage_DotNetProject_AutoDocumentsOnBuild()
    {
        var packagesDir = SkipIfPackageAbsent();
        var outputDir = string.Empty;

        RunInIsolation(packagesDir, "DotNet/SampleLib", "SampleLib.csproj", workDir =>
        {
            outputDir = Path.Join(workDir, "api");
            var result = RunProcess(
                "dotnet",
                $"build SampleLib.csproj --configuration Release -p:ApiMarkOutputDir=\"{outputDir}\"",
                workDir,
                IsolatedNuGetEnv(workDir));

            Assert.True(
                result.ExitCode == 0,
                $"dotnet build failed (exit {result.ExitCode}).\nstdout:\n{result.Output}\nstderr:\n{result.Error}");

            Assert.True(
                File.Exists(Path.Join(outputDir, "api.md")),
                $"api.md was not created in '{outputDir}'.\nBuild output:\n{result.Output}");
        });
    }

    /// <summary>
    ///     Validates that <c>dotnet pack</c> includes the generated <c>api/</c> documentation folder
    ///     in the <c>.nupkg</c> when <c>ApiMarkPackDocs=true</c> is set.
    /// </summary>
    [Fact]
    public void ApiMarkMsbuild_NuGetPackage_DotNetProject_PacksDocs_WhenApiMarkPackDocsTrue()
    {
        var packagesDir = SkipIfPackageAbsent();

        RunInIsolation(packagesDir, "DotNet/SampleLib", "SampleLib.csproj", workDir =>
        {
            var outputDir = Path.Join(workDir, "api");
            var packOutputDir = Path.Join(workDir, "pkg");
            Directory.CreateDirectory(packOutputDir);

            var result = RunProcess(
                "dotnet",
                $"pack SampleLib.csproj --configuration Release " +
                $"-p:ApiMarkOutputDir=\"{outputDir}\" " +
                $"-p:ApiMarkPackDocs=true " +
                $"--output \"{packOutputDir}\"",
                workDir,
                IsolatedNuGetEnv(workDir));

            Assert.True(
                result.ExitCode == 0,
                $"dotnet pack failed (exit {result.ExitCode}).\nstdout:\n{result.Output}\nstderr:\n{result.Error}");

            var nupkg = Directory.GetFiles(packOutputDir, "*.nupkg").FirstOrDefault();
            Assert.NotNull(nupkg);

            using var zip = ZipFile.OpenRead(nupkg);
            Assert.Contains(zip.Entries, e => e.FullName == "api/api.md");
        });
    }

    /// <summary>
    ///     Validates that the <c>api/</c> documentation folder is <em>not</em> included in the
    ///     <c>.nupkg</c> when <c>ApiMarkPackDocs</c> is not set (opt-in behavior).
    /// </summary>
    [Fact]
    public void ApiMarkMsbuild_NuGetPackage_DotNetProject_DoesNotPackDocs_ByDefault()
    {
        var packagesDir = SkipIfPackageAbsent();

        RunInIsolation(packagesDir, "DotNet/SampleLib", "SampleLib.csproj", workDir =>
        {
            var outputDir = Path.Join(workDir, "api");
            var packOutputDir = Path.Join(workDir, "pkg");
            Directory.CreateDirectory(packOutputDir);

            var result = RunProcess(
                "dotnet",
                $"pack SampleLib.csproj --configuration Release " +
                $"-p:ApiMarkOutputDir=\"{outputDir}\" " +
                $"-p:ApiMarkPackDocs=false " +
                $"--output \"{packOutputDir}\"",
                workDir,
                IsolatedNuGetEnv(workDir));

            Assert.True(
                result.ExitCode == 0,
                $"dotnet pack failed (exit {result.ExitCode}).\nstdout:\n{result.Output}\nstderr:\n{result.Error}");

            var nupkg = Directory.GetFiles(packOutputDir, "*.nupkg").FirstOrDefault();
            Assert.NotNull(nupkg);

            using var zip = ZipFile.OpenRead(nupkg);
            Assert.DoesNotContain(zip.Entries, e => e.FullName.StartsWith("api/"));
        });
    }

    /// <summary>
    ///     Skips the calling test if the pre-built <c>DemaConsulting.ApiMark.MSBuild</c> package is
    ///     absent, and returns the packages directory path when present.
    /// </summary>
    private static string SkipIfPackageAbsent()
    {
        var packagesDir = ResolvePackagesDir();
        var packageExists = Directory.Exists(packagesDir) &&
                            Directory.GetFiles(packagesDir, "DemaConsulting.ApiMark.MSBuild.*.nupkg").Length > 0;
        if (!packageExists)
        {
            Assert.Skip(
                $"No DemaConsulting.ApiMark.MSBuild .nupkg found in '{packagesDir}'. " +
                "Run 'dotnet pack src/ApiMark.MSBuild/ApiMark.MSBuild.csproj' first.");
        }

        return packagesDir;
    }

    /// <summary>
    ///     Runs <paramref name="action"/> in an isolated temp directory pre-populated with the
    ///     fixture project files and a local <c>nuget.config</c>, then cleans up on exit.
    /// </summary>
    /// <remarks>
    ///     The fixture project file at <paramref name="projectFileName"/> uses a placeholder version of
    ///     <c>0.0.0</c> for the <c>DemaConsulting.ApiMark.MSBuild</c> package reference.
    ///     This method detects the actual version from the <c>.nupkg</c> filename and patches
    ///     the copy in the isolated directory so <c>dotnet restore</c> can resolve it
    ///     regardless of whether the build is a local dev build or a CI versioned build.
    /// </remarks>
    /// <param name="packagesDir">Directory containing the pre-built <c>.nupkg</c> files.</param>
    /// <param name="fixtureSubPath">
    ///     Sub-path relative to the test binary's <c>Fixtures/</c> directory (e.g.
    ///     <c>"DotNet/SampleLib"</c> or <c>"Cpp/SampleLib"</c>).
    /// </param>
    /// <param name="projectFileName">
    ///     Name of the project file within the fixture directory (e.g. <c>"SampleLib.csproj"</c> or
    ///     <c>"SampleLib.vcxproj"</c>). The package version placeholder is patched in this file.
    /// </param>
    /// <param name="action">Callback invoked with the path to the prepared work directory.</param>
    private static void RunInIsolation(string packagesDir, string fixtureSubPath, string projectFileName, Action<string> action)
    {
        var testBinDir = Path.GetDirectoryName(typeof(PackageIntegrationTests).Assembly.Location)!;
        var fixtureDir = Path.Join(testBinDir, "Fixtures", fixtureSubPath);
        var workDir = Path.Join(
            Path.GetTempPath(),
            $"apimark-pkg-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);

        try
        {
            // Copy all files and subdirectories from fixtureDir to workDir so that fixtures
            // with nested directories (e.g. the Cpp fixture's include/ subdirectory) are
            // fully available in the isolated environment.
            foreach (var file in Directory.GetFiles(fixtureDir, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(fixtureDir, file);
                var destPath = Path.Join(workDir, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                File.Copy(file, destPath);
            }

            // Detect the actual package version from the .nupkg filename and patch the
            // fixture project file so restore resolves it in both dev and CI builds.
            var nupkgPath = Directory.GetFiles(packagesDir, "DemaConsulting.ApiMark.MSBuild.*.nupkg").First();
            var packageVersion = Path.GetFileNameWithoutExtension(nupkgPath)
                .Substring("DemaConsulting.ApiMark.MSBuild.".Length);
            var projectPath = Path.Join(workDir, projectFileName);
            File.WriteAllText(
                projectPath,
                File.ReadAllText(projectPath).Replace(
                    "Include=\"DemaConsulting.ApiMark.MSBuild\" Version=\"0.0.0\"",
                    $"Include=\"DemaConsulting.ApiMark.MSBuild\" Version=\"{packageVersion}\""));

            // Write a nuget.config so NuGet restores DemaConsulting.ApiMark.MSBuild from the
            // local packages directory without reaching out to nuget.org.
            var nugetConfig = $"""
                <?xml version="1.0" encoding="utf-8"?>
                <configuration>
                  <packageSources>
                    <clear />
                    <add key="local" value="{packagesDir}" />
                    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                  </packageSources>
                </configuration>
                """;
            File.WriteAllText(Path.Join(workDir, "nuget.config"), nugetConfig);

            action(workDir);
        }
        finally
        {
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, recursive: true);
            }
        }
    }

    /// <summary>
    ///     Validates that a C++ vcxproj referencing the <c>DemaConsulting.ApiMark.MSBuild</c> NuGet
    ///     package generates <c>api.md</c> automatically when <c>msbuild.exe</c> runs.
    /// </summary>
    /// <remarks>
    ///     This test is skipped on non-Windows platforms and when MSBuild with C++ tools is not
    ///     installed. It exercises the complete package-consumption path via a real msbuild.exe
    ///     invocation: NuGet restore, C++ targets import, and out-of-process tool spawn.
    /// </remarks>
    [Fact]
    public void ApiMarkMsbuild_NuGetPackage_CppVcxprojProject_AutoDocumentsOnBuild()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Assert.Skip("vcxproj integration tests only run on Windows.");
        }

        var packagesDir = SkipIfPackageAbsent();

        var msbuildExe = FindMsBuildExe();
        if (msbuildExe == null)
        {
            Assert.Skip("MSBuild with C++ tools (VC.Tools.x86.x64) not found; skipping vcxproj test.");
        }

        // Arrange / Act / Assert: run restore as a separate msbuild pass so that the
        // NuGet-generated .g.targets is on disk before the Build evaluation begins.
        // A single /t:restore;Build invocation evaluates the project once — before restore
        // creates obj\*.nuget.g.targets — so the Condition="Exists(...)" import fires false
        // and the ApiMark targets are never loaded. Two separate invocations ensure that the
        // generated file exists when the project is re-evaluated for the Build pass.
        RunInIsolation(packagesDir, "Cpp/SampleLib", "SampleLib.vcxproj", workDir =>
        {
            // Restore pass: generates obj\SampleLib.vcxproj.nuget.g.targets on disk
            var restoreResult = RunProcess(
                msbuildExe,
                "SampleLib.vcxproj /t:restore",
                workDir,
                IsolatedNuGetEnv(workDir));

            Assert.True(
                restoreResult.ExitCode == 0,
                $"msbuild restore failed (exit {restoreResult.ExitCode}).\nstdout:\n{restoreResult.Output}\nstderr:\n{restoreResult.Error}");

            var outputDir = Path.Join(workDir, "api");

            // Build pass: re-evaluates the project so Exists(...) picks up the generated
            // targets file and the AfterTargets="Build" hook runs ApiMark documentation generation
            var buildResult = RunProcess(
                msbuildExe,
                $"SampleLib.vcxproj /t:Build /p:Configuration=Release /p:Platform=x64 /p:ApiMarkOutputDir=\"{outputDir}\"",
                workDir,
                IsolatedNuGetEnv(workDir));

            Assert.True(
                buildResult.ExitCode == 0,
                $"msbuild build failed (exit {buildResult.ExitCode}).\nstdout:\n{buildResult.Output}\nstderr:\n{buildResult.Error}");

            Assert.True(
                File.Exists(Path.Join(outputDir, "api.md")),
                $"api.md was not created in '{outputDir}'.\nBuild output:\n{buildResult.Output}");
        });
    }

    /// <summary>
    ///     Locates <c>msbuild.exe</c> with C++ tools via vswhere, returning null when not found.
    /// </summary>
    /// <remarks>
    ///     Uses vswhere to query for a Visual Studio installation that includes
    ///     <c>Microsoft.VisualStudio.Component.VC.Tools.x86.x64</c>, then resolves the
    ///     MSBuild executable path within that installation. Returns the first valid match when
    ///     vswhere reports multiple results.
    /// </remarks>
    /// <returns>
    ///     Absolute path to <c>MSBuild.exe</c>, or <c>null</c> if vswhere is absent or no
    ///     matching installation exists.
    /// </returns>
    private static string? FindMsBuildExe()
    {
        var vsWherePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio", "Installer", "vswhere.exe");

        if (!File.Exists(vsWherePath))
        {
            return null;
        }

        var psi = new ProcessStartInfo(vsWherePath)
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        psi.ArgumentList.Add("-latest");
        psi.ArgumentList.Add("-requires");
        psi.ArgumentList.Add("Microsoft.VisualStudio.Component.VC.Tools.x86.x64");
        psi.ArgumentList.Add("-find");
        psi.ArgumentList.Add(@"MSBuild\**\Bin\MSBuild.exe");

        using var process = Process.Start(psi);
        if (process == null)
        {
            return null;
        }

        var output = process.StandardOutput.ReadToEnd().Trim();
        process.WaitForExit();

        // vswhere -find may return multiple matches; take the first valid one
        var msbuildPath = output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .FirstOrDefault(File.Exists);

        return msbuildPath;
    }

    /// <summary>
    ///     Returns extra environment variables that redirect the NuGet global package cache into
    ///     the isolated work directory, preventing stale cached versions of the package under test
    ///     from shadowing the freshly-packed <c>.nupkg</c>.
    /// </summary>
    private static Dictionary<string, string> IsolatedNuGetEnv(string workDir) =>
        new()
        {
            ["NUGET_PACKAGES"] = Path.Join(workDir, "nuget-packages")
        };

    /// <summary>
    ///     Resolves the directory containing the pre-built <c>DemaConsulting.ApiMark.MSBuild</c>
    ///     NuGet package.
    /// </summary>
    /// <remarks>
    ///     Checks <c>APIMARK_TEST_PACKAGES_DIR</c> first (set by the build script) and falls back
    ///     to navigating from the test binary up to the repository <c>test/packages/</c> directory.
    /// </remarks>
    /// <returns>The absolute path to the packages directory (may or may not exist).</returns>
    private static string ResolvePackagesDir()
    {
        var fromEnv = Environment.GetEnvironmentVariable("APIMARK_TEST_PACKAGES_DIR");
        if (!string.IsNullOrEmpty(fromEnv))
        {
            return Path.GetFullPath(fromEnv);
        }

        // Navigate from bin/[Config]/net8.0 up 4 levels to test/, then into packages/
        var testBinDir = Path.GetDirectoryName(typeof(PackageIntegrationTests).Assembly.Location)!;
        return Path.GetFullPath(Path.Join(testBinDir, "..", "..", "..", "..", "packages"));
    }

    /// <summary>
    ///     Runs an external process and captures its stdout, stderr, and exit code.
    /// </summary>
    private static (string Output, string Error, int ExitCode) RunProcess(
        string executable,
        string arguments,
        string workingDirectory,
        Dictionary<string, string>? extraEnvironment = null)
    {
        var psi = new ProcessStartInfo(executable, arguments)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        if (extraEnvironment != null)
        {
            foreach (var (key, value) in extraEnvironment)
            {
                psi.Environment[key] = value;
            }
        }

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {executable}");

        // Read both streams concurrently before waiting for exit to prevent deadlock:
        // reading one stream synchronously while the process blocks writing to the other
        // would deadlock when either output buffer fills. Async reads drain both buffers in parallel.
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        var output = outputTask.GetAwaiter().GetResult();
        var error = errorTask.GetAwaiter().GetResult();

        return (output, error, process.ExitCode);
    }
}
