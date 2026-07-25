[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ProcessName,
    [ValidateRange(10, 3600)]
    [int]$Seconds = 60,
    [Parameter(Mandatory)]
    [string]$OutputPath
)

$ErrorActionPreference = "Stop"
$logicalProcessors = [Environment]::ProcessorCount
$matches = @(Get-Process -Name $ProcessName -ErrorAction Stop)
if ($matches.Count -ne 1) {
    throw "Expected exactly one '$ProcessName' process, found $($matches.Count)."
}

$samples = [System.Collections.Generic.List[object]]::new()
$previous = $matches[0]
$previousCpu = $previous.TotalProcessorTime.TotalSeconds
$previousAt = [DateTimeOffset]::UtcNow

for ($index = 0; $index -lt $Seconds; $index++) {
    Start-Sleep -Seconds 1
    $process = Get-Process -Id $previous.Id -ErrorAction Stop
    $now = [DateTimeOffset]::UtcNow
    $cpu = $process.TotalProcessorTime.TotalSeconds
    $elapsed = ($now - $previousAt).TotalSeconds
    $cpuPercent = (($cpu - $previousCpu) / $elapsed / $logicalProcessors) * 100
    $presentMonRunning =
        [bool](Get-Process -Name PresentMon -ErrorAction SilentlyContinue)

    $samples.Add([pscustomobject]@{
        Timestamp = $now.ToString("O")
        CpuPercent = [Math]::Round($cpuPercent, 4)
        WorkingSetBytes = $process.WorkingSet64
        PrivateBytes = $process.PrivateMemorySize64
        ThreadCount = $process.Threads.Count
        HandleCount = $process.HandleCount
        PresentMonRunning = $presentMonRunning
    })

    $previousCpu = $cpu
    $previousAt = $now
}

$directory = Split-Path -Parent $OutputPath
if ($directory) {
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
}
$samples | Export-Csv -NoTypeInformation -Encoding UTF8 -Path $OutputPath
