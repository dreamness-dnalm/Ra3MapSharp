<#
.SYNOPSIS
  验证 Ra3MapSharp Agent（Automation 内核）的 MCP stdio 接口是否可用。

.DESCRIPTION
  无外部依赖（Windows PowerShell 5.1 可运行）：以 stdio 启动 Agent，完成 initialize / tools/list 握手，
  报告工具数量与清单。

  -Smoke   额外跑真实编辑闭环：建图 -> 查询 -> 检查落盘文件。
  -Render  额外跑真实渲染闭环：建图 -> 保存 -> preview.start -> jobs.status 轮询 -> preview.inspect，
           需要 -Launcher 指向 WbLauncher.exe（默认自动探测标准安装位置）。

.EXAMPLE
  powershell -NoProfile -File scripts\mcp_probe.ps1
  powershell -NoProfile -File scripts\mcp_probe.ps1 -Smoke -Render
#>
[CmdletBinding()]
param(
    [string]$Dll = "",
    [string]$Launcher = "",
    [string]$Artifacts = "",
    [switch]$Smoke,
    [switch]$Render,
    [int]$TimeoutSeconds = 120,
    [int]$RenderTimeoutSeconds = 300
)

$ErrorActionPreference = "Stop"
$protocolVersion = "2025-06-18"
$deadlineMs = $TimeoutSeconds * 1000

if (-not $Dll) { $Dll = Join-Path $PSScriptRoot "..\src\Dreamness.RA3.Map.Agent\bin\Debug\net6.0\Dreamness.RA3.Map.Agent.dll" }
$Dll = [System.IO.Path]::GetFullPath($Dll)
if (-not (Test-Path -LiteralPath $Dll)) {
    Write-Output "[fail] 找不到 Agent 程序集: $Dll"
    Write-Output "       先执行: dotnet build src/Dreamness.RA3.Map.Agent/Dreamness.RA3.Map.Agent.csproj"
    exit 2
}
if (-not $Launcher) {
    $std = Join-Path $env:ProgramFiles "Red Alert 3(Incomplete)\CoronaLauncher\CoronaResources\NewWorldBuilder\WbLauncher.exe"
    $alt = "N:\Program Files (x86)\Red Alert 3(Incomplete)\CoronaLauncher\CoronaResources\NewWorldBuilder\WbLauncher.exe"
    if (Test-Path -LiteralPath $alt) { $Launcher = $alt }
    elseif (Test-Path -LiteralPath $std) { $Launcher = $std }
}
if (-not $Artifacts) { $Artifacts = Join-Path ([System.IO.Path]::GetDirectoryName($Dll)) "probe-artifacts" }
if ($Render -and -not $Launcher) {
    Write-Output "[fail] -Render 需要 -Launcher 指向 WbLauncher.exe，且未探测到标准安装位置。"
    exit 2
}

function Format-Arg([string]$a) {
    if ($a -match '^[A-Za-z0-9_./:=+\\-]+$') { return $a }
    return '"' + $a + '"'
}
$argv = @($Dll, "--mcp")
if ($Launcher) { $argv += @("--launcher", $Launcher) }
$argv += @("--artifacts", $Artifacts)

$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "dotnet"
$psi.Arguments = (($argv | ForEach-Object { Format-Arg $_ }) -join " ")
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$psi.CreateNoWindow = $true
$utf8 = New-Object System.Text.UTF8Encoding($false)
$psi.StandardOutputEncoding = $utf8
$psi.StandardErrorEncoding = $utf8

$proc = [System.Diagnostics.Process]::Start($psi)
$stderr = New-Object System.Text.StringBuilder
$script:errRef = $stderr
$proc.add_ErrorDataReceived([System.Diagnostics.DataReceivedEventHandler]{
    param($s, $e) if ($null -ne $e.Data) { [void]$script:errRef.AppendLine($e.Data) }
})
$proc.BeginErrorReadLine()

function Send-Rpc($payload) {
    $json = $payload | ConvertTo-Json -Depth 20 -Compress
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json + "`n")
    $proc.StandardInput.BaseStream.Write($bytes, 0, $bytes.Length)
    $proc.StandardInput.BaseStream.Flush()
}

