using Dreamness.Ra3.Map.Facade.Core;

namespace Dreamness.RA3.Map.Transform.Ra3MapTransform.Commands;

public abstract class BaseTransformCommand
{
    public Ra3MapFacade OriginMapFacade { get; private set; }
    
    public Ra3MapFacade DestinationRa3MapFacade { get; protected set; }

    public BaseTransformCommand(Ra3MapFacade origin)
    {
        OriginMapFacade = origin;
    }

    public abstract void Transform();
}