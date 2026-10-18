param([string]$OutputDirectory = 'dist', [string]$CompilerPath = '')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not [IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory = Join-Path $root $OutputDirectory }
if (-not $CompilerPath) { $CompilerPath = Join-Path $root 'artifacts\tools\roslyn\tasks\net472\csc.exe' }
if (-not (Test-Path -LiteralPath $CompilerPath)) {
    $toolDirectory = Join-Path $root 'artifacts\tools\roslyn'
    New-Item -ItemType Directory -Path $toolDirectory -Force | Out-Null
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $package = Join-Path $toolDirectory 'compiler.zip'
    Write-Host 'Downloading Roslyn 4.14.0 from NuGet...'
    Invoke-WebRequest -UseBasicParsing -Uri 'https://api.nuget.org/v3-flatcontainer/microsoft.net.compilers.toolset/4.14.0/microsoft.net.compilers.toolset.4.14.0.nupkg' -OutFile $package
    Expand-Archive -LiteralPath $package -DestinationPath $toolDirectory -Force
}
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
if (-not (Test-Path -LiteralPath (Join-Path $framework 'WPF\PresentationFramework.dll'))) { throw 'Windows x64 with .NET Framework 4.8 is required.' }
$references = @('/reference:System.Xaml.dll','/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll','/reference:System.Web.Extensions.dll','/reference:Microsoft.CSharp.dll',"/reference:$framework\WPF\PresentationFramework.dll","/reference:$framework\WPF\PresentationCore.dll","/reference:$framework\WPF\WindowsBase.dll","/reference:$framework\WPF\UIAutomationClient.dll","/reference:$framework\WPF\UIAutomationTypes.dll")
$source = Join-Path $root 'src'
$files = @(Get-ChildItem -LiteralPath $source -File -Filter '*.cs' | ForEach-Object FullName)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
& $CompilerPath /nologo /nowarn:0649 /target:winexe /platform:x64 /optimize+ /main:Luma.Program "/out:$OutputDirectory\LumaDock.exe" "/win32icon:$root\LumaDock.ico" "/win32manifest:$root\LumaDock.manifest" "/resource:$source\Luma.AiriTheme.xaml,Luma.AiriTheme.xaml" "/resource:$source\default-groups.json,Luma.DefaultGroups" "/resource:$source\assets\avatar.png,Luma.Avatar" @references @files
if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }
Copy-Item -LiteralPath (Join-Path $root 'LumaDock.exe.config') -Destination $OutputDirectory -Force
New-Item -ItemType Directory -Path (Join-Path $OutputDirectory 'assets') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $source 'assets\fonts') -Destination (Join-Path $OutputDirectory 'assets') -Recurse -Force
Write-Host "Build complete: $OutputDirectory"
