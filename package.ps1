param([string]$CompilerPath = '', [switch]$SkipVerify)
$ErrorActionPreference = 'Stop'
if (-not $SkipVerify) { & (Join-Path $PSScriptRoot 'verify.ps1') -CompilerPath $CompilerPath }
$build = Join-Path $PSScriptRoot 'artifacts\package-build'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $build -CompilerPath $CompilerPath
$stage = Join-Path $PSScriptRoot ('artifacts\packages\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
# Explicit allow-list: no data, personal seed, caches, logs or backups are copied.
Copy-Item -LiteralPath (Join-Path $build 'LumaDock.exe') -Destination (Join-Path $stage 'BlueCloakLauncher.exe')
Copy-Item -LiteralPath (Join-Path $build 'LumaDock.exe.config') -Destination (Join-Path $stage 'BlueCloakLauncher.exe.config')
Copy-Item -LiteralPath (Join-Path $build 'assets') -Destination $stage -Recurse
foreach ($file in @('README.md','LICENSE','THIRD_PARTY_NOTICES.md')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $stage }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'docs\使用说明.md') -Destination $stage
$output = Join-Path $PSScriptRoot 'release'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$zip = Join-Path $output 'BlueCloakLauncher-v1.3.21-public-Windows-x64.zip'
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($zip + '.sha256'), ($hash + '  ' + [IO.Path]::GetFileName($zip) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
Write-Host "Public package: $zip"
