using System.Text.Json.Serialization;

namespace Dreamness.RA3.Map.Parser.Asset.ScriptData;

public class ArgumentModel
{
    [JsonPropertyName("typeNumber")]
    public int TypeNumber { get; set; }
    
    [JsonPropertyName("realType")]
    public string RealType { get; set; }
    
    [JsonPropertyName("exampleData")]
    public string ExampleData { get; set; }
}

