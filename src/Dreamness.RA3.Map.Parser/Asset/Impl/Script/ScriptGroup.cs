using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Core.Base;


namespace Dreamness.Ra3.Map.Parser.Asset.Impl.Script;

public class ScriptGroup: BaseAsset
{
    private string name;

    public string Name
    {
        get { return name; }
        set
        {
            if (name != value)
            {
                name = value;
                MarkModified();
            }
        }
    }
    
    public override short GetVersion()
    {
        return 3;
    }

    public override string GetName()
    {
        return Name;
    }

    protected override void _Parse(BaseContext context)
    {
        throw new NotImplementedException();
    }

    protected override byte[] Deparse(BaseContext context)
    {
        Id = context.RegisterStringDeclare(GetName());
        
        throw new NotImplementedException();
    }
}