# build.ps1
#
#
# PURPOSE:
#   Builds the ApiMark solution in Release configuration, packs ApiMark.MSBuild for the package
#   integration tests, and runs the unit tests, either all of them
#   (-All, the full local build and test run) or only the test projects a change affects
#   (-Changed / -Project).
#
# USAGE:
#   pwsh ./build.ps1 -Help                     Print the usage text and exit 0
#   pwsh ./build.ps1 -All                      Full local build and all tests
#   pwsh ./build.ps1 -Changed [-Base <ref>]    Only the test projects affected by the changes since <ref>
#                                              (default origin/main, else main) plus uncommitted changes
#   pwsh ./build.ps1 -Project Core,Tests       Only the test projects affected by the named projects
#   Add -DryRun to any scope to print the selection without building anything.
#   With no arguments the usage text is printed and the script exits 1 (a scope must be chosen).
#
#   A changed project selects every project that references it, directly or transitively, from the
#   ProjectReference graph, and files linked into another project (Compile/None/EmbeddedResource items
#   that point outside the project folder) select that project too. Changes that affect the whole
#   build (solution, project, props/targets, global.json, .editorconfig, nuget.config, tool manifest,
#   this script, scripts/ProjectScripts.psm1) fall back to the full build; repository files tests read
#   at run time select their test projects through $RuntimeReads; other changes the build does not
#   read select nothing. This works unchanged for a solution with one project pair or many.
#
#   ApiMark specifics: ApiMark.MSBuild.PackageTests consume the packed DemaConsulting.ApiMark.MSBuild
#   package, which bundles the tool and every library, so a change to any project under src/ also
#   selects them and packs ApiMark.MSBuild into test/packages first. The sample projects under
#   test/*/Fixtures are inputs of the package tests, not part of the project graph.
#
# EXTENSION POINTS:
#   Search for "[PROJECT-SPECIFIC]" comments to find the designated locations
#   for adding project-specific build or test operations.
#
# CUSTOMIZATION:
#   This script belongs to the project: extend it as the project grows. Add steps at the
#   [PROJECT-SPECIFIC] points and add optional parameters that focus a run. The reusable scoping
#   logic lives in scripts/ProjectScripts.psm1, which is delivered by the Agents release zip, is
#   shared across repositories, and must never be edited here. Slow or environment-dependent test
#   tiers (GUI automation, integration) may be opt-in switches.
#   With no arguments the script prints the usage text and exits 1; -All must still build and run
#   the standard test suite (agents rely on that), and never remove or weaken a test step to make
#   it pass.

param(
    # Full local build and all tests.
    [switch]$All,

    # Build and test only the projects affected by the changed files.
    [switch]$Changed,

    # Git reference to compare against for -Changed (default: origin/main, else main).
    [string]$Base,

    # Build and test only the projects affected by these projects (full or short names, e.g. Core, Tests).
    [string[]]$Project,

    # Print the selection and exit without building.
    [switch]$DryRun,

    # Print the usage text and exit 0.
    [switch]$Help
)

Import-Module (Join-Path $PSScriptRoot 'scripts/ProjectScripts.psm1') -Force

# [PROJECT-SPECIFIC] Repository files that tests read at run time (through the repository root, not through a
# project item), mapped to the test project that reads them. Keep in step with the tests.
# The C++ fixture headers are read by path from the Cpp and MSBuild test projects (FixturePaths.cs).
$RuntimeReads = [ordered]@{
    '^test/ApiMark\.Cpp\.Fixtures/'          = 'ApiMark.Cpp.Tests'
    '^test/ApiMark\.Cpp\.Fixtures/include/'  = 'ApiMark.MSBuild.Tests'
}

# [PROJECT-SPECIFIC] The package integration tests consume the packed ApiMark.MSBuild package; pack it into
# test/packages and expose that folder to the tests.
function Invoke-PackageBuild {
    param([switch]$NoBuild)

    Write-Host "Packing ApiMark.MSBuild..."
    $packArgs = @('--configuration', 'Release', '--output', "$PSScriptRoot/test/packages")
    if ($NoBuild) { $packArgs += '--no-build' }
    dotnet pack src/ApiMark.MSBuild/ApiMark.MSBuild.csproj @packArgs
    $packed = $LASTEXITCODE -eq 0

    # Expose the packages directory to the package integration tests
    $env:APIMARK_TEST_PACKAGES_DIR = "$PSScriptRoot/test/packages"
    return $packed
}