function Receive-Rpc([int]$id) {
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    while ($sw.ElapsedMilliseconds -lt $deadlineMs) {
        $remaining = $deadlineMs - [int]$sw.ElapsedMilliseconds
        $task = $proc.StandardOutput.ReadLineAsync()
        if (-not $task.Wait([Math]::Max(1000, $remaining))) { throw "等待响应超时 (id=$id)" }
        $line = $task.Result
        if ($null -eq $line) { throw "Agent 标准输出已关闭 (id=$id)" }
        if ($line.Trim().Length -eq 0) { continue }
        $obj = $line | ConvertFrom-Json
        if ($null -ne $obj.id -and [int]$obj.id -eq $id) { return $obj }
    }
    throw "等待响应超时 (id=$id)"
}

function Get-CallText($result) {
    $item = @($result.content) | Where-Object { $_.type -eq "text" } | Select-Object -First 1
    if ($null -eq $item) { return "" }
    return [string]$item.text
}
function Get-ImageBytes($result) {
    $item = @($result.content) | Where-Object { $_.type -eq "image" } | Select-Object -First 1
    if ($null -eq $item) { return 0 }
    return [int]($item.data.Length * 0.75)
}
function Clip([string]$s, [int]$n) { if ($s.Length -gt $n) { return $s.Substring(0, $n) } return $s }

$script:nextId = 10
function Invoke-Tool([string]$tool, $arguments, [string]$sessionId, [int]$expectedRevision = -1) {
    $script:nextId++
    $envelope = @{ arguments = $arguments }
    if ($sessionId) { $envelope.sessionId = $sessionId }
    if ($expectedRevision -ge 0) { $envelope.expectedRevision = $expectedRevision }
    Send-Rpc @{ jsonrpc = "2.0"; id = $script:nextId; method = "tools/call"; params = @{ name = $tool; arguments = $envelope } }
    $resp = Receive-Rpc $script:nextId
    if ($resp.error) { throw "$tool 协议错误: $($resp.error.message)" }
    $text = Get-CallText $resp.result
    $parsed = $null
    try { $parsed = $text | ConvertFrom-Json } catch { }
    return @{ Text = $text; Parsed = $parsed; IsError = [bool]$resp.result.isError; ImageBytes = (Get-ImageBytes $resp.result) }
}

