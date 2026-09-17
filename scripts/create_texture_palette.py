"""Render an inspectable material contact sheet using the real WorldBuilder.

Requires a built Agent host and --launcher. Uses only Python's standard library.
Keeps the source map, requests, original PNG and named HTML swatches together.
"""
import argparse
import datetime
import html
import json
import math
from pathlib import Path
import queue
import subprocess
import threading
import time


DEFAULT_TEXTURES = [
    "Grass_Yucatan03", "Grass_Yucatan04", "Grass_Yucatan05", "Grass_Yucatan06",
    "Grass_Yucatan07", "Grass_Yucatan08", "Dirt_Yucatan01", "Dirt_Yucatan02",
    "Dirt_Yucatan03", "Dirt_Yucatan04", "Dirt_Yucatan05", "Dirt_Yucatan06",
    "Rock_Yucatan01", "Rock_Yucatan02", "Rock_Yucatan03", "Gravel_Yucatan01",
]
DEFAULT_OBJECTS = [
    "CC_Tree01", "CC_Tree02", "CC_Tree03", "CC_TREE04",
    "CS_Tree01", "GC_Tree01", "CS_Palm01", "EI_Palm01",
    "CC_Bush01", "GC_Bush01", "CS_FERN01", "CC_Grass01",
    "HA_ROCKS01", "MY_ROCKS01", "SV_Rocks01", "YU_LARGEROCKS01",
]


