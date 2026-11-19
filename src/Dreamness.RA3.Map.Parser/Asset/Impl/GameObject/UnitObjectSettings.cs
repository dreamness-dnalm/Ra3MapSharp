using Dreamness.Ra3.Map.Parser.Asset.Collection.Property;

namespace Dreamness.Ra3.Map.Parser.Asset.Impl.GameObject;

/// <summary>
/// Official World Builder object property keys and their stored types.
/// The property bag already keeps every entry; this catalog is how those entries are read and written
/// without changing a float into an int or dropping <c>uniqueID</c>.
/// </summary>
public static class UnitObjectSettings
{
    public enum ValueKind { Bool, Int, Float, String }

    public readonly record struct Field(string Key, ValueKind Kind);

    public const string UniqueId = "uniqueID";
    public const string Name = "objectName";
    public const string Owner = "originalOwner";
    public const string EventsList = "objectEventsList";
    public const string BaseName = "objectBaseName";
    public const string BasePhase = "objectBasePhase";
    public const string BasePriority = "objectBasePriority";
    public const string Layer = "objectLayer";
    public const string PrototypeScale = "objectPrototypeScale";
    public const string AlignToTerrain = "alignToTerrain";
    public const string Time = "objectTime";
    public const string Weather = "objectWeather";
    public const string ExportWithScript = "exportWithScript";
    public const string InitialHealth = "objectInitialHealth";
    public const string MaxHPs = "objectMaxHPs";
    public const string ExperienceLevel = "objectExperienceLevel";
    public const string Veterancy = "objectVeterancy";
    public const string InitialStance = "objectInitialStance";
    public const string Aggressiveness = "objectAggressiveness";
    public const string VisionRangeMod = "objectVisionRangeMod";
    public const string VisualRange = "objectVisualRange";
    public const string ShroudClearingDistance = "objectShroudClearingDistance";
    public const string StoppingDistance = "objectStoppingDistance";
    public const string Enabled = "objectEnabled";
    public const string Unsellable = "objectUnsellable";
    public const string Targetable = "objectTargetable";
    public const string Indestructible = "objectIndestructible";
    public const string Sleeping = "objectSleeping";
    public const string RecruitableAI = "objectRecruitableAI";
    public const string Powered = "objectPowered";
    public const string IsABase = "objectIsABase";
    public const string Selectable = "objectSelectable";
    public const string UpgradesList = "objectUpgradesList";
    public const string SoundAmbient = "objectSoundAmbient";
    public const string SoundCustomized = "objectSoundAmbientCustomized";
    public const string SoundEnabled = "objectSoundAmbientEnabled";
    public const string SoundLooping = "objectSoundAmbientLooping";
    public const string SoundPriority = "objectSoundAmbientPriority";
    public const string SoundVolume = "objectSoundAmbientVolume";
    public const string SoundMinVolume = "objectSoundAmbientMinVolume";
    public const string SoundMinRange = "objectSoundAmbientMinRange";
    public const string SoundMaxRange = "objectSoundAmbientMaxRange";

