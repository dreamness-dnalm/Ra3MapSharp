"""Generate embedded tool schemas. Keep contract changes paired with handler changes."""
import json
from pathlib import Path


def obj(properties=None, required=()):
    return dict(type="object", properties=properties or {}, required=list(required), additionalProperties=False)


def scalar(kind, **kw):
    return dict(type=kind, **kw)


def array(items, minimum=0, maximum=100):
    return dict(type="array", items=items, minItems=minimum, maxItems=maximum)


S = scalar("string")
N = scalar("number")
I = scalar("integer")
POS = scalar("integer", minimum=1)
HEIGHT = scalar("number", minimum=0, maximum=2559.96)
SPACE = scalar("string", enum=["playableGrid", "mapGrid"], default="playableGrid")
POINT = obj(dict(x=N, y=N), ["x", "y"])
RECT = obj(dict(kind=scalar("string", enum=["rectangle"], default="rectangle"), space=SPACE,
                x=I, y=I, width=POS, height=POS), ["x", "y", "width", "height"])
CIRCLE = obj(dict(kind=scalar("string", enum=["circle"]), space=SPACE,
                  centerX=N, centerY=N, radius=scalar("number", exclusiveMinimum=0)), ["kind", "centerX", "centerY", "radius"])
POLYGON = obj(dict(kind=scalar("string", enum=["polygon"]), space=SPACE, vertices=array(POINT, 3, 64)), ["kind", "vertices"])
REGION = dict(anyOf=[RECT, CIRCLE, POLYGON])
PROFILE = obj(dict(maxSlopeDegrees=scalar("number", minimum=0, exclusiveMaximum=90, default=35), waterLevel=N,
                   maxWaterDepth=scalar("number", minimum=0, default=0), clearanceCells=scalar("integer", minimum=0, maximum=32, default=0),
                   respectPassability=scalar("boolean", default=True)))
POSITION = dict(x=N, y=N, z=N, space=scalar("string", enum=["playableGrid", "mapGrid", "world"], default="playableGrid"),
                anchor=scalar("string", enum=["center", "gridPoint"], default="center"))
SEARCH = dict(query=S, offset=scalar("integer", minimum=0, default=0), limit=scalar("integer", minimum=1, maximum=200, default=50))
SCHEMAS = {}


def add(name, description, props=None, required=()):
    SCHEMAS[name] = dict(description=description, argumentsSchema=obj(props, required))


add("map.create", "Create a map with default water/light and no random starting waypoints.",
    dict(parentPath=S, mapName=S, playableWidth=POS, playableHeight=POS, border=scalar("integer", minimum=0, default=8),
         defaultTexture=scalar("string", default="Dirt_Yucatan03"), compress=scalar("boolean", default=True)),
    ["parentPath", "mapName", "playableWidth", "playableHeight"])
for command in ["map.open", "map.export_project", "map.export_package"]:
    add(command, {"map.open": "Open an authoring session.", "map.export_project": "Export current map, companions and complete authoring history into a new directory.",
                  "map.export_package": "Export current map and companion files without internal authoring history."}[command], dict(parentPath=S, mapName=S), ["parentPath", "mapName"])
add("map.save", "Save the current map and protection metadata.", dict(compress=scalar("boolean", default=True)))
add("map.save_as", "Export only the map file; does not change this session's save target.",
    dict(parentPath=S, mapName=S, compress=scalar("boolean", default=True), overwrite=scalar("boolean", default=False)), ["parentPath", "mapName"])
for command, description in [("map.info", "Query dimensions, revision and authoring state."), ("map.close", "Close session, retaining unsaved work."),
                             ("history.undo", "Restore previous map and authoring state."), ("history.redo", "Restore next map and authoring state."),
                             ("protections.list", "List persistent protected regions."), ("system.capabilities", "Discover current capabilities and configured providers.")]:
    add(command, description)
add("system.schema", "Read command argument schemas; omit command for all.", dict(command=S))
add("map.inspect_file", "Inspect an existing file through an immutable copy without creating a session in its directory. Returns observed players, teams, starts and paged regular objects; does not certify authenticity or gameplay.",
    dict(path=S, typeNames=array(S, 0, 32), offset=scalar("integer", minimum=0, default=0), limit=scalar("integer", minimum=1, maximum=100, default=50), includeProperties=scalar("boolean", default=False)), ["path"])
add("terrain.query", "Read a terrain cell height.", dict(x=I, y=I, space=SPACE), ["x", "y"])
add("terrain.set_height", "Set rectangle height; passability is not automatically rebuilt.", dict(region=RECT, height=HEIGHT), ["region", "height"])
add("terrain.sculpt", "Set or raise rectangle/circle height with smooth edge falloff.",
    dict(region=REGION, mode=scalar("string", enum=["set", "raise"]), value=N, falloff=scalar("number", minimum=0, maximum=1, default=0)), ["region", "mode", "value"])
