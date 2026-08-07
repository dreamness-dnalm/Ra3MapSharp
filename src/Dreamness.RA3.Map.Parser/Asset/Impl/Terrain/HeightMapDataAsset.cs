using System.Net.Mail;
using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim2Array;
using Dreamness.Ra3.Map.Parser.Asset.SubAsset;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Terrain;

public class HeightMapDataAsset: BaseAsset
{
    private int _mapWidth;

    public int MapWidth { get => _mapWidth; private set => _mapWidth = value; }
    
    private int _mapHeight;
    public int MapHeight { get => _mapHeight; private set => _mapHeight = value; }
    
    private int _borderWidth;
    public int BorderWidth { get => _borderWidth; private set => _borderWidth = value; }

    public int MapPlayableWidth => MapWidth - 2 * BorderWidth;

    public int MapPlayableHeight => MapHeight - 2 * BorderWidth;
    
    public WritableList<BorderData> Borders { get; private set; } = new WritableList<BorderData>();

    public int Area => checked(MapWidth * MapHeight);
    
    public ElevationDim2Array Elevations { get; set; }
    

    public override short GetVersion()
    {
        return 6;
    }

    public override string GetAssetType()
    {
        return AssetNameConst.HeightMapData;
    }

    protected override void _Parse(BaseContext context)
    {
        var memoryStream = new MemoryStream(Data);
        var binaryReader = new BinaryReader(memoryStream);

        MapWidth = binaryReader.ReadInt32();
        MapHeight = binaryReader.ReadInt32();
        BorderWidth = binaryReader.ReadInt32();
        if (MapWidth <= 0 || MapHeight <= 0 || MapWidth > 16_384 || MapHeight > 16_384 ||
            (long)MapWidth * MapHeight > 64_000_000)
        {
            throw new InvalidDataException($"Invalid map dimensions: {MapWidth} x {MapHeight}.");
        }

        if (BorderWidth < 0 || BorderWidth * 2 >= MapWidth || BorderWidth * 2 >= MapHeight)
        {
            throw new InvalidDataException($"Invalid map border width: {BorderWidth}.");
        }
            
        int borderCount = binaryReader.ReadInt32();
        if (borderCount < 0 || borderCount > 100_000)
        {
            throw new InvalidDataException($"Invalid border count: {borderCount}.");
        }
        
        for (int i = 0; i < borderCount; i++)
        {
            var x1 = binaryReader.ReadInt32();
            var y1 = binaryReader.ReadInt32();
            var x2 = binaryReader.ReadInt32();
            var y2 = binaryReader.ReadInt32();

            var border = new BorderData(x1, y1, x2, y2);
            Borders.Add(border, ignoreModified:true);
        }
        ObservableUtil.Subscribe(Borders, this);
        var _area = binaryReader.ReadInt32();
        if (_area != Area)
        {
            throw new InvalidDataException($"HeightMapData area mismatch: declared {_area}, expected {Area}.");
        }
            
        var elevations = new float[MapWidth, MapHeight];
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < MapWidth; x++)
            {
                // elevations[x, y] = binaryReader.ReadUInt16();
                elevations[x, y] = StreamExtension.FromSageFloat16(binaryReader.ReadUInt16());
            }
        }
        Elevations = new ElevationDim2Array(elevations);
        ObservableUtil.Subscribe(Elevations, this);
        if (binaryReader.BaseStream.Position != binaryReader.BaseStream.Length)
        {
            throw new InvalidDataException("HeightMapData contains trailing data.");
        }
    }

    protected override byte[] Deparse(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.Write(MapWidth);
        binaryWriter.Write(MapHeight);
        binaryWriter.Write(BorderWidth);
        
        binaryWriter.Write(Borders.Count);
        foreach (var border in Borders)
        {
            binaryWriter.Write(border.ToBytes(context));
        }
        binaryWriter.Write(Area);
        binaryWriter.Write(Elevations.ToBytes(context));
        // for (int y = 0; y < MapHeight; y++)
        // {
        //     for (int x = 0; x < MapWidth; x++)
        //     {
        //         binaryWriter.Write(StreamExtension.ToSageFloat16(Elevations[x, y]));
        //     }
        // }
        binaryWriter.Flush();
        return memoryStream.ToArray();
    }

    public static HeightMapDataAsset Default(int mapPlayableWidth, int mapPlayableHeight, int borderWidth, BaseContext context)
    {
        if (mapPlayableWidth <= 0 || mapPlayableHeight <= 0 || borderWidth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(mapPlayableWidth), "Map dimensions must be positive and border width non-negative.");
        }

        var totalWidth = checked(mapPlayableWidth + 2 * borderWidth);
        var totalHeight = checked(mapPlayableHeight + 2 * borderWidth);
        if (totalWidth > 16_384 || totalHeight > 16_384 || (long)totalWidth * totalHeight > 64_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(mapPlayableWidth), "Requested map dimensions exceed parser limits.");
        }

        var heightMapDataAsset = new HeightMapDataAsset();
        
        heightMapDataAsset.ApplyBasicInfo(context);
        
        heightMapDataAsset.BorderWidth = borderWidth;
        heightMapDataAsset.MapWidth = mapPlayableWidth + 2 * heightMapDataAsset.BorderWidth;
        heightMapDataAsset.MapHeight = mapPlayableHeight + 2 * heightMapDataAsset.BorderWidth;
        
        heightMapDataAsset.Borders = new WritableList<BorderData>();
        heightMapDataAsset.Borders.Add(new BorderData(0, 0, heightMapDataAsset.MapPlayableWidth, heightMapDataAsset.MapPlayableHeight));
        ObservableUtil.Subscribe(heightMapDataAsset.Borders, heightMapDataAsset);

        heightMapDataAsset.Elevations = new ElevationDim2Array(new float[heightMapDataAsset.MapWidth, heightMapDataAsset.MapHeight]);
        
        for (int y = 0; y < heightMapDataAsset.MapHeight; y++)
        {
            for (int x = 0; x < heightMapDataAsset.MapWidth; x++)
            {
                heightMapDataAsset.Elevations[x, y] = 210;
            }
        }
        ObservableUtil.Subscribe(heightMapDataAsset.Elevations, heightMapDataAsset);
        
        
        heightMapDataAsset.MarkModified();
        return heightMapDataAsset;
    }
    
}
