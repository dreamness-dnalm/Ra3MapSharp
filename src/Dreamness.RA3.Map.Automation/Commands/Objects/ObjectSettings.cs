using System.Text.Json;
using Dreamness.Ra3.Map.Facade.Core;
using Dreamness.Ra3.Map.Parser.Asset.Collection.Property;
using Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;

namespace Dreamness.RA3.Map.Automation.Commands.Objects;

internal static class ObjectSettings
{
    private static readonly HashSet<string> Booleans = new(StringComparer.Ordinal)
    { "objectEnabled", "objectIndestructible", "objectUnsellable", "objectPowered", "objectRecruitableAI", "objectTargetable", "objectSleeping" };
    private static readonly HashSet<string> Integers = new(StringComparer.Ordinal)
    { "objectInitialHealth", "objectBasePriority", "objectBasePhase" };

    internal static Dictionary<string, JsonElement> Read(UnitObjectWrap obj) => obj.Properties.PropertiesDict
        .Where(p => Booleans.Contains(p.Key) || Integers.Contains(p.Key))
        .ToDictionary(p => p.Key, p => JsonSerializer.SerializeToElement(p.Value.Value), StringComparer.Ordinal);

    internal static void Apply(Ra3MapFacade map, UnitObjectWrap obj, string? ownerTeam, Dictionary<string, JsonElement>? settings)
    {
        if (ownerTeam != null)
        {
            var teams = map.GetTeams().Where(t => t.FullName == ownerTeam).ToArray();
            if (teams.Length != 1 || !map.GetPlayers().Any(p => p.Name == teams[0].OwnerPlayerName))
                throw new AutomationException("INVALID_OWNER", "ownerTeam 必须唯一匹配已有队伍，且该队伍的玩家必须存在。");
            obj.BelongToTeam = ownerTeam;
        }
        foreach (var pair in settings ?? new Dictionary<string, JsonElement>())
        {
            object value;
            AssetProperty.AssetPropertyType type;
            if (Booleans.Contains(pair.Key) && pair.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            { value = pair.Value.GetBoolean(); type = AssetProperty.AssetPropertyType.boolType; }
            else if (Integers.Contains(pair.Key) && pair.Value.ValueKind == JsonValueKind.Number && pair.Value.TryGetInt32(out var number))
            { value = number; type = AssetProperty.AssetPropertyType.intType; }
            else throw new AutomationException("INVALID_ARGUMENT", "不支持的对象属性或属性类型: " + pair.Key);
            // Replace rather than SetProperty: imported properties may have a different encoded type.
            obj.Properties.RemoveProperty(pair.Key);
            obj.Properties.PutProperty(pair.Key, type, value);
        }
    }
}
