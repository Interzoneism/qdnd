#!/usr/bin/env pwsh

param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectPathWindows,

    [Parameter(Mandatory = $true)]
    [string]$GodotBin,

    [Parameter(Mandatory = $true)]
    [int]$HardTimeoutSeconds,

    [Parameter(Mandatory = $true)]
    [string]$GodotArgsBase64
)

$ErrorActionPreference = "Stop"

if ($HardTimeoutSeconds -le 0) {
    Write-Host "[ERROR] HardTimeoutSeconds must be greater than zero"
    exit 2
}

$stdoutPath = [System.IO.Path]::GetTempFileName()
$stderrPath = [System.IO.Path]::GetTempFileName()
$timedOut = $false

try {
    $decodedArgsText = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($GodotArgsBase64))
    $GodotArgs = @()
    foreach ($entry in ($decodedArgsText -split "`0")) {
        if (-not [string]::IsNullOrEmpty($entry)) {
            $GodotArgs += $entry
        }
    }

    $argumentList = @("--path", $ProjectPathWindows, "res://Combat/Arena/CombatArena.tscn", "--")
    if ($GodotArgs) {
        $argumentList += $GodotArgs
    }

    $process = Start-Process `
        -FilePath $GodotBin `
        -ArgumentList $argumentList `
        -WorkingDirectory $ProjectPathWindows `
        -NoNewWindow `
        -PassThru `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath

    $timedOut = -not $process.WaitForExit($HardTimeoutSeconds * 1000)
    if ($timedOut) {
        Write-Host "[ERROR] Godot was killed by hard timeout after ${HardTimeoutSeconds}s (process hung after internal watchdog)"
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit()
    }

    if (Test-Path -LiteralPath $stdoutPath) {
        Get-Content -LiteralPath $stdoutPath
    }
    if (Test-Path -LiteralPath $stderrPath) {
        Get-Content -LiteralPath $stderrPath
    }

    if ($timedOut) {
        exit 137
    }

    exit $process.ExitCode
}
catch {
    Write-Host "[ERROR] Failed to launch Windows Godot process: $($_.Exception.Message)"
    exit 2
}
finally {
    if (Test-Path -LiteralPath $stdoutPath) {
        Remove-Item -LiteralPath $stdoutPath -Force -ErrorAction SilentlyContinue
    }
    if (Test-Path -LiteralPath $stderrPath) {
        Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
    }
}
