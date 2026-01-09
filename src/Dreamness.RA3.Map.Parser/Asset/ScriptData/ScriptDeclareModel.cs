using System.Text.Json.Serialization;

namespace Dreamness.RA3.Map.Parser.Asset.ScriptData;

public class ScriptDeclareModel
{
    [JsonPropertyName("scriptName")]
    public string ScriptName { get; set; }
    
    [JsonPropertyName("scriptDesc")]
    public string ScriptDesc { get; set; }
    
    [JsonPropertyName("scriptArg")]
    public string ScriptArg { get; set; }
    
    [JsonPropertyName("scriptTrans")]
    public string ScriptTrans { get; set; }
    
    [JsonPropertyName("commandWord")]
    public string CommandWord { get; set; }
    
    [JsonPropertyName("editorNumber")]
    public int EditorNumber {get; set;}
    
    [JsonPropertyName("argumentModel")]
    public List<ArgumentModel> Arguments { get; set; } = new List<ArgumentModel>();
}