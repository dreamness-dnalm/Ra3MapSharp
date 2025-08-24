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
using Dreamness.Ra3.Map.Parser.Util.Compress;

namespace Dreamness.Ra3.Map.Parser.Core.Map;

public class Ra3Map
{
    private Ra3Map()
    {

    }
    
    public string MapFilePath { get; private set; }

    public MapContext Context = new MapContext();

    public static Ra3Map Open(string mapFilePath)
    {
        var map = new Ra3Map();
        map.MapFilePath = mapFilePath;

        try
        {
            var bytes = File.ReadAllBytes(mapFilePath);
            using var memoryStream = new MemoryStream(bytes);
            var binaryReader = new BinaryReader(memoryStream);

            var compressFlag = binaryReader.ReadUInt32();

            switch (compressFlag)
            {
                case CompressConst.UnCompressFlag:
                    break;
                case CompressConst.CompressFlag:
                    binaryReader.BaseStream.Position = 8L;
                    // var compressedData = binaryReader.ReadBytes((int)binaryReader.BaseStream.Length - 8);
                    // var decompressBytes = Compress.DecompressData(compressedData);

                    var binaryWriter = new BinaryWriter(new MemoryStream());
                    RefpackComrpessor.Decompress(binaryReader, binaryWriter);
                

                    binaryReader.Close();
                    memoryStream.Close();

                    // memoryStream = new MemoryStream(binaryWriter.BaseStream);
                    binaryReader = new BinaryReader(binaryWriter.BaseStream);
                    binaryReader.BaseStream.Position = 4L;
                    break;
                default:
                    throw new System.Exception("Invalid map file format");
            }
        
            var sectionDeclareCount = binaryReader.ReadInt32();
            for (int i = 0; i < sectionDeclareCount; i++)
            {
                var name = binaryReader.ReadString();
                var id = binaryReader.ReadInt32();
                map.Context.RegisterStringDeclare(id, name);
            }

            while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
            {
                var asset = AssetParser.FromBinaryReader(binaryReader, map.Context);
                map.Context.RegisterAsset(asset);
            }
        
            binaryReader.Close();
            // memoryStream.Close();

            return map;
        }catch (System.Exception ex)
        {
            map._hasError = true;
            throw new BadMapException();
        }
    }
    
    public void SaveAs(string mapFilePath, bool compress = true)
    {
        var dirPath = Path.GetDirectoryName(mapFilePath);
        if (!Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
        }
        
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);

        byte[] data = Context.ToBytes();
        binaryWriter.Write(CompressConst.UnCompressFlag);
        binaryWriter.Write(data);
        
        using var fileStream = File.Create(mapFilePath);
        
        if(compress)
        {
            byte[] output;
            memoryStream.GetBuffer().Skip(0).Take((int) memoryStream.Length).ToArray().RefPackCompress(out output);
            fileStream.Write(output, 0, output.Length);
        }
        else
        {
            fileStream.Write(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
        }
    }

    public async Task<bool> SaveAsAsync(string mapFilePath, bool compress = true)
    {
        SaveAs(mapFilePath, compress);
        return true;
    }
    
    public void Save(bool compress = true)
    {
        if (MapFilePath == null)
        {
            throw new System.Exception("MapFilePath is null, if it's a new map, use SaveAs method");
        }
        
        SaveAs(MapFilePath, compress);
    }

    public async Task<bool> SaveAsync(bool compress = true)
    {
        Save(compress);
        return true;
    }

    public static Ra3Map NewMap(int mapPlayableWidth, int mapPlayableHeight, int borderWidth=0, string defaultTexture="Dirt_Yucatan03")
    {
        // throw new System.NotImplementedException();
        
        var ra3Map = new Ra3Map();
        var context = ra3Map.Context;


        BaseAsset[] assets =
        {
            AssetListAsset.Default(context),
            GlobalVersionAsset.Default(context),
            HeightMapDataAsset.Default(mapPlayableWidth, mapPlayableHeight, borderWidth, context),
            BlendTileDataAsset.Default(mapPlayableWidth, mapPlayableHeight, borderWidth, defaultTexture, context),
            WorldInfoAsset.Default(defaultTexture, context),
            MPPositionListAsset.Default(context),
            SidesListAsset.Default(context),
            LibraryMapListsAsset.Default(context),
            TeamsAsset.Default(context),
            PlayerScriptsList.Default(context),
            ObjectsListAsset.Default(context),
            StandingWaterAreasAsset.Default(mapPlayableWidth, mapPlayableHeight, borderWidth, context),
            GlobalWaterSettingsAsset.Default(context),
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