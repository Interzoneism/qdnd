<#
.SYNOPSIS
    Batch spell verification runner — runs all unverified spells through the verification test.
.DESCRIPTION
    Reads the spell verification tracker, finds all "unverified" spells (skipping "blocked"),
    and runs each one through run_spell_verification.ps1. Generates a markdown summary.
    
    This is for bulk smoke-testing. Real verification requires agent analysis of combat logs
    and comparison against BG3 wiki data.
.PARAMETER TrackerPath
    Path to the spell_verification_tracker.json file.
    Default: Data/Validation/spell_verification_tracker.json
.PARAMETER Category
    Only run spells from this category (e.g., "cantrips", "level_1_spells").
    Default: all categories.
.PARAMETER Seed
    RNG seed for all tests. Default: 42.
.PARAMETER MaxTime
    Maximum test runtime per spell in seconds. Default: 15.
.PARAMETER Level
    Character level for casters. Default: 5.
.PARAMETER LogDir
    Directory for output logs. Default: artifacts/autobattle/spell_verify.
.PARAMETER ReportPath
    Path for the markdown summary report.
    Default: artifacts/autobattle/spell_verify/batch_report.md
.PARAMETER MaxSpells
    Maximum number of spells to test in this batch run. Default: 0 (no limit).
.EXAMPLE
    .\Scripts\run_spell_verification_batch.ps1
.EXAMPLE
    .\Scripts\run_spell_verification_batch.ps1 -Category cantrips -MaxSpells 5
.EXAMPLE
    .\Scripts\run_spell_verification_batch.ps1 -Seed 123 -MaxTime 20
#>

[CmdletBinding()]
param(
    [Parameter()]
    [string]$TrackerPath = "Data/Validation/spell_verification_tracker.json",

    [Parameter()]
    [string]$Category = "",

    [Parameter()]
    [int]$Seed = 42,

    [Parameter()]
    [int]$MaxTime = 15,

    [Parameter()]
    [int]$Level = 5,

    [Parameter()]
    [string]$LogDir = "artifacts/autobattle/spell_verify",

    [Parameter()]
    [string]$ReportPath = "artifacts/autobattle/spell_verify/batch_report.md",

    [Parameter()]
    [int]$MaxSpells = 0
)

$ErrorActionPreference = "Stop"

# Resolve project root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Split-Path -Parent $ScriptDir
$VerifyScript = Join-Path $ScriptDir "run_spell_verification.ps1"