# ==============================================================================
# USAGE AND SCOPE VALIDATION
# ==============================================================================

function Show-Usage {
    Write-ScriptUsage -Script 'build.ps1' -IterateWith '-Changed (or -Project)' `
        -Purpose 'build the solution (Release) and run the unit tests.' `
        -Options ([ordered]@{
            '-All'             = 'Full local build and all tests; cannot be combined with another scope.'
            '-Changed'         = 'Only the test projects affected by files changed since the base, plus uncommitted files.'
            '-Base <ref>'      = 'Base reference for -Changed (default origin/main, else main).'
            '-Project <names>' = 'Only the test projects affected by these projects (full or short names, e.g. Core,DotNet.Tests).'
            '-DryRun'          = 'Print what would be built, tested, and skipped, then exit 0 without running anything.'
            '-Help'            = 'Print this text and exit 0.'
        }) `
        -Examples @('-Changed', '-Project Core', '-Changed -DryRun', '-All')
}

if ($Help) { Show-Usage; exit 0 }

$scopeProblems = Test-ScopeArguments -Base $Base -Scopes ([ordered]@{
        All     = $All.IsPresent
        Changed = $Changed.IsPresent
        Project = [bool]$Project
    })
if ($scopeProblems.Count -gt 0) {
    $scopeProblems | ForEach-Object { Write-Host "Error: $_" }
    Write-Host ""
    Show-Usage
    exit 1
}

$buildError = $false

# ==============================================================================
# SCOPED BUILD (-Changed / -Project)
# Selects the affected test projects; falls back to the full build when needed.
# ==============================================================================

$scoped = $false
if (-not $All) {
    # [PROJECT-SPECIFIC] The sample projects under test/ApiMark.MSBuild.PackageTests/Fixtures are inputs of the
    # package tests (some are deliberately not well-formed project files), so that project is scanned by hand
    # and added with its known shape: no project references and no linked files.
    $packageTestsDir = 'test/ApiMark.MSBuild.PackageTests'
    $scanRoots = @('src') + @(Get-ChildItem test -Directory | Where-Object { "test/$($_.Name)" -ne $packageTestsDir } | ForEach-Object { "test/$($_.Name)" })
    $graph = Get-ProjectGraph -Root $scanRoots
    $graph['ApiMark.MSBuild.PackageTests'] = [pscustomobject]@{
        Name       = 'ApiMark.MSBuild.PackageTests'
        Path       = "$packageTestsDir/ApiMark.MSBuild.PackageTests.csproj"
        Directory  = $packageTestsDir
        References = @()
        Links      = @()
    }
    $selected = [System.Collections.Generic.HashSet[string]]::new()
    $fullReason = $null

    foreach ($single in @($Project | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })) {
        # [PROJECT-SPECIFIC] Short names are relative to the ApiMark. prefix (Core, DotNet.Tests, MSBuild, ...); the
        # module's own short-name rule assumes the core library has the shortest name, which ApiMark.Cpp breaks.
        if ($graph.Contains("ApiMark.$single")) { $single = "ApiMark.$single" }
        $resolved = Resolve-ProjectName $graph $single
        if (-not $resolved) {
            Write-Host "Unknown project '$single'. Known projects: $($graph.Keys -join ', ')"
            exit 1
        }
        [void]$selected.Add($resolved)
    }

    if ($Changed) {
        $changedFiles = Get-ChangedFiles -Base $Base
        if ($null -eq $changedFiles) {
            $fullReason = 'the changed files could not be determined'
        } else {
            Write-Host "Changed files: $($changedFiles.Count)"
            $fullReason = Add-ChangedProjects -Graph $graph -Files $changedFiles -Selected $selected -RuntimeReads $RuntimeReads
        }
    }

    if ($fullReason) {
        Write-Host "Full build required: $fullReason."
    } else {
        Expand-ReferencingProjects -Graph $graph -Selected $selected

        # [PROJECT-SPECIFIC] The package tests consume the packed package, which bundles the tool and every
        # library, so a change to any selected src/ project selects them too
        $packageTests = 'ApiMark.MSBuild.PackageTests'
        if ($graph.Contains($packageTests) -and @($graph.Values | Where-Object { $selected.Contains($_.Name) -and $_.Directory -like 'src/*' }).Count -gt 0) {
            [void]$selected.Add($packageTests)
        }

        # Test projects are built and tested; other leaf projects (tools/) are built only.
        # [PROJECT-SPECIFIC] Only projects named *Tests are tests; the helper and fixture libraries under test/
        # are built as references of the test projects.
        $tests = @($graph.Values | Where-Object { $selected.Contains($_.Name) -and $_.Directory -like 'test/*' -and $_.Name -like '*Tests' })
        $tools = @($graph.Values | Where-Object { $selected.Contains($_.Name) -and $_.Directory -like 'tools/*' })
        $skipped = @($graph.Values | Where-Object { $_.Directory -like 'test/*' -and $_.Name -like '*Tests' -and -not $selected.Contains($_.Name) })
        if ($tests.Count -eq 0 -and $tools.Count -eq 0) {
            Write-Host "Nothing to build: no changed file is read by the build."
            exit 0
        }

        Write-Host "Affected test projects (built and tested):"
        $tests | ForEach-Object { Write-Host "  $($_.Name)" }
        if ($tools.Count -gt 0) {
            Write-Host "Affected tool projects (build only):"
            $tools | ForEach-Object { Write-Host "  $($_.Name)" }
        }
        if ($DryRun) {
            Write-Host "Skipped test projects ($($skipped.Count)): $(($skipped | ForEach-Object { $_.Name }) -join ', ')"
            Write-Host "Dry run: nothing was built or tested."
            exit 0
        }

        $scoped = $true
        foreach ($entry in $tests + $tools) {
            Write-Host "Building $($entry.Name)..."
            dotnet build $entry.Path --configuration Release
            if ($LASTEXITCODE -ne 0) { $buildError = $true }
        }

        if (-not $buildError) {
            # [PROJECT-SPECIFIC] Pack ApiMark.MSBuild before the package tests run
            if ($tests | Where-Object { $_.Name -eq $packageTests }) {
                if (-not (Invoke-PackageBuild)) { $buildError = $true }
            }
        }

        if (-not $buildError) {
            foreach ($entry in $tests) {
                Write-Host "Running tests: $($entry.Name)..."
                dotnet test $entry.Path --no-build --configuration Release --logger trx --results-directory artifacts/tests
                if ($LASTEXITCODE -ne 0) { $buildError = $true }
            }
        } else {
            Write-Host "Skipping tests: the build failed."
        }
    }
}

if ($DryRun -and -not $scoped) { Write-Host "Dry run: would run the full build and all tests."; exit 0 }

# ==============================================================================
# FULL BUILD (-All, or a scoped run that needs everything)
# ==============================================================================

if (-not $scoped) {
    # Each step runs only if the steps before it succeeded, so tests never run against stale binaries
    Write-Host "Restoring dependencies..."
    dotnet restore
    if ($LASTEXITCODE -ne 0) { $buildError = $true }

    if (-not $buildError) {
        Write-Host "Building..."
        dotnet build --no-restore --configuration Release
        if ($LASTEXITCODE -ne 0) { $buildError = $true }
    }

    # [PROJECT-SPECIFIC] Pack ApiMark.MSBuild for the package integration tests (the tests read test/packages)
    if (-not $buildError) {
        if (-not (Invoke-PackageBuild -NoBuild)) { $buildError = $true }
    }

    if (-not $buildError) {
        Write-Host "Running tests..."
        dotnet test --no-build --configuration Release --logger trx --results-directory artifacts/tests
        if ($LASTEXITCODE -ne 0) { $buildError = $true }
    } else {
        Write-Host "Skipping tests: the build failed."
    }
}

if (-not $buildError) {
    Write-Host "Build and tests completed successfully!"
}

exit ($buildError ? 1 : 0)
