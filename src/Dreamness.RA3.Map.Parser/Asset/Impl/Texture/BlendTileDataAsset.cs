using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim2Array;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Terrain;
using Dreamness.Ra3.Map.Parser.Asset.Impl.World;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Texture;
// TODO: not finished yet
public class BlendTileDataAsset: BaseAsset
{
    private int mapWidth;
    
    private int mapHeight;

    public UshortDim2Array Tiles;
    
    public UshortDim2Array Blends;
    
    public WritableList<BlendInfo> BlendInfos = new WritableList<BlendInfo>();

    public UshortDim2Array SingleEdgeBlends;
    
    public PassabilityDim2Array Passabilities;

    public UshortDim2Array CliffBlends;

    public int cliffBlendsCount;
    
    public int CliffBlendsCount
    {
        get => cliffBlendsCount;
        set
        {
            if (cliffBlendsCount != value)
            {
                cliffBlendsCount = value;
                MarkModified();
            }
        }
    }

    private int TextureCellCount;

    public BoolDim2Array PassageWidths;
    
    public BoolDim2Array Visibilities;
    
    public BoolDim2Array Buildabilities;
    
    public BoolDim2Array TiberiumGrowabilities;
    
    public ByteDim2Array DynamicShrubberies;
    
    
    
    
    
    public WritableList<Texture> Textures = new WritableList<Texture>();
    
    private Dictionary<string, int> textureIndexDict = new Dictionary<string, int>();
    
    private uint magic1 = 3452816845u;

    private int magic2 = 0;
    
    public override short GetVersion()
    {
        return 27;
    }

    public override string GetName()
    {
        return AssetNameConst.BlendTileData;
    }

