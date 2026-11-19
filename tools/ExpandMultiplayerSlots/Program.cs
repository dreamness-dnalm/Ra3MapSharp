using System.Text.Json;
using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Parser.Asset.Base;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: ExpandMultiplayerSlots <input-directory> <new-output-directory>");
    return 2;
}
var input = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
if (!Directory.Exists(input)) throw new DirectoryNotFoundException(input);
if (Directory.Exists(output) || File.Exists(output)) throw new IOException("Output must be a new directory.");
if (output.StartsWith(input.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
    throw new ArgumentException("Output must be outside the input directory.");
var maps = Directory.GetFiles(input, "*.map", SearchOption.AllDirectories).OrderBy(p => p).ToArray();
if (maps.Length == 0) throw new InvalidOperationException("No .map files found.");
Directory.CreateDirectory(output);
foreach (var file in Directory.GetFiles(input, "*", SearchOption.AllDirectories))
{
    var dest = Path.Combine(output, Path.GetRelativePath(input, file));
    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
    File.Copy(file, dest);
}
var report = new List<object>();
foreach (var path in maps)
{
    var map = Ra3MapFacade.Open(path);
    var ctx = map.ra3Map.Context;
    var slots = map.GetMultiplayerPositions();
    if (slots.Count > 8) throw new InvalidDataException($"Refusing to truncate {path}: {slots.Count} slots.");
    var originals = slots.Select(s => s.ToBytes(ctx)).ToArray();
    var otherAssets = ctx.AssetDict.Where(p => p.Key != AssetNameConst.MPPositionList)
        .ToDictionary(p => p.Key, p => p.Value.ToBytes(ctx));
    map.ResizeMultiplayerPositions(8);
    var destination = Path.Combine(output, Path.GetRelativePath(input, path));
    map.SaveAs(destination);
    var verified = Ra3MapFacade.Open(destination);
    var verifiedCtx = verified.ra3Map.Context;
    var actual = verified.GetMultiplayerPositions();
    if (actual.Count != 8) throw new InvalidDataException("Slot count verification failed.");
    for (var i = 0; i < originals.Length; i++)
        if (!actual[i].ToBytes(verifiedCtx).SequenceEqual(originals[i])) throw new InvalidDataException("Existing slot changed.");
    foreach (var (name, bytes) in otherAssets)
        if (!verifiedCtx.AssetDict[name].ToBytes(verifiedCtx).SequenceEqual(bytes))
            throw new InvalidDataException($"Unrelated asset changed: {name}");
    var starts = verified.GetWaypoints().Where(w => w.WaypointName.StartsWith("Player_", StringComparison.Ordinal) && w.WaypointName.EndsWith("_Start", StringComparison.Ordinal))
        .Select(w => w.WaypointName).OrderBy(n => n).ToArray();
    report.Add(new { map = Path.GetRelativePath(input, path), before = originals.Length, after = actual.Count, playerStarts = starts, verified = true });
    Console.WriteLine($"{Path.GetFileName(path)}: {originals.Length} -> 8; other assets unchanged");
}
File.WriteAllText(Path.Combine(output, "slot-expansion-report.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"Verified {maps.Length} maps. Output: {output}");
return 0;
