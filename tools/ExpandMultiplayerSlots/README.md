# ExpandMultiplayerSlots

Copies an entire map directory to a new output directory, preserving companion files, and pads every `.map` to eight multiplayer slot records. Existing slots are preserved; added slots allow humans and computers, load AI scripts, have no fixed team (`uint.MaxValue`) and no faction restrictions.

```powershell
dotnet run --project tools/ExpandMultiplayerSlots -- "H:\sys\Downloads\8ptest_map" "H:\sys\Downloads\8ptest_map_8slots"
```

The output directory must not exist. Sources are never overwritten. Each saved map is reopened and checked for eight slots, byte-identical original slots, and byte-identical non-slot assets. A JSON report records results and player start waypoint names. Any error exits with failure; incomplete output can remain and should not be used.

This changes slot records only; it does not add players or spawn points. WorldBuilder may write six slots again when saving.
