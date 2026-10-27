[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^\d+\.\d+$')][string]$Version,
    [Parameter(Mandatory = $true)][string]$InstructionsPath
)
. (Join-Path $PSScriptRoot 'common.ps1')
$InstructionsPath = (Resolve-Path -LiteralPath $InstructionsPath -ErrorAction Stop).Path
Push-Location -LiteralPath $script:PetProjectRoot
try {
    $executable = Join-Path $script:PetProjectRoot '原神启动.exe'
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) { throw 'Build the executable first.' }
    if (-not (Test-Path -LiteralPath $InstructionsPath -PathType Leaf)) { throw 'Usage instructions were not found.' }
    $binaryVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($executable)
    if (("{0}.{1}" -f $binaryVersion.FileMajorPart, $binaryVersion.FileMinorPart) -ne $Version) {
        throw "The executable version does not match V$Version. Rebuild before packaging."
    }
    $destination = Join-Path $script:PetProjectRoot ('release/V' + $Version)
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $packagedExe = Join-Path $destination '原神启动.exe'
    $packagedReadme = Join-Path $destination '使用说明.txt'
    Copy-Item -LiteralPath $executable -Destination $packagedExe -Force
    if ((Resolve-Path -LiteralPath $InstructionsPath).Path -ne $packagedReadme) {
        Copy-Item -LiteralPath $InstructionsPath -Destination $packagedReadme -Force
    }
    $zip = Join-Path $destination ('原神启动-V' + $Version + '.zip')
    Compress-Archive -LiteralPath @($packagedExe, $packagedReadme) -DestinationPath $zip -Force
    if ((Get-FileHash -LiteralPath $executable).Hash -ne (Get-FileHash -LiteralPath $packagedExe).Hash) {
        throw 'Packaged executable checksum mismatch.'
    }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $names = @($archive.Entries | ForEach-Object { $_.FullName })
        if ($names.Count -ne 2 -or $names -notcontains '原神启动.exe' -or $names -notcontains '使用说明.txt') {
            throw 'Release package must contain only the executable and usage instructions.'
        }
    } finally { $archive.Dispose() }
    Write-Host "Packaged: $zip"
    Get-FileHash -LiteralPath $zip -Algorithm SHA256
} finally { Pop-Location }
