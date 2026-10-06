"""Read cliff declarations and exercise the real JSONL host and WorldBuilder renderer.

All generated maps and evidence are isolated beneath --output. No source map is edited.
"""
import argparse
import hashlib
import json
import pathlib
import subprocess
import time
import xml.etree.ElementTree as ET
from ra3_cliff_data import AssetStream, model_bounds


def declarations(library, sdk):
    source = json.loads(library.read_text(encoding="utf-8-sig"))
    folder = sdk / "SageXml/Civilian/Island_Fortress_IF/Optimized_Props"
    result = []
    ns = {"a": "uri:ea.com:eala:asset"}
    for key, value in source["objects"].items():
        if "浮岛要塞_悬崖" not in value.get("favoriteFolders", []):
            continue
        xml = folder / (key + ".xml")
        data = ET.parse(xml).getroot()
        obj = data.find("a:GameObject", ns)
        if obj is None or obj.attrib["id"].lower() != key.lower():
            raise ValueError("Template mismatch: " + key)
        result.append(dict(libraryKey=key, displayName=value["name"],
                           typeName=obj.attrib["id"], editorName=obj.attrib.get("EditorName"),
                           inherits=obj.attrib.get("inheritFrom"),
                           geometry=[x.attrib for x in obj.findall("a:Geometry/a:Shape", ns)],
                           source=str(xml), sourceHash=hashlib.sha256(xml.read_bytes()).hexdigest()))
    return result


class Host:
    def __init__(self, root, output, launcher):
        dll = root / "src/Dreamness.RA3.Map.Agent/bin/Debug/net6.0/Dreamness.RA3.Map.Agent.dll"
        self.output = output
        self.evidence = []
        # Declaration IDs are read directly from SDK; do not validate against the editor's label list.
        command = ["dotnet", str(dll), "--stdio", "--artifacts", str(output / "renders"), "--launcher", str(launcher)]
        catalog = output / "templates.json"
        if catalog.exists():
            command += ["--object-catalog", str(catalog)]
        self.process = subprocess.Popen(command,
                                        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=open(output / "host.stderr.log", "w"),
                                        text=True, encoding="utf-8", cwd=root)
        self.session = None
        self.revision = 0
        self.launcher = launcher

    def call(self, name, arguments=None, write=False, session=True):
        req = dict(requestId="cliffs-" + str(len(self.evidence)), command=name, arguments=arguments or {})
        if session:
            req["sessionId"] = self.session
        if write:
            req["expectedRevision"] = self.revision
        self.process.stdin.write(json.dumps(req, ensure_ascii=True) + "\n")
        self.process.stdin.flush()
        line = self.process.stdout.readline()
        if not line:
            raise RuntimeError("Host exited; see host.stderr.log")
        reply = json.loads(line)
        self.evidence.append(dict(request=req, result=reply))
        (self.output / "evidence.json").write_text(json.dumps(self.evidence, ensure_ascii=False, indent=2), encoding="utf-8")
        if reply["status"] != "succeeded":
            raise RuntimeError(reply)
        if reply.get("revisionAfter") is not None:
            self.revision = reply["revisionAfter"]
        return reply.get("data")

    def create(self, name, width, height):
        d = self.call("map.create", dict(parentPath=str(self.output), mapName=name,
                                        playableWidth=width, playableHeight=height, border=8), session=False)
        self.session = d["sessionId"]
        self.revision = 0

    def render(self, path):
        # WbLauncher --export-overview itself hides the editor and does not save its input map.
        completed = subprocess.run([str(self.launcher), "--export-overview", str(path)],
                                   stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=325,
                                   creationflags=subprocess.CREATE_NO_WINDOW)
        (self.output / (path.stem + ".render.json")).write_text(json.dumps(dict(exitCode=completed.returncode,
            stdout=completed.stdout.decode("utf-8", errors="replace"), stderr=completed.stderr.decode("utf-8", errors="replace")), indent=2), encoding="utf-8")
        if completed.returncode != 0:
            raise RuntimeError("Renderer exit code " + str(completed.returncode))
        image = path.with_suffix(".overview.png")
        print("Rendered " + str(image), flush=True)
        return image

    def close(self):
        self.process.stdin.close()
        self.process.wait(timeout=15)

    def preview(self, prepared=None):
        args = {} if prepared is None else dict(preparedPlanId=prepared["preparedPlanId"], planHash=prepared["planHash"])
        job = self.call("preview.start", args)["jobId"]
        deadline = time.monotonic() + 340
        while True:
            state = self.call("jobs.status", dict(jobId=job), session=False)
            if state["state"] == "succeeded":
                print("Rendered " + state["result"]["imagePath"], flush=True)
                self.call("preview.inspect", dict(jobId=job, maxEdge=2048), session=False)
                return state["result"]
            if state["state"] in ("failed", "cancelled"):
                raise RuntimeError(state)
            if time.monotonic() > deadline:
                self.call("jobs.cancel", dict(jobId=job), session=False)
                raise TimeoutError("Render deadline exceeded")
            time.sleep(2)


