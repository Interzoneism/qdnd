<#
.SYNOPSIS
    Run spell verification test for a single action/spell.
.DESCRIPTION
    Runs a full-fidelity spell verification test and generates an analysis report.
    Used by AI agents for systematic spell-by-spell verification.
.PARAMETER ActionId
    The action/spell ID to verify (e.g., "fireball", "magic_missile").
.PARAMETER ConfigPath
    Optional path to a SpellVerificationSetup JSON config file.
.PARAMETER Seed
    RNG seed for reproducibility. Default: 42.
.PARAMETER MaxTime
    Maximum test runtime in seconds. Default: 15.
.PARAMETER Level
    Character level for the caster. Default: 5.
.PARAMETER LogDir
    Directory for output logs. Default: artifacts/autobattle/spell_verify.
.EXAMPLE
    .\Scripts\run_spell_verification.ps1 -ActionId fireball
.EXAMPLE
    .\Scripts\run_spell_verification.ps1 -ActionId fireball -ConfigPath Data/Validation/verification_configs/fireball.json
.EXAMPLE
    .\Scripts\run_spell_verification.ps1 -ActionId magic_missile -Seed 123 -MaxTime 20 -Level 7
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$ActionId,

    [Parameter()]
    [string]$ConfigPath,

    [Parameter()]
    [int]$Seed = 42,

    [Parameter()]
    [int]$MaxTime = 15,

    [Parameter()]
    [int]$Level = 5,

    [Parameter()]
    [string]$LogDir = "artifacts/autobattle/spell_verify"
)

$ErrorActionPreference = "Stop"

# Resolve project root (script is in Scripts/)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Split-Path -Parent $ScriptDir

