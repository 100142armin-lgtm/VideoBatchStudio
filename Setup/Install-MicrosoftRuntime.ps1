param([Parameter(Mandatory=$true)][ValidateSet('vcredist','webview2','net48')][string]$Component)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$componentUrls = @{
    vcredist = 'https://aka.ms/vc14/vc_redist.x64.exe'
    webview2 = 'https://go.microsoft.com/fwlink/p/?LinkId=2124703'
    net48 = 'https://go.microsoft.com/fwlink/?linkid=2088631'
}
$componentCache = Join-Path $PSScriptRoot '..\component_cache'
New-Item -ItemType Directory -Force -Path $componentCache | Out-Null
$componentInstaller = Join-Path $componentCache ($Component + '-Microsoft.exe')
Write-Output ('Downloading Microsoft runtime: ' + $Component)
Invoke-WebRequest -UseBasicParsing -Uri $componentUrls[$Component] -OutFile ($componentInstaller + '.part')
$componentSignature = Get-AuthenticodeSignature -LiteralPath ($componentInstaller + '.part')
if ($componentSignature.Status -ne 'Valid' -or $componentSignature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation(?:,|$)') {
    throw 'Microsoft signature verification failed. Installer was not executed.'
}
Move-Item -LiteralPath ($componentInstaller + '.part') -Destination $componentInstaller -Force
$componentArguments = if ($Component -eq 'webview2') { '/silent /install' } else { '/q /norestart' }
$componentStart = @{FilePath=$componentInstaller;ArgumentList=$componentArguments;Wait=$true;PassThru=$true;WindowStyle='Hidden'}
if ($Component -ne 'webview2') { $componentStart.Verb = 'RunAs' }
Write-Output 'Signature valid. Windows may request administrator approval.'
$componentProcess = Start-Process @componentStart
if ($componentProcess.ExitCode -notin @(0,3010,1638)) { throw ('Microsoft installer failed: ' + $componentProcess.ExitCode) }
if ($componentProcess.ExitCode -eq 3010) { Write-Output 'A Windows restart is required to complete installation.' }
Write-Output ('Microsoft runtime installed: ' + $Component)
