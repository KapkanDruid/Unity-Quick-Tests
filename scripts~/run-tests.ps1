[CmdletBinding()]
param(
    [ValidateSet("EditMode", "PlayMode", "All")]
    [string]$Mode = "All",

    [string]$UnityPath,

    [string]$TestFilter,

    [ValidateRange(1, 3600)]
    [int]$TimeoutSeconds = 300
)

$ErrorActionPreference = "Stop"

$packageRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $packageRoot "TestProject~"
$artifactsPath = Join-Path $projectPath "artifacts"
$projectVersionPath = Join-Path $projectPath "ProjectSettings\ProjectVersion.txt"

if (-not $UnityPath) {
    $projectVersion = Get-Content $projectVersionPath |
        Select-String "^m_EditorVersion: (.+)$" |
        ForEach-Object { $_.Matches[0].Groups[1].Value }

    if (-not $projectVersion) {
        throw "Unable to read Unity version from $projectVersionPath."
    }

    $UnityPath = Join-Path "C:\Program Files\Unity\Hub\Editor" "$projectVersion\Editor\Unity.exe"
}

if (-not (Test-Path -LiteralPath $UnityPath)) {
    throw "Unity executable was not found at $UnityPath. Pass -UnityPath explicitly."
}

New-Item -ItemType Directory -Force -Path $artifactsPath | Out-Null

function Invoke-UnityTests {
    param(
        [ValidateSet("EditMode", "PlayMode")]
        [string]$TestMode
    )

    $resultsPath = Join-Path $artifactsPath "$TestMode-results.xml"
    $logPath = Join-Path $artifactsPath "$TestMode.log"
    $arguments = @(
        "-batchmode",
        "-nographics",
        "-runTests",
        "-projectPath", $projectPath,
        "-testPlatform", $TestMode,
        "-testResults", $resultsPath,
        "-logFile", $logPath
    )

    if ($TestFilter) {
        $arguments += @("-testFilter", $TestFilter)
    }

    Write-Host "Running Unity Quick Tests: $TestMode"
    $process = Start-Process `
        -FilePath $UnityPath `
        -ArgumentList $arguments `
        -PassThru `
        -WindowStyle Hidden

    try {
        Wait-Process -Id $process.Id -Timeout $TimeoutSeconds -ErrorAction Stop
    }
    catch {
        if (-not $process.HasExited) {
            Stop-Process -Id $process.Id -Force
        }

        throw "Unity test run timed out after $TimeoutSeconds seconds. See $logPath."
    }

    $process.Refresh()

    if ($process.ExitCode -ne 0) {
        throw "Unity returned exit code $($process.ExitCode). See $logPath."
    }

    if (-not (Test-Path -LiteralPath $resultsPath)) {
        throw "Unity did not create $resultsPath. See $logPath."
    }

    [xml]$results = Get-Content -Raw $resultsPath
    $testRun = $results.SelectSingleNode("/test-run")

    if (-not $testRun) {
        throw "Unexpected Unity test result format in $resultsPath."
    }

    Write-Host (
        "$TestMode result: {0}; passed: {1}; failed: {2}; skipped: {3}" -f
        $testRun.result,
        $testRun.passed,
        $testRun.failed,
        $testRun.skipped
    )

    if ([int]$testRun.failed -gt 0 -or $testRun.result -ne "Passed") {
        throw "$TestMode tests failed. See $resultsPath and $logPath."
    }
}

if ($Mode -eq "All") {
    Invoke-UnityTests -TestMode "EditMode"
    Invoke-UnityTests -TestMode "PlayMode"
}
else {
    Invoke-UnityTests -TestMode $Mode
}
