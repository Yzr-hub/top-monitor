[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runtimeDirectory = Join-Path $repositoryRoot "third_party/runtime"

$dependencies = @(
    @{
        Name = "PresentMon.exe"
        Uri = "https://github.com/GameTechDev/PresentMon/releases/download/v2.4.1/PresentMon-2.4.1-x64.exe"
        Sha256 = "D74183E7AE630F72CD3690BE0373ECBFDC6CBB86578148AAB8FA2A7166068F34"
    },
    @{
        Name = "PawnIO_setup.exe"
        Uri = "https://raw.githubusercontent.com/LibreHardwareMonitor/LibreHardwareMonitor/3d331e3370efb858411f19511373eff65a218701/LibreHardwareMonitor/Resources/PawnIO_setup.exe"
        Sha256 = "A3A46226C5E2824F4CDD42BE0EECBABFC672C86F7889710F5AB1E6AD385B47A0"
    }
)

New-Item -ItemType Directory -Force -Path $runtimeDirectory | Out-Null

foreach ($dependency in $dependencies) {
    $destinationPath = Join-Path $runtimeDirectory $dependency.Name
    if (Test-Path -LiteralPath $destinationPath) {
        $existingHash = (Get-FileHash -LiteralPath $destinationPath -Algorithm SHA256).Hash
        if ($existingHash -eq $dependency.Sha256) {
            Write-Host "$($dependency.Name) is already verified."
            continue
        }
    }

    $temporaryPath = Join-Path `
        $runtimeDirectory `
        ".$($dependency.Name).$([Guid]::NewGuid().ToString('N')).download"
    try {
        Invoke-WebRequest -Uri $dependency.Uri -OutFile $temporaryPath
        $actualHash = (Get-FileHash -LiteralPath $temporaryPath -Algorithm SHA256).Hash
        if ($actualHash -ne $dependency.Sha256) {
            throw "SHA-256 mismatch for $($dependency.Name). Expected $($dependency.Sha256), got $actualHash."
        }

        Move-Item -LiteralPath $temporaryPath -Destination $destinationPath -Force
        Write-Host "$($dependency.Name) downloaded and verified."
    }
    finally {
        if (Test-Path -LiteralPath $temporaryPath) {
            Remove-Item -LiteralPath $temporaryPath -Force
        }
    }
}
