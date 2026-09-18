using System.Text.RegularExpressions;

namespace Dreamness.RA3.Map.Automation.Catalog;

/// <summary>
/// Derives surface, theme and variant labels from a terrain texture name.
/// <para>
/// The engine exposes texture <em>names</em> and nothing else about them, so the name is the
/// only semantic signal available. Fortunately the library is structured as
/// `&lt;Surface&gt;_&lt;Theme&gt;&lt;Variant&gt;` (`Grass_Yucatan03`), and the theme token is the map
/// region the material was authored for (`CapeCod`, `Hawaii`, `Iceland`). That is exactly
/// the axis an author needs: pick the region, then the surface.
/// </para>
/// <para>
/// Names that do not match the shape are reported as `other`/`unknown` rather than guessed,
/// because a wrong material suggestion is worse than an admitted gap.
/// </para>
/// </summary>
public static class TextureSemantics
{
    public const string KindGround = "ground";
    public const string KindCliff = "cliff";
    public const string KindTransition = "transition";
    public const string KindShore = "shore";
    public const string KindStructure = "structure";
    public const string KindOther = "other";

    public const string UnknownSurface = "unknown";
    public const string UnknownTheme = "unknown";

    /// <summary>Surfaces observed in the shipped library, mapped to a coarse role.</summary>
    private static readonly Dictionary<string, string> Surfaces = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Grass"] = KindGround,
        ["Dirt"] = KindGround,
        ["Sand"] = KindGround,
        ["Snow"] = KindGround,
        ["Mud"] = KindGround,
        ["Gravel"] = KindGround,
        ["Rock"] = KindGround,
        ["Pavement"] = KindGround,
        ["Pave"] = KindGround,
        ["Asphalt"] = KindGround,
        ["Cliff"] = KindCliff,
        ["Transition"] = KindTransition,
        ["Reef"] = KindShore,
        ["Dock"] = KindStructure,
        ["SteelDeck"] = KindStructure,
        ["FortressBlackEdge"] = KindStructure
    };

    /// <summary>Surfaces that are engine furniture rather than art direction.</summary>
    private static readonly HashSet<string> NotArtSurfaces = new(StringComparer.OrdinalIgnoreCase) { "BB", "RA3", "RA3Grid" };

    private static readonly Regex Pattern = new("^(?<surface>[A-Za-z][A-Za-z0-9]*)_(?<theme>[A-Za-z][A-Za-z0-9]*?)(?<variant>[0-9]+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Labels every name at once, because themes are also normalised against each other first
    /// (the library contains both `Heidel` and `Heidelberg`, and they are the same region).
    /// </summary>
    public static IReadOnlyList<TextureAsset> DescribeAll(IEnumerable<string> names)
    {
        var ordered = names.Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var split = ordered.ToDictionary(n => n, Split, StringComparer.Ordinal);
        var canonical = CanonicalThemes(split.Values.Select(s => s.Theme));
        return ordered.Select(n =>
        {
            var (surface, theme, variant, kind) = split[n];
            return new TextureAsset(n, surface, canonical(theme), variant, kind,
                kind is KindTransition, kind is KindStructure);
        }).ToArray();
    }

    internal static (string Surface, string Theme, int? Variant, string Kind) Split(string name)
    {
        var match = Pattern.Match(name);
        if (!match.Success)
        {
            // Names such as "RA3Grid1" carry no separator: keep the whole token as the surface.
            var bare = Regex.Match(name, "^(?<surface>[A-Za-z][A-Za-z]*?)(?<variant>[0-9]+)?$");
            var surface = bare.Success ? bare.Groups["surface"].Value : UnknownSurface;
            return (surface, UnknownTheme, bare.Success && bare.Groups["variant"].Success
                ? int.Parse(bare.Groups["variant"].Value, System.Globalization.CultureInfo.InvariantCulture) : null,
                NotArtSurfaces.Contains(surface) ? KindOther : KindOther);
        }

        var surfaceName = match.Groups["surface"].Value;
        var theme = match.Groups["theme"].Value;
        var variant = match.Groups["variant"].Success
            ? int.Parse(match.Groups["variant"].Value, System.Globalization.CultureInfo.InvariantCulture) : (int?)null;
        var kind = !Surfaces.TryGetValue(surfaceName, out var role) || NotArtSurfaces.Contains(surfaceName)
            ? KindOther : role;
        if (surfaceName.Equals("Pave", StringComparison.OrdinalIgnoreCase)) surfaceName = "Pavement";
        return (surfaceName, theme, variant, kind);
    }

    /// <summary>
    /// Collapses a theme that the library truncated onto its full spelling: `Heidel` is a
    /// clipped `Heidelberg`, and both spellings ship in the same install.
    /// <para>
    /// Only a remainder with no uppercase counts as a truncation. A camel-case remainder is a
    /// different place, not a clipped one: merging `Geneva` into `GenevaClockA` would hide 11
    /// real Geneva textures behind a name nobody would search for.
    /// </para>
    /// </summary>
    private static Func<string, string> CanonicalThemes(IEnumerable<string> themes)
    {
        var distinct = themes.Where(t => t != UnknownTheme).Distinct(StringComparer.Ordinal).ToArray();
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var theme in distinct)
        {
            var longer = distinct
                .Where(other => other.Length > theme.Length
                    && other.StartsWith(theme, StringComparison.Ordinal)
                    && IsClippedContinuation(other.AsSpan(theme.Length)))
                .OrderBy(other => other.Length).ThenBy(other => other, StringComparer.Ordinal)
                .FirstOrDefault();
            map[theme] = longer ?? theme;
        }
        return theme => map.TryGetValue(theme, out var canonical) ? canonical : theme;
    }

    private static bool IsClippedContinuation(ReadOnlySpan<char> remainder)
    {
        if (remainder.Length == 0) return false;
        foreach (var character in remainder)
        {
            if (char.IsUpper(character)) return false;
        }
        return true;
    }
}