Push-Location $ProjectDir
try {
    # --- Load tracker ---
    if (-not (Test-Path $TrackerPath)) {
        Write-Error "Tracker not found: $TrackerPath"
        exit 1
    }

    $tracker = Get-Content $TrackerPath -Raw | ConvertFrom-Json

    # --- Collect unverified spells in priority order ---
    $spellsToTest = @()

    # Sort categories by priority
    $sortedCategories = $tracker.categories.PSObject.Properties |
        Sort-Object { $_.Value.priority }

    foreach ($cat in $sortedCategories) {
        $catName = $cat.Name
        $catData = $cat.Value

        # Filter by category if specified
        if ($Category -and $catName -ne $Category) {
            continue
        }

        foreach ($spell in $catData.spells.PSObject.Properties) {
            $spellId = $spell.Name
            $spellData = $spell.Value

            if ($spellData.status -eq "unverified") {
                $spellsToTest += [PSCustomObject]@{
                    Id       = $spellId
                    Name     = $spellData.name
                    Category = $catName
                    Priority = $catData.priority
                    WikiUrl  = $spellData.bg3_wiki_url
                    TestMode = $spellData.test_mode
                }
            }
        }
    }

    if ($spellsToTest.Count -eq 0) {
        Write-Host "No unverified spells found." -ForegroundColor Green
        exit 0
    }

    # Apply max limit
    if ($MaxSpells -gt 0 -and $spellsToTest.Count -gt $MaxSpells) {
        $spellsToTest = $spellsToTest | Select-Object -First $MaxSpells
    }

    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Spell Verification Batch Runner" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Spells to test: $($spellsToTest.Count)"
    Write-Host "Seed: $Seed | MaxTime: ${MaxTime}s | Level: $Level"
    if ($Category) { Write-Host "Category filter: $Category" }
    Write-Host ""

    # --- Run each spell ---
    $passCount = 0
    $failCount = 0
    $crashCount = 0
    $inconclusiveCount = 0
    $results = @()
    $startTime = Get-Date

    for ($i = 0; $i -lt $spellsToTest.Count; $i++) {
        $spell = $spellsToTest[$i]
        $progress = "[$($i + 1)/$($spellsToTest.Count)]"

        Write-Host "$progress Testing $($spell.Id) ($($spell.Category))..." -NoNewline

        # Check for config file
        $configFile = "Data/Validation/verification_configs/$($spell.Id).json"
        $configArg = @()
        if (Test-Path $configFile) {
            $configArg = @("-ConfigPath", $configFile)
        }

        # Run verification
        $spellStart = Get-Date
        try {
            & $VerifyScript `
                -ActionId $spell.Id `
                -Seed $Seed `
                -MaxTime $MaxTime `
                -Level $Level `
                -LogDir $LogDir `
                @configArg *> $null

            $spellExitCode = $LASTEXITCODE
        }
        catch {
            $spellExitCode = 2
        }
        $spellDuration = (Get-Date) - $spellStart

        # Check log for ability usage
        $logFile = Join-Path $LogDir "$($spell.Id).jsonl"
        $abilityUsed = $false
        $battleEnded = $false
        if (Test-Path $logFile) {
            $logContent = Get-Content $logFile -Raw -ErrorAction SilentlyContinue
            if ($logContent) {
                $abilityUsed = $logContent -match "$($spell.Id)"
                $battleEnded = $logContent -match "BATTLE_END"
            }
        }

        # Determine result
        $result = "UNKNOWN"
        switch ($spellExitCode) {
            0 {
                if ($abilityUsed) {
                    $result = "PASS"
                    $passCount++
                    Write-Host " PASS" -ForegroundColor Green -NoNewline
                }
                else {
                    $result = "INCONCLUSIVE (ability not detected)"
                    $inconclusiveCount++
                    Write-Host " INCONCLUSIVE" -ForegroundColor Yellow -NoNewline
                }
            }
            1 {
                $result = "FAIL"
                $failCount++
                Write-Host " FAIL" -ForegroundColor Red -NoNewline
            }
            default {
                $result = "CRASH (exit $spellExitCode)"
                $crashCount++
                Write-Host " CRASH" -ForegroundColor Red -NoNewline
            }
        }

        Write-Host " ($([math]::Round($spellDuration.TotalSeconds, 1))s)"

        $results += [PSCustomObject]@{
            Id           = $spell.Id
            Name         = $spell.Name
            Category     = $spell.Category
            Result       = $result
            ExitCode     = $spellExitCode
            AbilityUsed  = $abilityUsed
            BattleEnded  = $battleEnded
            Duration     = [math]::Round($spellDuration.TotalSeconds, 1)
        }
    }

    $totalDuration = (Get-Date) - $startTime

    # --- Print summary ---
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Batch Results" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Total:        $($spellsToTest.Count)" -ForegroundColor White
    Write-Host "Pass:         $passCount" -ForegroundColor Green
    Write-Host "Inconclusive: $inconclusiveCount" -ForegroundColor Yellow
    Write-Host "Fail:         $failCount" -ForegroundColor Red
    Write-Host "Crash:        $crashCount" -ForegroundColor Red
    Write-Host "Time:         $([math]::Round($totalDuration.TotalMinutes, 1)) minutes"
    Write-Host ""

    # --- Generate markdown report ---
    $reportDir = Split-Path -Parent $ReportPath
    if (-not (Test-Path $reportDir)) {
        New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
    }

    $report = @"
# Spell Verification Batch Report
**Date**: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")
**Seed**: $Seed | **Level**: $Level | **MaxTime**: ${MaxTime}s
$(if ($Category) { "**Category**: $Category" } else { "**Category**: All" })

## Summary
| Metric | Count |
|--------|-------|
| Total tested | $($spellsToTest.Count) |
| Pass | $passCount |
| Inconclusive | $inconclusiveCount |
| Fail | $failCount |
| Crash | $crashCount |
| Duration | $([math]::Round($totalDuration.TotalMinutes, 1)) min |

## Results

| # | Action ID | Category | Result | Ability Used | Battle End | Time |
|---|-----------|----------|--------|-------------|------------|------|
"@

    for ($i = 0; $i -lt $results.Count; $i++) {
        $r = $results[$i]
        $usedIcon = if ($r.AbilityUsed) { "Y" } else { "N" }
        $endIcon = if ($r.BattleEnded) { "Y" } else { "N" }
        $report += "| $($i + 1) | $($r.Id) | $($r.Category) | $($r.Result) | $usedIcon | $endIcon | $($r.Duration)s |`n"
    }

    # Failed spells section
    $failedResults = $results | Where-Object { $_.Result -match "FAIL|CRASH" }
    if ($failedResults.Count -gt 0) {
        $report += @"

## Failed / Crashed Spells
These need investigation:

"@
        foreach ($f in $failedResults) {
            $logPath = Join-Path $LogDir "$($f.Id).jsonl"
            $report += "- **$($f.Id)** ($($f.Category)): $($f.Result) — Log: ``$logPath```n"
        }
    }

    $report += @"

## Next Steps
1. Review failed spells — check combat logs in ``$LogDir/``
2. For passing spells, an agent should still verify behavior against BG3 wiki
3. Update ``Data/Validation/spell_verification_tracker.json`` with results
4. This batch run is a smoke test only — full verification requires wiki comparison
"@

    Set-Content -Path $ReportPath -Value $report -Encoding UTF8
    Write-Host "Report saved: $ReportPath"
    Write-Host ""

    # Return appropriate exit code
    if ($crashCount -gt 0) { exit 2 }
    if ($failCount -gt 0) { exit 1 }
    exit 0
}
finally {
    Pop-Location
}
