using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Commands.Batch;
using Dreamness.RA3.Map.Automation.Commands.History;
using Dreamness.RA3.Map.Automation.Commands.Map;
using Dreamness.RA3.Map.Automation.Commands.Terrain;
using Dreamness.RA3.Map.Automation.Commands.Waypoints;
using Dreamness.RA3.Map.Automation.Commands.Objects;

namespace Dreamness.RA3.Map.Automation.Executor;

public sealed class CommandRegistry
{
    private readonly Dictionary<string, ICommandHandler> _handlers = new(StringComparer.Ordinal);

    public IReadOnlyList<CommandDescription> Describe() => _handlers.Values
        .OrderBy(h => h.Name, StringComparer.Ordinal)
        .Select(h => new CommandDescription(h.Name, h.Effect, 1)).ToArray();

    public void Register(ICommandHandler handler)
    {
        if (!_handlers.TryAdd(handler.Name, handler))
        {
            throw new InvalidOperationException("重复注册命令: " + handler.Name);
        }
    }

    public ICommandHandler Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !_handlers.TryGetValue(name, out var handler))
        {
            throw new AutomationException(
                "UNKNOWN_COMMAND",
                $"未知命令: {name}",
                details: new Dictionary<string, string> { ["command"] = name ?? "" });
        }

        return handler;
    }

    public static CommandRegistry CreateDefault(Catalog.ObjectCatalog? catalog = null,
        Catalog.FootprintCatalog? footprints = null)
    {
        var registry = new CommandRegistry();
        registry.Register(new CreateMapHandler());
        registry.Register(new OpenMapHandler());
        registry.Register(new MapInfoHandler());
        registry.Register(new ValidateMapHandler());
        foreach (var name in new[] { "protections.list", "protections.add", "protections.remove" })
            registry.Register(new ProtectionHandler(name));
        registry.Register(new SaveMapHandler());
        registry.Register(new SaveMapAsHandler());
        registry.Register(new ExportProjectHandler("map.export_project"));
        registry.Register(new ExportProjectHandler("map.export_package"));
        registry.Register(new SetHeightHandler());
        registry.Register(new QueryHeightHandler());
        registry.Register(new AnalyzeTerrainHandler());
        registry.Register(new RampTerrainHandler());
        registry.Register(new RebuildPassabilityHandler());
        registry.Register(new SculptTerrainHandler());
        registry.Register(new SmoothTerrainHandler());
        registry.Register(new Commands.Art.ArtProfileHandler(catalog));
        registry.Register(new QueryTexturesHandler());
        registry.Register(new PaintTextureHandler());
        registry.Register(new PaintTextureByHeightHandler());
        registry.Register(new PlaceWaypointHandler());
        registry.Register(new MoveWaypointHandler());
        registry.Register(new DeleteWaypointHandler());
        registry.Register(new PlayerStartHandler("starts.list"));
        registry.Register(new PlayerStartHandler("starts.place"));
        registry.Register(new ScatterObjectsHandler(catalog, footprints));
        registry.Register(new AnalyzeObjectSpaceHandler());
        foreach (var name in new[] { "objects.query", "objects.place", "objects.move", "objects.delete", "objects.configure" })
            registry.Register(new ObjectHandler(name, catalog));
        registry.Register(new UndoHandler());
        registry.Register(new RedoHandler());
        foreach (var name in new[] { "edits.prepare", "edits.apply", "edits.discard" })
            registry.Register(new PreparedEditHandler(name, registry, catalog?.ContentHash));
        foreach (var name in new[] { "design.query", "design.prepare", "design.apply" })
            registry.Register(new DesignHandler(name, registry, catalog?.ContentHash));
        return registry;
    }
}

public sealed record CommandDescription(string Name, CommandEffect Effect, int CommandVersion);
