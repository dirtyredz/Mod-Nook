<#
    Builds a release archive laid out the way Nexus and Vortex expect:

        BepInEx/plugins/ModNook/ModNook.dll

    Deliberately not the dev deploy path (plugins/MoonlightPeaksMods/ModNook), which only
    exists to keep hand-built DLLs clear of Vortex during development.

    There is no test project to run: every code path either reads another mod's BepInEx config or
    clones a live Unity object out of the running game, so a console runner could not exercise
    anything meaningful. Verification is in TESTING.md instead.
#>

$ErrorActionPreference = 'Stop'

$modRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

# The mod lives at mods/ModNook in the notes repo and at the root of its own standalone
# repo. Detect which, so one script works in both and the two copies never diverge.
$parent   = Split-Path -Parent $modRoot
$repoRoot = if ((Split-Path -Leaf $parent) -eq 'mods') { Split-Path -Parent $parent } else { $modRoot }

$project = Join-Path $modRoot 'src\ModNook.csproj'

# Single source of truth for the version, so the archive can never disagree with the DLL.
$version = ([xml](Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ }
if (-not $version) { throw "Could not read <Version> from $project" }

Write-Host "Packing Mod Nook $version"

# SkipDeploy keeps a release build from overwriting the copy under test in the game folder.
dotnet build $project -c Release -p:SkipDeploy=true
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }

$dll = Join-Path $modRoot 'src\bin\Release\netstandard2.1\ModNook.dll'
if (-not (Test-Path $dll)) { throw "Built DLL not found at $dll" }

$staging = Join-Path $env:TEMP "ModNook-pack-$([guid]::NewGuid().ToString('N'))"
$target  = Join-Path $staging 'BepInEx\plugins\ModNook'
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item $dll $target

$dist = Join-Path $repoRoot 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$archive = Join-Path $dist "ModNook-$version.zip"
if (Test-Path $archive) { Remove-Item $archive }

Compress-Archive -Path (Join-Path $staging 'BepInEx') -DestinationPath $archive
Remove-Item $staging -Recurse -Force

Write-Host "Created $archive"
Write-Host 'Extract it over the game folder to install.'
