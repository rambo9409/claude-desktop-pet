[CmdletBinding()]
param([switch]$RebuildAssets, [string]$Python)
. (Join-Path $PSScriptRoot 'common.ps1')
Push-Location -LiteralPath $script:PetProjectRoot
try {
    if ($RebuildAssets) {
        & (Get-PetPython -Python $Python) 'build_assets.py'
        if ($LASTEXITCODE -ne 0) { throw 'Asset build failed.' }
    }
    $executable = Invoke-PetCompile
    Write-Host "Built: $executable"
} finally { Pop-Location }
