# tools/deploy.ps1 -- build the mod, re-export the PCK when it is needed, then verify.
#
# Usage:
#   powershell -File tools/deploy.ps1              # build; auto re-export when JSON/scene/script set changed
#   powershell -File tools/deploy.ps1 -Pck         # force a PCK re-export
#   powershell -File tools/deploy.ps1 -SkipCheck   # skip tools/check.ps1 at the end
#
# Refuses to touch the PCK while the game is running (the file is locked).
param(
    [string]$ModRoot,
    [string]$DeployDir = "E:\game_part2\SteamLibrary\steamapps\common\Slay the Spire 2\mods\STS2_Aeonglass",
    [string]$Godot     = "E:\game_part2\mod_c\sts2\base\Godot_v4.5.1-stable_mono_win64\Godot_v4.5.1-stable_mono_win64.exe",
    [switch]$Pck,
    [switch]$SkipCheck,
    [switch]$Force
)

$ErrorActionPreference = "Stop"
if (-not $ModRoot) { $ModRoot = Split-Path -Parent $PSScriptRoot }

function Head([string]$title) { Write-Host ""; Write-Host ("== " + $title) -ForegroundColor Cyan }
function Die([string]$message)  { Write-Host ("[FAIL] " + $message) -ForegroundColor Red; exit 1 }
function Ok([string]$message)   { Write-Host ("  [ok] " + $message) -ForegroundColor Green }

# Godot writes the well-known editor-side noise ("Failed to load project assembly" / TypeLoadException)
# to stderr even when the import/export succeeds. Run it with a relaxed error preference and judge the
# result by the output file instead of by stderr or the exit code.
function Invoke-Godot([string]$exe, [string[]]$arguments) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $output = & $exe @arguments 2>&1 | Out-String
    } finally {
        $ErrorActionPreference = $previous
    }
    return $output
}

Head "0. Environment"
$game = Get-Process -Name SlayTheSpire2 -ErrorAction SilentlyContinue
if ($game -and -not $Force) {
    Die "Slay the Spire 2 is running -- close the game first (or pass -Force to skip the PCK step)."
}
if (-not (Test-Path -LiteralPath $Godot)) { Die ("Godot not found: " + $Godot) }
if (-not (Test-Path -LiteralPath $ModRoot)) { Die ("mod root not found: " + $ModRoot) }
Ok ("mod root  = " + $ModRoot)
Ok ("deploy dir = " + $DeployDir)
if ($game) { Ok "game is running and -Force was given: the PCK step will be skipped" }

Head "1. dotnet build -p:RunPckExport=false"
Push-Location $ModRoot
try {
    $build = & dotnet build -p:RunPckExport=false --nologo 2>&1 | Out-String
} finally {
    Pop-Location
}
$warnings = @([regex]::Matches($build, '(?i)warning\s+[A-Z]{2}\d+'))
$errors   = @([regex]::Matches($build, '(?i)error\s+[A-Z]{2}\d+'))
if ($errors.Count -gt 0)   { Write-Host $build; Die ($errors.Count.ToString() + " compiler error(s)") }
if ($warnings.Count -gt 0) { Write-Host $build; Die ($warnings.Count.ToString() + " compiler warning(s) -- the project requires 0/0") }
Ok "0 warnings / 0 errors"

Head "2. Does the PCK need a re-export?"
$needPck = [bool]$Pck
$reason = "forced with -Pck"
if (-not $needPck) {
    if ($game) {
        $reason = "game is running -- cannot rebuild the PCK"
    } else {
        $status = & git -C $ModRoot status --porcelain 2>$null
        $contentChanged = @($status | Where-Object { $_ -match '\.(json|tscn|tres|import|png|ogg|wav)$' })
        $scriptSetChanged = @($status | Where-Object { $_ -match '^(\s*[AD?]{1,2}|\s*R)' -and $_ -match '\.cs$' })
        if ($contentChanged.Count -gt 0) {
            $needPck = $true
            $reason = ($contentChanged.Count.ToString() + " content file(s) changed (json/scene/asset)")
        } elseif ($scriptSetChanged.Count -gt 0) {
            $needPck = $true
            $reason = ($scriptSetChanged.Count.ToString() + " script(s) added/removed/renamed")
        } else {
            $reason = "only .cs bodies changed -- DLL is enough"
        }
    }
}
Ok $reason

Head "3. Re-export the PCK"
$pckTarget = Join-Path $DeployDir "STS2_Aeonglass.pck"
if (-not $needPck) {
    Ok "skipped"
} else {
    $before = if (Test-Path -LiteralPath $pckTarget) { (Get-Item -LiteralPath $pckTarget).LastWriteTime } else { [datetime]::MinValue }
    Invoke-Godot $Godot @("--headless", "--path", $ModRoot, "--import") | Out-Null
    $exportLog = Invoke-Godot $Godot @("--headless", "--path", $ModRoot, "--export-pack", "BasicExport", $pckTarget)
    if (-not (Test-Path -LiteralPath $pckTarget)) { Write-Host $exportLog; Die "PCK was not written" }

    $after = (Get-Item -LiteralPath $pckTarget).LastWriteTime
    if ($after -le $before) {
        Write-Host $exportLog
        Die ("PCK was not refreshed (still " + $after.ToString("yyyy-MM-dd HH:mm:ss") + ") -- see the Godot log above")
    }

    $noise = @([regex]::Matches($exportLog, 'TypeLoadException|Failed to load project assembly'))
    Ok ("wrote " + $pckTarget + " (" + (Get-Item -LiteralPath $pckTarget).Length + " bytes, " + $after.ToString("HH:mm:ss") + ")")
    if ($noise.Count -gt 0) { Ok "editor-side assembly noise ignored (documented, harmless)" }
}

Head "4. Deployed files"
foreach ($name in @("STS2_Aeonglass.dll", "STS2_Aeonglass.pdb", "STS2_Aeonglass.json", "STS2_Aeonglass.pck")) {
    $path = Join-Path $DeployDir $name
    if (Test-Path -LiteralPath $path) {
        $item = Get-Item -LiteralPath $path
        Ok ($name.PadRight(22) + $item.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") + "  " + $item.Length + " bytes")
    } else {
        Ok ($name.PadRight(22) + "(missing)")
    }
}

Head "5. Self-check"
if ($SkipCheck) {
    Ok "skipped (-SkipCheck)"
} else {
    & powershell -NoProfile -File (Join-Path $PSScriptRoot "check.ps1") -ModRoot $ModRoot -DeployDir $DeployDir
    if ($LASTEXITCODE -ne 0) { Die "check.ps1 reported failures" }
}

Write-Host ""
Write-Host "DEPLOY OK -- restart the game to load the new DLL/PCK." -ForegroundColor Green
exit 0
