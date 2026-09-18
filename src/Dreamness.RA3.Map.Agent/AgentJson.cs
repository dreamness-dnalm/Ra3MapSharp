using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dreamness.RA3.Map.Agent;

public static class AgentJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Drops a leading UTF-8 BOM from a received message.
    /// <para>
    /// System.Text.Json rejects U+FEFF at position 0, and the console reader does not strip a
    /// BOM from a redirected stream, so a client that prefixes its first message would fail the
    /// very first request of a session for no visible reason. Observed on this host: the first
    /// JSONL message of every run arrived BOM-prefixed while later ones did not.
    /// </para>
    /// </summary>
    public static string WithoutBom(string text) =>
        text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;
}
