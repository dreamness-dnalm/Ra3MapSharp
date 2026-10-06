using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Dreamness.RA3.Map.Automation;
using Dreamness.RA3.Map.Automation.Session;
using SixLabors.ImageSharp;

namespace Dreamness.RA3.Map.Agent.Rendering;

public sealed record OverviewResult(string ImagePath, int Width, int Height, int Revision,
    string MapContentHash, string ImageHash, string RendererConfigHash, JsonElement RendererConfig,
    double[] PixelToPlayableGrid, string CoordinateConvention, string DiagnosticsDirectory,
    string? PreparedPlanId = null, string? PlanHash = null, string DesignHash = "");

public interface IOverviewProcess
{
    Task<int> RunAsync(string launcherPath, string mapPath, CancellationToken cancellationToken);
}

public sealed class OverviewProcess : IOverviewProcess
{
    public async Task<int> RunAsync(string launcherPath, string mapPath, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(launcherPath)
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            WorkingDirectory = Path.GetDirectoryName(launcherPath)!,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add("--export-overview");
        start.ArgumentList.Add(mapPath);
        using var process = Process.Start(start) ?? throw new AutomationException("RENDER_START_FAILED", "无法启动 WorldBuilder。");
        await using var stdout = File.Create(Path.Combine(Path.GetDirectoryName(mapPath)!, "launcher.stdout.log"));
        await using var stderr = File.Create(Path.Combine(Path.GetDirectoryName(mapPath)!, "launcher.stderr.log"));
        var output = process.StandardOutput.BaseStream.CopyToAsync(stdout);
        var errors = process.StandardError.BaseStream.CopyToAsync(stderr);
        // The launcher's documented deadline is 300s + up to 10s cleanup.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(325));
        try { await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false); }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await process.WaitForExitAsync(cleanup.Token).ConfigureAwait(false);
            }
            cancellationToken.ThrowIfCancellationRequested();
            throw new AutomationException("RENDER_TIMEOUT", "WorldBuilder 超过宿主渲染期限。", true);
        }
        await Task.WhenAll(output, errors).WaitAsync(TimeSpan.FromSeconds(10));
        return process.ExitCode;
    }
}

/// <summary>Renders a private snapshot, never the user's map or a mutable working file.</summary>
public sealed class WorldBuilderRenderer
{
    private readonly IOverviewProcess _process;
    public string LauncherPath { get; }
    public string ArtifactRoot { get; }
    public string ConfigPath => Path.Combine(Path.GetDirectoryName(LauncherPath)!, "data", "config", "map-task-launch.json");

    public WorldBuilderRenderer(string launcherPath, string artifactRoot, IOverviewProcess? process = null)
    {
        LauncherPath = Path.GetFullPath(launcherPath);
        ArtifactRoot = Path.GetFullPath(artifactRoot);
        _process = process ?? new OverviewProcess();
    }

    public object GetAvailability() => new
    {
        configured = File.Exists(LauncherPath) && File.Exists(ConfigPath),
        launcherPath = LauncherPath, configPath = ConfigPath,
        note = "需要已初始化的游戏/Mod 配置和可用 Windows 图形会话；configured 不代表渲染验收通过。"
    };

    public async Task<OverviewResult> RenderAsync(MapSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(LauncherPath) || !File.Exists(ConfigPath))
            throw new AutomationException("RENDER_NOT_CONFIGURED", "找不到 WbLauncher 或 map-task-launch.json。先正常启动地编选择游戏/Mod。");
        var configBytes = await File.ReadAllBytesAsync(ConfigPath, cancellationToken);
        using var configDocument = JsonDocument.Parse(configBytes);
        var configHash = Hash(configBytes);
        if (Hash(snapshot.MapBytes) != snapshot.ContentHash)
            throw new AutomationException("SNAPSHOT_CHANGED", "快照内容与记录的哈希不一致。");
        var directory = Path.Combine(ArtifactRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var mapPath = Path.Combine(directory, "scene.map");
        await File.WriteAllBytesAsync(mapPath, snapshot.MapBytes, cancellationToken);
        var exitCode = await _process.RunAsync(LauncherPath, mapPath, cancellationToken);
        if (exitCode != 0)
            throw new AutomationException("RENDER_FAILED", $"WorldBuilder 导出退出码 {exitCode}。",
                details: new Dictionary<string, string>
                {
                    ["exitCode"] = exitCode.ToString(), ["snapshotPath"] = mapPath,
                    ["diagnosticsDirectory"] = DiagnosticsDirectory
                });
        var imagePath = Path.ChangeExtension(mapPath, ".overview.png");
        if (!File.Exists(imagePath))
            throw new AutomationException("RENDER_OUTPUT_MISSING", "渲染器退出成功但未生成 PNG。");
        if (Hash(await File.ReadAllBytesAsync(mapPath, cancellationToken)) != snapshot.ContentHash)
            throw new AutomationException("SNAPSHOT_CHANGED", "渲染过程修改了地图副本。");
        if (Hash(await File.ReadAllBytesAsync(ConfigPath, cancellationToken)) != configHash)
            throw new AutomationException("RENDER_CONFIG_CHANGED", "渲染期间游戏/Mod 配置变化，不能确认图像来源。");
        // Decode the PNG, not merely test existence: a stale or truncated file is not success.
        using var image = await Image.LoadAsync(imagePath, cancellationToken);
        if (image.Width < 1 || image.Height < 1 || Math.Max(image.Width, image.Height) > 2048)
            throw new AutomationException("RENDER_OUTPUT_INVALID", "鸟瞰图尺寸不符合 MAP_TASKS 协议。");
        var sx = (double)snapshot.PlayableWidth / image.Width;
        var sy = (double)snapshot.PlayableHeight / image.Height;
        var result = new OverviewResult(imagePath, image.Width, image.Height, snapshot.Revision,
            snapshot.ContentHash, Hash(await File.ReadAllBytesAsync(imagePath, cancellationToken)), configHash,
            configDocument.RootElement.Clone(), new[] { sx, 0, 0, 0, -sy, (double)snapshot.PlayableHeight },
            "北朝上；像素边界坐标(u,v)映射到可玩网格；像素中心使用(u+0.5,v+0.5)，不含地图边界。", DiagnosticsDirectory,
            snapshot.PreparedPlanId, snapshot.PlanHash, snapshot.DesignHash);
        await File.WriteAllTextAsync(Path.Combine(directory, "overview.json"),
            JsonSerializer.Serialize(result, AgentJson.Options), cancellationToken);
        return result;
    }

    private static string DiagnosticsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NewWorldBuilder", "MapTasks");
    private static string Hash(byte[] bytes) => "sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
