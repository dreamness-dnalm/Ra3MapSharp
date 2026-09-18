using Dreamness.RA3.Map.Automation;
using Dreamness.RA3.Map.Automation.Catalog;

namespace Dreamness.RA3.Map.Agent;

/// <summary>
/// Builds the frozen asset catalogue from an editor install (--build-catalog).
/// Kept in the host rather than the kernel because it reads machine-local editor data; the
/// resulting catalogue is a plain file the kernel never needs to know about.
/// </summary>
internal static class CatalogBuildCommand
{
    public static int Run(ObjectCatalog objects, string objectCategoryPath, string? screenshotsDirectory, string outputPath)
    {
        var categories = ThumbnailIndex.ReadCategories(objectCategoryPath);

        ThumbnailIndex? thumbnails = null;
        if (screenshotsDirectory != null)
        {
            thumbnails = ThumbnailIndex.Scan(screenshotsDirectory);
        }
        else
        {
            Console.Error.WriteLine("Warning: no editor screenshot directory found; building without thumbnails.");
        }

        var catalog = AssetCatalog.Build(objects, categories, thumbnails, DateTimeOffset.UtcNow);
        catalog.Save(outputPath);

        Console.WriteLine("Asset catalogue written to " + Path.GetFullPath(outputPath));
        Console.WriteLine("  catalogHash       " + catalog.CatalogHash);
        Console.WriteLine("  textures          " + catalog.Textures.Count);
        Console.WriteLine("  objects           " + catalog.Objects.Count);
        Console.WriteLine("  categories        " + catalog.Categories.Count);
        Console.WriteLine("  editorScreenshots " + catalog.Thumbnails.Files
            + "  covering " + catalog.Thumbnails.ObjectsWithScreenshot + "/" + catalog.Thumbnails.Objects + " objects"
            + "  unmatched " + catalog.Thumbnails.UnmatchedFiles);
        Console.WriteLine("  surfaces          " + string.Join(", ", catalog.Textures
            .GroupBy(t => t.Surface, StringComparer.Ordinal).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key + "=" + g.Count())));
        Console.WriteLine("  themes            " + string.Join(", ", catalog.Textures
            .Where(t => t.Theme != TextureSemantics.UnknownTheme)
            .GroupBy(t => t.Theme, StringComparer.Ordinal).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Take(14).Select(g => g.Key + "=" + g.Count())));
        return 0;
    }
}