add("terrain.smooth", "Smooth selected heights using previous-iteration neighbors.",
    dict(region=REGION, iterations=scalar("integer", minimum=1, maximum=32, default=1), strength=scalar("number", minimum=0, maximum=1, default=1)), ["region"])
add("terrain.ramp", "Create a quantized polyline ramp. Validate external connections separately.",
    dict(polyline=array(POINT, 2, 64), startHeight=HEIGHT, endHeight=HEIGHT, widthCells=scalar("number", minimum=2, default=4),
         transitionCells=scalar("number", minimum=0, default=2), maxSlopeDegrees=scalar("number", minimum=0, exclusiveMaximum=90, default=35)), ["polyline", "startHeight", "endHeight"])
add("terrain.analyze", "Terrain-only connectivity with explicit movement assumptions; does not evaluate object collisions or game pathfinding.",
    dict(profile=PROFILE, points=array(obj(dict(x=I, y=I), ["x", "y"]))), ["profile"])
add("terrain.rebuild_passability", "Replace ordinary passability flags across the map; preserve special flags. Supports undo.",
    dict(maxSlopeDegrees=scalar("number", minimum=0, exclusiveMaximum=90, default=45), expandCardinalHalo=scalar("boolean", default=True)))
add("textures.list", "Search texture names declared by the library; mod availability is unverified.", SEARCH)
add("assets.objects", "Search editor-declared object names and category labels with source hash.", SEARCH)
add("texture.paint", "Paint texture and optionally blend a one-cell halo.", dict(region=REGION, texture=S, autoBlend=scalar("boolean", default=True)), ["region", "texture"])
add("waypoints.place", "Place a waypoint; z defaults to zero.", dict(POSITION, name=S), ["x", "y"])
add("starts.place", "Place one Player_N_Start waypoint (slot 1–6). Rejects duplicate slots, coincident starts and positions outside playable area. Does not validate spawn clearance or initialize gameplay.",
    dict(POSITION, playerSlot=scalar("integer", minimum=1, maximum=6)), ["playerSlot", "x", "y"])
add("starts.list", "List player-start waypoints, handles, invalid names and duplicate slots.")
add("waypoints.move", "Move a waypoint by stable handle; omitted z resets to zero.", dict(POSITION, objectId=S), ["objectId", "x", "y"])
for kind in ["waypoints", "objects"]:
    add(kind + ".delete", "Delete one exact object instance by stable handle.", dict(objectId=S), ["objectId"])
add("objects.query", "List regular objects and stable handles; excludes roads and waypoints.",
    dict(objectId=S, typeName=S, offset=scalar("integer", minimum=0, default=0), limit=scalar("integer", minimum=1, maximum=200, default=100)))
OBJECT_SETTINGS = obj(dict(
    {key: scalar("boolean") for key in ["objectEnabled", "objectIndestructible", "objectUnsellable", "objectPowered", "objectRecruitableAI", "objectTargetable", "objectSleeping"]},
    objectInitialHealth=I, objectBasePriority=I, objectBasePhase=I))
add("objects.place", "Place a regular object; optional ownerTeam must exist with a valid player. Typed settings preserve unspecified defaults. Does not validate footprint or gameplay.",
    dict(POSITION, typeName=S, name=S, angleRadians=N, catalogHash=S, ownerTeam=S, settings=OBJECT_SETTINGS), ["typeName", "x", "y"])
add("objects.configure", "Update name, existing ownerTeam or typed settings without moving the object. Does not certify gameplay behavior.",
    dict(objectId=S, name=S, ownerTeam=S, settings=OBJECT_SETTINGS), ["objectId"])
add("objects.move", "Move a regular object; omitted z and angle preserve current values.", dict(POSITION, objectId=S, angleRadians=N), ["objectId", "x", "y"])
FOOTPRINTS = array(obj(dict(typeName=S, boxes=array(obj(dict(label=scalar("string", minLength=1, maxLength=64, default="body"),
    widthCells=scalar("number", exclusiveMinimum=0, maximum=4096), depthCells=scalar("number", exclusiveMinimum=0, maximum=4096), blocksMovement=scalar("boolean", default=True),
    offsetXCells=scalar("number", minimum=-4096, maximum=4096, default=0), offsetYCells=scalar("number", minimum=-4096, maximum=4096, default=0)),
    ["widthCells", "depthCells"]), 1, 8)), ["typeName", "boxes"]), 1, 256)
add("objects.analyze_space", "Check caller-supplied rotated rectangles, including optional access boxes, for overlap and playable bounds. Unknown types make coverage incomplete. Not engine collision or gameplay validation.",
    dict(footprints=FOOTPRINTS, preparedPlanId=S, planHash=S, limit=scalar("integer", minimum=1, maximum=1000, default=100)), ["footprints"])
