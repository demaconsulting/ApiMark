# build.ps1
#
# Builds the ApiMark solution and runs all tests.

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
