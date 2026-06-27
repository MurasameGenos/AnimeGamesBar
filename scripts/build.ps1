param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [ValidateSet("x64", "x86", "ARM64")]
    [string]$Platform = "x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$dotnetRoot = "D:\Users\unnat\dotnet"
$msbuild = "D:\Program Files\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe"

if (-not (Test-Path $dotnetRoot)) {
    throw "Missing .NET SDK at $dotnetRoot"
}

if (-not (Test-Path $msbuild)) {
    throw "Missing MSBuild at $msbuild"
}

$env:DOTNET_ROOT = $dotnetRoot
$env:PATH = "$dotnetRoot;$env:PATH"
$env:MSBuildSDKsPath = Join-Path $dotnetRoot "sdk\8.0.422\Sdks"

& $msbuild (Join-Path $repoRoot "AnimeGamesBar.sln") /restore "/p:Configuration=$Configuration" "/p:Platform=$Platform" /m
