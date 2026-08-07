using Dreamness.Ra3.Map.Parser.Asset;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Lighting;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Player;
using Dreamness.Ra3.Map.Parser.Asset.Impl.PostEffect;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Script;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Team;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Terrain;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Texture;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Unknown;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Water;
using Dreamness.Ra3.Map.Parser.Asset.Impl.World;
using Dreamness.Ra3.Map.Parser.Asset.Util;
using Dreamness.Ra3.Map.Parser.Exception;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Core.Map;

public class Ra3Map
{
    private Ra3Map()
    {

    }
    
    public string? MapFilePath { get; private set; }

    public MapContext Context = new MapContext();

    public static Ra3Map Open(string mapFilePath)
    {
        var map = new Ra3Map();
        var fullPath = Path.GetFullPath(mapFilePath);
        var bytes = File.ReadAllBytes(fullPath);
        using var binaryReader = MapFileCodec.CreatePayloadReader(bytes);
        MapFileCodec.ReadContext(binaryReader, map.Context);
        map.MapFilePath = fullPath;
        return map;
    }
    
    public void SaveAs(string mapFilePath, bool compress = true)
    {
        SynchronizeModifiedSideMetadata();
        MapFilePath = MapFileCodec.AtomicWrite(mapFilePath, MapFileCodec.Encode(Context, compress));
    }

    public async Task<bool> SaveAsAsync(
        string mapFilePath,
        bool compress = true,
        CancellationToken cancellationToken = default)
    {
        SynchronizeModifiedSideMetadata();
        MapFilePath = await MapFileCodec.AtomicWriteAsync(
            mapFilePath,
            MapFileCodec.Encode(Context, compress),
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    private void SynchronizeModifiedSideMetadata()
    {
        if (Context.AssetDict.TryGetValue(AssetNameConst.SidesList, out var sides) && sides._modified)
        {
            Context.SynchronizeSideMetadata();
        }
    }
    
    public void Save(bool compress = true)
    {
        if (MapFilePath == null)
        {
            throw new System.Exception("MapFilePath is null, if it's a new map, use SaveAs method");
        }
        
        SaveAs(MapFilePath, compress);
    }

    public Task<bool> SaveAsync(bool compress = true, CancellationToken cancellationToken = default)
    {
        if (MapFilePath is null)
        {
            throw new InvalidOperationException("MapFilePath is null; use SaveAsAsync for a new map.");
        }

        return SaveAsAsync(MapFilePath, compress, cancellationToken);
    }

    public static Ra3Map NewMap(int mapPlayableWidth, int mapPlayableHeight, int borderWidth=0, string defaultTexture="Dirt_Yucatan03")
    {
        // throw new System.NotImplementedException();
        
        var ra3Map = new Ra3Map();
        var context = ra3Map.Context;
        var sidesList = SidesListAsset.Default(context);

        BaseAsset[] assets =
        {
            AssetListAsset.Default(context),
            GlobalVersionAsset.Default(context),
            HeightMapDataAsset.Default(mapPlayableWidth, mapPlayableHeight, borderWidth, context),
            BlendTileDataAsset.Default(mapPlayableWidth, mapPlayableHeight, borderWidth, defaultTexture, context),
            WorldInfoAsset.Default(defaultTexture, context),
            MPPositionListAsset.Default(context),
            sidesList,
            LibraryMapListsAsset.Default(sidesList, context),
            TeamsAsset.Default(context),
            PlayerScriptsList.Default(context),
            BuildListsAsset.Default(sidesList, context),
            ObjectsListAsset.Default(context),
            GlobalWaterSettingsAsset.Default(context),
            StandingWaterAreasAsset.Default(mapPlayableWidth, mapPlayableHeight, borderWidth, context),
            PostEffectsChunkAsset.Default(context),
            GlobalLightingAsset.Default(context)
            
        };

        foreach (var asset in assets)
        {
            context.RegisterAsset(asset);
        }
        
        return ra3Map;
    }

    private bool _hasError = false;

    public bool Errored
    {
        get
        {
            if (!_hasError)
            {
                foreach(var asset in Context.AssetDict.Values)
                {
                    _hasError = _hasError || asset.Errored;
                }
            }
        
            return _hasError;
        }
    }
    
}
