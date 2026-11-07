<#
.SYNOPSIS
    Generates the Visual Studio solution for UEFI Dev Kit from CMakeLists.txt into .\build.

.DESCRIPTION
    Picks the newest installed Visual Studio that your CMake has a generator for, runs CMake with
    that generator, and leaves the solution in .\build\UefiDevKit.sln. Everything the solution
    produces afterwards (bin, obj, the packaged .vsix under build\dist) stays inside .\build as
    well, so the source tree stays clean and `build` is the only folder to delete or ignore.

    The solution contains the C# project (UefiDevExtension) plus the CMake helper projects
    (ALL_BUILD, vsix, vsix_clean, ...). "Build Solution" runs the `vsix` target, which restores
    NuGet packages, builds the extension and copies the .vsix to build\dist\<Configuration>\.

.PARAMETER Generator
    CMake generator to use, e.g. "Visual Studio 17 2022". Detected automatically when omitted.

.PARAMETER Clean
    Deletes the whole .\build folder first, then generates from scratch.

.PARAMETER Open
    Opens the generated solution when done.

.EXAMPLE
    .\build.ps1 -Open

.EXAMPLE
    .\build.ps1 -Clean -Generator "Visual Studio 17 2022"

.NOTES
    If scripts are blocked on your machine:
        powershell -ExecutionPolicy Bypass -File .\build.ps1
#>
[CmdletBinding()]
param(
    [string]$Generator,
    [switch]$Clean,
    [switch]$Open
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root     = $PSScriptRoot
$buildDir = Join-Path $root 'build'

# --- Locate Visual Studio -------------------------------------------------------------------
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw "vswhere.exe not found at '$vswhere'. Install Visual Studio 2022 or newer (or its Build Tools)."
}

$json = & $vswhere -products '*' -requires Microsoft.Component.MSBuild -format json
if ($LASTEXITCODE -ne 0) { throw "vswhere failed (exit code $LASTEXITCODE)." }
# Assign first: Windows PowerShell 5.1 emits the parsed JSON array as one object, not per item.
$parsed    = ($json -join "`n") | ConvertFrom-Json
$instances = @($parsed | Where-Object { $_ } | Sort-Object { [version]$_.installationVersion } -Descending)
if ($instances.Count -eq 0) {
    throw 'No Visual Studio installation with MSBuild was found.'
}

# --- Locate CMake: PATH first, then the copy bundled with Visual Studio ----------------------
$cmake = $null
$cmd = Get-Command cmake.exe -ErrorAction SilentlyContinue
if ($cmd) {
    $cmake = $cmd.Source
} else {
    foreach ($vs in $instances) {
        $candidate = Join-Path $vs.installationPath 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
        if (Test-Path -LiteralPath $candidate) { $cmake = $candidate; break }
    }
}
if (-not $cmake) {
    throw "CMake was not found. Install CMake 3.21+ or the 'C++ CMake tools for Windows' Visual Studio component."
}
Write-Host "CMake: $cmake"

# --- Choose the generator ---------------------------------------------------------------------
if (-not $Generator) {
    $generatorByMajor = @{ 17 = 'Visual Studio 17 2022'; 18 = 'Visual Studio 18 2026' }
    $supported = (& $cmake --help) -join "`n"
    foreach ($vs in $instances) {
        $name = $generatorByMajor[([version]$vs.installationVersion).Major]
        if ($name -and $supported.Contains($name)) {
            $Generator = $name
            Write-Host "Visual Studio: $($vs.displayName) $($vs.installationVersion)"
            break
        }
    }
    if (-not $Generator) {
        throw ("None of the installed Visual Studio versions has a generator in this CMake. " +
               "Update CMake, or pass -Generator explicitly.")
    }
}
Write-Host "Generator: $Generator"

# --- Prepare the build folder -----------------------------------------------------------------
if ($Clean -and (Test-Path -LiteralPath $buildDir)) {
    Write-Host "Removing $buildDir"
    Remove-Item -LiteralPath $buildDir -Recurse -Force
}

$cache = Join-Path $buildDir 'CMakeCache.txt'
if (Test-Path -LiteralPath $cache) {
    $line = Select-String -LiteralPath $cache -Pattern '^CMAKE_GENERATOR:INTERNAL=(.*)$' | Select-Object -First 1
    if ($line -and $line.Matches[0].Groups[1].Value -ne $Generator) {
        throw ("'$buildDir' was configured with '$($line.Matches[0].Groups[1].Value)', not '$Generator'. " +
               "Re-run with -Clean to regenerate it.")
    }
}

# --- Generate the solution --------------------------------------------------------------------
$cmakeArgs = @('-S', $root, '-B', $buildDir, '-G', $Generator)
if ($Generator -like 'Visual Studio*') { $cmakeArgs += @('-A', 'x64') }

& $cmake @cmakeArgs
if ($LASTEXITCODE -ne 0) { throw "CMake configure failed (exit code $LASTEXITCODE)." }

$solution = Get-ChildItem -LiteralPath $buildDir -File |
    Where-Object { $_.Extension -in '.sln', '.slnx' } |
    Select-Object -First 1
if (-not $solution) { throw "CMake finished but no solution file was found in '$buildDir'." }

Write-Host ''
Write-Host "Solution: $($solution.FullName)" -ForegroundColor Green
Write-Host 'Build it from Visual Studio (Build > Build Solution), or from a shell:'
Write-Host '    cmake --build build --config Release'
Write-Host '    ctest --test-dir build -C Release --output-on-failure'

if ($Open) { Start-Process -FilePath $solution.FullName }
