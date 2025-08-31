using Dreamness.Ra3.Map.Facade.Core;

namespace Dreamness.RA3.Map.Transform.Ra3MapTransform.Commands;

public class InitTransformCommand: BaseTransformCommand
{
    public InitTransformCommand(Ra3MapFacade origin) : base(origin)
    {
    }

    public override void Transform()
    {
        DestinationRa3MapFacade = OriginMapFacade;
    }
}