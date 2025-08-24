using System.Text.Json.Serialization;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Parser.Asset.Base;

public abstract class Ra3MapWritable: INotify, IObserver
{
    
    [JsonIgnore]
    public bool _modified { get; set; } = false;
    
    protected byte[] Data { get; set; }

    public abstract byte[] ToBytes(BaseContext context);
    
    public void MarkModified()
    {
        if (_modified)
        {
            return;
        }
        
        _modified = true;
        ObservableUtil.Notify(this, new NotifyEventArgs("Modified", true));
    }

    [JsonIgnore]
    public List<IObserver> _observers { get; set; } = new List<IObserver>();
    
    public void OnNotify(object sender, NotifyEventArgs e)
    {
        if (e.EventName == "Modified")
        {
            MarkModified();
        }
    }
    
}