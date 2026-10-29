param([switch]$Components)
$ErrorActionPreference = 'Stop'
$appDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
try {
    if (-not [Environment]::Is64BitOperatingSystem) { throw 'This package requires 64-bit Windows 10 / 11.' }
    $frameworkReady = $false
    foreach ($frameworkKey in @('HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full','HKLM:\SOFTWARE\WOW6432Node\Microsoft\NET Framework Setup\NDP\v4\Full')) {
        $frameworkVersion = Get-ItemProperty -LiteralPath $frameworkKey -Name Release -ErrorAction SilentlyContinue
        if ($frameworkVersion.Release -ge 528040) { $frameworkReady = $true }
    }
    if (-not $frameworkReady) {
        & (Join-Path $PSScriptRoot 'Install-MicrosoftRuntime.ps1') -Component net48
        $frameworkVersion = Get-ItemProperty -LiteralPath 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -Name Release -ErrorAction SilentlyContinue
        if ($frameworkVersion.Release -lt 528040) { throw 'Please restart Windows to complete .NET installation, then start this program again.' }
    }
    $appExecutable = Join-Path $appDirectory 'VideoBatchStudio.exe'
    if (-not (Test-Path -LiteralPath $appExecutable)) { throw 'VideoBatchStudio.exe is missing. Extract the complete ZIP before starting.' }
    if ($Components) { Start-Process -FilePath $appExecutable -WorkingDirectory $appDirectory -ArgumentList '--components' }
    else { Start-Process -FilePath $appExecutable -WorkingDirectory $appDirectory }
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    Read-Host 'Setup stopped. Press Enter to close'
    exit 1
}
