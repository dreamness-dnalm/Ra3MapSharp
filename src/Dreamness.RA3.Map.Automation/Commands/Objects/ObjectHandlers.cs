using System.Text.Json;
using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.Ra3.Map.Parser.Util;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Session;
using Dreamness.RA3.Map.Automation.Storage;
using Dreamness.RA3.Map.Automation.Catalog;

namespace Dreamness.RA3.Map.Automation.Commands.Objects;

internal sealed class ObjectHandler : ICommandHandler
{
    public string Name { get; }
    public CommandEffect Effect => Name == "objects.query" ? CommandEffect.Query : CommandEffect.Mutation;
    private readonly ObjectCatalog? _catalog;
    public ObjectHandler(string name, ObjectCatalog? catalog = null) { Name = name; _catalog = catalog; }

    public Task<object?> ExecuteAsync(CommandContext? context, JsonElement arguments, CancellationToken cancellationToken)
    {
        var session = context?.Session ?? throw new AutomationException("SESSION_NOT_FOUND", "对象命令需要已打开的工作区。");
        var args = AutomationJson.Deserialize<ObjectArgs>(arguments);
        var map = session.Facade;
        session.Handles.EnsureMatches(map);
        object result;
        if (Name == "objects.query")
        {
            if (args.Offset < 0 || args.Limit < 1 || args.Limit > 200)
                throw new AutomationException("INVALID_ARGUMENT", "offset 必须非负，limit 必须为 1–200。");
            var all = map.GetUnitObjects().Select((obj, i) => (obj, id: session.Handles.UnitObjectIds[i]))
                .Where(p => (args.ObjectId == null || p.id == args.ObjectId)
                    && (args.TypeName == null || p.obj.TypeName == args.TypeName)).ToArray();
            result = new { total = all.Length, offset = args.Offset, items = all.Skip(args.Offset).Take(args.Limit).Select(p => Data(p.id, p.obj)).ToArray() };
        }
        else if (Name == "objects.delete")
        {
            var obj = session.Handles.ResolveUnit(map, RequireId(args));
            map.Remove(obj);
            session.Handles.RemoveUnit(args.ObjectId!);
            result = new { objectId = args.ObjectId, deleted = true };
        }
        else if (Name == "objects.configure")
        {
            if (args.OwnerTeam == null && args.Name == null && (args.Settings == null || args.Settings.Count == 0))
                throw new AutomationException("INVALID_ARGUMENT", "至少指定 ownerTeam、name 或 settings 中的一项。");
            var id = RequireId(args);
            var obj = session.Handles.ResolveUnit(map, id);
            ObjectSettings.Apply(map, obj, args.OwnerTeam, args.Settings);
            if (args.Name != null) obj.ObjName = args.Name;
            result = Data(id, obj);
        }
        else
        {
            if (!args.X.HasValue || !args.Y.HasValue)
                throw new AutomationException("INVALID_ARGUMENT", "放置和移动需要 x、y。");
            Coordinates.ToWorld(map, args.Space, args.X.Value, args.Y.Value, args.Anchor, out var x, out var y);
            if (args.Z.HasValue) Coordinates.EnsureFinite("z", args.Z.Value);
            if (args.AngleRadians.HasValue) Coordinates.EnsureFinite("angleRadians", args.AngleRadians.Value);
            UnitObjectWrap obj;
            string id;
            if (Name == "objects.place")
            {
                if (!ValidTypeName(args.TypeName))
                    throw new AutomationException("INVALID_ARGUMENT", "typeName 必须是普通对象的资源名称（ASCII 字母、数字、下划线）。");
                // Resource existence is not inferred from a syntactically valid name.
                // The catalogue/profile layer must provide verified names to this primitive.
                _catalog?.Require(args.TypeName, args.CatalogHash);
                if (_catalog == null && args.CatalogHash != null)
                    throw new AutomationException("CATALOG_UNAVAILABLE", "当前未加载素材目录，无法核对 catalogHash。");
                obj = map.AddUnitObject(args.TypeName, x, y, args.Z ?? 0);
                ObjectSettings.Apply(map, obj, args.OwnerTeam, args.Settings);
                if (args.Name != null) obj.ObjName = args.Name;
                id = session.Handles.RegisterNewUnit();
            }
            else
            {
                id = RequireId(args);
                obj = session.Handles.ResolveUnit(map, id);
                obj.Position = new Vec3D(x, y, args.Z ?? obj.Position.Z);
            }
            if (args.AngleRadians.HasValue) obj.Angle = Geometry.MapAngles.ToDegrees(args.AngleRadians.Value);
            result = Data(id, obj);
        }
        return Task.FromResult<object?>(result);
    }

    private static string RequireId(ObjectArgs args) => !string.IsNullOrWhiteSpace(args.ObjectId)
        ? args.ObjectId : throw new AutomationException("INVALID_ARGUMENT", "objectId 不能为空。");

    internal static bool ValidTypeName([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 256
        && name.All(c => char.IsAscii(c) && (char.IsLetterOrDigit(c) || c == '_'));

    private object Data(string id, UnitObjectWrap obj) => new
    {
        objectId = id, uniqueId = obj.UniqueId, typeName = obj.TypeName, name = obj.ObjName,
        x = obj.Position.X, y = obj.Position.Y, z = obj.Position.Z, angleRadians = Geometry.MapAngles.ToRadians(obj.Angle),
        space = "world", assetValidation = _catalog?.Contains(obj.TypeName) == true ? "editor-declared" : "unverified",
        catalogHash = _catalog?.ContentHash, ownerTeam = obj.BelongToTeam, settings = ObjectSettings.Read(obj)
    };
}

internal sealed class ObjectArgs
{
    public string? ObjectId { get; set; }
    public string? CatalogHash { get; set; }
    public string? TypeName { get; set; }
    public string? Name { get; set; }
    public string? OwnerTeam { get; set; }
    public Dictionary<string, JsonElement>? Settings { get; set; }
    public string Space { get; set; } = "playableGrid";
    public string Anchor { get; set; } = "center";
    public float? X { get; set; }
    public float? Y { get; set; }
    public float? Z { get; set; }
    public float? AngleRadians { get; set; }
    public int Offset { get; set; }
    public int Limit { get; set; } = 100;
}
