using Dreamness.Ra3.Map.Parser.Asset.Base;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Lighting;
using Dreamness.Ra3.Map.Parser.Asset.Impl.PostEffect;
using Dreamness.Ra3.Map.Parser.Asset.Impl.Fog;
using Dreamness.Ra3.Map.Parser.Core.Base;
using Dreamness.Ra3.Map.Parser.Util;

namespace Dreamness.Ra3.Map.Facade.Core;

public partial class Ra3MapFacade
{
    // Reading optional data never inserts/replaces a chunk. Reject errored typed assets so
    // a failed tolerant parse cannot accidentally overwrite the original payload.
    private T? GetEditableAsset<T>(string name) where T : BaseAsset
    {
        if (!ra3Map.Context.AssetDict.TryGetValue(name, out var asset)) return null;
        if (asset is not T typed || asset.Errored)
            throw new InvalidOperationException($"Cannot edit unparsed {name} (version {asset.Version}).", asset.ErrorException);
        return typed;
    }

    private T EnsureEditableAsset<T>(string name, Func<BaseContext, T> create) where T : BaseAsset
    {
        var asset = GetEditableAsset<T>(name);
        if (asset != null) return asset;
        asset = create(ra3Map.Context);
        ra3Map.Context.RegisterAsset(asset);
        return asset;
    }

    /// <summary>Live lighting data, or null when the map has no lighting chunk. Time/config indices are zero based.</summary>
    public GlobalLightingAsset? GetGlobalLighting() => GetEditableAsset<GlobalLightingAsset>(AssetNameConst.GlobalLighting);

    /// <summary>Selects one of the four stored time-of-day configurations (0..3).</summary>
    public void SetLightingTime(int time)
    {
        if (time < 0 || time > 3) throw new ArgumentOutOfRangeException(nameof(time));
        EnsureEditableAsset(AssetNameConst.GlobalLighting, GlobalLightingAsset.Default).Time = time;
    }

    public void SetLightingConfiguration(int index, GlobalLightingConfig configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (index < 0 || index > 3) throw new ArgumentOutOfRangeException(nameof(index));
        EnsureEditableAsset(AssetNameConst.GlobalLighting, GlobalLightingAsset.Default).Configs[index] = configuration;
    }

    public void SetShadowColor(MapColorArgb color)
    {
        ArgumentNullException.ThrowIfNull(color);
        EnsureEditableAsset(AssetNameConst.GlobalLighting, GlobalLightingAsset.Default).ShadowColor = color;
    }

    public void SetNoCloudFactor(ColorRgbF color)
    {
        ArgumentNullException.ThrowIfNull(color);
        EnsureEditableAsset(AssetNameConst.GlobalLighting, GlobalLightingAsset.Default).NoCloudFactor = color;
    }

    /// <summary>A snapshot of the effect list containing live editable effect records.</summary>
    public List<PostEffect> GetPostEffects() => GetEditableAsset<PostEffectsChunkAsset>(AssetNameConst.PostEffectsChunk)?.PostEffects.ToList() ?? new();

    public PostEffect AddPostEffect(string name, params PostEffectParameter[] parameters)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An effect name is required.", nameof(name));
        ArgumentNullException.ThrowIfNull(parameters);
        if (parameters.Any(p => p == null)) throw new ArgumentException("Parameters cannot contain null.", nameof(parameters));
        if (GetPostEffects().Any(p => p.Name == name)) throw new ArgumentException("Duplicate post effect.", nameof(name));
        var effect = PostEffect.Of(name, parameters);
        EnsureEditableAsset(AssetNameConst.PostEffectsChunk, EmptyPostEffects).PostEffects.Add(effect);
        return effect;
    }

    public bool RemovePostEffect(string name)
    {
        var asset = GetEditableAsset<PostEffectsChunkAsset>(AssetNameConst.PostEffectsChunk);
        var effect = asset?.PostEffects.FirstOrDefault(p => p.Name == name);
        if (effect == null) return false;
        asset!.PostEffects.Remove(effect); return true;
    }

    public void SetPostEffectParameter(string effectName, string parameterName, object value)
    {
        var effect = GetPostEffects().FirstOrDefault(p => p.Name == effectName) ?? throw new KeyNotFoundException($"Effect '{effectName}' not found.");
        var parameter = effect.Parameters.FirstOrDefault(p => p.Name == parameterName) ?? throw new KeyNotFoundException($"Parameter '{parameterName}' not found.");
        parameter.SetValue(value);
    }

    private static PostEffectsChunkAsset EmptyPostEffects(BaseContext context)
    {
        var asset = new PostEffectsChunkAsset(); asset.ApplyBasicInfo(context); asset.MarkModified(); return asset;
    }

    /// <summary>Distance fog, or null when absent; unrelated to fog of war.</summary>
    public FogSettingsAsset? GetFogSettings() => GetEditableAsset<FogSettingsAsset>(AssetNameConst.FogSettings);

    public void ConfigureFog(bool enabled, float rangeStart, float rangeEnd, ColorRgbF color)
    {
        ArgumentNullException.ThrowIfNull(color);
        if (!float.IsFinite(rangeStart) || !float.IsFinite(rangeEnd) || rangeStart > rangeEnd)
            throw new ArgumentException("Fog distances must be finite and ordered.");
        var asset = EnsureEditableAsset(AssetNameConst.FogSettings, FogSettingsAsset.Default);
        asset.Enabled = enabled; asset.RangeStart = rangeStart; asset.RangeEnd = rangeEnd; asset.Color = color;
    }
}
