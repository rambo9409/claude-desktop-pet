$ErrorActionPreference = 'Stop'
$script:PetProjectRoot = Split-Path -Parent $PSScriptRoot

function Get-PetCompiler {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
        throw '.NET Framework 4.x C# compiler was not found.'
    }
    return $compiler
}

function Get-PetPython {
    param([string]$Python)
    if ($Python) {
        if (-not (Test-Path -LiteralPath $Python -PathType Leaf)) { throw "Python was not found: $Python" }
        return (Resolve-Path -LiteralPath $Python).Path
    }
    if ($env:KOLO_PYTHON) { return Get-PetPython -Python $env:KOLO_PYTHON }
    $bundled = Join-Path $env:USERPROFILE '.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe'
    if (Test-Path -LiteralPath $bundled -PathType Leaf) { return $bundled }
    $installed = Get-Command python -ErrorAction SilentlyContinue
    if ($installed) { return $installed.Source }
    throw 'Python with Pillow and NumPy is required. Set KOLO_PYTHON or pass -Python.'
}

function Write-PetLog {
    param([Parameter(ValueFromPipeline = $true)]$Message, [string]$Path)
    process {
        Add-Content -LiteralPath $Path -Value $Message -Encoding UTF8
        $Message | Out-Host
    }
}

function Invoke-PetCompile {
    param([string]$TestName, [string]$EntryPoint, [string]$LogPath)
    $compiler = Get-PetCompiler
    Push-Location -LiteralPath $script:PetProjectRoot
    try {
        if ($TestName) {
            if ($TestName -notmatch '^[A-Za-z][A-Za-z0-9]+$') { throw 'Invalid test name.' }
            $outputDirectory = Join-Path $script:PetProjectRoot 'artifacts/tests'
            New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
            $output = Join-Path $outputDirectory ($TestName + '.exe')
            $response = Join-Path $outputDirectory ($TestName + '.rsp')
            $arguments = (Get-Content -LiteralPath 'build.rsp') `
                -replace '^/target:winexe$', '/target:exe' `
                -replace '^/out:.*$', ('/out:artifacts\tests\' + $TestName + '.exe')
            if (-not $EntryPoint) { $EntryPoint = $TestName }
            $arguments += '/main:' + $EntryPoint
            $arguments += 'tests\' + $TestName + '.cs'
            Set-Content -LiteralPath $response -Value $arguments -Encoding UTF8
        } else {
            $response = Join-Path $script:PetProjectRoot 'build.rsp'
            $output = Join-Path $script:PetProjectRoot '原神启动.exe'
        }
        if ($LogPath) {
            & $compiler ('@' + $response) 2>&1 | Write-PetLog -Path $LogPath
        } else {
            & $compiler ('@' + $response) | Out-Host
        }
        if ($LASTEXITCODE -ne 0) { throw "C# compilation failed: $response" }
        return $output
    } finally { Pop-Location }
}
