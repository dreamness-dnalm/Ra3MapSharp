"""Interoperability smoke test using the official Python MCP SDK 2.2.0."""
import asyncio
import argparse
import base64
import hashlib
import datetime
import json
import math
from pathlib import Path
from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client
from jsonschema import Draft202012Validator


async def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--launcher", help="Also verify real overview and inline image content")
    parser.add_argument("--design", action="store_true", help="Prepare and render an entity layout, then verify repeat application does not duplicate objects")
    parser.add_argument("--symmetric", action="store_true", help="Also mirror generated entities by 180 degrees; implies --design")
    parser.add_argument("--resources", action="store_true", help="Use mirrored OreNode objects and verify ownership, orientation and configure/save; implies --symmetric")
    args = parser.parse_args()
    if args.resources:
        args.symmetric = True
    if args.symmetric:
        args.design = True
    root = Path(__file__).resolve().parents[1]
    folder = root / "artifacts/mcp-smoke" / datetime.datetime.now().strftime("%Y%m%d-%H%M%S-%f")
    folder.mkdir(parents=True)
    params = StdioServerParameters(command="dotnet", args=[str(root / "src/Dreamness.RA3.Map.Agent/bin/Debug/net6.0/Dreamness.RA3.Map.Agent.dll"), "--mcp", "--artifacts", str(folder / "renders")])
    if args.launcher:
        params.args += ["--launcher", args.launcher]
    evidence = []
    async with stdio_client(params) as (read, write):
        async with ClientSession(read, write) as client:
            initialized = await client.initialize()
            tools = await client.list_tools()
            names = [tool.name for tool in tools.tools]
            schemas = {tool.name: tool.input_schema for tool in tools.tools}
            for schema in schemas.values():
                Draft202012Validator.check_schema(schema)
            assert "ra3_terrain_ramp" in names and "ra3_map_export_project" in names

            async def call(name, arguments):
                Draft202012Validator(schemas[name]).validate(arguments)
                result = await client.call_tool(name, arguments)
                data = json.loads(result.content[0].text)
                evidence.append(dict(tool=name, arguments=arguments, result=data))
                (folder / "evidence.json").write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding="utf-8")
                assert not result.is_error, result
                return data

            if args.launcher:
                resources = await call("ra3_assets_objects", {"arguments": {"query": "OreNode"}})
                assert any(item["typeName"] == "OreNode" for item in resources["data"]["items"])
                assert len(resources["data"]["sources"]) == 2
            size = 96 if args.resources else 32
            await call("ra3_map_create", {"arguments": {"parentPath": str(folder), "mapName": "McpSmoke", "playableWidth": size, "playableHeight": size}})
            await call("ra3_objects_place", {"sessionId": "$current", "expectedRevision": 0,
                "arguments": {"typeName": "CC_Tree01", "x": 5, "y": 5}})
            objects = await call("ra3_objects_query", {"sessionId": "$current"})
            assert objects["data"]["total"] == 1
            await call("ra3_protections_add", {"sessionId": "$current", "expectedRevision": 1,
                "arguments": {"id": "base", "region": {"x": 2, "y": 2, "width": 8, "height": 8}, "layers": ["objects"]}})
            await call("ra3_map_export_project", {"sessionId": "$current", "expectedRevision": 2,
                "arguments": {"parentPath": str(folder), "mapName": "McpCopy"}})
            await call("ra3_map_close", {"sessionId": "$current"})
            await call("ra3_map_open", {"arguments": {"parentPath": str(folder), "mapName": "McpCopy"}})
            zones = await call("ra3_protections_list", {"sessionId": "$current"})
            assert zones["data"]["zones"][0]["id"] == "base"
            if args.design:
                patch = {"upsert": [
                    {"id": "landmark", "kind": "objects", "parameters": {"placements": [{"typeName": "CC_Tree01", "x": 12, "y": 15}]}},
                    {"id": "platform", "kind": "platform", "parameters": {"region": {"x": 18, "y": 20, "width": 10, "height": 8}, "value": 320, "falloff": 0.5}},
                    {"id": "ramp", "kind": "ramp", "dependsOn": ["platform"], "parameters": {"polyline": [{"x": 15, "y": 24}, {"x": 26, "y": 24}],
                        "startHeight": 280, "endHeightFrom": "platform", "widthCells": 4, "transitionCells": 1}},
                    {"id": "forest", "kind": "scatter", "dependsOn": ["ramp"], "parameters": {"region": {"x": 2, "y": 12, "width": 8, "height": 16},
                        "profile": {"waterLevel": 200, "maxSlopeDegrees": 35}, "seed": 731, "count": 8, "typeNames": ["CC_Tree01"], "minDistanceCells": 1.5}}]}
                if args.resources:
                    # Conservative planning assumptions, not engine-extracted footprints.
                    patch["upsert"][1]["parameters"]["region"] = {"kind": "polygon", "vertices": [
                        {"x": 18, "y": 20}, {"x": 28, "y": 20}, {"x": 27, "y": 27}, {"x": 20, "y": 28}]}
                    footprints = [{"typeName": "OreNode", "boxes": [
                        {"label": "body", "widthCells": 18, "depthCells": 18},
                        {"label": "access", "widthCells": 10, "depthCells": 8, "offsetXCells": 14, "blocksMovement": False}]},
                        {"typeName": "CC_Tree01", "boxes": [{"label": "canopy", "widthCells": 3, "depthCells": 3}]}]
                    patch["upsert"][0]["parameters"]["placements"][0].update(typeName="OreNode", x=40, y=20, anchor="gridPoint", angleRadians=math.pi / 2,
                        ownerTeam="PlyrNeutral/teamPlyrNeutral", settings={"objectInitialHealth": 100, "objectEnabled": True})
                    patch["upsert"][3]["parameters"]["footprints"] = footprints
                    patch["upsert"][3]["dependsOn"].append("landmark")
                patch["upsert"].append({"id": "ground", "kind": "texture", "dependsOn": ["platform"],
                    "parameters": {"region": {"x": 18, "y": 20, "width": 10, "height": 8}, "texture": "Dirt_Yucatan02"}})
                if args.symmetric:
                    for source in ("landmark", "platform", "ramp", "forest", "ground"):
                        dependencies = {"ramp": ["platform-mirror"], "forest": ["ramp-mirror"]}.get(source, [])
                        patch["upsert"].append({"id": source + "-mirror", "kind": "derived", "dependsOn": dependencies,
                            "parameters": {"sourceEntityId": source, "transform": "rotate180"}})
                patch["upsert"].append({"id": "start-a", "kind": "playerStart", "dependsOn": ["platform-mirror"] if args.symmetric else [],
                    "parameters": {"playerSlot": 1, "x": 12, "y": 8}})
                patch["upsert"].append({"id": "start-b", "kind": "derived" if args.symmetric else "playerStart", "dependsOn": ["platform"],
                    "parameters": {"sourceEntityId": "start-a", "transform": "rotate180", "playerSlot": 2} if args.symmetric else {"playerSlot": 2, "x": 24, "y": 24}})
                prepare_name = "ra3_design_prepare"
                prepare_args = {"baseRevision": 2, "patch": patch}
            else:
                prepare_name = "ra3_edits_prepare"
                prepare_args = {"baseRevision": 2, "commands": [{"command": "objects.place", "arguments": {"typeName": "CC_Tree01", "x": 12, "y": 15}}]}
            plan = (await call(prepare_name, {"sessionId": "$current", "arguments": prepare_args}))["data"]
            plan_args = {"preparedPlanId": plan["preparedPlanId"], "planHash": plan["planHash"]}
            if args.resources:
                space = await call("ra3_objects_analyze_space", {"sessionId": "$current", "arguments": dict(footprints=footprints, **plan_args)})
                assert space["data"]["mapContentHash"] == plan["candidateContentHash"]
                assert space["data"]["clearUnderProfile"], space
                candidate_routes = await call("ra3_terrain_analyze", {"sessionId": "$current", "arguments": dict(
                    profile={"waterLevel": 200, "clearanceCells": 1}, footprints=footprints,
                    routes=[{"from": {"x": 12, "y": 8}, "to": {"x": 84, "y": 88}}], **plan_args)})
                assert candidate_routes["data"]["routes"][0]["route"]["status"] == "found"
                assert candidate_routes["data"]["mapContentHash"] == plan["candidateContentHash"]
                validation = await call("ra3_map_validate", {"sessionId": "$current", "arguments": dict(
                    expectedPlayers=2, profile={"waterLevel": 200, "clearanceCells": 1}, footprints=footprints,
                    resources=[{"typeName": "OreNode", "minCount": 2, "maxCount": 2}],
                    resourceAccess=[{"typeName": "OreNode", "accessLabel": "access", "maxRankDistanceSpreadCells": 2}], **plan_args)})
                assert validation["data"]["status"] == "passed", validation
                assert validation["data"]["mapContentHash"] == plan["candidateContentHash"]
            unchanged = await call("ra3_objects_query", {"sessionId": "$current"})
            assert unchanged["data"]["total"] == 1
            diagnostic_hashes = set()
            for layer in ("height", "textures", "objects", "passability", "constraints"):
                arguments = {"sessionId": "$current", "arguments": {"layer": layer, "requiredRevision": 2, "maxEdge": 512}}
                arguments["arguments"].update(plan_args)
                if layer == "passability":
                    arguments["arguments"]["profile"] = {"waterLevel": 200, "maxSlopeDegrees": 35}
                    if args.resources:
                        arguments["arguments"]["footprints"] = footprints
                        arguments["arguments"]["profile"]["clearanceCells"] = 1
                        arguments["arguments"]["routes"] = [{"from": {"x": 12, "y": 8}, "to": {"x": 84, "y": 88}}]
                Draft202012Validator(schemas["ra3_diagnostics_render"]).validate(arguments)
                diagnostic = await client.call_tool("ra3_diagnostics_render", arguments)
                assert not diagnostic.is_error and len(diagnostic.content) == 2, diagnostic
                assert diagnostic.content[1].type == "image"
                pixels = base64.b64decode(diagnostic.content[1].data)
                metadata = json.loads(diagnostic.content[0].text)
                data = metadata["data"]
                assert data["revision"] == 2 and data["layer"] == layer
                assert data["preparedPlanId"] == plan["preparedPlanId"] and data["planHash"] == plan["planHash"]
                assert data["mapContentHash"] == plan["candidateContentHash"]
                if layer == "passability" and args.resources:
                    assert data["footprintProfileHash"] == candidate_routes["data"]["footprintProfileHash"]
                    assert next(item["cells"] for item in data["legend"] if item["label"] == "blocked") == candidate_routes["data"]["blockedCells"]
                    assert data["routes"][0]["route"] == candidate_routes["data"]["routes"][0]["route"]
                assert "sha256:" + hashlib.sha256(pixels).hexdigest() == data["imageHash"]
                assert Path(data["imagePath"]).is_relative_to(folder)
                diagnostic_hashes.add((data["mapContentHash"], data["protectionHash"]))
                (folder / (layer + ".png")).write_bytes(pixels)
                evidence.append(dict(tool="ra3_diagnostics_render", arguments=arguments, result=metadata, receivedImage=layer + ".png"))
            assert len(diagnostic_hashes) == 1
            (folder / "evidence.json").write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding="utf-8")
            if args.launcher:
                async with asyncio.timeout(360):
                    job = await call("ra3_preview_start", {"sessionId": "$current", "arguments": dict(requiredRevision=2, **plan_args)})
                    job_id = job["data"]["jobId"]
                    previous = None
                    while True:
                        status = (await call("ra3_jobs_status", {"arguments": {"jobId": job_id}}))["data"]
                        if status["state"] != previous:
                            print("render:", status["state"], flush=True)
                            previous = status["state"]
                        assert status["state"] not in ("failed", "cancelled"), status
                        if status["state"] == "succeeded":
                            break
                        await asyncio.sleep(2)
                    inspected = await client.call_tool("ra3_preview_inspect", {"arguments": {"jobId": job_id, "maxEdge": 768}})
                    assert not inspected.is_error and len(inspected.content) == 2
                    assert inspected.content[1].type == "image"
                    pixels = base64.b64decode(inspected.content[1].data)
                    metadata = json.loads(inspected.content[0].text)
                    assert metadata["data"]["preparedPlanId"] == plan["preparedPlanId"]
                    assert metadata["data"]["mapContentHash"] == plan["candidateContentHash"]
                    assert "sha256:" + hashlib.sha256(pixels).hexdigest() == metadata["data"]["imageHash"]
                    (folder / "received.png").write_bytes(pixels)
                    evidence.append(dict(tool="ra3_preview_inspect", result=metadata, receivedImage="received.png"))
                    (folder / "evidence.json").write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding="utf-8")
            apply_name = "ra3_design_apply" if args.design else "ra3_edits_apply"
            await call(apply_name, {"sessionId": "$current", "expectedRevision": 2, "arguments": plan_args})
            applied = await call("ra3_objects_query", {"sessionId": "$current"})
            expected_objects = 19 if args.symmetric else (10 if args.design else 2)
            assert applied["data"]["total"] == expected_objects
            committed = await call("ra3_diagnostics_render", {"sessionId": "$current", "arguments": {"layer": "objects", "requiredRevision": 3, "maxEdge": 512}})
            assert committed["data"]["mapContentHash"] == plan["candidateContentHash"]
            assert committed["data"].get("preparedPlanId") is None
            assert committed["data"]["designHash"] == plan["candidateDesignHash"]
            if args.design:
                design = await call("ra3_design_query", {"sessionId": "$current"})
                assert len(design["data"]["entities"]) == (12 if args.symmetric else 7)
                assert next(e for e in design["data"]["entities"] if e["spec"]["id"] == "ground")["ownedTextureCells"] == 120
                if args.symmetric:
                    assert next(e for e in design["data"]["entities"] if e["spec"]["id"] == "ground-mirror")["ownedTextureCells"] == 120
                starts = await call("ra3_starts_list", {"sessionId": "$current"})
                assert starts["data"]["total"] == 2 and starts["data"]["invalidNames"] == 0
                assert sorted(item["playerSlot"] for item in starts["data"]["items"]) == [1, 2]
                again = (await call("ra3_design_prepare", {"sessionId": "$current", "arguments": {"baseRevision": 3, "patch": patch}}))["data"]
                assert again["candidateContentHash"] == plan["candidateContentHash"]
                await call("ra3_design_apply", {"sessionId": "$current", "expectedRevision": 3,
                    "arguments": {"preparedPlanId": again["preparedPlanId"], "planHash": again["planHash"]}})
                repeated = await call("ra3_objects_query", {"sessionId": "$current"})
                assert repeated["data"] == applied["data"]
                assert (await call("ra3_starts_list", {"sessionId": "$current"}))["data"] == starts["data"]
                platform = next(entity for entity in patch["upsert"] if entity["id"] == "platform")
                platform["parameters"]["value"] = 350
                changed = (await call("ra3_design_prepare", {"sessionId": "$current", "arguments": {"baseRevision": 4, "patch": {"upsert": [platform]}}}))["data"]
                expected_rebuild = {"ramp", "forest", "start-b", "ground"}
                if args.symmetric:
                    expected_rebuild.update(("platform-mirror", "ramp-mirror", "forest-mirror", "ground-mirror"))
                    expected_rebuild.add("start-a")
                assert set(changed["results"][0]["automaticallyRebuilt"]) == expected_rebuild
                await call("ra3_design_apply", {"sessionId": "$current", "expectedRevision": 4,
                    "arguments": {"preparedPlanId": changed["preparedPlanId"], "planHash": changed["planHash"]}})
                assert (await call("ra3_objects_query", {"sessionId": "$current"}))["data"]["total"] == expected_objects
            if args.resources:
                connectivity = await call("ra3_terrain_analyze", {"sessionId": "$current", "arguments": {
                    "profile": {"waterLevel": 200, "clearanceCells": 1}, "footprints": footprints,
                    "routes": [{"from": {"x": 12, "y": 8}, "to": {"x": 84, "y": 88}}]}})
                assert connectivity["data"]["routes"][0]["route"]["status"] == "found"
                ores = (await call("ra3_objects_query", {"sessionId": "$current", "arguments": {"typeName": "OreNode"}}))["data"]["items"]
                assert len(ores) == 2
                assert all(ore["ownerTeam"] == "PlyrNeutral/teamPlyrNeutral" for ore in ores)
                assert all(ore["settings"]["objectInitialHealth"] == 100 for ore in ores)
                assert abs(abs(ores[0]["angleRadians"] - ores[1]["angleRadians"]) - math.pi) < 0.00001
                configured = await call("ra3_objects_configure", {"sessionId": "$current", "expectedRevision": 5,
                    "arguments": {"objectId": ores[0]["objectId"], "name": "ResourceCheck", "settings": {"objectTargetable": False}}})
                assert configured["data"]["x"] == ores[0]["x"]
                await call("ra3_map_save", {"sessionId": "$current", "expectedRevision": 6})
                inspected = await call("ra3_map_inspect_file", {"arguments": {"path": str(folder / "McpCopy/McpCopy.map"),
                    "typeNames": ["OreNode"], "includeProperties": True}})
                assert inspected["data"]["total"] == 2
                assert any(ore["name"] == "ResourceCheck" for ore in inspected["data"]["objects"])
            await call("ra3_map_close", {"sessionId": "$current"})
            print("protocol:", initialized.protocol_version, "tools:", len(names))
    print("evidence:", folder / "evidence.json")


if __name__ == "__main__":
    asyncio.run(main())
