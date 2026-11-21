# build.ps1
#
# Builds the ApiMark solution, packs ApiMark.MSBuild for the package tests, and runs all tests.
#
# CUSTOMIZATION:
#   This script belongs to the project: extend it as the project grows. Add steps at the
#   [PROJECT-SPECIFIC] points and add optional parameters that focus a run (for example
#   -Project <name> in a multi-library repository). Slow or environment-dependent test tiers
#   (GUI automation, integration) may be opt-in switches.
#   Run with no arguments it must still build and run the standard test suite (agents rely
#   on that), and never remove or weaken a test step to make it pass.

$buildError = $false

Write-Host "Restoring dependencies..."
dotnet restore
if ($LASTEXITCODE -ne 0) { $buildError = $true }

Write-Host "Building..."
dotnet build --no-restore --configuration Release
if ($LASTEXITCODE -ne 0) { $buildError = $true }

Write-Host "Packing ApiMark.MSBuild..."
dotnet pack src/ApiMark.MSBuild/ApiMark.MSBuild.csproj --no-build --configuration Release --output "$PSScriptRoot/test/packages"
if ($LASTEXITCODE -ne 0) { $buildError = $true }

# Expose the packages directory to the package integration tests
$env:APIMARK_TEST_PACKAGES_DIR = "$PSScriptRoot/test/packages"

Write-Host "Running tests..."
dotnet test --no-build --configuration Release --logger trx --results-directory artifacts/tests
if ($LASTEXITCODE -ne 0) { $buildError = $true }

if ($buildError) {
    exit 1
}

exit 0
