using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Dim1Array;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Water;

public class StandingWaterAreasAsset: BaseAsset
{

    public WritableList<StandingWaterArea> StandingWaterAreas { get; } = new WritableList<StandingWaterArea>();
    
    public override short GetVersion()
    {
        return 2;
    }

    public override string GetName()
    {
        return AssetNameConst.StandingWaterAreas;
    }

    protected override void _Parse(BaseContext context)
    {
        using var memoryStream = new MemoryStream(Data);
        using var binaryReader = new BinaryReader(memoryStream);
        
        int areaCount = binaryReader.ReadInt32();
        for (int i = 0; i < areaCount; i++)
        {
            StandingWaterAreas.Add(StandingWaterArea.FromBinaryReader(binaryReader, context), ignoreModified:true);
        }
        ObservableUtil.Subscribe(StandingWaterAreas, this);
    }

    protected override byte[] Deparse(BaseContext context)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);
        
        binaryWriter.Write(StandingWaterAreas.Count);
        binaryWriter.Write(StandingWaterAreas.ToBytes(context));
        binaryWriter.Flush();
        
        return memoryStream.ToArray();
    }
    
    public static StandingWaterAreasAsset Default(int playableWidth, int playableHeight, int border, BaseContext context)
    {
        var asset = new StandingWaterAreasAsset();
        
        asset.ApplyBasicInfo(context);
        
        asset.StandingWaterAreas.Add(
            StandingWaterArea.Of(
                1,
                "",
                0.0600000024f, 
                new []{
                    new Vec2D(-border * 10, -border * 10), 
                    new Vec2D(-border * 10, (playableHeight + border) * 10), 
                    new Vec2D((playableWidth + border) * 10, (playableHeight + border) * 10), 
                    new Vec2D((playableWidth + border) * 10, -border * 10)
                },
                200)
            );
        ObservableUtil.Subscribe(asset.StandingWaterAreas, asset);
        
        asset.MarkModified();
        
        return asset;
    }
}