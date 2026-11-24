# fix.ps1
#
# PURPOSE:
#   Applies the available auto-fixers with progress output. A fixer run always exits 0.
#   Run this after making changes to automatically handle formatting
#   so agents and developers do not need to respond to lint output.
#   Handles: dotnet format, LintMark (markdown), yamlfix, YAML line endings.
#
# USAGE:
#   pwsh ./fix.ps1 -Help                      Print the usage text and exit 0
#   pwsh ./fix.ps1 -All                       All fixers
#   pwsh ./fix.ps1 -Changed [-Base <ref>]     Only the fixers for the file types changed since <ref>
#                                             (default origin/main, else main) plus uncommitted changes;
#                                             dotnet format is limited to the changed .cs files unless
#                                             project or build configuration files changed
#   pwsh ./fix.ps1 -Check lintmark,format     Only the named fixers, in full; combine with -Changed to
#                                             limit them to changed inputs
#   Add -DryRun to any scope to print which fixers would run without running them.
#   With no arguments the usage text is printed and the script exits 1 (a scope must be chosen).
#
# EXTENSION POINTS:
#   Search for "[PROJECT-SPECIFIC]" comments to find the designated locations
#   for adding project-specific auto-fix operations.
#
# CUSTOMIZATION:
#   This script belongs to the project: extend it as the project grows. Add steps at the
#   [PROJECT-SPECIFIC] points, update tool versions, and add optional parameters that focus a run.
#   To add a fixer, add it to the $fixers table (its name becomes a valid -Check value and
#   appears in the usage text), then add its input filter and its run step at the
#   [PROJECT-SPECIFIC] points. The reusable scoping logic lives in scripts/ProjectScripts.psm1,
#   which is delivered by the Agents release zip, is shared across repositories, and must never
#   be edited here.
#   With no arguments the script prints the usage text and exits 1; -All must still apply every
#   fixer. A fixer run always exits 0 (usage and argument errors exit 1).

param(
    # Run every fixer.
    [switch]$All,

    # Run only the fixers for the changed file types.
    [switch]$Changed,

    # Git reference to compare against for -Changed (default: origin/main, else main).
    [string]$Base,

    # Run only the named fixers (see the usage text for the valid names).
    [string[]]$Check,

    # Print which fixers would run and exit without running them.
    [switch]$DryRun,

    # Print the usage text and exit 0.
    [switch]$Help
)

Import-Module (Join-Path $PSScriptRoot 'scripts/ProjectScripts.psm1') -Force

# ==============================================================================
# FIXER TABLE
# Every fixer in run order: its -Check name and the reason shown when it is skipped.
# [PROJECT-SPECIFIC] Add repository-specific fixers here.
# ==============================================================================

$fixers = [ordered]@{
    yamlfix  = @{ Skip = 'no YAML files changed' }
    lintmark = @{ Skip = 'no Markdown files changed' }
    format   = @{ Skip = 'no C# or project files changed' }
}

# ==============================================================================
# USAGE AND SCOPE VALIDATION
# ==============================================================================

function Show-Usage {
    Write-ScriptUsage -Script 'fix.ps1' -IterateWith '-Changed (or -Check)' `
        -Purpose 'apply the auto-fixers (formatting); a fixer run always exits 0.' `
        -Options ([ordered]@{
            '-All'           = 'Every fixer; cannot be combined with another scope.'
            '-Changed'       = 'Only the fixers for file types changed since the base, plus uncommitted files.'
            '-Base <ref>'    = 'Base reference for -Changed (default origin/main, else main).'
            '-Check <names>' = 'Only the named fixers, in full; with -Changed, limited to changed inputs.'
            '-DryRun'        = 'Print which fixers would run or be skipped, then exit 0 without running anything.'
            '-Help'          = 'Print this text and exit 0.'
        }) `
        -ChecksLabel 'Fixers (-Check names)' -Checks @($fixers.Keys) `
        -Examples @('-Changed', '-Check lintmark', '-Check format -Changed -DryRun', '-All')
}

if ($Help) { Show-Usage; exit 0 }

$scopeProblems = Test-ScopeArguments -Base $Base -Scopes ([ordered]@{
        All     = $All.IsPresent
        Changed = $Changed.IsPresent
        Check   = [bool]$Check
    })
if ($scopeProblems.Count -gt 0) {
    $scopeProblems | ForEach-Object { Write-Host "Error: $_" }
    Write-Host ""
    Show-Usage
    exit 1
}

