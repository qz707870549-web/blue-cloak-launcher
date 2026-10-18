param([string]$CompilerPath = '')
$ErrorActionPreference = 'Stop'
$output = Join-Path $PSScriptRoot 'artifacts\verify'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $output -CompilerPath $CompilerPath
if (-not $CompilerPath) { $CompilerPath = Join-Path $PSScriptRoot 'artifacts\tools\roslyn\tasks\net472\csc.exe' }
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$references = @("/reference:$output\LumaDock.exe",'/reference:System.Xaml.dll','/reference:System.Web.Extensions.dll','/reference:System.Drawing.dll',"/reference:$framework\WPF\PresentationFramework.dll","/reference:$framework\WPF\PresentationCore.dll","/reference:$framework\WPF\WindowsBase.dll")
foreach ($name in @('SearchTests','PublicIntegrationTests','WindowSizingTests','CustomizationTests','SystemEntriesTests')) {
    & $CompilerPath /nologo "/out:$output\$name.exe" @references (Join-Path $PSScriptRoot ("tests\$name.cs"))
    if ($LASTEXITCODE -ne 0) { throw "$name compilation failed." }
    & (Join-Path $output ($name + '.exe'))
    if ($LASTEXITCODE -ne 0) { throw "$name failed." }
}