GRID_POINT = obj(dict(x=I, y=I), ["x", "y"])
add("map.validate", "Validate current or prepared map against explicit static rules: expected starts, bounds, owners, resource counts, resource access distance fairness, build areas, footprints and connectivity. Resource access uses a named nonblocking footprint box and compares ranked nearest distances by player. Returns a report, not gameplay certification.",
    dict(expectedPlayers=scalar("integer", minimum=1, maximum=6), profile=PROFILE, footprints=FOOTPRINTS,
         resources=array(obj(dict(typeName=S, minCount=scalar("integer", minimum=0, default=0), maxCount=scalar("integer", minimum=0, default=2147483647)), ["typeName"]), 0, 32),
         resourceAccess=array(obj(dict(typeName=S, accessLabel=scalar("string", minLength=1, maxLength=64, default="access"), nearestCount=scalar("integer", minimum=1, maximum=32, default=1), maxDistanceCells=scalar("integer", minimum=0, default=2147483647), maxRankDistanceSpreadCells=scalar("integer", minimum=0, default=0), requireAllResourcesReachable=scalar("boolean", default=True)), ["typeName"]), 0, 16),
         buildAreas=array(obj(dict(id=scalar("string", minLength=1, maxLength=64), region=REGION, maxHeightDifference=scalar("number", minimum=0, default=1), playerSlot=scalar("integer", minimum=1, maximum=6)), ["id", "region"]), 0, 16),
         routes=array(obj({"from": GRID_POINT, "to": GRID_POINT}, ["from", "to"]), 0, 16), preparedPlanId=S, planHash=S), ["expectedPlayers", "profile", "footprints"])
SCHEMAS["terrain.analyze"]["argumentsSchema"]["properties"].update(footprints=FOOTPRINTS, preparedPlanId=S, planHash=S,
    routes=array(obj(dict(**{"from": GRID_POINT, "to": GRID_POINT}), ["from", "to"]), 0, 16),
    maxRouteCells=scalar("integer", minimum=1, maximum=10000, default=2048))
SCHEMAS["terrain.analyze"]["description"] = "Analyze current or prepared terrain and optional explicit object footprints with square clearance; return components and optional shortest four-neighbor routes. Unknown object footprints reject. Not game pathfinding."
add("objects.scatter", "Deterministic region scatter with center spacing, exclusions and terrain filtering. Optional footprints require all existing and new types; reject footprint overlap, boundary and blocked terrain/exclusion intersections. Rejects partial placement.",
    dict(region=REGION, profile=PROFILE, seed=I, count=scalar("integer", minimum=1, maximum=2000), typeNames=array(S, 1, 64),
         minDistanceCells=scalar("number", minimum=1, default=2), exclusions=array(REGION), catalogHash=S, footprints=FOOTPRINTS), ["region", "profile", "seed", "count", "typeNames"])
add("protections.add", "Protect selected layers; removal and edits cannot bypass protection in the same batch.",
    dict(id=scalar("string", minLength=1, maxLength=128), region=REGION, layers=array(scalar("string", enum=["terrain", "textures", "passability", "objects"]), 1, 4)), ["id", "region", "layers"])
add("protections.remove", "Remove protection by id; use a separate transaction before changing that region.", dict(id=S), ["id"])
add("preview.start", "Queue real WorldBuilder overview from current state or a prepared candidate; poll jobs.status.", dict(requiredRevision=scalar("integer", minimum=0), preparedPlanId=S, planHash=S))
add("diagnostics.render", "Render analytical height, base texture, object-center, connectivity or protection layers with legend and revision. Optional passability routes draw shortest paths and endpoint markers, with route status and coordinates in metadata; not game appearance.",
    dict(layer=scalar("string", enum=["height", "textures", "objects", "passability", "constraints"], default="height"),
         profile=PROFILE, routes=array(obj({"from": GRID_POINT, "to": GRID_POINT}, ["from", "to"]), 0, 16), requiredRevision=scalar("integer", minimum=0), preparedPlanId=S, planHash=S, maxEdge=scalar("integer", minimum=1, maximum=2048, default=1024)))
add("preview.inspect", "Return a verified overview or pixel crop with coordinate mapping; MCP also returns inline PNG image content.",
    dict(jobId=S, maxEdge=scalar("integer", minimum=1, maximum=2048, default=1024),
         crop=obj(dict(x=scalar("integer", minimum=0), y=scalar("integer", minimum=0), width=POS, height=POS), ["x", "y", "width", "height"])), ["jobId"])
for command in ["jobs.status", "jobs.cancel"]:
    add(command, "Query or cancel a render job created by this host.", dict(jobId=S), ["jobId"])
