param([switch]$SkipVerify, [string]$NsisPath = '', [string]$CompilerPath = '')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not $SkipVerify) { & (Join-Path $root 'verify.ps1') -CompilerPath $CompilerPath }
$versionMatch = [regex]::Match([IO.File]::ReadAllText((Join-Path $root 'src\VersionInfo.cs')), 'AssemblyVersion\("(\d+\.\d+\.\d+)\.\d+"\)')
if (-not $versionMatch.Success) { throw 'Cannot determine application version.' }
$version = $versionMatch.Groups[1].Value
if (-not $NsisPath) {
    $toolRoot = Join-Path $root 'artifacts\tools\nsis\nsis-tool-3.13.0'
    $NsisPath = Join-Path $toolRoot 'tools\makensis.exe'
    if (-not (Test-Path -LiteralPath $NsisPath)) {
        $archive = Join-Path $root 'artifacts\tools\nsis\nsis-tool-3.13.0.zip'
        New-Item -ItemType Directory -Path (Split-Path $archive) -Force | Out-Null
        Invoke-WebRequest -UseBasicParsing -Uri 'https://api.nuget.org/v3-flatcontainer/nsis-tool/3.13.0/nsis-tool.3.13.0.nupkg' -OutFile $archive
        if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne '11B9880836E6D25389A63D2D3F547FD8638C094E5BED4B7AE436E252F8511E3C') { throw 'NSIS tool archive hash mismatch.' }
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [IO.Compression.ZipFile]::ExtractToDirectory($archive, $toolRoot)
    }
}
$build = Join-Path $root 'artifacts\installer-build'
& (Join-Path $root 'build.ps1') -OutputDirectory $build -CompilerPath $CompilerPath
$payload = Join-Path $root ('artifacts\installer-payload\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $payload -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $build 'LumaDock.exe') -Destination (Join-Path $payload '蓝斗篷启动.exe')
Copy-Item -LiteralPath (Join-Path $build 'LumaDock.exe.config') -Destination (Join-Path $payload '蓝斗篷启动.exe.config')
Copy-Item -LiteralPath (Join-Path $build 'assets') -Destination $payload -Recurse
foreach ($name in @('LICENSE', 'THIRD_PARTY_NOTICES.md')) { Copy-Item -LiteralPath (Join-Path $root $name) -Destination $payload }
Copy-Item -LiteralPath (Join-Path $root 'docs\使用说明.md') -Destination $payload
Copy-Item -LiteralPath (Join-Path (Split-Path $NsisPath) 'COPYING') -Destination (Join-Path $payload 'NSIS-LICENSE.txt')
$release = Join-Path $root 'release'
New-Item -ItemType Directory -Path $release -Force | Out-Null
$output = Join-Path $release ("蓝斗篷启动-v$version-Windows-x64-安装程序.exe")
& $NsisPath /V3 /WX /INPUTCHARSET UTF8 "/DVERSION=$version" "/DPAYLOAD=$payload" "/DSOURCE=$root" "/DOUTPUT=$output" (Join-Path $root 'installer\launcher.nsi')
if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
$hash = (Get-FileHash -LiteralPath $output -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText(($output + '.sha256'), ($hash + '  ' + [IO.Path]::GetFileName($output) + [Environment]::NewLine), [Text.UTF8Encoding]::new($false))
Write-Host "Installer built (not executed): $output"
