using System.Text.Json;
using Dreamness.RA3.Map.Automation.Commands.Abstractions;
using Dreamness.RA3.Map.Automation.Executor;

namespace Dreamness.RA3.Map.Agent.Protocol;

public static class CommandSchemas
{
    public static IReadOnlyDictionary<string, JsonElement> Definitions { get; } = Load();
    private static Dictionary<string, JsonElement> Load()
    {
        var assembly = typeof(CommandSchemas).Assembly;
        using var stream = assembly.GetManifestResourceStream(assembly.GetManifestResourceNames().Single(n => n.EndsWith("command-schemas.json")))!;
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stream)!;
    }

    public static object[] Tools()
    {
        var effects = CommandRegistry.CreateDefault().Describe().ToDictionary(c => c.Name, c => c.Effect);
        return Definitions.OrderBy(p => p.Key, StringComparer.Ordinal).Select(pair =>
        {
            var hasEffect = effects.TryGetValue(pair.Key, out var effect);
            var session = hasEffect ? effect != CommandEffect.Session : pair.Key is "map.close" or "preview.start" or "batch.execute" or "diagnostics.render";
            var revision = hasEffect && effect is CommandEffect.Mutation or CommandEffect.History or CommandEffect.Export || pair.Key == "batch.execute";
            var readOnly = hasEffect ? effect == CommandEffect.Query : pair.Key is "system.capabilities" or "system.schema" or "assets.objects" or "assets.catalog_info" or "assets.search" or "assets.album" or "art.rules" or "footprints.get" or "footprints.list" or "jobs.status";
            if (pair.Key is "edits.prepare" or "edits.discard" or "design.prepare") readOnly = false;
            var required = new List<string>();
            if (session) required.Add("sessionId");
            if (revision) required.Add("expectedRevision");
            if (pair.Value.GetProperty("argumentsSchema").GetProperty("required").GetArrayLength() > 0) required.Add("arguments");
            return (object)new
            {
                name = "ra3_" + pair.Key.Replace('.', '_'),
                description = pair.Value.GetProperty("description").GetString(),
                inputSchema = new
                {
                    type = "object", additionalProperties = false, required,
                    properties = new Dictionary<string, object>
                    {
                        ["requestId"] = new { type = "string", minLength = 1, description = "Reuse a stable requestId only when retrying the identical logical edit." },
                        ["sessionId"] = new { type = "string", description = "Returned sessionId, or $current for the last opened map in this process." },
                        ["expectedRevision"] = new { type = "integer", minimum = 0, description = "Required for writes; use the current returned revision." },
                        ["arguments"] = pair.Value.GetProperty("argumentsSchema")
                    }
                },
                annotations = new { readOnlyHint = readOnly, destructiveHint = !readOnly, openWorldHint = false }
            };
        }).ToArray();
    }

    // Validator for the finite schema vocabulary emitted by build_command_schemas.py.
    // It is intentionally not exposed as a general-purpose JSON Schema implementation.
    public static void Validate(JsonElement value, JsonElement schema, string path = "arguments")
    {
        if (schema.TryGetProperty("anyOf", out var choices))
        {
            foreach (var choice in choices.EnumerateArray())
                try { Validate(value, choice, path); return; } catch (ArgumentException) { }
            throw new ArgumentException(path + " does not match any allowed shape.");
        }
        if (schema.TryGetProperty("type", out var type))
        {
            var valid = type.GetString() switch
            {
                "object" => value.ValueKind == JsonValueKind.Object,
                "array" => value.ValueKind == JsonValueKind.Array,
                "string" => value.ValueKind == JsonValueKind.String,
                "number" => value.ValueKind == JsonValueKind.Number && double.IsFinite(value.GetDouble()),
                "integer" => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
                "boolean" => value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                _ => throw new InvalidOperationException("Unsupported embedded schema type.")
            };
            if (!valid) throw new ArgumentException(path + " must be " + type.GetString() + ".");
        }
        if (schema.TryGetProperty("enum", out var options)
            && !options.EnumerateArray().Any(option => option.ValueKind == value.ValueKind && option.ToString() == value.ToString()))
            throw new ArgumentException(path + " is not an allowed value.");
        if (value.ValueKind == JsonValueKind.Object)
        {
            var properties = schema.GetProperty("properties");
            foreach (var field in schema.GetProperty("required").EnumerateArray())
                if (!value.TryGetProperty(field.GetString()!, out _)) throw new ArgumentException(path + "." + field.GetString() + " is required.");
            foreach (var field in value.EnumerateObject())
            {
                if (!properties.TryGetProperty(field.Name, out var child)) throw new ArgumentException(path + "." + field.Name + " is not recognized.");
                Validate(field.Value, child, path + "." + field.Name);
            }
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            Range(value.GetArrayLength(), "minItems", "maxItems");
            var index = 0;
            foreach (var item in value.EnumerateArray()) Validate(item, schema.GetProperty("items"), path + "[" + index++ + "]");
        }
        if (value.ValueKind == JsonValueKind.String) Range(value.GetString()!.Length, "minLength", "maxLength");
        if (value.ValueKind == JsonValueKind.Number)
        {
            var n = value.GetDouble();
            Range(n, "minimum", "maximum");
            if (schema.TryGetProperty("exclusiveMinimum", out var min) && n <= min.GetDouble()
                || schema.TryGetProperty("exclusiveMaximum", out var max) && n >= max.GetDouble())
                throw new ArgumentException(path + " exceeds the allowed range.");
        }
        void Range(double n, string minKey, string maxKey)
        {
            if (schema.TryGetProperty(minKey, out var min) && n < min.GetDouble()
                || schema.TryGetProperty(maxKey, out var max) && n > max.GetDouble())
                throw new ArgumentException(path + " exceeds the allowed range.");
        }
    }
}
