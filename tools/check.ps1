# tools/check.ps1 -- read-only self-check for STS2_Aeonglass.
# Collects the assertions we kept typing by hand. Nothing here writes to the repo.
#
# Usage:
#   powershell -File tools/check.ps1
#   powershell -File tools/check.ps1 -ExpectedPatches 18 -SkipGameLog
#
# Exit code: 0 = all green, 1 = at least one check failed.
param(
    [string]$ModRoot,                       # default: parent of this script's folder
    [string]$DeployDir  = "E:\game_part2\SteamLibrary\steamapps\common\Slay the Spire 2\mods\STS2_Aeonglass",
    [string]$GameLog    = "$env:APPDATA\SlayTheSpire2\logs\godot.log",
    [string]$CharacterId = "STS2_AEONGLASS_CHARACTER_AEONGLASS",
    [int]$ExpectedPatches = 18,
    [switch]$SkipGameLog,
    [switch]$SkipPck
)

$ErrorActionPreference = "Stop"
if (-not $ModRoot) { $ModRoot = Split-Path -Parent $PSScriptRoot }

$script:Failed = 0
function Fail([string]$message) { $script:Failed++; Write-Host ("  [FAIL] " + $message) -ForegroundColor Red }
function Ok([string]$message)   { Write-Host ("  [ok]   " + $message) -ForegroundColor Green }
function Head([string]$title)   { Write-Host ""; Write-Host ("== " + $title) -ForegroundColor Cyan }
function ReadText([string]$path) { return [System.IO.File]::ReadAllText($path) }

function Get-CoreFiles {
    $files = @()
    $files += Get-ChildItem -Recurse -File -Filter *.cs (Join-Path $ModRoot "Core")
    $files += Get-ChildItem -File -Filter *.cs $ModRoot
    return $files
}

# Sections 2 and 3 follow the project rule verbatim: only Core/**. Root-level files (Main.cs/Const.cs)
# are the mod's startup lines, which the logging rule explicitly allows to be prefix-less.
function Get-CoreDirFiles {
    return Get-ChildItem -Recurse -File -Filter *.cs (Join-Path $ModRoot "Core")
}

# ---------------------------------------------------------------- 1. patch discipline
Head "1. Patch discipline (each [HarmonyPatch] needs a runtime gate + a guarded body)"
$gates = 'CharacterGate|MasterEnabled|RiotEnabled|DecksEnabled|SandEnabled|IntentEnabled|RewindEnabled|RestoreGuard|OperationBarrier|IsManaged|TwinOf|ShouldApply|HasPair|HasNodes|IsOurActionCard|IsIntentNode|AimingOurCard|CanonicalEvent'
$guards = 'ShouldBlock\(|\.Redirect\(|MirrorGuard\.Safe|SubstituteAsync|Safe\('
$badPatches = @()
foreach ($file in (Get-CoreFiles)) {
    $text = ReadText $file.FullName
    if ($text -notmatch '\[HarmonyPatch') { continue }
    foreach ($m in [regex]::Matches($text, '\[HarmonyPatch(?:\(|\])(?:.|\n)*?\n(?:\s*\[[^\]]*\]\s*\n)*\s*(?:internal|public)\s+static\s+class\s+([A-Za-z_][A-Za-z0-9_]*)')) {
        $segment = $text.Substring($m.Index)
        $hasGuard = ([regex]::IsMatch($segment, 'try\s*\r?\n?\s*\{') -or [regex]::IsMatch($segment, $guards))
        $hasGate = [regex]::IsMatch($segment, $gates)
        if (-not ($hasGuard -and $hasGate)) { $badPatches += ($file.Name + ":" + $m.Groups[1].Value) }
    }
}
if ($badPatches.Count -eq 0) { Ok "0 patch(es) missing a gate/guard" } else { Fail ("patch(es) missing gate/guard: " + ($badPatches -join ", ")) }

# ---------------------------------------------------------------- 2. log prefixes
Head "2. Every Logger call carries a mechanism prefix (Const.Prefix.*)"
$noPrefix = @()
foreach ($file in (Get-CoreDirFiles)) {
    $text = ReadText $file.FullName
    foreach ($m in [regex]::Matches($text, 'Logger\.(?:Info|Warn|Error)\(')) {
        $window = $text.Substring($m.Index, [Math]::Min(700, $text.Length - $m.Index))
        if ($window -notmatch 'Const\.Prefix\.') { $noPrefix += $file.Name }
    }
}
if ($noPrefix.Count -eq 0) { Ok "0 Logger call(s) without a prefix" } else { Fail ("prefix-less Logger calls in: " + (($noPrefix | Sort-Object -Unique) -join ", ")) }

# ---------------------------------------------------------------- 3. patch count
Head ("3. [HarmonyPatch] count (expected " + $ExpectedPatches + ")")
$patchCount = 0
foreach ($file in (Get-CoreDirFiles)) { $patchCount += ([regex]::Matches((ReadText $file.FullName), '\[HarmonyPatch')).Count }
if ($patchCount -eq $ExpectedPatches) { Ok ("count = " + $patchCount) } else { Fail ("count = " + $patchCount + ", expected " + $ExpectedPatches + " (pass -ExpectedPatches to override)") }

