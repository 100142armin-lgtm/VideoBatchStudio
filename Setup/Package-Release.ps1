param(
    [Parameter(Mandatory=$true)][string]$BuildDirectory,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$CacheDirectory = ''
)
$ErrorActionPreference = 'Stop'
$repositoryDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$buildDirectoryFull = (Resolve-Path -LiteralPath $BuildDirectory).Path
$outputDirectoryFull = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $outputDirectoryFull | Out-Null
$stagingDirectory = Join-Path $outputDirectoryFull ('stage-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stagingDirectory | Out-Null

# Whitelist the distributable files. Never copy the developer's working directory or user settings.
foreach ($pattern in @('VideoBatchStudio.exe','VideoBatchStudio.exe.config','*.dll')) {
    Get-ChildItem -LiteralPath $buildDirectoryFull -Filter $pattern -File | Copy-Item -Destination $stagingDirectory
}
foreach ($folder in @('ScriptBatch','Setup','web_preview','runtimes')) {
    $source = Join-Path $buildDirectoryFull $folder
    if (-not (Test-Path -LiteralPath $source)) { throw ('Missing build folder: ' + $folder) }
    Copy-Item -LiteralPath $source -Destination (Join-Path $stagingDirectory $folder) -Recurse
}
foreach ($file in @('Install_Components.cmd','Start_VideoBatchStudio.cmd','THIRD_PARTY_NOTICES.txt','Chatterbox_使用说明.md','Chatterbox_组件说明.txt','团队安装说明.txt','version.json','双击启动软件.bat')) {
    $source = Join-Path $repositoryDirectory $file
    if (-not (Test-Path -LiteralPath $source)) { throw ('Missing release file: ' + $file) }
    Copy-Item -LiteralPath $source -Destination (Join-Path $stagingDirectory $file)
}
Copy-Item -LiteralPath (Join-Path $repositoryDirectory 'fonts') -Destination (Join-Path $stagingDirectory 'fonts') -Recurse

if ($CacheDirectory) {
    New-Item -ItemType Directory -Force -Path (Join-Path $stagingDirectory 'component_cache') | Out-Null
    $componentCatalog = Get-Content -LiteralPath (Join-Path $repositoryDirectory 'Setup/components.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $requiredHashes = @($componentCatalog.components | Where-Object { $_.Recommended -and $_.Sha256 } | ForEach-Object { $_.Sha256 })
    $requiredHashes += @('4acbed6dd1c744b0376e3b1cf57ce906f9dc9e95e68824584c8099a63025a3c3','f4448a01e3ab2914c65f4ca59d4cacba6b2dbde19a483fbac46587f8bbaff424')
    Get-ChildItem -LiteralPath $CacheDirectory -File | Where-Object { $_.BaseName -in $requiredHashes } | Copy-Item -Destination (Join-Path $stagingDirectory 'component_cache')
}

# This runs from the staged application directory: no system Python, developer drive, or PATH binaries are used.
$process = Start-Process -FilePath (Join-Path $stagingDirectory 'VideoBatchStudio.exe') -ArgumentList '--prepare-package' -WorkingDirectory $stagingDirectory -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) {
    Get-Content -LiteralPath (Join-Path $stagingDirectory 'package-validation.log') -Tail 50
    throw 'A required component failed installation or validation. Release packaging was stopped.'
}
$process = Start-Process -FilePath (Join-Path $stagingDirectory 'VideoBatchStudio.exe') -ArgumentList '--verify-package' -WorkingDirectory $stagingDirectory -WindowStyle Hidden -Wait -PassThru
if ($process.ExitCode -ne 0) { throw 'The final staged package failed validation.' }

$required = @('ffmpeg.exe','ffprobe.exe','yt-dlp.exe','tools/node/node.exe','edge_runtime/python/python.exe','edge_runtime/ready.json','whisper/whisper-cli.exe','whisper/models/ggml-base.bin','Setup/components.json','Setup/edge-requirements.lock','ScriptBatch/edge_tts_worker.py','ScriptBatch/chatterbox_worker.py','ScriptBatch/chatterbox_install.py','ScriptBatch/chatterbox-requirements.lock')
foreach ($file in $required) { if (-not (Test-Path -LiteralPath (Join-Path $stagingDirectory $file))) { throw ('Incomplete package: ' + $file) } }

$packageFiles = @(Get-ChildItem -LiteralPath $stagingDirectory -File -Recurse | Where-Object {
    $relative = $_.FullName.Substring($stagingDirectory.Length + 1)
    $relative -notmatch '(^|\\)(component_cache|__pycache__)(\\|$)' -and $relative -notmatch '\.(pyc|pdb)$' -and
    $_.Name -notmatch '(settings|keys)\.json$' -and $_.Name -notmatch 'package-validation\.log$'
})
$privateFiles = $packageFiles | Where-Object { $_.Name -match '\.(key|pfx)$|^\.env' -or ($_.Extension -eq '.pem' -and $_.Name -ne 'cacert.pem') -or $_.FullName -match '\\(voices|cloned_voices|auditions)\\' }
if ($privateFiles) { throw 'Personal credentials or reference audio detected in package.' }

$manifest = @($packageFiles | ForEach-Object { [ordered]@{path=$_.FullName.Substring($stagingDirectory.Length+1).Replace('\','/');size=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()} })
$manifestFile = Join-Path $stagingDirectory 'package-manifest.json'
[IO.File]::WriteAllText($manifestFile, (ConvertTo-Json -InputObject $manifest -Depth 5), [Text.UTF8Encoding]::new($false))
$packageFiles += Get-Item -LiteralPath $manifestFile

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipPath = Join-Path $outputDirectoryFull 'VideoBatchStudio_Portable.zip'
$zipStream = [IO.File]::Open($zipPath,[IO.FileMode]::Create)
try {
    $zip = [IO.Compression.ZipArchive]::new($zipStream,[IO.Compression.ZipArchiveMode]::Create,$true,[Text.Encoding]::UTF8)
    try {
        foreach ($file in $packageFiles) {
            $relative = $file.FullName.Substring($stagingDirectory.Length+1).Replace('\','/')
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
        }
    } finally { $zip.Dispose() }
} finally { $zipStream.Dispose() }
Copy-Item -LiteralPath (Join-Path $stagingDirectory 'VideoBatchStudio.exe') -Destination (Join-Path $outputDirectoryFull 'VideoBatchStudio.exe') -Force
$checksums = @('VideoBatchStudio.exe','VideoBatchStudio_Portable.zip') | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $outputDirectoryFull $_) -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_ }
[IO.File]::WriteAllLines((Join-Path $outputDirectoryFull 'SHA256SUMS.txt'), $checksums, [Text.UTF8Encoding]::new($false))
Write-Output ('PACKAGE_READY=' + $stagingDirectory)
Write-Output ('PORTABLE_ZIP=' + $zipPath)