    public static readonly Field[] Fields =
    {
        new(Name, ValueKind.String),
        new(Owner, ValueKind.String),
        new(EventsList, ValueKind.String),
        new(BaseName, ValueKind.String),
        new(BasePhase, ValueKind.Int),
        new(BasePriority, ValueKind.Int),
        new(Layer, ValueKind.String),
        new(PrototypeScale, ValueKind.Float),
        new(AlignToTerrain, ValueKind.Bool),
        new(Time, ValueKind.Int),
        new(Weather, ValueKind.Int),
        new(ExportWithScript, ValueKind.Bool),
        new(InitialHealth, ValueKind.Int),
        new(MaxHPs, ValueKind.Int),
        new(ExperienceLevel, ValueKind.Int),
        new(Veterancy, ValueKind.Int),
        new(InitialStance, ValueKind.Int),
        new(Aggressiveness, ValueKind.Int),
        new(VisionRangeMod, ValueKind.Float),
        new(VisualRange, ValueKind.Int),
        new(ShroudClearingDistance, ValueKind.Int),
        new(StoppingDistance, ValueKind.Float),
        new(Enabled, ValueKind.Bool),
        new(Unsellable, ValueKind.Bool),
        new(Targetable, ValueKind.Bool),
        new(Indestructible, ValueKind.Bool),
        new(Sleeping, ValueKind.Bool),
        new(RecruitableAI, ValueKind.Bool),
        new(Powered, ValueKind.Bool),
        new(IsABase, ValueKind.Bool),
        new(Selectable, ValueKind.Bool),
        new(UpgradesList, ValueKind.String),
        new(SoundAmbient, ValueKind.String),
        new(SoundCustomized, ValueKind.Bool),
        new(SoundEnabled, ValueKind.Bool),
        new(SoundLooping, ValueKind.Bool),
        new(SoundPriority, ValueKind.Int),
        new(SoundVolume, ValueKind.Float),
        new(SoundMinVolume, ValueKind.Float),
        new(SoundMinRange, ValueKind.Float),
        new(SoundMaxRange, ValueKind.Float),
        new("objectRadius", ValueKind.Float),
        new("scorchType", ValueKind.Int),
        new("objectGroupNumber", ValueKind.Int),
        new("objectThreatFinderRadius", ValueKind.Float),
        new("objectDisabledGameplayTypes", ValueKind.String),
        new("objectSoundAmbientLoopCount", ValueKind.Int)
    };

    private static readonly Dictionary<string, ValueKind> Kinds = Fields.ToDictionary(field => field.Key, field => field.Kind, StringComparer.Ordinal);

    public static bool IsUniqueId(string key) => key.Equals(UniqueId, StringComparison.OrdinalIgnoreCase);

    public static Dictionary<string, object> Read(AssetProperties properties)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var pair in properties.PropertiesDict)
        {
            if (IsUniqueId(pair.Key) || pair.Value.Value is null) continue;
            result[pair.Key] = pair.Value.Value;
        }
        return result;
    }

    /// <summary>
    /// Writes <paramref name="values"/> and removes every other property except <c>uniqueID</c>.
    /// Same-typed equal values do not mark the property modified.
    /// </summary>
    public static void Apply(AssetProperties properties, IReadOnlyDictionary<string, object> values)
    {
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { UniqueId };
        foreach (var pair in values)
        {
            if (IsUniqueId(pair.Key) || pair.Value is null) continue;
            properties.PutProperty(pair.Key, Coerce(pair.Key, pair.Value, properties));
            keep.Add(pair.Key);
        }
        foreach (var name in properties.PropertyNames)
        {
            if (!keep.Contains(name)) properties.RemoveProperty(name);
        }
    }

    public static void Put(AssetProperties properties, string key, object value)
    {
        if (IsUniqueId(key)) return;
        properties.PutProperty(key, Coerce(key, value, properties));
    }

    public static object Coerce(string key, object value, AssetProperties? existing = null)
    {
        var kind = KindOf(key, value, existing);
        return kind switch
        {
            ValueKind.Bool => Convert.ToBoolean(value),
            ValueKind.Int => Convert.ToInt32(value),
            ValueKind.Float => Convert.ToSingle(value),
            _ => Convert.ToString(value) ?? ""
        };
    }

    private static ValueKind KindOf(string key, object value, AssetProperties? existing)
    {
        if (Kinds.TryGetValue(key, out var known)) return known;
        if (existing is not null && existing.PropertiesDict.TryGetValue(key, out var property) && property.Value is not null)
        {
            return property.Value switch
            {
                bool => ValueKind.Bool,
                int or long => ValueKind.Int,
                float or double => ValueKind.Float,
                _ => ValueKind.String
            };
        }
        return value switch
        {
            bool => ValueKind.Bool,
            int or long => ValueKind.Int,
            float or double => ValueKind.Float,
            _ => ValueKind.String
        };
    }
}
