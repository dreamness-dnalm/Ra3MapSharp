using System.Security.Cryptography;
using System.Text.Json;
using Dreamness.RA3.Map.Agent;
using Dreamness.RA3.Map.Agent.Inspection;
using Dreamness.RA3.Map.Automation.Session;
using Dreamness.Ra3.Map.Facade.Core;
using NUnit.Framework;

namespace Dreamness.RA3.Map.Agent.Test;

public class MapInspectionTests
{
    [Test]
    public async Task InspectionPreservesSourceAndReportsTypedPropertiesAndRotation()
    {
        var root = Path.Combine(Path.GetTempPath(), "ra3-inspection-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (await MapSessionManager.CreateAsync(root, "Seed", 16, 12, 4)) { }
            var source = Path.Combine(root, "Source.map");
            File.Copy(Path.Combine(root, "Seed", "Seed.map"), source);
            var map = Ra3MapFacade.Open(source);
            map.AddUnitObject("OreNode", 20, 30).Angle = 135;
            map.AddUnitObject("CC_Tree01", 40, 30);
            map.AddUnitObject("OreNode", 60, 30).Angle = -45;
            map.Save();
            var bytes = File.ReadAllBytes(source);
            var result = JsonSerializer.SerializeToElement(await MapFileInspection.ReadAsync(source, Path.Combine(root, "reports"),
                new[] { "OreNode" }, 1, 1, true, CancellationToken.None), AgentJson.Options);
            Assert.That(File.ReadAllBytes(source), Is.EqualTo(bytes));
            Assert.That(Directory.Exists(Path.Combine(root, ".automation")), Is.False);
            Assert.That(result.GetProperty("contentHash").GetString(), Is.EqualTo("sha256:" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()));
            Assert.That(result.GetProperty("total").GetInt32(), Is.EqualTo(2));
            var obj = result.GetProperty("objects")[0];
            Assert.That(obj.GetProperty("sourceIndex").GetInt32(), Is.EqualTo(2));
            Assert.That(obj.GetProperty("angleDegrees").GetSingle(), Is.EqualTo(-45).Within(.0001));
            Assert.That(obj.GetProperty("angleRadians").GetSingle(), Is.EqualTo(-MathF.PI / 4).Within(.00001));
            Assert.That(obj.GetProperty("properties").GetProperty("objectEnabled").GetBoolean(), Is.True);
            Assert.That(obj.GetProperty("propertyTypes").TryGetProperty("objectEnabled", out _), Is.True);
            Assert.That(File.ReadAllBytes(result.GetProperty("snapshotPath").GetString()!), Is.EqualTo(bytes));
            var page = JsonSerializer.SerializeToElement(await MapFileInspection.ReadAsync(source, Path.Combine(root, "reports"),
                null, 3, 1, false, CancellationToken.None), AgentJson.Options);
            Assert.That(page.GetProperty("objects").GetArrayLength(), Is.Zero);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