def main(mode="textures"):
    objects = mode == "objects"
    title = "RA3 对象对照" if objects else "RA3 材质对照"
    map_name = "ObjectPalette" if objects else "TexturePalette"
    parser = argparse.ArgumentParser(description="Render real WorldBuilder object swatches." if objects else __doc__)
    parser.add_argument("--launcher", required=True)
    parser.add_argument("--objects" if objects else "--textures", dest="names", nargs="+", default=DEFAULT_OBJECTS if objects else DEFAULT_TEXTURES)
    if objects:
        parser.add_argument("--ground-texture", default="Grass_Yucatan05")
        parser.add_argument("--angle-radians", type=float, default=0)
        parser.add_argument("--view-cells", type=int, default=16)
    parser.add_argument("--columns", type=int, default=4)
    parser.add_argument("--tile-cells", type=int, default=32)
    args = parser.parse_args()
    if not 1 <= len(args.names) <= 64 or len(set(args.names)) != len(args.names):
        parser.error("Use 1–64 unique asset names.")
    if objects and not math.isfinite(args.angle_radians):
        parser.error("angle-radians must be finite.")
    if objects and not 4 <= args.view_cells <= args.tile_cells - 2:
        parser.error("view-cells must be 4 through tile-cells minus 2.")
    if not 1 <= args.columns <= 8 or not 16 <= args.tile_cells <= 64:
        parser.error("columns must be 1–8; tile-cells must be 16–64.")
    root = Path(__file__).resolve().parents[1]
    output = root / ("artifacts/object-palette" if objects else "artifacts/texture-palette") / datetime.datetime.now().strftime("%Y%m%d-%H%M%S-%f")
    output.mkdir(parents=True)
    evidence = []
    report = {"status": "in-progress", "purpose": "object-selection" if objects else "material-selection", "validationLevel": "not-rendered"}
    revision = 0
    command = ["dotnet", str(root / "src/Dreamness.RA3.Map.Agent/bin/Debug/net6.0/Dreamness.RA3.Map.Agent.dll"),
               "--stdio", "--launcher", args.launcher, "--artifacts", str(output / "observations")]
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
            request = {"requestId": "palette-" + str(len(evidence)), "command": name, "arguments": arguments or {}}
            if session:
                request["sessionId"] = "$current"
            if mutation:
                request["expectedRevision"] = revision
            process.stdin.write(json.dumps(request) + "\n")
            process.stdin.flush()
            line = responses.get(timeout=120)
            if line is None:
                raise RuntimeError("Agent exited; inspect host-stderr.log")
            result = json.loads(line)
            evidence.append({"request": request, "result": result})
            (output / "evidence.json").write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding="utf-8")
            if result["status"] != "succeeded":
                raise RuntimeError(result)
            if result.get("revisionAfter") is not None:
                revision = result["revisionAfter"]
            return result["data"]

        try:
            columns = min(args.columns, len(args.names))
            rows = math.ceil(len(args.names) / columns)
            width, height = columns * args.tile_cells, rows * args.tile_cells
            create = {"parentPath": str(output / "source"), "mapName": map_name,
                      "playableWidth": width, "playableHeight": height, "border": 8}
            if objects:
                create["defaultTexture"] = args.ground_texture
            call("map.create", create, session=False)
            for texture in [args.ground_texture] if objects else args.names:
                known = call("textures.list", {"query": texture, "limit": 200})
                if texture not in known["textures"]:
                    raise ValueError("Unknown library texture: " + texture)
            catalog = {}
            if objects:
                for name in args.names:
                    known = call("assets.objects", {"query": name, "limit": 200}, session=False)
                    matches = [item for item in known["items"] if item["typeName"] == name]
                    if len(matches) != 1:
                        raise ValueError("Missing exact editor object type: " + name)
                    catalog[name] = matches[0]
                    report.update(catalogHash=known["catalogHash"], catalogSources=known["sources"])
                report.update(groundTexture=args.ground_texture, angleRadians=args.angle_radians,
                              notEvaluated=["engineCollision", "modelExtentOutsideSwatch", "allViewAngles"])
            commands = [{"command": "terrain.set_height", "arguments": {
                "region": {"space": "mapGrid", "x": 0, "y": 0, "width": width + 16, "height": height + 16}, "height": 260}}]
            swatches = []
            for i, name in enumerate(args.names):
                column, row = i % columns, i // columns
                region = {"kind": "rectangle", "space": "playableGrid", "x": column * args.tile_cells + 1,
                          "y": height - (row + 1) * args.tile_cells + 1, "width": args.tile_cells - 2, "height": args.tile_cells - 2}
                swatch = {"index": i, "name": name, "rowFromNorth": row, "column": column, "region": region}
                if objects:
                    placement = {"typeName": name, "x": (column + .5) * args.tile_cells,
                                 "y": height - (row + .5) * args.tile_cells, "z": 260, "space": "playableGrid",
                                 "anchor": "gridPoint", "angleRadians": args.angle_radians, "catalogHash": report["catalogHash"]}
                    commands.append({"command": "objects.place", "arguments": placement})
                    swatch.update(typeName=name, placement=placement, catalogEntry=catalog[name])
                    swatch["viewRegion"] = {"x": placement["x"] - args.view_cells / 2, "y": placement["y"] - args.view_cells / 2,
                                            "width": args.view_cells, "height": args.view_cells}
                else:
                    commands.append({"command": "texture.paint", "arguments": {"region": region, "texture": name, "autoBlend": False}})
                    swatch["texture"] = name
                swatches.append(swatch)
            call("batch.execute", {"commands": commands}, mutation=True)
            call("map.save", mutation=True)
            job = call("preview.start", {"requiredRevision": revision})
            deadline = time.monotonic() + 360
            while True:
                status = call("jobs.status", {"jobId": job["jobId"]}, session=False)
                if status["state"] == "succeeded":
                    break
                if status["state"] in ("failed", "cancelled") or time.monotonic() > deadline:
                    raise RuntimeError(status)
                time.sleep(2)
            preview = call("preview.inspect", {"jobId": job["jobId"], "maxEdge": 2048}, session=False)
            transform = preview["pixelToPlayableGrid"]
            if transform[1:4] != [0, 0, 0] or transform[0] <= 0 or transform[4] >= 0 or transform[5] != height:
                raise ValueError("Unexpected north-up preview mapping")
            for swatch in swatches:
                region = swatch.get("viewRegion", swatch["region"])
                swatch["pixelBounds"] = {"x": region["x"] / transform[0],
                    "y": (height - region["y"] - region["height"]) / -transform[4],
                    "width": region["width"] / transform[0], "height": region["height"] / -transform[4]}
                if objects:
                    bounds = swatch["pixelBounds"]
                    x, y = math.floor(bounds["x"]), math.floor(bounds["y"])
                    crop = {"x": x, "y": y, "width": math.ceil(bounds["x"] + bounds["width"]) - x,
                            "height": math.ceil(bounds["y"] + bounds["height"]) - y}
                    swatch["thumbnail"] = call("preview.inspect", {"jobId": job["jobId"], "crop": crop, "maxEdge": 512}, session=False)
            source = Path(preview["imagePath"]).relative_to(output).as_posix()
            cards = []
            for swatch in swatches:
                b = swatch["pixelBounds"]
                cards.append(f'<figure><svg viewBox="{b["x"]} {b["y"]} {b["width"]} {b["height"]}" role="img" aria-label="{html.escape(swatch["name"])}">'
                             f'<image href="{html.escape(source)}" width="{preview["width"]}" height="{preview["height"]}"/></svg>'
                             f'<figcaption>{swatch["index"] + 1:02d} · {html.escape(swatch["name"])}</figcaption></figure>')
            description = ("每格放置一个对象，使用相同朝向与底色。样片范围不代表碰撞占地，大型模型可能超出裁切框。" if objects
                           else "每格仅一种材质，不启用边缘混合。")
            page = ('<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width">'
                    f'<title>{title}</title><style>body{{margin:32px;background:#171b20;color:#edf0f4;font:15px system-ui}} '
                    'main{display:grid;grid-template-columns:repeat(auto-fit,minmax(230px,1fr));gap:18px}figure{margin:0;background:#252b33;border-radius:8px;overflow:hidden}'
                    'svg{display:block;width:100%;aspect-ratio:1}figcaption{padding:12px}p{color:#b8c1cc;line-height:1.6}a{color:#8dd8ff}</style>'
                    f'<h1>{title}</h1><p>同一张平地地图、默认灯光与水域、真实 WorldBuilder 渲染。{description}'
                    '名称与位置见 <a href="report.json">来源记录</a>；此页用于选材，不代表完整地图效果。</p><main>' + ''.join(cards) + '</main></html>')
            (output / "index.html").write_text(page, encoding="utf-8")
            report.update(status="rendered", validationLevel="editor-rendered-under-recorded-config", preview=preview,
                          swatches=swatches, columns=columns, rows=rows, tileCells=args.tile_cells, height=260,
                          sourceMap=str(output / "source" / map_name / (map_name + ".map")), indexPath=str(output / "index.html"))
            call("map.close")
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
