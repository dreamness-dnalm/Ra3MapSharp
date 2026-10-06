# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Ra3MapSharp is a C# library for parsing, manipulating, and creating Command & Conquer: Red Alert 3 map files (.map). The project is built on .NET 6.0 and consists of multiple layers that work together to provide comprehensive map editing capabilities.

## Build and Test Commands

### Building
```bash
# Build entire solution
dotnet build Ra3MapSharp.sln

# Build specific project
dotnet build src/Dreamness.RA3.Map.Parser/Dreamness.RA3.Map.Parser.csproj

# Build in Release mode
dotnet build Ra3MapSharp.sln -c Release
```

### Testing
```bash
# Run all tests
dotnet test Ra3MapSharp.sln

# Run tests for specific project
dotnet test test/Dreamness.Ra3.Map.Facade.Test/Dreamness.Ra3.Map.Facade.Test.csproj

# Run single test class
dotnet test --filter "FullyQualifiedName~BlendTests"

# Run single test method
dotnet test --filter "FullyQualifiedName~BlendTests.TestGetBlendDetailInfo"

# Agent stack: stable, no RA3 install needed (199 + 38 tests)
dotnet test test/Dreamness.RA3.Map.Automation.Test/Dreamness.RA3.Map.Automation.Test.csproj --no-restore --filter "TestCategory!=UsageExamples"
dotnet test test/Dreamness.RA3.Map.Agent.Test/Dreamness.RA3.Map.Agent.Test.csproj --no-restore

# MCP end-to-end probe: handshake + tool list; -Render additionally renders via WbLauncher.exe
powershell -NoProfile -File scripts/mcp_probe.ps1 -Smoke
```

### Packaging
```bash
# Create NuGet packages
dotnet pack Ra3MapSharp.sln -c Release
```

## Architecture

### Layer Structure

The codebase follows a layered architecture with clear separation of concerns:

1. **Parser Layer** (`Dreamness.RA3.Map.Parser`)
   - Low-level binary parsing and serialization of RA3 map files
   - Handles compression/decompression (maps can be compressed or uncompressed)
   - Asset-based architecture where each map component is an Asset
   - Core classes: `Ra3Map`, `MapContext`, `BaseAsset`

2. **Facade Layer** (`Dreamness.RA3.Map.Facade`)
   - High-level API built on top of the Parser
   - Provides user-friendly methods for common map operations
   - Implemented as partial classes (e.g., `Ra3MapFacade` split into `HeightPart`, `TilePart`, `ObjectPart`, etc.)
   - Core class: `Ra3MapFacade` wraps `Ra3Map` from Parser layer

3. **Transform Layer** (`Dreamness.RA3.Map.Transform`)
   - Map transformation operations (resize, rotate, symmetry)
   - Command pattern implementation for transformations
   - Symmetry strategies for creating mirrored maps

4. **Visualization Layer** (`Dreamness.Ra3.Map.Visualization`)
   - Generates preview images from map data
   - Uses ImageSharp for image processing
   - Extension methods on `Ra3MapFacade`

5. **Lua Layer** (`Dreamness.RA3.Map.Lua`)
   - Lua script parsing using ANTLR4
   - Grammar file: `Lua4.g4`

6. **Automation Layer** (`Dreamness.RA3.Map.Automation`)
   - The command kernel ("WoWA") that exposes editor capability as deterministic, machine-callable commands.
   - Sessions, revisions, transactions, undo/redo, request dedup, object handles, prepared candidates, design entities, dependency graph, protection zones, geometry/terrain operators.
   - Entry points: `CommandRegistry` (registers every command), `AgentRuntime` (session + job host).
   - Every mutation requires an explicit `expectedRevision`; this is what makes concurrent edits detectable instead of silently lost.

7. **Agent Layer** (`Dreamness.RA3.Map.Agent`)
   - Host process for the kernel: MCP stdio server (`Protocol/McpServer.cs`), plus JSONL/one-shot batch modes.
   - Tool surface: 61 MCP tools defined by `Protocol/command-schemas.json` (`map.*`, `terrain.*`, `objects.*`, `texture.*`, `design.*`, `edits.*`, `preview.*`, `jobs.*`, `protections.*`, `history.*`).
   - Rendering: `Rendering/{DiagnosticRenderer,WorldBuilderRenderer,PreviewInspection}.cs`. Diagnostic images are pure managed; real overview images are delegated to the external `WbLauncher.exe` and are **asynchronous jobs** (`preview.start` returns a `jobId`; poll `jobs.status`; EOF on stdin cancels outstanding jobs).
   - Real overview rendering needs `--launcher <WbLauncher.exe>` or `RA3_WB_LAUNCHER`. Without it, file editing still works and only real rendering is unavailable.

