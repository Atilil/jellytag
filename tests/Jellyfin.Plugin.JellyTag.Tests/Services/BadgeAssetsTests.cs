using Jellyfin.Plugin.JellyTag.Services;

namespace Jellyfin.Plugin.JellyTag.Tests.Services;

public class BadgeAssetsTests
{
    private static HashSet<string> EmbeddedAssets() =>
        typeof(QualityDetectionService).Assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Assets."))
            .Select(name => name[(name.IndexOf(".Assets.", StringComparison.Ordinal) + ".Assets.".Length)..])
            .ToHashSet();

    // A code without its embedded SVG would detect the language but draw nothing.
    [Fact]
    public void EveryKnownFlag_HasAnEmbeddedSvg()
    {
        var resources = EmbeddedAssets();

        var missing = QualityDetectionService.KnownFlagCodes
            .Select(code => $"flag-{code}.svg")
            .Where(file => !resources.Contains(file));

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryKnownDvProfile_HasAnEmbeddedSvg()
    {
        var resources = EmbeddedAssets();
        var missing = QualityDetectionService.KnownDvProfiles
            .Select(profile => QualityDetectionService.DvProfileFileName($"DV P{profile}"))
            .Where(file => !resources.Contains(file));

        Assert.Empty(missing);
    }

    // #11: the rating icons drawn before the text.
    [Fact]
    public void EveryRatingIcon_IsEmbedded()
    {
        var resources = EmbeddedAssets();
        var missing = QualityDetectionService.DetectRatings(new MediaBrowser.Controller.Entities.Movies.Movie { CommunityRating = 7f, CriticRating = 70f })
            .Select(b => b.IconFileName)
            .Where(file => !resources.Contains(file));

        Assert.Empty(missing);
    }
}
