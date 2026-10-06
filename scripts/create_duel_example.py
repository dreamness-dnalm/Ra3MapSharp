"""Build a reproducible 256-cell two-player draft and retain every verification result.

Uses only Python's standard library and the built Agent JSONL host. Nothing is installed
into the game's Maps directory. --launcher enables catalog checks and real rendering.
"""
import argparse
import datetime
import json
import math
from pathlib import Path
import queue
import subprocess
import threading
import time


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--launcher")
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    output = root / "artifacts/duel-example" / datetime.datetime.now().strftime("%Y%m%d-%H%M%S-%f")
    output.mkdir(parents=True)
    command = ["dotnet", str(root / "src/Dreamness.RA3.Map.Agent/bin/Debug/net6.0/Dreamness.RA3.Map.Agent.dll"),
               "--stdio", "--artifacts", str(output / "observations")]
    if args.launcher:
        command += ["--launcher", args.launcher]
    evidence = []
    report = {"status": "in-progress", "gameplay": "not-tested", "artReview": "pending",
              "notValidated": ["baseConstruction", "harvesting", "victoryRules", "enginePathfinding", "footprintAccuracy"]}
    revision = 0
    with (output / "host-stderr.log").open("w", encoding="utf-8") as errors:
        process = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=errors,
                                   text=True, encoding="utf-8", cwd=root)
        responses = queue.Queue()

        def read_output():
            for line in process.stdout:
                responses.put(line)
            responses.put(None)

        threading.Thread(target=read_output, daemon=True).start()

        def call(name, arguments=None, mutation=False, session=True):
            nonlocal revision
            request = {"requestId": "duel-" + str(len(evidence)), "command": name, "arguments": arguments or {}}
            if session:
                request["sessionId"] = "$current"
            if mutation:
                request["expectedRevision"] = revision
            process.stdin.write(json.dumps(request) + "\n")
            process.stdin.flush()
            line = responses.get(timeout=120)
            if line is None:
                raise RuntimeError("Agent exited; see host-stderr.log")
            result = json.loads(line)
            evidence.append({"request": request, "result": result})
            (output / "evidence.json").write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding="utf-8")
            if result["status"] != "succeeded":
                raise RuntimeError(result)
            if result.get("revisionAfter") is not None:
                revision = result["revisionAfter"]
            return result["data"]

        try:
            call("system.capabilities", session=False)
            if args.launcher:
                for name in ("OreNode", "OilDerrick", "CC_Tree01", "CC_Tree02", "CS_Tree01"):
                    found = call("assets.objects", {"query": name}, session=False)
                    assert any(item["typeName"] == name for item in found["items"]), name
            call("map.create", {"parentPath": str(output), "mapName": "VerdantDivideDraft", "playableWidth": 256,
                                "playableHeight": 256, "border": 8, "defaultTexture": "Grass_Yucatan05"}, session=False)
            for texture in ("Grass_Yucatan05", "Dirt_Yucatan01", "Gravel_Yucatan01", "Rock_Yucatan01"):
                assert texture in call("textures.list", {"query": texture})["textures"]
            call("terrain.set_height", {"region": {"space": "mapGrid", "x": 0, "y": 0, "width": 272, "height": 272}, "height": 260}, True)
            footprints = [
                {"typeName": "OreNode", "boxes": [{"label": "body", "widthCells": 18, "depthCells": 18},
                    {"label": "access", "widthCells": 10, "depthCells": 8, "offsetXCells": 14, "blocksMovement": False}]},
                {"typeName": "OilDerrick", "boxes": [{"widthCells": 10, "depthCells": 10}]},
                *[{"typeName": tree, "boxes": [{"label": "canopy", "widthCells": 4, "depthCells": 4}]}
                  for tree in ("CC_Tree01", "CC_Tree02", "CS_Tree01")]]
            entities = []

            def entity(name, kind, parameters, dependencies=()):
                entities.append({"id": name, "kind": kind, "parameters": parameters, "dependsOn": list(dependencies)})

            def mirror(name, dependencies=()):
                entity(name + "-b", "derived", {"sourceEntityId": name, "transform": "rotate180"}, dependencies)

            entity("base-a", "platform", {"region": {"x": 16, "y": 88, "width": 64, "height": 80}, "value": 300, "falloff": .25})
            mirror("base-a")
            entity("ramp-a", "ramp", {"polyline": [{"x": 64, "y": 128}, {"x": 104, "y": 128}],
                "startHeightFrom": "base-a", "endHeight": 260, "widthCells": 18, "transitionCells": 4})
            mirror("ramp-a", ["base-a-b"])
            ridge = [{"x": x, "y": y} for x, y in [(116, 70), (128, 66), (143, 78), (149, 88), (139, 103), (125, 107), (119, 94), (112, 83)]]
            entity("ridge-a", "platform", {"region": {"kind": "polygon", "vertices": ridge},
                "value": 370, "falloff": .65})
            mirror("ridge-a")
            entity("start-a", "playerStart", {"playerSlot": 1, "x": 48, "y": 128, "anchor": "gridPoint"}, ["base-a"])
            entity("start-b", "derived", {"sourceEntityId": "start-a", "transform": "rotate180", "playerSlot": 2}, ["base-a-b"])
            resources = [(44, 102, math.pi / 2), (44, 154, -math.pi / 2), (88, 52, 0), (88, 204, 0)]
            placements = [{"typeName": "OreNode", "x": x, "y": y, "anchor": "gridPoint", "angleRadians": angle,
                           "ownerTeam": "PlyrNeutral/teamPlyrNeutral"} for x, y, angle in resources]
            placements.append({"typeName": "OilDerrick", "x": 128, "y": 48, "anchor": "gridPoint",
                               "ownerTeam": "PlyrNeutral/teamPlyrNeutral"})
            entity("resources-a", "objects", {"placements": placements}, ["ramp-a"])
            mirror("resources-a", ["ramp-a-b"])
            groves = [(28, 28), (68, 24), (44, 64), (28, 220), (68, 232), (44, 184)]
            grove_counts = [10, 20, 15, 12, 18, 15]
            grove_radii = [16, 20, 18, 17, 20, 18]
            for index, (x, y) in enumerate(groves):
                name = "grove-" + str(index)
                entity(name, "scatter", {"region": {"kind": "circle", "centerX": x, "centerY": y, "radius": grove_radii[index]},
                    "profile": {"waterLevel": 200}, "footprints": footprints, "count": grove_counts[index],
                    "seed": 731 + index * 31, "minDistanceCells": 5, "typeNames": ["CC_Tree01"]},
                    ["resources-a", "resources-a-b", "ridge-a", "ridge-a-b"])
                mirror(name)
            recipe = {"schemaVersion": 1, "upsert": entities}
            (output / "design-patch.json").write_text(json.dumps(recipe, indent=2), encoding="utf-8")
            (output / "footprints.json").write_text(json.dumps(footprints, indent=2), encoding="utf-8")
            plan = call("design.prepare", {"baseRevision": revision, "patch": recipe})
            selected = {"preparedPlanId": plan["preparedPlanId"], "planHash": plan["planHash"]}
            space = call("objects.analyze_space", dict(footprints=footprints, **selected))
            assert space["clearUnderProfile"], space
            routes = [{"from": {"x": 48, "y": 128}, "to": {"x": 208, "y": 128}},
                      {"from": {"x": 48, "y": 128}, "to": {"x": 112, "y": 52}},
                      {"from": {"x": 208, "y": 128}, "to": {"x": 144, "y": 204}}]
            movement = {"waterLevel": 200, "clearanceCells": 2}
            validation = {"expectedPlayers": 2, "profile": movement, "footprints": footprints, "routes": routes,
                          "resourceAccess": [{"typeName": "OreNode", "accessLabel": "access", "nearestCount": 2,
                                              "maxDistanceCells": 30, "maxRankDistanceSpreadCells": 2}],
                          "buildAreas": [{"id": "base-" + str(slot), "playerSlot": slot, "maxHeightDifference": .1,
                                          "region": {"kind": "rectangle", "space": "playableGrid", "x": x, "y": 122, "width": 12, "height": 12}}
                                         for slot, x in [(1, 42), (2, 202)]],
                          "resources": [{"typeName": "OreNode", "minCount": 8, "maxCount": 8}, {"typeName": "OilDerrick", "minCount": 2, "maxCount": 2}]}
            report["candidateValidation"] = call("map.validate", dict(validation, **selected))
            assert report["candidateValidation"]["status"] == "passed", report["candidateValidation"]
            paths = call("terrain.analyze", dict(profile=movement, footprints=footprints, routes=routes, **selected))
            assert all(route["route"]["status"] == "found" for route in paths["routes"]), paths
            assert paths["mapContentHash"] == space["mapContentHash"] == plan["candidateContentHash"]
            call("design.apply", selected, True)
            paints = []

            def paint_pair(x, y, radius, texture):
                for cx, cy in ((x, y), (256 - x, 256 - y)):
                    paints.append({"command": "texture.paint", "arguments": {"region": {
                        "kind": "circle", "centerX": cx, "centerY": cy, "radius": radius}, "texture": texture}})

            for x, y, radius in ((42, 127, 22), (57, 130, 18), (45, 143, 15), (40, 112, 13)):
                paint_pair(x, y, radius, "Dirt_Yucatan01")
            for index, x in enumerate(range(64, 129, 8)):
                paint_pair(x, 128 + 4 * math.sin(index * .7), 6 + (index % 3), "Dirt_Yucatan01")
            ridge_source_index = len(paints)
            for vertices in (ridge, [{"x": 256 - p["x"], "y": 256 - p["y"]} for p in ridge]):
                paints.append({"command": "texture.paint", "arguments": {"region": {"kind": "polygon", "vertices": vertices}, "texture": "Rock_Yucatan01"}})
            for x, y, _ in resources:
                paint_pair(x, y, 10, "Gravel_Yucatan01")
            texture_patch = {"upsert": [{"id": "surface-" + str(i), "kind": "derived" if i == ridge_source_index + 1 else "texture",
                                          "dependsOn": ["surface-" + str(i - 1)] if i else [],
                                          "parameters": {"sourceEntityId": "surface-" + str(ridge_source_index), "transform": "rotate180"}
                                          if i == ridge_source_index + 1 else paint["arguments"]} for i, paint in enumerate(paints)]}
            (output / "texture-patch.json").write_text(json.dumps(texture_patch, indent=2), encoding="utf-8")
            texture_plan = call("design.prepare", {"baseRevision": revision, "patch": texture_patch})
            call("design.apply", {"preparedPlanId": texture_plan["preparedPlanId"], "planHash": texture_plan["planHash"]}, True)
            call("terrain.rebuild_passability", {"maxSlopeDegrees": 35}, True)
            final_paths = call("terrain.analyze", {"profile": movement, "footprints": footprints, "routes": routes})
            report["finalValidation"] = call("map.validate", validation)
            assert report["finalValidation"]["status"] == "passed", report["finalValidation"]
            assert all(route["route"]["status"] == "found" for route in final_paths["routes"]), final_paths
            starts = call("starts.list")
            assert starts["total"] == 2 and starts["invalidNames"] == 0
            assert call("objects.query")["total"] == 190
            for layer in ("height", "textures", "objects", "passability"):
                options = {"layer": layer, "requiredRevision": revision, "maxEdge": 1024}
                if layer == "passability":
                    options.update(profile=movement, footprints=footprints, routes=routes)
                report[layer] = call("diagnostics.render", options)
            if args.launcher:
                job = call("preview.start", {"requiredRevision": revision})
                deadline = time.monotonic() + 360
                while True:
                    status = call("jobs.status", {"jobId": job["jobId"]}, session=False)
                    if status["state"] == "succeeded":
                        break
                    if status["state"] in ("failed", "cancelled") or time.monotonic() > deadline:
                        raise RuntimeError(status)
                    time.sleep(2)
                report["preview"] = call("preview.inspect", {"jobId": job["jobId"], "maxEdge": 1024}, session=False)
            call("map.save", mutation=True)
            report["package"] = call("map.export_package", {"parentPath": str(output / "delivery"), "mapName": "VerdantDivideDraft"}, True)
            call("map.close")
            inspected = call("map.inspect_file", {"path": str(output / "delivery/VerdantDivideDraft/VerdantDivideDraft.map")}, session=False)
            assert inspected["total"] == 190 and len(inspected["starts"]) == 2
            assert not (output / "delivery/VerdantDivideDraft/.automation").exists()
            report.update(status="draft-static-checks-passed", staticRoutes=final_paths,
                          editorRender="passed" if args.launcher else "not-run")
        except Exception as error:
            report.update(status="failed", error=str(error))
            raise
        finally:
            (output / "report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
            process.stdin.close()
            try:
                process.wait(timeout=15)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=15)
            print("Report:", output / "report.json", flush=True)


if __name__ == "__main__":
    main()