# ---------------------------------------------------------------- 4. file size
Head "4. Core/Systems: no .cs file above 250 lines"
$bigFiles = @()
foreach ($file in (Get-ChildItem -Recurse -File -Filter *.cs (Join-Path $ModRoot "Core\Systems"))) {
    $lines = (Get-Content -LiteralPath $file.FullName).Count
    if ($lines -gt 250) { $bigFiles += ($file.Name + " (" + $lines + " lines)") }
}
if ($bigFiles.Count -eq 0) { Ok "0 oversized file(s)" } else { Fail ("oversized: " + ($bigFiles -join ", ")) }

# ---------------------------------------------------------------- 5. live-game scope gate (4.1)
Head "5. Scope gate: no mod lines after the last 'Run started' (protocol 4.1)"
if ($SkipGameLog) {
    Ok "skipped (-SkipGameLog)"
} elseif (-not (Test-Path -LiteralPath $GameLog)) {
    Fail ("game log not found: " + $GameLog)
} else {
    $lines = Get-Content -LiteralPath $GameLog
    $runStart = ($lines | Select-String 'Run started' | Select-Object -Last 1)
    $embark = ($lines | Select-String 'Embarking on a singleplayer' | Select-Object -Last 1)
    if (-not $runStart) {
        Ok "no 'Run started' in the log yet -- nothing to check"
    } elseif ($embark -and ($embark.Line -notmatch [regex]::Escape($CharacterId))) {
        Ok ("last run is not Aeonglass (" + ($embark.Line -replace '.*Embarking on a singleplayer ', '') + ") -- gate not applicable")
    } else {
        $tail = $lines[$runStart.LineNumber..($lines.Count - 1)]
        $leaks = @($tail | Select-String '\[STS2_Aeonglass\]')
        if ($leaks.Count -eq 0) { Ok "0 mod line(s) after the last 'Run started'" }
        else {
            Fail ($leaks.Count.ToString() + " mod line(s) after the last 'Run started'; first: " + $leaks[0].Line)
        }
    }
}

# ---------------------------------------------------------------- 6. PCK consistency
Head "6. PCK vs disk: same .cs set, and no tools/ inside the PCK"
if ($SkipPck) {
    Ok "skipped (-SkipPck)"
} else {
    $pck = Join-Path $DeployDir "STS2_Aeonglass.pck"
    if (-not (Test-Path -LiteralPath $pck)) {
        Fail ("pck not found: " + $pck)
    } else {
        $raw = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($pck))
        $inPck = @{}
        foreach ($m in [regex]::Matches($raw, '(?:res://)?((?:Core|tools)[/\\][A-Za-z0-9_./\\-]*\.cs|[A-Za-z][A-Za-z0-9_]*\.cs)')) {
            $path = $m.Groups[1].Value -replace '\\', '/'
            if ($path.StartsWith('tools/')) { continue }   # tools/ is checked separately below
            $inPck[$path] = $true
        }

        $onDisk = @{}
        foreach ($file in (Get-CoreFiles)) {
            $rel = $file.FullName.Substring($ModRoot.Length).TrimStart('\', '/') -replace '\\', '/'
            $onDisk[$rel] = $true
        }

        $onlyPck = @($inPck.Keys | Where-Object { -not $onDisk.ContainsKey($_) }) | Sort-Object
        $onlyDisk = @($onDisk.Keys | Where-Object { -not $inPck.ContainsKey($_) }) | Sort-Object
        if ($onlyPck.Count -eq 0 -and $onlyDisk.Count -eq 0) {
            Ok ("pck and disk hold the same " + $onDisk.Count + " script path(s)")
        } else {
            if ($onlyPck.Count -gt 0)  { Fail ("only in pck (" + $onlyPck.Count + "): " + (($onlyPck | Select-Object -First 5) -join ", ")) }
            if ($onlyDisk.Count -gt 0) { Fail ("only on disk (" + $onlyDisk.Count + "): " + (($onlyDisk | Select-Object -First 5) -join ", ") + " -> re-export the PCK (tools/deploy.ps1 -Pck)") }
        }

        $toolsInPck = @([regex]::Matches($raw, 'tools[/\\][A-Za-z0-9_./\\-]*\.ps1'))
        if ($toolsInPck.Count -eq 0) { Ok "no tools/ entry inside the pck" }
        else { Fail ($toolsInPck.Count.ToString() + " tools/ entry(ies) inside the pck -- add tools/* to exclude_filter in export_presets.cfg") }
    }
}

# ---------------------------------------------------------------- summary
Write-Host ""
if ($script:Failed -eq 0) {
    Write-Host "ALL CHECKS PASSED" -ForegroundColor Green
    exit 0
} else {
    Write-Host ("CHECKS FAILED: " + $script:Failed) -ForegroundColor Red
    exit 1
}
