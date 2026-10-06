<#
.SYNOPSIS
Measures history-index growth, the acceptance metric behind the schemaVersion 9 delta format.

.DESCRIPTION
Creates a map, raises one large platform, then adds 40 placed objects per revision and
reports .automation/History/index.json after each step. The platform is a fixed payload that
never changes, so a correct index stores it once: before the delta format this grew by about
+5.5 MB per revision (a 200x200 platform owns 40000 height cells, re-serialized in full for
every revision); it is now about +8 KB per revision.

Run it after touching history persistence, entity compilation or storage to catch a
regression back to per-revision full snapshots. Prints a before/after table; no assertions
are made because the duration depends on the machine.

.EXAMPLE
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/history_index_bench.ps1
#>
param(
    # Do not name the loop variable $size: PowerShell variables are case-insensitive, so it
    # would overwrite this parameter and break iteration over more than one size.
    [int[]]$Sizes = @(256, 500),
    [string]$Launcher = "N:\Program Files (x86)\Red Alert 3(Incomplete)\CoronaLauncher\CoronaResources\NewWorldBuilder\WbLauncher.exe",
    [string]$OutputRoot
)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$dll = Join-Path $root "src\Dreamness.RA3.Map.Agent\bin\Debug\net6.0\Dreamness.RA3.Map.Agent.dll"
if (-not (Test-Path -LiteralPath $dll)) { throw "先构建 Agent 项目: dotnet build src/Dreamness.RA3.Map.Agent/Dreamness.RA3.Map.Agent.csproj" }
if (-not $OutputRoot) { $OutputRoot = Join-Path $root "artifacts\history-index-bench" }
if (-not (Test-Path -LiteralPath $OutputRoot)) { New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null }

$script:rid = 0
$script:lastJson = ""
function New-Agent([string]$artifacts, [string]$launcher) {
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = "dotnet"
    $psi.Arguments = '"' + $script:dll + '" --stdio --artifacts "' + $artifacts + '"'
    if ($launcher) { $psi.Arguments += ' --launcher "' + $launcher + '"' }
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.CreateNoWindow = $true
    $utf8 = New-Object System.Text.UTF8Encoding($false)
    $psi.StandardOutputEncoding = $utf8
    $psi.StandardErrorEncoding = $utf8
    return [System.Diagnostics.Process]::Start($psi)
}
function Invoke-Agent($proc, [string]$command, $arguments, [string]$session, [int]$revision) {
    $script:rid++
    $request = @{ requestId = ("bench-" + $script:rid); command = $command; arguments = $arguments }
    if ($session) { $request.sessionId = $session }
    if ($revision -ge 0) { $request.expectedRevision = $revision }
    $json = $request | ConvertTo-Json -Depth 30 -Compress
    $script:lastJson = $json
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json + "`n")
    $proc.StandardInput.BaseStream.Write($bytes, 0, $bytes.Length)
    $proc.StandardInput.BaseStream.Flush()
    $reader = $proc.StandardOutput.ReadLineAsync()
    if (-not $reader.Wait(600000)) { throw ($command + " 超时") }
    if ($null -eq $reader.Result) { throw ($command + ": Agent 已退出") }
    return ($reader.Result | ConvertFrom-Json)
}
function Format-Kb([long]$bytes) { return [math]::Round($bytes / 1KB, 1) }