def main():
    p = argparse.ArgumentParser()
    p.add_argument("--library", type=pathlib.Path, required=True)
    p.add_argument("--sdk", type=pathlib.Path, required=True)
    p.add_argument("--launcher", type=pathlib.Path, required=True)
    p.add_argument("--output", type=pathlib.Path, required=True)
    p.add_argument("--data-root", type=pathlib.Path, required=True)
    p.add_argument("--skip-calibration", action="store_true")
    args = p.parse_args()
    args.output = args.output.resolve()
    args.output.mkdir(parents=True, exist_ok=True)
    items = declarations(args.library, args.sdk)
    root = pathlib.Path(__file__).resolve().parents[1]
    exclusions_path = root / "data/cliff-styles/floating-island.exclusions.json"
    exclusions = json.loads(exclusions_path.read_text(encoding="utf-8"))["excludedTypes"]
    excluded = {i["typeName"].lower(): i["reason"] for i in exclusions}
    stream = AssetStream(args.data_root / "WBData.big")
    try:
        for item in items:
            # Compiled template evidence also verifies that this is the actual template ID.
            stream.read("GameObject:" + item["typeName"])
            item["model"] = model_bounds(stream, item["typeName"])
            item["modelArchive"] = str(args.data_root / "WBData.big")
            item["excluded"] = item["typeName"].lower() in excluded
            if item["excluded"]:
                item["exclusionReason"] = excluded[item["typeName"].lower()]
    finally:
        stream.close()
    (args.output / "units.json").write_text(json.dumps(items, ensure_ascii=False, indent=2), encoding="utf-8")
    (args.output / "templates.json").write_text(json.dumps([dict(englishName="SDK verified cliff templates",
        chineseName="SDK verified cliff templates", subObjects=[i["typeName"] for i in items if not i["excluded"]])], indent=2), encoding="utf-8")
    host = Host(pathlib.Path(__file__).resolve().parents[1], args.output, args.launcher)
    try:
        if not args.skip_calibration:
            host.create("CliffCalibration", 192, 160)
            host.call("terrain.set_height", dict(region=dict(x=0, y=0, width=192, height=160), height=300), write=True)
            placements = []
            for i, item in enumerate(items):
                if item["excluded"]:
                    continue
                x, y = 32 + i % 5 * 32, 24 + i // 5 * 36
                host.call("objects.place", dict(typeName=item["typeName"], x=x, y=y, z=0,
                                                 anchor="gridPoint", angleRadians=0), write=True)
                placements.append(dict(item, x=x, y=y, z=0))
            (args.output / "calibration-layout.json").write_text(json.dumps(placements, ensure_ascii=False, indent=2), encoding="utf-8")
            host.call("map.save", write=True)
            host.render(args.output / "CliffCalibration/CliffCalibration.map")
        style = json.loads((root / "data/cliff-styles/floating-island.json").read_text(encoding="utf-8"))
        allowed = {i["typeName"] for i in items if not i["excluded"]}
        if any(p["typeName"] not in allowed for p in style["pieces"]):
            raise ValueError("Style includes excluded or undeclared template")
        (args.output / "style.json").write_text(json.dumps(style, indent=2), encoding="utf-8")
        summaries = []
        for name, shape in [("CliffStraightDemo", dict(x=0, y=48, width=128, height=48)),
                            ("CliffPlateauDemo", dict(x=24, y=24, width=80, height=48))]:
            host.create(name, 128, 96)
            whole = dict(x=0, y=0, width=128, height=96)
            host.call("terrain.set_height", dict(region=whole, height=300), write=True)
            host.call("terrain.set_height", dict(region=shape, height=450), write=True)
            detection = host.call("terrain.detect_cliffs", dict(region=whole))
            host.call("map.save", write=True)
            baseline = host.preview()
            revision = host.revision
            prepared = host.call("edits.prepare", dict(baseRevision=revision, commands=[dict(command="objects.place_cliffs",
                arguments=dict(region=whole, seed=42, style=style, baseHeight="low", maxBendDegrees=0))]))
            unchanged = host.call("objects.query")
            if unchanged["total"] != 0 or host.revision != revision:
                raise AssertionError("Prepared edit changed current map")
            preview = host.preview(prepared)
            applied = host.call("edits.apply", dict(preparedPlanId=prepared["preparedPlanId"], planHash=prepared["planHash"]), write=True)
            placed = host.call("objects.query")
            if placed["total"] <= 0:
                raise AssertionError("No cliff objects placed")
            host.call("history.undo", write=True)
            if host.call("objects.query")["total"] != 0:
                raise AssertionError("Undo failed")
            host.call("history.redo", write=True)
            if host.call("objects.query")["total"] != placed["total"]:
                raise AssertionError("Redo failed")
            host.call("map.save", write=True)
            summaries.append(dict(name=name, detectedLines=len(detection["lines"]), placed=placed["total"],
                baseline=baseline, preview=preview, applied=applied, undoRedoVerified=True,
                mapPath=str(args.output / name / (name + ".map"))))
            (args.output / "summary.json").write_text(json.dumps(summaries, indent=2), encoding="utf-8")
    finally:
        host.close()


if __name__ == "__main__":
    main()
