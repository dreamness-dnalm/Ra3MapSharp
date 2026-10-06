"""Exercise the actual JSONL host and optional WorldBuilder render, preserving evidence.

python scripts/agent_smoke.py --launcher <WbLauncher.exe>
Requires a built net6.0 Agent host; creates an isolated map under artifacts/agent-smoke.
"""
import argparse
import datetime
import json
import pathlib
import subprocess
import time


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--launcher")
    parser.add_argument("--landscape", action="store_true", help="Also exercise sculpt, smooth and texture tools")
    parser.add_argument("--objects", action="store_true", help="Exercise object edits, history and saved-session recovery")
    parser.add_argument("--ramp", action="store_true", help="Replace terrain with two platforms and verify a connecting ramp")
    parser.add_argument("--scatter", action="store_true", help="Scatter trees while reserving a central corridor and base areas")
    args = parser.parse_args()
    root = pathlib.Path(__file__).resolve().parent.parent
    folder = root / "artifacts" / "agent-smoke" / datetime.datetime.now().strftime("%Y%m%d-%H%M%S-%f")
    folder.mkdir(parents=True)
    dll = root / "src/Dreamness.RA3.Map.Agent/bin/Debug/net6.0/Dreamness.RA3.Map.Agent.dll"
    command = ["dotnet", str(dll), "--stdio", "--artifacts", str(folder / "renders")]
    if args.launcher:
        command += ["--launcher", args.launcher]
    evidence = []
    process = subprocess.Popen(command, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                               stderr=subprocess.PIPE, text=True, encoding="utf-8", cwd=root)

    def call(name, arguments=None, revision=None, session=False):
        request = {"requestId": "smoke-" + str(len(evidence)), "command": name,
                   "arguments": arguments or {}}
        if session:
            request["sessionId"] = "$current"
        if revision is not None:
            request["expectedRevision"] = revision
        process.stdin.write(json.dumps(request) + "\n")
        process.stdin.flush()
        line = process.stdout.readline()
        if not line:
            raise RuntimeError("Host exited: " + process.stderr.read())
        result = json.loads(line)
        evidence.append({"request": request, "result": result})
        (folder / "evidence.json").write_text(json.dumps(evidence, indent=2, ensure_ascii=False), encoding="utf-8")
        if result["status"] != "succeeded":
            raise RuntimeError(result)
        return result

    try:
        call("system.capabilities")
        created = call("map.create", {"parentPath": str(folder), "mapName": "AgentSmoke",
                                       "playableWidth": 64, "playableHeight": 64, "border": 8})
        call("batch.execute", {"commands": [
            {"command": "terrain.set_height", "arguments": {"height": 260,
             "region": {"space": "mapGrid", "x": 0, "y": 0, "width": 80, "height": 80}}},
            {"command": "terrain.set_height", "arguments": {"height": 185,
             "region": {"x": 22, "y": 8, "width": 20, "height": 48}}},
            {"command": "waypoints.place", "arguments": {"name": "Player_1_Start", "x": 10, "y": 10}},
            {"command": "waypoints.place", "arguments": {"name": "Player_2_Start", "x": 54, "y": 54}}
        ]}, revision=0, session=True)
        revision = 1
        if args.landscape:
            call("batch.execute", {"commands": [
                {"command": "terrain.set_height", "arguments": {"height": 260,
                 "region": {"space": "mapGrid", "x": 0, "y": 0, "width": 80, "height": 80}}},
                {"command": "terrain.sculpt", "arguments": {"region": {"kind": "circle", "centerX": 32, "centerY": 32, "radius": 18},
                 "mode": "set", "value": 185, "falloff": 0.4}},
                {"command": "terrain.sculpt", "arguments": {"region": {"kind": "circle", "centerX": 12, "centerY": 49, "radius": 10},
                 "mode": "raise", "value": 80, "falloff": 1}},
                {"command": "terrain.sculpt", "arguments": {"region": {"kind": "circle", "centerX": 52, "centerY": 15, "radius": 10},
                 "mode": "raise", "value": 80, "falloff": 1}},
                {"command": "terrain.smooth", "arguments": {"region": {"x": 0, "y": 0, "width": 64, "height": 64}, "iterations": 2, "strength": 0.6}},
                {"command": "texture.paint", "arguments": {"region": {"space": "mapGrid", "x": 0, "y": 0, "width": 80, "height": 80},
                 "texture": "Grass_Yucatan01", "autoBlend": False}},
                {"command": "texture.paint", "arguments": {"region": {"kind": "circle", "centerX": 32, "centerY": 32, "radius": 19},
                 "texture": "Dirt_Yucatan03", "autoBlend": True}}
            ]}, revision=revision, session=True)
            revision += 1
        if args.ramp:
            call("batch.execute", {"commands": [
                {"command": "terrain.set_height", "arguments": {"height": 260,
                 "region": {"space": "mapGrid", "x": 0, "y": 0, "width": 80, "height": 80}}},
                {"command": "terrain.set_height", "arguments": {"height": 360,
                 "region": {"x": 32, "y": 0, "width": 32, "height": 64}}}
            ]}, revision=revision, session=True)
            revision += 1
            check = {"profile": {"maxSlopeDegrees": 35, "waterLevel": 200, "clearanceCells": 1},
                     "points": [{"x": 10, "y": 32}, {"x": 54, "y": 32}]}
            if call("terrain.analyze", check, session=True)["data"]["allPointsConnected"]:
                raise AssertionError("Platforms unexpectedly connected before ramp")
            call("terrain.ramp", {"polyline": [{"x": 16, "y": 32}, {"x": 48, "y": 32}],
                 "widthCells": 10, "transitionCells": 5, "startHeight": 260, "endHeight": 360,
                 "maxSlopeDegrees": 35}, revision=revision, session=True)
            revision += 1
            if not call("terrain.analyze", check, session=True)["data"]["allPointsConnected"]:
                raise AssertionError("Ramp failed to connect platforms")
            call("terrain.rebuild_passability", {"maxSlopeDegrees": 35, "expandCardinalHalo": True}, revision=revision, session=True)
            revision += 1
            if not call("terrain.analyze", check, session=True)["data"]["allPointsConnected"]:
                raise AssertionError("Rebuilt passability blocks the ramp")
        if args.objects:
            call("batch.execute", {"commands": [
                {"command": "objects.place", "arguments": {"typeName": "CC_Tree01", "x": x, "y": y,
                 "angleRadians": (i % 5) * 0.7}}
                for i, (x, y) in enumerate((x, y) for x in (5, 11, 48, 56) for y in (12, 24, 36, 48))
            ]}, revision=revision, session=True)
            revision += 1
            items = call("objects.query", session=True)["data"]["items"]
            first_id = items[0]["objectId"]
            call("objects.move", {"objectId": first_id, "x": 8, "y": 8}, revision=revision, session=True)
            revision += 1
            call("objects.delete", {"objectId": first_id}, revision=revision, session=True)
            revision += 1
            call("history.undo", revision=revision, session=True)
            revision += 1
        if args.scatter:
            call("objects.scatter", {"region": {"x": 2, "y": 2, "width": 60, "height": 60},
                "profile": {"waterLevel": 200, "maxSlopeDegrees": 35, "clearanceCells": 1},
                "seed": 731, "count": 55, "minDistanceCells": 4.5, "typeNames": ["CC_Tree01"],
                "exclusions": [{"x": 0, "y": 23, "width": 64, "height": 18},
                    {"kind": "circle", "centerX": 10, "centerY": 10, "radius": 8},
                    {"kind": "circle", "centerX": 54, "centerY": 54, "radius": 8}]}, revision=revision, session=True)
            revision += 1
        call("map.save", revision=revision, session=True)
        analysis = call("terrain.analyze", {"profile": {"maxSlopeDegrees": 35, "waterLevel": 200,
            "maxWaterDepth": 0, "clearanceCells": 1}, "points": [{"x": 10, "y": 10}, {"x": 54, "y": 54}]}, session=True)
        if analysis["data"]["revision"] != revision:
            raise AssertionError("Analysis revision does not match current map")
        if args.objects:
            before = call("objects.query", session=True)["data"]
            call("map.close", session=True)
            call("map.open", {"parentPath": str(folder), "mapName": "AgentSmoke"})
            after = call("objects.query", session=True)["data"]
            if before != after:
                raise AssertionError("Object state or handles changed after reopening")
        if args.launcher:
            job = call("preview.start", {"requiredRevision": revision}, session=True)["data"]["jobId"]
            deadline = time.monotonic() + 360
            previous = None
            while True:
                result = call("jobs.status", {"jobId": job})["data"]
                if result["state"] != previous:
                    print("render:", result["state"], flush=True)
                    previous = result["state"]
                if result["state"] in ("failed", "cancelled"):
                    raise RuntimeError(result)
                if result["state"] == "succeeded":
                    print("image:", result["result"]["imagePath"], flush=True)
                    break
                if time.monotonic() > deadline:
                    call("jobs.cancel", {"jobId": job})
                    raise TimeoutError("Render deadline exceeded")
                time.sleep(2)
        call("map.close", session=True)
        print("evidence:", folder / "evidence.json", flush=True)
    finally:
        process.stdin.close()
        try:
            process.wait(timeout=25)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()


if __name__ == "__main__":
    main()
