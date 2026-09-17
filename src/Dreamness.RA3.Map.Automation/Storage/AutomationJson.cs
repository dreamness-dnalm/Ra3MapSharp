using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dreamness.RA3.Map.Automation.Storage;

internal static class AutomationJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly JsonSerializerOptions JsonlOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken)
                .ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new AutomationException(
                "VALIDATION_FAILED",
                $"无法解析元数据文件: {path}",
                innerException: ex);
        }
    }

    public static async Task WriteAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(value, Options);
        var tempPath = path + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, Encoding.UTF8, cancellationToken)
            .ConfigureAwait(false);
        File.Move(tempPath, path, overwrite: true);
    }

    public static T Deserialize<T>(JsonElement element)
    {
        try { return DeserializeArguments<T>(element); }
        catch (JsonException ex)
        {
            throw new AutomationException("INVALID_ARGUMENT", "命令参数类型或格式不正确。",
                details: new Dictionary<string, string> { ["path"] = ex.Path ?? "$" },
                innerException: ex);
        }
    }

    private static T DeserializeArguments<T>(JsonElement element)
    {
        if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            var empty = JsonSerializer.Deserialize<T>("{}", Options);
            if (empty == null)
            {
                throw new AutomationException("INVALID_ARGUMENT", "命令参数不能为空。");
            }

            return empty;
        }

        var value = element.Deserialize<T>(Options);
        if (value == null)
        {
            throw new AutomationException("INVALID_ARGUMENT", "无法解析命令参数。");
        }

        return value;
    }

    public static async Task AppendJsonlAsync(string path, object value, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var line = JsonSerializer.Serialize(value, JsonlOptions) + Environment.NewLine;
        await File.AppendAllTextAsync(path, line, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
    }
}
