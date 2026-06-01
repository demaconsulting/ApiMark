using System.Diagnostics;
using System.IO.Compression;
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

        RunInIsolation(packagesDir, workDir =>
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

        RunInIsolation(packagesDir, workDir =>
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

        RunInIsolation(packagesDir, workDir =>
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
    ///     The fixture <c>SampleLib.csproj</c> uses a placeholder version of
    ///     <c>0.0.0</c> for the <c>DemaConsulting.ApiMark.MSBuild</c> package reference.
    ///     This method detects the actual version from the <c>.nupkg</c> filename and patches
    ///     the copy in the isolated directory so <c>dotnet restore</c> can resolve it
    ///     regardless of whether the build is a local dev build or a CI versioned build.
    /// </remarks>
    private static void RunInIsolation(string packagesDir, Action<string> action)
    {
        var testBinDir = Path.GetDirectoryName(typeof(PackageIntegrationTests).Assembly.Location)!;
        var fixtureDir = Path.Join(testBinDir, "Fixtures", "SampleLib");
        var workDir = Path.Join(
            Path.GetTempPath(),
            $"apimark-pkg-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workDir);

        try
        {
            foreach (var file in Directory.GetFiles(fixtureDir))
            {
                File.Copy(file, Path.Join(workDir, Path.GetFileName(file)));
            }

            // Detect the actual package version from the .nupkg filename and patch the
            // fixture .csproj so dotnet restore resolves it in both dev and CI builds.
            var nupkgPath = Directory.GetFiles(packagesDir, "DemaConsulting.ApiMark.MSBuild.*.nupkg").First();
            var packageVersion = Path.GetFileNameWithoutExtension(nupkgPath)
                .Substring("DemaConsulting.ApiMark.MSBuild.".Length);
            var csprojPath = Path.Join(workDir, "SampleLib.csproj");
            File.WriteAllText(
                csprojPath,
                File.ReadAllText(csprojPath).Replace(
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
