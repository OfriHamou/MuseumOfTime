<#
.SYNOPSIS
    Headless verification: compiles the project and runs the existing Unity
    PlayMode tests, then prints PASS or FAIL.

.DESCRIPTION
    Two Unity batch-mode passes:
      1. A plain -quit run, to catch compile errors (e.g. the CS0101
         duplicate-class error that broke Phase 0).
      2. -runTests -testPlatform PlayMode, to run the suite already in
         Assets/Tests/PlayMode/.

    Unity must be closed before running this - batch mode takes an
    exclusive lock on the project.

.USAGE
    powershell -File Tools/verify.ps1
#>

$UnityPath   = "C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Unity.exe"
$ProjectPath = (Resolve-Path "$PSScriptRoot\..").Path

$SetupLog    = Join-Path $ProjectPath "Setup.log"
$TestRunLog  = Join-Path $ProjectPath "TestRun.log"
$TestResults = Join-Path $ProjectPath "TestResults.xml"

Remove-Item $SetupLog, $TestRunLog, $TestResults -ErrorAction SilentlyContinue

if (-not (Test-Path $UnityPath)) {
    Write-Host "RESULT: FAIL - Unity not found at $UnityPath" -ForegroundColor Red
    exit 1
}

# ---------------------------------------------------------------------------
# Step 1: compile check
# ---------------------------------------------------------------------------
Write-Host "=== Step 1/2: compiling (this can take a couple of minutes) ==="

$compileArgs = @(
    "-batchmode", "-quit",
    "-projectPath", $ProjectPath,
    "-logFile", $SetupLog
)
$compileProc = Start-Process -FilePath $UnityPath -ArgumentList $compileArgs -Wait -PassThru -NoNewWindow
$compileExitCode = $compileProc.ExitCode

$compileErrors = @()
if (Test-Path $SetupLog) {
    $compileErrors = Select-String -Path $SetupLog -Pattern "error CS\d+"
}

$compileOk = ($compileExitCode -eq 0) -and ($compileErrors.Count -eq 0)

if (-not $compileOk) {
    Write-Host ""
    Write-Host "Compile FAILED (Unity exit code $compileExitCode, $($compileErrors.Count) CS error(s) in log)" -ForegroundColor Red
    $compileErrors | Select-Object -First 10 | ForEach-Object { Write-Host "  $($_.Line.Trim())" }
    Write-Host ""
    Write-Host "Full log: $SetupLog"
    Write-Host ""
    Write-Host "RESULT: FAIL" -ForegroundColor Red
    exit 1
}

Write-Host "Compile OK (exit code $compileExitCode, no CS errors in $SetupLog)"
Write-Host ""

# ---------------------------------------------------------------------------
# Step 2: run the existing PlayMode tests
# ---------------------------------------------------------------------------
Write-Host "=== Step 2/2: running PlayMode tests ==="

# No -quit here: Unity would exit before the tests run and write nothing.
$testArgs = @(
    "-batchmode",
    "-projectPath", $ProjectPath,
    "-runTests",
    "-testPlatform", "PlayMode",
    "-testResults", $TestResults,
    "-logFile", $TestRunLog
)
$testProc = Start-Process -FilePath $UnityPath -ArgumentList $testArgs -Wait -PassThru -NoNewWindow
$testExitCode = $testProc.ExitCode

if (-not (Test-Path $TestResults)) {
    Write-Host ""
    Write-Host "No TestResults.xml was produced (Unity exit code $testExitCode)." -ForegroundColor Red
    Write-Host "Full log: $TestRunLog"
    Write-Host ""
    Write-Host "RESULT: FAIL" -ForegroundColor Red
    exit 1
}

$testsOk = $false
try {
    [xml]$results = Get-Content $TestResults
    $run = $results."test-run"
    $total  = [int]$run.total
    $passed = [int]$run.passed
    $failed = [int]$run.failed
    $result = [string]$run.result

    Write-Host "Tests: $passed/$total passed, $failed failed (result: $result)"

    # A handful of input-simulation tests call Assert.Ignore in batch mode by
    # design (no focused window, so the Input System resets devices every
    # frame - see Phase0_Walkthrough.md Part F.5). That shows up as
    # result="Skipped:Ignored" even though nothing actually failed, so the
    # pass/fail gate is "zero failed", not "result says Passed".
    $testsOk = ($total -gt 0) -and ($failed -eq 0)
}
catch {
    Write-Host "Could not parse $TestResults ($($_.Exception.Message)); falling back to a text scan." -ForegroundColor Yellow
    $failLines = Select-String -Path $TestResults -Pattern 'result="Failed"'
    $testsOk = ($failLines.Count -eq 0)
    Write-Host "Failed test-case entries found: $($failLines.Count)"
}

Write-Host ""
Write-Host "Logs saved:"
Write-Host "  $SetupLog"
Write-Host "  $TestRunLog"
Write-Host "  $TestResults"
Write-Host ""

if ($compileOk -and $testsOk) {
    Write-Host "RESULT: PASS" -ForegroundColor Green
    exit 0
}
else {
    Write-Host "RESULT: FAIL" -ForegroundColor Red
    exit 1
}