### Asset System

The Parser layer uses an Asset-based architecture where different map components are represented as Assets:

- **BaseAsset**: Abstract base class for all assets
  - Properties: `Id`, `Version`, `AssetType`, `DataSize`, `Data`
  - Lazy parsing: Assets are only parsed when accessed
  - Modified flag tracking for efficient serialization

- **Asset Types** (in `src/Dreamness.RA3.Map.Parser/Asset/Impl/`):
  - `GameObject`: Map objects (units, buildings, props)
  - `Terrain`: Height map and terrain data
  - `Texture`: Tile textures and blending information
  - `Script`: Map scripts (triggers, conditions, actions)
  - `Player`: Player start positions and settings
  - `Team`: Team definitions
  - `Lighting`: Lighting settings
  - `Water`: Water configuration
  - `World`: World settings
  - `PostEffect`: Post-processing effects
  - `MissionObjective`: Mission objectives

### Context Pattern

Both `MapContext` and `ClipBoardContext` extend `BaseContext`:
- Manages string declarations (string interning for efficiency)
- Tracks all assets in the map
- Handles serialization/deserialization state

### Partial Class Organization

`Ra3MapFacade` is split into logical parts:
- `HeightPart.cs`: Terrain height manipulation
- `TilePart.cs`: Texture and blending operations
- `ObjectPart.cs`: Game object management
- `PlayerPart.cs`: Player configuration
- `TeamPart.cs`: Team management
- `ScriptPart.cs`: Script operations
- `WorldInfoPart.cs`: World settings
- `MissionObjectPart.cs`: Mission objectives

## Key Concepts

### Map Coordinates
- **Grid Coordinates**: Integer tile coordinates (used in most APIs)
- **World Coordinates**: Float coordinates (10 world units = 1 grid unit)
- Maps have a playable area plus border (typically 8 tiles)
- Actual map size = playable size + 2 * border

### Texture Blending
The texture system supports blending between two textures per tile:
- Each tile has a primary texture and optional secondary texture
- `BlendInfo` defines how textures blend (12 direction types)
- Blend directions: 8 basic (TopLeft, Top, TopRight, Right, etc.) + 4 "except" types
- Sub-tile coordinates (0-7) for 8x8 sub-tile grid
- See `docs/BlendQueryAPI.md` for detailed blending API documentation

### Script System
Scripts use a declarative system with JSON definitions:
- `data/script_declare/ScriptAction.json`: Available script actions
- `data/script_declare/ScriptConditon.json`: Available script conditions
- Scripts are loaded from JSON at runtime
- Script components: `Script`, `ScriptGroup`, `ScriptCondition`, `ScriptAction`, `ScriptArgument`

### Compression
RA3 maps can be compressed or uncompressed:
- Compression flag: `0x5A4C4942` (compressed) or `0x00000000` (uncompressed)
- Compression handled automatically by `Ra3Map.Open()` and `Ra3Map.Save(compress: bool)`
- Always save compressed unless debugging

## Common Patterns

### Opening and Saving Maps
```csharp
// Open map
var facade = Ra3MapFacade.Open(parentPath, mapName);
// or
var facade = Ra3MapFacade.Open(fullMapFilePath);

// Create new map
var facade = Ra3MapFacade.NewMap(
    playableWidth: 64,
    playableHeight: 64,
    border: 8,
    initPlayerStartWaypointCnt: 2,
    defaultTexture: "Dirt_Yucatan03"
);

// Save map (compressed by default)
facade.Save(compress: true);
```

### Accessing Underlying Parser
```csharp
// Facade wraps Parser's Ra3Map
Ra3Map parserMap = facade.ra3Map;
MapContext context = parserMap.Context;
```

### Working with Assets
Assets use lazy parsing - they're only parsed when accessed:
```csharp
// Asset.Parsed flag indicates if parsing has occurred
// Asset.Errored flag indicates if parsing failed
// Asset.Data contains raw bytes if not yet parsed
```

## Testing Framework

