using Dreamness.Ra3.Map.Parser.Asset;
using Dreamness.Ra3.Map.Parser.Asset.Collection;
using Dreamness.Ra3.Map.Parser.Asset.Impl.World;
using Dreamness.Ra3.Map.Parser.Core;

namespace Dreamness.Ra3.Map.Facade.Core;

public partial class Ra3MapFacade
{
    private WorldInfoAsset _worldInfoAsset;
    
    public float CameraGroundMinHeight
    {
        get => _worldInfoAsset.Properties.GetProperty<float>("cameraGroundMinHeight");
        set => _worldInfoAsset.Properties.SetProperty("cameraGroundMinHeight", value);
    }
    
    public float CameraGroundMaxHeight
    {
        get => _worldInfoAsset.Properties.GetProperty<float>("cameraGroundMaxHeight");
        set => _worldInfoAsset.Properties.SetProperty("cameraGroundMaxHeight", value);
    }

    public float CameraMinHeight
    {
        get => _worldInfoAsset.Properties.GetProperty<float>("cameraMinHeight");
        set => _worldInfoAsset.Properties.SetProperty("cameraMinHeight", value);
    }
    
    public float CameraMaxHeight
    {
        get => _worldInfoAsset.Properties.GetProperty<float>("cameraMaxHeight");
        set => _worldInfoAsset.Properties.SetProperty("cameraMaxHeight", value);
    }
    
    
    // ---- init ----
    private void LoadWorldInfo()
    {
        _worldInfoAsset = ra3Map.Context.WorldInfoAsset;
    }
}