    protected override void _Parse(BaseContext context)
    {
        var heightMapDataAsset = context.AssetDict[AssetNameConst.HeightMapData] as HeightMapDataAsset;
        mapWidth = heightMapDataAsset.MapWidth;
        mapHeight = heightMapDataAsset.MapHeight;
        
        using var memoryStream = new MemoryStream(Data);
        using var binaryReader = new BinaryReader(memoryStream);

        int area = binaryReader.ReadInt32();
        Tiles = new UshortDim2Array(IOUtility.ReadArray<ushort>(binaryReader, mapWidth, mapHeight));
        Blends = new UshortDim2Array(IOUtility.ReadArray<ushort>(binaryReader, mapWidth, mapHeight));
        SingleEdgeBlends = new UshortDim2Array(IOUtility.ReadArray<ushort>(binaryReader, mapWidth, mapHeight));
        CliffBlends = new UshortDim2Array(IOUtility.ReadArray<ushort>(binaryReader, mapWidth, mapHeight));
        Passabilities = new PassabilityDim2Array(new Passability[mapWidth, mapHeight]);
        var impassable = IOUtility.ReadArray<bool>(binaryReader, mapWidth, mapHeight);
        bool[,] impassableToPlayers = IOUtility.ReadArray<bool>(binaryReader, mapWidth, mapHeight);
        PassageWidths = new BoolDim2Array(IOUtility.ReadArray<bool>(binaryReader, mapWidth, mapHeight));
        bool[,] extraPassable = IOUtility.ReadArray<bool>(binaryReader, mapWidth, mapHeight);
        Visibilities = new BoolDim2Array(IOUtility.ReadArray<bool>(binaryReader, mapWidth, mapHeight));
        Buildabilities = new BoolDim2Array(IOUtility.ReadArray<bool>(binaryReader, mapWidth, mapHeight));
        bool[,] impassableToAirUnits = IOUtility.ReadArray<bool>(binaryReader, mapWidth, mapHeight);
        TiberiumGrowabilities = new BoolDim2Array(IOUtility.ReadArray<bool>(binaryReader, mapWidth, mapHeight));

        
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x2 = 0; x2 < mapWidth; x2++)
            {
                if (impassable[x2, y])
                {
                    Passabilities[x2, y] = Passability.Impassable;
                }
                else if (impassableToPlayers[x2, y])
                {
                    Passabilities[x2, y] = Passability.ImpassableToPlayers;
                }
                else if (impassableToAirUnits[x2, y])
                {
                    Passabilities[x2, y] = Passability.ImpassableToAirUnits;
                }
                else if (extraPassable[x2, y])
                {
                    Passabilities[x2, y] = Passability.ExtraPassable;
                }
                else
                {
                    Passabilities[x2, y] = Passability.Passable;
                }
            }
        }
        
        DynamicShrubberies = new ByteDim2Array(IOUtility.ReadArray<byte>(binaryReader, mapWidth, mapHeight));
        TextureCellCount = binaryReader.ReadInt32();
        var blendsCount = binaryReader.ReadInt32() - 1;
        cliffBlendsCount = binaryReader.ReadInt32() - 1;
        int textureCount = binaryReader.ReadInt32();
        // textures = new Texture[br.ReadInt32()];
        for (int j = 0; j < textureCount; j++)
        {
            var texture = Texture.FromBinaryReader(binaryReader, context);
            // AddTexture(context, texture);
            Textures.Add(texture);
            textureIndexDict.Add(texture.Name, Textures.Count - 1);
        }
        var magic1 = binaryReader.ReadUInt32();
        if (magic1 != this.magic1)
        {
            throw new System.Exception("BlendTileDataAsset magic1 mismatch: " + magic1 + " != " + this.magic1);
        }
        var magic2 = binaryReader.ReadInt32();
        if (magic2 != this.magic2)
        {
            throw new System.Exception("BlendTileDataAsset magic2 mismatch: " + magic2 + " != " + this.magic2);
        }
        
        for (int i = 0; i < blendsCount; i++)
        {
            BlendInfos.Add(BlendInfo.FromBinaryReader(binaryReader, context));
        }
        
        
    }

    protected override byte[] Deparse(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.Write(mapWidth * mapHeight);
        IOUtility.WriteArray(binaryWriter, Tiles.Array);
        IOUtility.WriteArray(binaryWriter, Blends.Array);
        IOUtility.WriteArray(binaryWriter, SingleEdgeBlends.Array);
        // binaryWriter.Write(new byte[mapWidth * mapHeight * 2]);
        IOUtility.WriteArray(binaryWriter, CliffBlends.Array);
        bool[,] impassable = new bool[mapWidth, mapHeight];
        bool[,] impassableToPlayers = new bool[mapWidth, mapHeight];
        bool[,] impassableToAirUnits = new bool[mapWidth, mapHeight];
        bool[,] extraPassable = new bool[mapWidth, mapHeight];
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                switch (Passabilities[x, y])
                {
                    case Passability.Impassable:
                        impassable[x, y] = true;
                        break;
                    case Passability.ImpassableToAirUnits:
                        impassableToAirUnits[x, y] = true;
                        break;
                    case Passability.ImpassableToPlayers:
                        impassableToPlayers[x, y] = true;
                        break;
                    case Passability.ExtraPassable:
                        extraPassable[x, y] = true;
                        break;
                }
            }
        }
        IOUtility.WriteArray(binaryWriter, impassable);
        IOUtility.WriteArray(binaryWriter, impassableToPlayers);
        IOUtility.WriteArray(binaryWriter, extraPassable);
        IOUtility.WriteArray(binaryWriter, PassageWidths.Array);
        IOUtility.WriteArray(binaryWriter, Visibilities.Array);
        IOUtility.WriteArray(binaryWriter, Buildabilities.Array);
        IOUtility.WriteArray(binaryWriter, impassableToAirUnits);
        IOUtility.WriteArray(binaryWriter, TiberiumGrowabilities.Array);
        IOUtility.WriteArray(binaryWriter, DynamicShrubberies.Array);
        binaryWriter.Write(TextureCellCount);
        // binaryWriter.Write(blendsCount + 1);
        binaryWriter.Write(BlendInfos.Count + 1);
        binaryWriter.Write(CliffBlendsCount + 1);
        binaryWriter.Write(Textures.Count);
        binaryWriter.Write(Textures.ToBytes(context));
        // foreach (Texture t in textures)
        // {
        //     t.saveData(binaryWriter, context);
        // }
        
        binaryWriter.Write(magic1);
        binaryWriter.Write(magic2);
        
        binaryWriter.Write(BlendInfos.ToBytes(context));
        
        // for (int i = 0; i < blendsCount; i++)
        // {
        //     blendInfo[i].saveData(binaryWriter, context);
        // }
        
        
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }
    
    public static BlendTileDataAsset Default(int playableWidth, int playableHeight, int borderWidth, string defaultTexture, BaseContext context)
    {
        var asset = new BlendTileDataAsset();
        asset.ApplyBasicInfo(context);

        asset.mapWidth = playableWidth + borderWidth * 2;
        asset.mapHeight = playableHeight + borderWidth * 2;

        asset.TextureCellCount = 0; // ???
        var texture = Texture.Of(asset.TextureCellCount, defaultTexture);
        // asset.AddTexture(context, texture);
        asset.Textures.Add(texture);
        asset.textureIndexDict.Put(texture.Name, asset.Textures.Count - 1);
        asset.TextureCellCount += texture.CellCount;

        asset.Tiles = new UshortDim2Array(new ushort[asset.mapWidth, asset.mapHeight]);
        asset.Blends = new UshortDim2Array(new ushort[asset.mapWidth, asset.mapHeight]);
        asset.SingleEdgeBlends = new UshortDim2Array(new ushort[asset.mapWidth, asset.mapHeight]);
        asset.CliffBlends = new UshortDim2Array(new ushort[asset.mapWidth, asset.mapHeight]);
        asset.Passabilities = new PassabilityDim2Array(new Passability[asset.mapWidth, asset.mapHeight]);
        asset.Visibilities = new BoolDim2Array(new bool[asset.mapWidth, asset.mapHeight]);
        asset.PassageWidths = new BoolDim2Array(new bool[asset.mapWidth, asset.mapHeight]);
        asset.Buildabilities = new BoolDim2Array(new bool[asset.mapWidth, asset.mapHeight]);
        asset.TiberiumGrowabilities = new BoolDim2Array(new bool[asset.mapWidth, asset.mapHeight]);
        asset.DynamicShrubberies = new ByteDim2Array(new byte[asset.mapWidth, asset.mapHeight]);

        for (int y = 0; y < asset.mapHeight; y++)
        {
            for (int x = 0; x < asset.mapWidth; x++)
            {
                asset.Visibilities[x, y] = true;
                asset.Passabilities[x, y] = Passability.Passable;
                asset.Tiles[x, y] = asset.GetTile(x, y, 0);
            }
        }

        asset.CliffBlendsCount = 0;
        
        asset.MarkModified();
        return asset;
    }
    
    public ushort GetTile(int x, int y, int texture)
    {
        int rowFirst = y % 8 / 2 * 16 + y % 2 * 2;
        int current = x % 8 / 2 * 4 + x % 2 + rowFirst;
        current += 64 * texture;
        return (ushort)current;
    }

    public void AddTexture(BaseContext context, Texture texture)
    {
        Textures.Add(texture);
        
        textureIndexDict.Add(texture.Name, Textures.Count - 1);
        
        TextureCellCount += texture.CellCount;
        var worldInfo = context.AssetDict[AssetNameConst.WorldInfo] as WorldInfoAsset;
        
        var terrainTextureStrings = worldInfo.Properties.GetProperty<string>("terrainTextureStrings");
        if (terrainTextureStrings == null)
        {
            terrainTextureStrings = "";
        }

        if (!worldInfo.SupportedTextureDict.ContainsKey(texture.Name))
        {
            throw new System.Exception("Texture name not found in supported texture dictionary: " + texture.Name);
        }

        terrainTextureStrings += worldInfo.SupportedTextureDict[texture.Name];
        worldInfo.Properties.PutProperty("terrainTextureStrings", terrainTextureStrings);
    }
    
    public void AddTexture(BaseContext context, string textureName)
    {
        foreach (var texture in Textures)
        {
            if (texture.Name == textureName)
            {
                //重复texture
                return;
            }
        }

        var newTexture = Texture.Of(TextureCellCount, textureName);
        AddTexture(context, newTexture);
    }
        
    public ushort GetTexture(int x, int y)
    {
        int rowFirst = y % 8 / 2 * 16 + y % 2 * 2;
        int current = x % 8 / 2 * 4 + x % 2 + rowFirst;
        return (ushort)((Tiles[x, y] - current) / 64);
    }
        
    public string GetTextureName(int x, int y)
    {
        int rowFirst = y % 8 / 2 * 16 + y % 2 * 2;
        int current = x % 8 / 2 * 4 + x % 2 + rowFirst;
        ushort tile =  (ushort)((Tiles[x, y] - current) / 64);
        return Textures[tile].Name;
    }
    
    public void UpdatePassabilityMap(BaseContext context)
    {
        var elev = (context.AssetDict[AssetNameConst.HeightMapData] as HeightMapDataAsset).Elevations;
        double a = 45;
        float tan = (float)Math.Tan(a);
        // impassiableCount = 0;
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                int passages = 0;
                if (x > 0 && Math.Abs((elev[x, y] - elev[x - 1, y]) / 10f) < tan)
                {
                    passages++;
                }
                if (x < mapWidth - 1 && Math.Abs((elev[x, y] - elev[x + 1, y]) / 10f) < tan)
                {
                    passages++;
                }
                if (y > 0 && Math.Abs((elev[x, y] - elev[x, y - 1]) / 10f) < tan)
                {
                    passages++;
                }
                if (y < mapHeight - 1 && Math.Abs((elev[x, y] - elev[x, y + 1]) / 10f) < tan)
                {
                    passages++;
                }
                if (passages < 4)
                {
                    Passabilities[x, y] = Passability.Impassable;
                    // impassiableCount++;
                    if (x > 0)
                    {
                        Passabilities[x - 1, y] = Passability.Impassable;
                    }
                    if (x < mapWidth - 1)
                    {
                        Passabilities[x + 1, y] = Passability.Impassable;
                    }
                    if (y > 0)
                    {
                        Passabilities[x, y - 1] = Passability.Impassable;
                    }
                    if (y < mapHeight - 1)
                    {
                        Passabilities[x, y + 1] = Passability.Impassable;
                    }
                }
                else
                {
                    Passabilities[x, y] = Passability.Passable;
                }
            }
        }
    }
    
    public bool[,] GetImpassible()
    {
        bool[,] res = new bool[mapWidth, mapHeight];
        for (int y = 0; y < mapHeight; y++)
        {
            for (int x = 0; x < mapWidth; x++)
            {
                switch (Passabilities[x, y])
                {
                    case Passability.Impassable:
                        res[x, y] = true;
                        break;
                    default:
                        res[x, y] = false;
                        break;
                }
            }
        }

        return res;
    }

    public void SetTileTexture(int x, int y, string textureName, BaseContext context)
    {
        if (!textureIndexDict.ContainsKey(textureName))
        {
            AddTexture(context, textureName);
        }
        
        Tiles[x, y] = GetTile(x, y, textureIndexDict[textureName]);
    }

    public string GetTileTexture(int x, int y)
    {
        int tmp = y % 8 / 2 * 16 + y % 2 * 2 + x % 8 / 2 * 4 + x % 2;
        
        var index = Tiles[x, y];
        
        // result = 64 * textureIndex + index
        var textureIndex = (Tiles[x, y] - tmp) / 64;
        return Textures[textureIndex].Name;
    }
    
    public enum Passability : byte
    {
        Passable,
        Impassable,
        ImpassableToPlayers,
        ImpassableToAirUnits,
        ExtraPassable
    }
}