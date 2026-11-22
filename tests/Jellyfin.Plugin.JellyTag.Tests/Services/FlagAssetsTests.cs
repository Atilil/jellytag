using Jellyfin.Plugin.JellyTag.Services;

namespace Jellyfin.Plugin.JellyTag.Tests.Services;

public class FlagAssetsTests
{
    // A code without its embedded SVG would detect the language but draw nothing.
    [Fact]
    public void EveryKnownFlag_HasAnEmbeddedSvg()
    {
        var resources = typeof(QualityDetectionService).Assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Assets.flag-"))
            .Select(name => name[(name.IndexOf(".Assets.", StringComparison.Ordinal) + ".Assets.".Length)..])
            .ToHashSet();

        var missing = QualityDetectionService.KnownFlagCodes
            .Select(code => $"flag-{code}.svg")
            .Where(file => !resources.Contains(file));

        Assert.Empty(missing);
    }
}