$wanted = @($fixers.Keys)
if ($Check) {
    $wanted = Resolve-NameList -Names $Check -Valid @($fixers.Keys) -Label 'fixer'
    if ($null -eq $wanted) { exit 1 }
}

# ==============================================================================
# FIXER SELECTION
# A fixer runs when it is wanted (-All, or named by -Check) and, with -Changed, when a file
# type it handles changed. When the changed files cannot be determined, every wanted fixer runs.
# ==============================================================================

$inputsChanged = @{}
foreach ($name in $fixers.Keys) { $inputsChanged[$name] = $true }
$formatInclude = @()

if ($Changed) {
    $files = Get-ChangedFiles -Base $Base
    if ($null -eq $files) {
        Write-Host "Running all selected fixers: the changed files could not be determined."
    } else {
        Write-Host "Changed files: $($files.Count)"
        $inputsChanged.yamlfix = Test-AnyMatch $files '\.ya?ml$|^\.yamlfix\.toml$'
        $inputsChanged.lintmark = Test-AnyMatch $files '\.md$|(^|/)\.lintmark\.yaml$|(^|/)\.editorconfig$'
        $formatScope = Get-DotnetFormatScope $files
        $inputsChanged.format = $formatScope.Run
        $formatInclude = $formatScope.Include

        # [PROJECT-SPECIFIC] Add input filters for repository-specific fixers here.
    }
}

$run = @{}
$skipReason = @{}
foreach ($name in $fixers.Keys) {
    $run[$name] = ($name -in $wanted) -and $inputsChanged[$name]
    if ($name -notin $wanted) { $skipReason[$name] = 'not selected by -Check' }
    else { $skipReason[$name] = $fixers[$name].Skip }
}
if (-not $run.format) { $formatInclude = @() }

if ($DryRun) {
    foreach ($name in $fixers.Keys) {
        Write-Host ("  {0,-12} {1}" -f $name, ($run[$name] ? 'run' : "skip ($($skipReason[$name]))"))
    }
    if ($formatInclude.Count -gt 0) { Write-Host "  dotnet format limited to: $($formatInclude -join ' ')" }
    Write-Host "Dry run: no fixer was run."
    exit 0
}

# ==============================================================================
# AUTO-FIX
# Applies the selected auto-fixers with progress output. Never fails - applies what it can and
# exits 0 so agents do not react to any output as a problem to solve.
# ==============================================================================

# --- YAML Auto-Fix ---
if ($run.yamlfix) {
    Write-Host "Fixing: YAML..."
    if (Initialize-PythonVenv -Silent) {
        yamlfix . 2>$null
        deactivate 2>$null
    }
    Convert-YamlLineEndings
} else {
    Write-Host "Fixing: YAML... skipped ($($skipReason.yamlfix))"
}

# --- Markdown Auto-Fix ---
if ($run.lintmark) {
    Write-Host "Fixing: markdown..."
    dotnet tool restore > $null 2>&1
    if ($LASTEXITCODE -eq 0) {
        dotnet lintmark --fix 2>$null
    }
} else {
    Write-Host "Fixing: markdown... skipped ($($skipReason.lintmark))"
}

# [PROJECT-SPECIFIC] Add additional auto-fixers here.

# --- .NET Auto-Format ---
if ($run.format) {
    if ($formatInclude.Count -gt 0) {
        Write-Host "Fixing: dotnet format ($($formatInclude.Count) changed C# files)..."
    } else {
        Write-Host "Fixing: dotnet format..."
    }
    $slnFiles = @(Get-ChildItem -Filter "*.sln" -ErrorAction SilentlyContinue) +
                @(Get-ChildItem -Filter "*.slnx" -ErrorAction SilentlyContinue)
    if ($slnFiles.Count -gt 0) {
        $formatArgs = @()
        if ($formatInclude.Count -gt 0) { $formatArgs = @('--include') + $formatInclude }
        dotnet format @formatArgs 2>$null
    }
} else {
    Write-Host "Fixing: dotnet format... skipped ($($skipReason.format))"
}

# [PROJECT-SPECIFIC] Add additional language-specific auto-formatters here.
# Example (C/C++ with clang-format):
#   Get-ChildItem -Recurse -Include "*.cpp","*.hpp","*.h" |
#       ForEach-Object { clang-format -i $_.FullName } 2>$null

Write-Host "Auto-fix complete."
exit 0