mutations = [key for key in SCHEMAS if key in ["terrain.set_height", "terrain.sculpt", "terrain.smooth", "terrain.ramp", "terrain.rebuild_passability", "texture.paint",
    "waypoints.place", "waypoints.move", "waypoints.delete", "starts.place", "objects.place", "objects.move", "objects.delete", "objects.configure", "objects.scatter", "protections.add", "protections.remove"]]
subcommands = [obj(dict(command=scalar("string", enum=[name]), commandVersion=scalar("integer", enum=[1], default=1),
                       arguments=SCHEMAS[name]["argumentsSchema"]), ["command", "arguments"]) for name in mutations]
add("batch.execute", "Execute up to 100 mutation commands as one transaction and revision; all-or-nothing.",
    dict(commands=array(dict(anyOf=subcommands), 1, 100)), ["commands"])
add("edits.prepare", "Run edits on isolated state, normalize and check protections; retain candidate for 30 minutes in this session. Does not commit. Not a semantic DesignSpec compiler.",
    dict(baseRevision=scalar("integer", minimum=0), commands=array(dict(anyOf=subcommands), 1, 100)), ["baseRevision", "commands"])
add("edits.apply", "Commit the exact prepared candidate with source revision, content and catalog checks. Does not rerun generation. Cannot be nested in a batch.",
    dict(preparedPlanId=S, planHash=S), ["preparedPlanId", "planHash"])
add("edits.discard", "Release a prepared candidate from this session.", dict(preparedPlanId=S), ["preparedPlanId"])
platform = obj(dict(region=REGION, value=HEIGHT, falloff=scalar("number", minimum=0, maximum=1, default=0)), ["region", "value"])
entity_schemas = []
ramp_choices = []
for start in ("startHeight", "startHeightFrom"):
    for end in ("endHeight", "endHeightFrom"):
        props = {k: v for k, v in SCHEMAS["terrain.ramp"]["argumentsSchema"]["properties"].items() if k not in ("startHeight", "endHeight")}
        props[start] = S if start.endswith("From") else HEIGHT
        props[end] = S if end.endswith("From") else HEIGHT
        ramp_choices.append(obj(props, ["polyline", start, end]))
for kind, parameters in [("platform", platform), ("ramp", dict(anyOf=ramp_choices)),
                         ("texture", SCHEMAS["texture.paint"]["argumentsSchema"]),
                         ("scatter", SCHEMAS["objects.scatter"]["argumentsSchema"]),
                         ("derived", obj(dict(sourceEntityId=S, transform=scalar("string", enum=["rotate180"]), playerSlot=scalar("integer", minimum=1, maximum=6)), ["sourceEntityId", "transform"])),
                         ("playerStart", SCHEMAS["starts.place"]["argumentsSchema"]),
                         ("objects", obj(dict(placements=array(SCHEMAS["objects.place"]["argumentsSchema"], 1, 2000)), ["placements"]))]:
    entity_schemas.append(obj(dict(id=scalar("string", minLength=1, maxLength=128), kind=scalar("string", enum=[kind]), label=S, dependsOn=array(S, 0, 32), parameters=parameters), ["id", "kind", "parameters"]))
add("design.query", "Read committed entity specifications, owned object handles and design hash. Original/generated height cells are optional.",
    dict(entityId=S, offset=scalar("integer", minimum=0, default=0), limit=scalar("integer", minimum=1, maximum=200, default=50), includeHeightCells=scalar("boolean", default=False)))
add("design.prepare", "Prepare platform, ramp, texture, object, scatter or derived rotate180 entities. Texture owns its region and auto-blend halo; rotate180 reflects selected cells and recomputes target blends without rotating bitmap pixels. Ramp heights can reference platform targets via startHeightFrom/endHeightFrom. References imply dependencies; dependsOn enables layering. Rejects manual conflicts and cycles. No gameplay route compiler yet.",
    dict(baseRevision=scalar("integer", minimum=0), patch=obj(dict(schemaVersion=scalar("integer", enum=[1], default=1),
         upsert=array(dict(anyOf=entity_schemas), 0, 100), remove=array(S, 0, 100)))), ["baseRevision", "patch"])
add("design.apply", "Commit a prepared design candidate and entity bindings in one history step. Uses design candidates only.",
    dict(preparedPlanId=S, planHash=S), ["preparedPlanId", "planHash"])

SCHEMAS["diagnostics.render"]["argumentsSchema"]["properties"]["footprints"] = FOOTPRINTS

if __name__ == "__main__":
    destination = Path(__file__).resolve().parents[1] / "src/Dreamness.RA3.Map.Agent/Protocol/command-schemas.json"
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(SCHEMAS, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote {len(SCHEMAS)} command schemas")
