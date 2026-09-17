using System.Text.Json;
using Dreamness.RA3.Map.Agent.Rendering;
using Dreamness.RA3.Map.Automation;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;
using Dreamness.RA3.Map.Automation.Geometry;
using Dreamness.RA3.Map.Automation.Session;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Dreamness.RA3.Map.Agent.Test;

public class DiagnosticTests
{
    private string _root = null!;
    private MapSession _session = null!;
    [SetUp]
    public async Task Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "ra3-diagnostics-" + Guid.NewGuid().ToString("N"));
        _session = await MapSessionManager.CreateAsync(_root, "Test", 16, 12, 4);
        await Edit("terrain.set_height", new { region = new { x = 0, y = 0, width = 16, height = 12 }, height = 220 });
        await Edit("terrain.set_height", new { region = new { x = 0, y = 6, width = 8, height = 6 }, height = 300 });
        await Edit("protections.add", new { id = "base", region = new { x = 2, y = 2, width = 4, height = 4 }, layers = new[] { "terrain" } });
        await Edit("objects.place", new { typeName = "CC_Tree01", x = 10, y = 2 });
    }
    [TearDown]
    public void Cleanup() { _session.Dispose(); Directory.Delete(_root, true); }
    private async Task Edit(string command, object arguments)
    {
        var result = await CommandExecutor.CreateDefault().ExecuteAsync(new CommandRequest { Command = command, SessionId = _session.SessionId,
            ExpectedRevision = _session.Revision, Arguments = JsonSerializer.SerializeToElement(arguments) });
        Assert.That(result.Status, Is.EqualTo("succeeded"), result.Error?.Message);
    }

    [Test]
    public async Task ObjectAwarePassabilityPixelsMatchTraversalAndHash()
    {
        var footprints = new[] { new ObjectFootprint { TypeName = "CC_Tree01", Boxes = new[] { new FootprintBox { WidthCells = 4, DepthCells = 4 } } } };
        var snapshot = await _session.CaptureSnapshotAsync();
        var profile = new TerrainMovementProfile { WaterLevel = 200, ClearanceCells = 1 };
        var rendered = await DiagnosticRenderer.RenderAsync(snapshot, _root, "passability", profile, 16, footprints: footprints);
        var map = Dreamness.Ra3.Map.Facade.Core.Ra3MapFacade.Open(Path.Combine(Path.GetDirectoryName(rendered.ImagePath)!, "scene.map"));
        var grid = new TerrainTraversal(map, profile, footprints: footprints);
        using var image = Image.Load<Rgba32>(rendered.ImagePath);
        for (var y = 0; y < 12; y++)
        for (var x = 0; x < 16; x++)
            Assert.That(image[x, 11 - y] == new Rgba32(207, 60, 75), Is.EqualTo(grid.Blocked[x, y]));
        Assert.That(rendered.FootprintProfileHash, Is.EqualTo(ObjectFootprintSet.ContentHash(footprints)));
        Assert.That(rendered.NotEvaluated, Does.Contain("engineCollision").And.Not.Contain("objectCollision"));
    }

    [Test]
    public async Task HeightAndObjectImagesAreNorthUpWithCorrectMapping()
    {
        var snapshot = await _session.CaptureSnapshotAsync();
        var height = await DiagnosticRenderer.RenderAsync(snapshot, _root, "height", maxEdge: 32);
        using var pixels = Image.Load<Rgba32>(height.ImagePath);
        Assert.That(pixels[0, 0].R, Is.EqualTo(231));
        Assert.That(pixels[0, 23].R, Is.EqualTo(24));
        Assert.That(height.PixelToPlayableGrid, Is.EqualTo(new double[] { .5, 0, 0, 0, -.5, 12 }));
        var objects = await DiagnosticRenderer.RenderAsync(snapshot, _root, "objects", maxEdge: 32);
        using var markers = Image.Load<Rgba32>(objects.ImagePath);
        Assert.That(markers[21, 18], Is.EqualTo(new Rgba32(249, 200, 74)));
        Assert.That(objects.NotEvaluated, Does.Contain("objectFootprints"));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(snapshot.ContentHash));
    }

    [Test]
    public async Task ConstraintsAndPassabilityCarryTheirActualLegendsAndProfile()
    {
        var snapshot = await _session.CaptureSnapshotAsync();
        var zones = await DiagnosticRenderer.RenderAsync(snapshot, _root, "constraints", maxEdge: 32);
        Assert.That(zones.Legend.Any(l => l.Label == "base:terrain"), Is.True);
        Assert.That(zones.ProtectionHash, Is.EqualTo(snapshot.ProtectionHash));
        using var image = Image.Load<Rgba32>(zones.ImagePath);
        Assert.That(image[4, 19], Is.Not.EqualTo(image[0, 23]));
        var profile = new TerrainMovementProfile { WaterLevel = 200 };
        var traversal = await DiagnosticRenderer.RenderAsync(snapshot, _root, "passability", profile, 32);
        Assert.That(traversal.Legend.Single(l => l.Label == "blocked").Cells, Is.Positive);
        Assert.That(traversal.Legend.Count(l => l.Label.StartsWith("component-")), Is.GreaterThanOrEqualTo(2));
        Assert.That(traversal.NotEvaluated, Does.Contain("objectCollision").And.Not.Contain("waterDepth"));
        Assert.That(traversal.Profile, Is.SameAs(profile));
        Assert.That(Assert.ThrowsAsync<AutomationException>(async () => await DiagnosticRenderer.RenderAsync(snapshot, _root, "passability"))!.Code, Is.EqualTo("INVALID_ARGUMENT"));
    }

    [Test]
    public async Task TextureLegendAndDetachedProtectionTamperCheck()
    {
        await Edit("texture.paint", new { region = new { x = 0, y = 0, width = 4, height = 4 }, texture = "Dirt_Romania01" });
        var snapshot = await _session.CaptureSnapshotAsync();
        var textures = await DiagnosticRenderer.RenderAsync(snapshot, _root, "textures", maxEdge: 32);
        Assert.That(textures.Legend.Select(l => l.Label), Does.Contain("Dirt_Romania01").And.Contain("Dirt_Yucatan03"));
        snapshot.ProtectionZones[0].Id = "changed";
        Assert.That(Assert.ThrowsAsync<AutomationException>(async () => await DiagnosticRenderer.RenderAsync(snapshot, _root, "constraints"))!.Code, Is.EqualTo("SNAPSHOT_CHANGED"));
        Assert.That((await _session.CaptureSnapshotAsync()).ProtectionZones[0].Id, Is.EqualTo("base"));
    }

    [Test]
    public async Task RoutePixelsUseNorthUpCellCentersAndShowOnlyRealPaths()
    {
        var snapshot = await _session.CaptureSnapshotAsync();
        var rendered = await DiagnosticRenderer.RenderAsync(snapshot, _root, "passability", new TerrainMovementProfile(), 160,
            routes: new[] {
                new DiagnosticRouteRequest { From = new(1, 2), To = new(7, 2) },
                new DiagnosticRouteRequest { From = new(1, 2), To = new(1, 9) },
                new DiagnosticRouteRequest { From = new(7, 6), To = new(10, 9) }
            });
        Assert.That(rendered.Routes![0].Route.Status, Is.EqualTo("found"));
        Assert.That(rendered.Routes[0].Route.Steps, Is.EqualTo(6));
        Assert.That(rendered.Routes[1].Route.Status, Is.EqualTo("disconnected"));
        Assert.That(rendered.Routes[1].Route.Cells, Is.Empty);
        Assert.That(rendered.Routes[2].FromBlocked, Is.True);
        using var pixels = Image.Load<Rgba32>(rendered.ImagePath);
        Assert.That(pixels[45, 94], Is.EqualTo(new Rgba32(0, 245, 255)));
        Assert.That(pixels[15, 94], Is.EqualTo(new Rgba32(0, 255, 154)));
        Assert.That(pixels[75, 94], Is.EqualTo(new Rgba32(255, 102, 204)));
        Assert.That(pixels[75, 54], Is.EqualTo(new Rgba32(255, 255, 255)));
        for (var y = 0; y < pixels.Height; y++)
        for (var x = 0; x < pixels.Width; x++)
            Assert.That(pixels[x,y], Is.Not.EqualTo(new Rgba32(255, 230, 0)), "Disconnected endpoints must not acquire a connecting line.");
        Assert.That(rendered.NotEvaluated, Does.Contain("routeOverlayOccludesUnderlyingCells"));
        Assert.That((await _session.CaptureSnapshotAsync()).ContentHash, Is.EqualTo(snapshot.ContentHash));
    }

    [Test]
    public async Task RouteRenderingRejectsWrongLayerInvalidEndpointsAndTooManyRoutes()
    {
        var snapshot = await _session.CaptureSnapshotAsync();
        var request = new DiagnosticRouteRequest { From = new(0, 0), To = new(15, 11) };
        Assert.That(Assert.ThrowsAsync<AutomationException>(async () => await DiagnosticRenderer.RenderAsync(snapshot, _root, "height",
            routes: new[] { request }))!.Code, Is.EqualTo("INVALID_ARGUMENT"));
        Assert.That(Assert.ThrowsAsync<AutomationException>(async () => await DiagnosticRenderer.RenderAsync(snapshot, _root, "passability",
            new TerrainMovementProfile(), routes: Enumerable.Repeat(request, 17).ToArray()))!.Code, Is.EqualTo("INVALID_ARGUMENT"));
        request.To = new(16, 11);
        Assert.That(Assert.ThrowsAsync<AutomationException>(async () => await DiagnosticRenderer.RenderAsync(snapshot, _root, "passability",
            new TerrainMovementProfile(), routes: new[] { request }))!.Code, Is.EqualTo("INVALID_ARGUMENT"));
    }
}