$tools = @()
$smokeRoot = $null
try {
    Send-Rpc @{ jsonrpc = "2.0"; id = 1; method = "initialize"; params = @{
        protocolVersion = $protocolVersion; capabilities = @{}
        clientInfo = @{ name = "ra3-mcp-probe"; version = "1.0.0" } } }
    $init = Receive-Rpc 1
    if ($init.error) { throw "initialize 失败: $($init.error.message)" }
    Write-Output ("[ok] initialize - server={0} {1} protocol={2}" -f $init.result.serverInfo.name, $init.result.serverInfo.version, $init.result.protocolVersion)

    Send-Rpc @{ jsonrpc = "2.0"; method = "notifications/initialized"; params = @{} }

    Send-Rpc @{ jsonrpc = "2.0"; id = 2; method = "tools/list"; params = @{} }
    $list = Receive-Rpc 2
    if ($list.error) { throw "tools/list 失败: $($list.error.message)" }
    $tools = @($list.result.tools)
    $withSchema = @($tools | Where-Object { $_.inputSchema }).Count
    Write-Output ("[ok] tools/list - {0} 个工具，{1} 个带 inputSchema" -f $tools.Count, $withSchema)
    foreach ($t in ($tools | Sort-Object { $_.name })) { Write-Output ("       " + $t.name) }

    if ($Smoke -or $Render) {
        $smokeRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("ra3-mcp-probe-" + [guid]::NewGuid().ToString("N").Substring(0, 8))
        New-Item -ItemType Directory -Path $smokeRoot -Force | Out-Null
        $mapName = "McpProbe"

        $created = Invoke-Tool "ra3_map_create" @{ parentPath = $smokeRoot; mapName = $mapName; playableWidth = 128; playableHeight = 128; border = 8 } $null
        if ($created.IsError) { throw "ra3_map_create 返回错误: $($created.Text)" }
        $sessionId = $created.Parsed.sessionId
        if (-not $sessionId) { throw "ra3_map_create 未返回 sessionId" }
        $rev = 0
        Write-Output ("[ok] ra3_map_create - sessionId=" + $sessionId + " revision=" + $created.Parsed.revisionAfter)

        $info = Invoke-Tool "ra3_map_info" @{} $sessionId
        Write-Output ("[ok] ra3_map_info - " + (Clip $info.Text 200))

        $mapFile = Join-Path (Join-Path $smokeRoot $mapName) "$mapName.map"
        if (Test-Path -LiteralPath $mapFile) {
            Write-Output ("[ok] 落盘文件 " + $mapFile + " (" + (Get-Item -LiteralPath $mapFile).Length + " 字节)")
        } else {
            Write-Output ("[warn] 未见预期地图文件: " + $mapFile)
        }

        if ($Render) {
            $saved = Invoke-Tool "ra3_map_save" @{ compress = $true } $sessionId $rev
            if ($saved.IsError) { throw "ra3_map_save 返回错误: $($saved.Text)" }
            $rev = [int]$saved.Parsed.revisionAfter
            Write-Output ("[ok] ra3_map_save - revision=" + $rev)

            $started = Invoke-Tool "ra3_preview_start" @{ requiredRevision = $rev } $sessionId $rev
            if ($started.IsError) { throw "ra3_preview_start 返回错误: $($started.Text)" }
            $jobId = $started.Parsed.data.jobId
            if (-not $jobId) { throw ("ra3_preview_start 未返回 jobId: " + (Clip $started.Text 300)) }
            Write-Output ("[ok] ra3_preview_start - jobId=" + $jobId)

            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            $state = "unknown"
            while ($sw.Elapsed.TotalSeconds -lt $RenderTimeoutSeconds) {
                Start-Sleep -Milliseconds 1500
                $st = Invoke-Tool "ra3_jobs_status" @{ jobId = $jobId } $sessionId
                # 注意：作业状态字段是 data.state，不是 data.status
                $state = [string]$st.Parsed.data.state
                if (-not $state) { $state = [string]$st.Parsed.data.status }
                if ($state -in @("succeeded", "failed", "cancelled")) { break }
            }
            Write-Output ("[ok] jobs.status - " + $state + " (" + [int]$sw.Elapsed.TotalSeconds + " 秒)")
            if ($state -ne "succeeded") { throw ("真实渲染未成功，状态=" + $state + " 详情=" + (Clip $st.Text 400)) }

            $inspected = Invoke-Tool "ra3_preview_inspect" @{ jobId = $jobId; maxEdge = 1024 } $sessionId
            if ($inspected.IsError) { throw "ra3_preview_inspect 返回错误: $($inspected.Text)" }
            $imgPath = $inspected.Parsed.data.imagePath
            Write-Output ("[ok] ra3_preview_inspect - " + $inspected.Parsed.data.width + "x" + $inspected.Parsed.data.height + " PNG，约 " + $inspected.ImageBytes + " 字节（MCP ImageContent）")
            if ($imgPath) { Write-Output ("[ok] 渲染产物 " + $imgPath) }
        }
    }

    Write-Output ""
    Write-Output ("结论: MCP 接口可用，" + $tools.Count + " 个工具。")
    if (-not $Launcher) { Write-Output "注意: 未提供 -Launcher，真实鸟瞰图 (preview.start) 不可用；文件编辑仍可用。" }
    $exitCode = 0
}
catch {
    Write-Output ("[fail] " + $_)
    if ($stderr.Length -gt 0) { Write-Output "--- agent stderr ---"; Write-Output (Clip $stderr.ToString() 1500) }
    $exitCode = 1
}
finally {
    if ($smokeRoot -and (Test-Path -LiteralPath $smokeRoot)) { Remove-Item -LiteralPath $smokeRoot -Recurse -Force -ErrorAction SilentlyContinue }
    if ($proc -and -not $proc.HasExited) { try { $proc.Kill() } catch { } }
}
exit $exitCode
