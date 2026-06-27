param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [ValidateSet("x64", "x86", "ARM64")]
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
& (Join-Path $PSScriptRoot "build.ps1") -Configuration $Configuration -Platform $Platform

$targetFramework = "net8.0-windows10.0.19041.0"
$exe = Join-Path $repoRoot "src\AnimeGamesBar.App\bin\$Platform\$Configuration\$targetFramework\AnimeGamesBar.App.exe"

if (-not (Test-Path $exe)) {
    throw "Missing app executable at $exe"
}

Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe)