- Uses NUnit 3.x
- Test projects mirror source structure
- Common test pattern: Create temporary test map, perform operations, verify results
- Test maps are typically small (64x64) for speed

## Important Files

- `Directory.Build.props`: Shared MSBuild properties (version, author, license)
- `src/Dreamness.RA3.Map.Parser/data/script_declare/`: Script definitions
- `src/Dreamness.RA3.Map.Agent/Protocol/command-schemas.json`: The MCP tool surface. **Adding or changing a command requires updating this file in the same change**, otherwise tool descriptions and argument validation drift.
- `docs/BlendQueryAPI.md`: Comprehensive texture blending API documentation
- `docs/Agent-Usage.md`, `docs/MCP-Usage.md`: How an agent drives the kernel over MCP (session/revision discipline, candidate workflow).
- `docs/Agent-System-Roadmap.md`: Feasibility conclusion and phased plan for the whole agent-map-authoring system (includes the current scope decisions).
- `src/Dreamness.RA3.Map.Automation/Catalog/`: the asset catalogue (textures with derived surface/theme semantics, editor-declared objects, categories, screenshot coverage), the rendered `AssetAlbum`, and `ObjectCatalog`. Build the catalogue with `dotnet <agent.dll> --build-catalog <path> --launcher <WbLauncher.exe>`; build appearance images for the 1206 objects the editor ships no screenshot for with `--build-album <artifacts>/album`. The host auto-loads `<artifacts>/catalog/catalog.json` and `<artifacts>/album/album.json`, served by `assets.catalog_info` / `assets.search` / `assets.album` (`assets.album` returns the PNG as MCP image content). Measure placement footprints from those renders with `--build-footprints <artifacts>/footprints/footprints.json`; `footprints.get/list/set` serve them, hand corrections live in a separate overrides file, and `objects.scatter` uses them automatically when the caller passes none and every relevant type is measured, and reports the `clumpingIndex` it produced: minimum-distance repulsion alone lands near 1 while shipped maps cluster around 3, so `clusters`, `clusterRadiusCells` and `clusterSpacingCells` seed groves instead. Passing `clusters: 0` keeps the old even spacing and simply reports its index. Measure art-direction thresholds from the shipped-map corpus with `--analyze-corpus <artifacts>/art-rules/art-rules.json --corpus <origin_maps>`; `art.rules` reports them (material counts and shares, blend share, prop density, clumping, category mix, and the most-used texture pairs). `art.profile` measures the open map the same way, and `review.render_set` renders a north-up overview plus four fixed-fraction local crops into one contact sheet while judging the same revision against those thresholds (`combatReadability` is reported as not-evaluated on purpose: it needs eyes). Both also report `patches` — patch count, median size, largest-patch share and mean compactness (perimeter squared over area) — and both shape checks are `descriptive` rather than pass/fail: compactness turned out to measure boundary ragginess (a terrain-following map scored 34 where a disc-stamped one scored 159), so letting it decide would reward adding noise. `texture.paint_by_height` paints by elevation band so material boundaries follow the terrain; note that design entities cannot share height cells, so overlapping landforms must go through `terrain.sculpt` instead.

## Development Notes

- **PowerShell**: only Windows PowerShell 5.1 is available on this machine (no pwsh 7). A `.ps1` without a BOM is read as ANSI, which corrupts non-ASCII text and breaks parsing — keep `scripts/*.ps1` saved as **UTF-8 with BOM** and avoid .NET Core-only APIs (`ProcessStartInfo.ArgumentList`, `StandardInputEncoding`, `ConvertFrom-Json -Depth`).
- History index (`.automation/History/index.json`, schemaVersion 9) stores design entities as **per-revision deltas** on disk and forward-fills them on load. The same workload (one 200x200 platform + 120 objects, 4 revisions) went from 21.9 MB to 1.73 MB, and the per-revision increment from ~5.5 MB to ~8 KB. The in-memory model still keeps a complete state per revision so undo/redo stays a direct lookup: long sessions still grow RSS with (revisions x total entity area), and `waypointObjectIds`/`unitObjectIds` are still written in full per revision. Keep `DesignHash()` producing identical values, or existing workspaces get reported as design-dirty.
- Target framework: .NET 6.0
- Nullable reference types enabled
- Unsafe code blocks allowed in Parser (for performance)
- XML documentation generation enabled (suppress warning 1591)
- All projects use implicit usings
