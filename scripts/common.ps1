$ErrorActionPreference = 'Stop'
$script:PetProjectRoot = Split-Path -Parent $PSScriptRoot

function Get-PetCompiler {
    $compiler = Join-Path (Get-PetFrameworkDirectory) 'csc.exe'
    if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
        throw '.NET Framework 4.x C# compiler was not found.'
    }
    return $compiler
}

function Get-PetFrameworkDirectory {
    foreach ($folder in @('Framework64', 'Framework')) {
        $directory = Join-Path $env:WINDIR ('Microsoft.NET/' + $folder + '/v4.0.30319')
        if (Test-Path -LiteralPath (Join-Path $directory 'csc.exe') -PathType Leaf) { return $directory }
    }
    throw '.NET Framework 4.x compiler was not found. Install the .NET Framework 4.8 Developer Pack.'
}

function Get-PetReferenceDirectory {
    $candidates = @()
    if ($env:PET_NET48_REFERENCES) { $candidates += $env:PET_NET48_REFERENCES }
    foreach ($base in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
        if ($base) { $candidates += Join-Path $base 'Reference Assemblies/Microsoft/Framework/.NETFramework/v4.8' }
    }
    foreach ($candidate in $candidates | Select-Object -Unique) {
        if (Test-Path -LiteralPath (Join-Path $candidate 'PresentationFramework.dll') -PathType Leaf) {
            return (Resolve-Path -LiteralPath $candidate).Path
        }
    }
    throw '.NET Framework 4.8 reference assemblies were not found. Install the Developer Pack or set PET_NET48_REFERENCES.'
}

function Get-PetMetadataFile {
    if ($env:PET_WINMD_FILE) {
        if (-not (Test-Path -LiteralPath $env:PET_WINMD_FILE -PathType Leaf)) { throw 'PET_WINMD_FILE does not exist.' }
        return (Resolve-Path -LiteralPath $env:PET_WINMD_FILE).Path
    }
    $roots = @()
    foreach ($key in @('HKLM:\SOFTWARE\Microsoft\Windows Kits\Installed Roots', 'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows Kits\Installed Roots')) {
        $kit = Get-ItemProperty -LiteralPath $key -ErrorAction SilentlyContinue
        if ($kit -and $kit.KitsRoot10) { $roots += $kit.KitsRoot10 }
    }
    foreach ($base in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
        if ($base) { $roots += Join-Path $base 'Windows Kits/10' }
    }
    $candidates = foreach ($root in $roots | Select-Object -Unique) {
        $directory = Join-Path $root 'UnionMetadata'
        foreach ($version in Get-ChildItem -LiteralPath $directory -Directory -ErrorAction SilentlyContinue) {
            if ($version.Name -match '^10\.\d+\.\d+\.\d+$') {
                $file = Join-Path $version.FullName 'Windows.winmd'
                if (Test-Path -LiteralPath $file -PathType Leaf) { [pscustomobject]@{ Version = [version]$version.Name; Path = $file } }
            }
        }
    }
    $selected = $candidates | Sort-Object Version -Descending | Select-Object -First 1
    if ($selected) { return $selected.Path }
    throw 'Windows 10/11 SDK Windows.winmd was not found. Install the Windows SDK or set PET_WINMD_FILE to its UnionMetadata Windows.winmd.'
}

function Get-PetCompileArguments {
    $references = Get-PetReferenceDirectory
    $framework = Get-PetFrameworkDirectory
    $metadata = Get-PetMetadataFile
    foreach ($line in Get-Content -LiteralPath (Join-Path $script:PetProjectRoot 'build.rsp') -Encoding UTF8) {
        $resolved = $line.Replace('{NET48}', $references).Replace('{FRAMEWORK}', $framework).Replace('{WINRT_METADATA}', $metadata)
        if ($resolved -match '^/reference:"([^"]+)"$' -and -not (Test-Path -LiteralPath $Matches[1] -PathType Leaf)) {
            throw ('Required reference is missing: ' + $Matches[1])
        }
        $resolved
    }
}

function Get-PetPython {
    param([string]$Python)
    if ($Python) {
        if (-not (Test-Path -LiteralPath $Python -PathType Leaf)) { throw "Python was not found: $Python" }
        return (Resolve-Path -LiteralPath $Python).Path
    }
    if ($env:KOLO_PYTHON) { return Get-PetPython -Python $env:KOLO_PYTHON }
    foreach ($name in @('python', 'python3')) {
        $installed = Get-Command $name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($installed -and $installed.Source -notlike '*\WindowsApps\*') { return $installed.Source }
    }
    $launcher = Get-Command py -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($launcher) {
        $executable = & $launcher.Source -3 -c 'import sys; print(sys.executable)' 2>$null
        if ($LASTEXITCODE -eq 0 -and $executable -and (Test-Path -LiteralPath ([string]$executable) -PathType Leaf)) {
            return [string]$executable
        }
    }
    throw 'Python was not found. Install Python with Pillow/NumPy, set KOLO_PYTHON, pass -Python, or use -SkipPreviews for tests.'
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
        $arguments = @(Get-PetCompileArguments)
        $outputDirectory = Join-Path $script:PetProjectRoot 'artifacts/tests'
        if ($TestName) {
            if ($TestName -notmatch '^[A-Za-z][A-Za-z0-9]+$') { throw 'Invalid test name.' }
            New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
            $output = Join-Path $outputDirectory ($TestName + '.exe')
            $response = Join-Path $outputDirectory ($TestName + '.rsp')
            $arguments = $arguments `
                -replace '^/target:winexe$', '/target:exe' `
                -replace '^/out:.*$', ('/out:artifacts\tests\' + $TestName + '.exe')
            if (-not $EntryPoint) { $EntryPoint = $TestName }
            $arguments += '/main:' + $EntryPoint
            $arguments += 'tests\WpfTestRender.cs'
            $arguments += 'tests\' + $TestName + '.cs'
        } else {
            $outputDirectory = Join-Path $script:PetProjectRoot 'artifacts/build'
            New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
            $response = Join-Path $outputDirectory 'production.rsp'
            $output = Join-Path $script:PetProjectRoot '原神启动.exe'
        }
        Set-Content -LiteralPath $response -Value $arguments -Encoding UTF8
        $previousPreference = $ErrorActionPreference
        try {
            # Windows PowerShell turns native stderr into ErrorRecord objects.
            # Collect the entire output before restoring Stop and writing logs.
            $ErrorActionPreference = 'Continue'
            $messages = @(& $compiler ('@' + $response) 2>&1)
            $compileExitCode = $LASTEXITCODE
        } finally { $ErrorActionPreference = $previousPreference }
        if ($LogPath) { $messages | Write-PetLog -Path $LogPath }
        else { $messages | Out-Host }
        if ($compileExitCode -ne 0) { throw "C# compilation failed (exit $compileExitCode): $response" }
        return $output
    } finally { Pop-Location }
}
