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
- `docs/BlendQueryAPI.md`: Comprehensive texture blending API documentation

## Development Notes

- Target framework: .NET 6.0
- Nullable reference types enabled
- Unsafe code blocks allowed in Parser (for performance)
- XML documentation generation enabled (suppress warning 1591)
- All projects use implicit usings