Write-Output "history index growth benchmark"
Write-Output ("  launcher: " + $Launcher)
Write-Output ""
foreach ($grid in $Sizes) {
    $size = $grid
    $mapParent = Join-Path $OutputRoot ("size" + $size)
    if (Test-Path -LiteralPath $mapParent) { Remove-Item -LiteralPath $mapParent -Recurse -Force }
    New-Item -ItemType Directory -Path $mapParent -Force | Out-Null
    $name = "Bench" + $size
    $agent = New-Agent (Join-Path $OutputRoot ("agent" + $size)) $Launcher
    try {
        $created = Invoke-Agent $agent "map.create" @{ parentPath = $mapParent; mapName = $name
            playableWidth = $size; playableHeight = $size; border = 8 } $null -1
        if ($created.status -ne "succeeded") {
            Write-Output ("  create " + $size + " 失败: " + $created.error.code + " / " + $created.error.message)
            Write-Output ("  request: " + $script:lastJson)
            continue
        }
        $session = $created.sessionId
        $revision = $created.revisionAfter
        $base = Join-Path $mapParent $name
        $indexPath = Join-Path $base ".automation\History\index.json"
        Write-Output ("[" + $size + "x" + $size + "]  create=rev" + $revision)

        function Invoke-Patch($handle, [string]$sessionId, [int]$atRevision, $entities) {
            $prepared = Invoke-Agent $handle "design.prepare" @{ baseRevision = $atRevision
                patch = @{ schemaVersion = 1; upsert = $entities } } $sessionId -1
            if ($prepared.status -ne "succeeded") { return @{ ok = $false; message = $prepared.error.code } }
            $applied = Invoke-Agent $handle "design.apply" @{ preparedPlanId = $prepared.data.preparedPlanId
                planHash = $prepared.data.planHash } $sessionId $atRevision
            if ($applied.status -ne "succeeded") { return @{ ok = $false; message = $applied.error.code } }
            return @{ ok = $true; revision = $applied.revisionAfter }
        }

        # One large platform: 200x200 = 40000 owned cells, never modified afterwards.
        $land = @(@{ id = "land"; kind = "platform"
            parameters = @{ region = @{ x = 8; y = 8; width = 200; height = 200 }; value = 300; falloff = 0.15 } })
        $step = Invoke-Patch $agent $session $revision $land
        if (-not $step.ok) { Write-Output ("    land 失败: " + $step.message); continue }
        $revision = $step.revision
        Write-Output ("           rev " + $revision + "  +platform      index=" + (Format-Kb (Get-Item -LiteralPath $indexPath).Length) + " KB")

        $band = 0
        foreach ($batch in 1..3) {
            $placements = @()
            for ($i = 0; $i -lt 40; $i++) {
                $placements += @{ typeName = "OreNode"; anchor = "gridPoint"
                    x = 12 + (($i % 8) * 22); y = 12 + ($band * 26) + ([math]::Floor($i / 8) * 26)
                    ownerTeam = "PlyrNeutral/teamPlyrNeutral" }
            }
            $band++
            $entities = @(@{ id = ("objs-" + $batch); kind = "objects"; parameters = @{ placements = $placements } })
            $watch = [System.Diagnostics.Stopwatch]::StartNew()
            $step = Invoke-Patch $agent $session $revision $entities
            $watch.Stop()
            if (-not $step.ok) { Write-Output ("    objs#" + $batch + " 失败: " + $step.message); break }
            $revision = $step.revision
            Write-Output ("           rev " + $revision + "  +40 objects   index=" + (Format-Kb (Get-Item -LiteralPath $indexPath).Length) + " KB   apply=" + $watch.Elapsed.TotalSeconds.ToString("0.0") + "s")
        }

        $saved = Invoke-Agent $agent "map.save" @{ compress = $true } $session $revision
        $mapBytes = (Get-Item -LiteralPath (Join-Path $base ($name + ".map"))).Length
        Write-Output ("           save=" + $saved.status + "  .map=" + (Format-Kb $mapBytes) + " KB  peakRSS=" + [math]::Round($agent.PeakWorkingSet64 / 1MB, 0) + " MB")
    }
    finally { if (-not $agent.HasExited) { try { $agent.Kill() } catch { } } }
    Write-Output ""
}
Write-Output "完成。对照基线（1 个 200x200 platform + 120 个对象）：修复前 4 次修订后 21,908.3 KB，每修订约 +5,476 KB；修复后 1,730.1 KB，每修订约 +8 KB。"
