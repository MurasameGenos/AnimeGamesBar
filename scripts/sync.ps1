param(
    [string]$Message = "",
    [switch]$PullFirst
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$env:Path = [System.Environment]::GetEnvironmentVariable("Path", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path", "User")

if ($PullFirst) {
    git pull --rebase
}

$status = git status --porcelain
if (-not $status) {
    Write-Output "No local changes to sync."
    exit 0
}

if ([string]::IsNullOrWhiteSpace($Message)) {
    $Message = "Sync project changes"
}

git add .
git commit -m $Message
git push
