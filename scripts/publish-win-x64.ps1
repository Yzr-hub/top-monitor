[CmdletBinding()]
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot "TopMonitor.sln"
$appProjectPath = Join-Path $repositoryRoot "src/TopMonitor.App/TopMonitor.App.csproj"
$publishDirectory = Join-Path $repositoryRoot "artifacts/publish/win-x64"

Push-Location $repositoryRoot
try {
    & (Join-Path $PSScriptRoot "fetch-runtime-dependencies.ps1")
    dotnet restore $solutionPath
    dotnet build $solutionPath -c $Configuration --no-restore
    dotnet test $solutionPath -c $Configuration --no-build

    New-Item -ItemType Directory -Force -Path $publishDirectory | Out-Null
    dotnet publish $appProjectPath `
        -c $Configuration `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        --output $publishDirectory

    Write-Host "TopMonitor portable package: $publishDirectory"
}
finally {
    Pop-Location
}
