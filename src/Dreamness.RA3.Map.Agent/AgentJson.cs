using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dreamness.RA3.Map.Agent;

public static class AgentJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
}
