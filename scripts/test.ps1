[CmdletBinding()]
param(
    [ValidateSet('All', 'DesktopMovement', 'Effects', 'Gait', 'MenuTheme', 'Motion', 'SpeechLayout', 'Wings', 'Highlights', 'VisualSmoke', 'GlideFall', 'Nightsoul', 'WindowAbility', 'GptReset', 'CombatPlanner', 'PointerInteraction', 'GanyuTraversal', 'GanyuDualScreen')]
    [string[]]$Suite = @('All'),
    [string[]]$TestArguments = @(),
    [switch]$SkipPreviews,
    [switch]$IncludeVisualSmoke,
    [string]$Python
)
. (Join-Path $PSScriptRoot 'common.ps1')
$suites = if ($Suite -contains 'All') {
    @('DesktopMovement', 'Effects', 'Gait', 'MenuTheme', 'Motion', 'SpeechLayout', 'Wings', 'Highlights', 'GlideFall', 'Nightsoul', 'WindowAbility', 'GptReset', 'CombatPlanner', 'PointerInteraction', 'GanyuTraversal', 'GanyuDualScreen')
} else { $Suite | Select-Object -Unique }
if ($IncludeVisualSmoke -and $suites -notcontains 'VisualSmoke') { $suites = @($suites) + 'VisualSmoke' }
Push-Location -LiteralPath $script:PetProjectRoot
try {
    New-Item -ItemType Directory -Path 'artifacts/tests' -Force | Out-Null
    $run = [pscustomobject]@{
        status = 'RUNNING'
        started = (Get-Date).ToString('o')
        completed = $null
        suites = @($suites | ForEach-Object {
            $testName = switch ($_) {
                'Wings' { 'WingTests' }
                'VisualSmoke' { 'VisualSmoke' }
                default { $_ + 'Tests' }
            }
            [pscustomobject]@{ suite = $_; testName = $testName; status = 'PENDING'; started = $null; completed = $null; error = $null }
        })
    }
    function Save-TestRun {
        $run | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath 'artifacts/tests/latest-run.json' -Encoding UTF8
    }
    Save-TestRun
    foreach ($result in $run.suites) {
        Set-Content -LiteralPath ('artifacts/tests/' + $result.testName + '.log') -Value ('PENDING: run started ' + $run.started) -Encoding UTF8
    }
    foreach ($result in $run.suites) {
        $testSuite = $result.suite
        $testName = $result.testName
        $entry = if ($testSuite -eq 'Highlights') { 'ClaudePet.HighlightsTests' } else { $testName }
        $logPath = 'artifacts/tests/' + $testName + '.log'
        $result.status = 'RUNNING'
        $result.started = (Get-Date).ToString('o')
        Set-Content -LiteralPath $logPath -Value ("RUNNING: $testSuite at " + $result.started) -Encoding UTF8
        Save-TestRun
        Write-Host "Testing: $testSuite"
        try {
            $executable = Invoke-PetCompile -TestName $testName -EntryPoint $entry -LogPath $logPath
            $parameters = $TestArguments
            if ($testSuite -eq 'VisualSmoke' -and $parameters.Count -eq 0) { $parameters = @('--verify') }
            $previousPreference = $ErrorActionPreference
            try {
                $ErrorActionPreference = 'Continue'
                $messages = @(& $executable @parameters 2>&1)
                $testExitCode = $LASTEXITCODE
            } finally { $ErrorActionPreference = $previousPreference }
            $messages | Write-PetLog -Path $logPath
            if ($testExitCode -ne 0) { throw "Tests failed (exit $testExitCode): $testSuite" }
            if (-not $SkipPreviews -and $testSuite -in @('Effects', 'Wings')) {
                $runtime = Get-PetPython -Python $Python
                if ($testSuite -eq 'Effects') {
                    & $runtime 'scripts/encode_preview.py' 'artifacts/tests/effects-run-frames' 'artifacts/tests/effects-transition-preview.gif' --frames 96 --cadence '30,30,40'
                } else {
                    & $runtime 'scripts/encode_preview.py' 'artifacts/tests/wing-frames' 'artifacts/tests/wings-preview.gif' --frames 120 --cadence 40
                }
                if ($LASTEXITCODE -ne 0) { throw "Preview encoding failed: $testSuite" }
            }
            $result.status = 'PASS'
            $result.completed = (Get-Date).ToString('o')
            Add-Content -LiteralPath $logPath -Value ("PASS: $testSuite at " + $result.completed) -Encoding UTF8
            Save-TestRun
        } catch {
            $result.status = 'FAIL'
            $result.completed = (Get-Date).ToString('o')
            $result.error = $_.Exception.Message
            $run.status = 'FAIL'
            $run.completed = $result.completed
            Add-Content -LiteralPath $logPath -Value ('FAIL: ' + $result.error) -Encoding UTF8
            Save-TestRun
            throw
        }
    }
    $run.status = 'PASS'
    $run.completed = (Get-Date).ToString('o')
    Save-TestRun
    Write-Host ("Passed {0} test suites. Outputs: artifacts/tests/" -f $run.suites.Count)
} finally { Pop-Location }