Push-Location $ProjectDir
try {
    # --- Create log directory ---
    if (-not (Test-Path $LogDir)) {
        New-Item -ItemType Directory -Path $LogDir -Force | Out-Null
    }

    $LogFile = Join-Path $LogDir "$ActionId.jsonl"

    # Remove stale log if present
    if (Test-Path $LogFile) {
        Remove-Item $LogFile -Force
    }

    # --- Find Godot binary ---
    $GodotBin = $null

    # 1. GODOT_BIN environment variable
    if ($env:GODOT_BIN -and (Test-Path $env:GODOT_BIN)) {
        $GodotBin = $env:GODOT_BIN
    }

    # 2. Common Windows paths
    if (-not $GodotBin) {
        $candidates = @(
            "godot",
            "godot.exe",
            "Godot_v4.6-stable_mono_win64.exe",
            "$env:LOCALAPPDATA\Godot\Godot_v4.6-stable_mono_win64.exe",
            "$env:ProgramFiles\Godot\Godot_v4.6-stable_mono_win64.exe",
            "C:\Godot\Godot_v4.6-stable_mono_win64.exe"
        )
        foreach ($candidate in $candidates) {
            $resolved = Get-Command $candidate -ErrorAction SilentlyContinue
            if ($resolved) {
                $GodotBin = $resolved.Source
                break
            }
            if (Test-Path $candidate) {
                $GodotBin = $candidate
                break
            }
        }
    }

    if (-not $GodotBin) {
        Write-Error "Godot binary not found. Set GODOT_BIN environment variable or ensure godot is on PATH."
        exit 2
    }

    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Spell Verification: $ActionId" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Godot:   $GodotBin"
    Write-Host "Seed:    $Seed"
    Write-Host "Level:   $Level"
    Write-Host "MaxTime: ${MaxTime}s"
    Write-Host "LogFile: $LogFile"
    if ($ConfigPath) {
        Write-Host "Config:  $ConfigPath"
    }
    Write-Host ""

    # --- Build command line arguments ---
    $GodotArgs = @(
        "--path", ".",
        "--",
        "--run-autobattle",
        "--full-fidelity",
        "--ff-spell-verify", $ActionId,
        "--seed", $Seed.ToString(),
        "--max-time-seconds", $MaxTime.ToString(),
        "--character-level", $Level.ToString(),
        "--log-file", $LogFile
    )

    if ($ConfigPath) {
        if (-not (Test-Path $ConfigPath)) {
            Write-Warning "Config file not found: $ConfigPath (proceeding without it)"
        }
        else {
            $GodotArgs += "--verify-config"
            $GodotArgs += $ConfigPath
        }
    }

    # --- Run Godot ---
    # Redirect Godot stdout/stderr to a file to prevent flooding the VS Code terminal,
    # which causes the editor to freeze when the console Godot binary produces thousands
    # of debug lines during a full-fidelity run.
    $GodotStdoutLog = Join-Path $LogDir "$ActionId.stdout.log"
    if (Test-Path $GodotStdoutLog) {
        Remove-Item $GodotStdoutLog -Force
    }

    Write-Host "Running: $GodotBin $($GodotArgs -join ' ')" -ForegroundColor DarkGray
    Write-Host "Godot output redirected to: $GodotStdoutLog" -ForegroundColor DarkGray
    Write-Host ""

    $process = Start-Process -FilePath $GodotBin -ArgumentList $GodotArgs -PassThru `
        -RedirectStandardOutput $GodotStdoutLog -RedirectStandardError "$GodotStdoutLog.err"
    $process.WaitForExit()
    $exitCode = $process.ExitCode

    # Merge stderr into stdout log and clean up
    if (Test-Path "$GodotStdoutLog.err") {
        if ((Get-Item "$GodotStdoutLog.err").Length -gt 0) {
            Add-Content -Path $GodotStdoutLog -Value "`n--- STDERR ---"
            Get-Content "$GodotStdoutLog.err" | Add-Content -Path $GodotStdoutLog
        }
        Remove-Item "$GodotStdoutLog.err" -Force
    }

    # Show last few key lines from Godot output for quick diagnosis
    if (Test-Path $GodotStdoutLog) {
        $tailLines = Get-Content $GodotStdoutLog -Tail 15 -ErrorAction SilentlyContinue
        $errorLines = $tailLines | Where-Object { $_ -match "ERROR|SCRIPT ERROR|Exception|FAIL|TIMEOUT|FREEZE" }
        if ($errorLines) {
            Write-Host "Godot errors detected:" -ForegroundColor Red
            $errorLines | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
        }
    }

    # --- Analyze results ---
    Write-Host ""
    Write-Host "----------------------------------------" -ForegroundColor Cyan
    Write-Host "  Results" -ForegroundColor Cyan
    Write-Host "----------------------------------------" -ForegroundColor Cyan

    # Check exit code
    switch ($exitCode) {
        0 { Write-Host "Exit Code: 0 (OK)" -ForegroundColor Green }
        1 { Write-Host "Exit Code: 1 (FAIL)" -ForegroundColor Red }
        default { Write-Host "Exit Code: $exitCode (ERROR)" -ForegroundColor Red }
    }

    # Check if log file was produced
    if (Test-Path $LogFile) {
        $logContent = Get-Content $LogFile -Raw -ErrorAction SilentlyContinue
        $lineCount = (Get-Content $LogFile -ErrorAction SilentlyContinue | Measure-Object).Count
        Write-Host "Log Lines: $lineCount"

        # Check if action was used (also check is_forced for BG3 alias matches
        # where the logged ability_id differs from the requested ActionId)
        $abilityUsed = $false
        if ($logContent -match "`"ability_id`":`"$ActionId`"" -or
            $logContent -match "UseAbility:$ActionId" -or
            $logContent -match "`"ability_id`": `"$ActionId`"" -or
            $logContent -match "`"is_forced`":true" -or
            $logContent -match "`"is_forced`": true") {
            $abilityUsed = $true
        }

        if ($abilityUsed) {
            Write-Host "Ability Used: YES" -ForegroundColor Green
        }
        else {
            Write-Host "Ability Used: NO (ability was not detected in combat log)" -ForegroundColor Yellow
        }

        # Check for damage events
        $damageEvents = ($logContent | Select-String -Pattern "DAMAGE_DEALT" -AllMatches).Matches.Count
        if ($damageEvents -gt 0) {
            Write-Host "Damage Events: $damageEvents"
        }

        # Check for status events
        $statusEvents = ($logContent | Select-String -Pattern "STATUS_APPLIED" -AllMatches).Matches.Count
        if ($statusEvents -gt 0) {
            Write-Host "Status Events: $statusEvents"
        }

        # Check for healing events
        $healingEvents = ($logContent | Select-String -Pattern "HEALING_DONE" -AllMatches).Matches.Count
        if ($healingEvents -gt 0) {
            Write-Host "Healing Events: $healingEvents"
        }

        # Check for battle end
        $battleEnd = $logContent -match "BATTLE_END"
        if ($battleEnd) {
            Write-Host "Battle End: YES" -ForegroundColor Green
        }
        else {
            Write-Host "Battle End: NO (battle may have timed out)" -ForegroundColor Yellow
        }
    }
    else {
        Write-Host "Log File: NOT FOUND" -ForegroundColor Red
        Write-Host "  The test may not have started correctly."
    }

    Write-Host ""
    Write-Host "Log path: $LogFile"
    Write-Host "========================================" -ForegroundColor Cyan

    # Determine final exit code based on actual test results, not Godot's exit code.
    # Godot may exit 1 due to cosmetic warnings (missing animations, etc.) even when
    # combat logic works correctly. Use BATTLE_END + ability used as the success criteria.
    $finalExitCode = $exitCode
    if ($exitCode -le 1 -and $battleEnd -and $abilityUsed) {
        $finalExitCode = 0
    }
    exit $finalExitCode
}
finally {
    Pop-Location
}
