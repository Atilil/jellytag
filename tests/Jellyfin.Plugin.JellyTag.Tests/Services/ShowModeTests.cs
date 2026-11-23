using Jellyfin.Plugin.JellyTag.Configuration;
using Jellyfin.Plugin.JellyTag.Services;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JellyTag.Tests.Services;

// Merged versions: detection, ordering and the Show filter chained as the render does it.
public class ShowModeTests
{
    private static MediaSourceInfo Version(int width, int height) => new()
    {
        MediaStreams = [new MediaStream { Type = MediaStreamType.Video, Width = width, Height = height, Codec = "h264" }]
    };

    private static List<string> Resolutions(BadgeDisplayMode mode, params MediaSourceInfo[] versions)
    {
        var detected = QualityDetectionService.OrderBestFirst(QualityDetectionService.MergeVersionBadges(versions, includeResolution: true));
        var panel = detected.Where(b => b.Category == BadgeCategory.Resolution).ToList();
        return ImageOverlayService.ApplyShowMode(mode, panel).Select(b => b.BadgeKey).ToList();
    }

    // Jellyfin lists the primary version first, here the 1080p one.
    [Fact]
    public void HighestKeepsTheBestVersion()
    {
        Assert.Equal(["4k"], Resolutions(BadgeDisplayMode.Highest, Version(1920, 1080), Version(3840, 2160)));
    }

    [Fact]
    public void AllShowsEveryVersionBestFirst()
    {
        Assert.Equal(["4k", "1080p"], Resolutions(BadgeDisplayMode.All, Version(1920, 1080), Version(3840, 2160)));
    }

    [Fact]
    public void SameResolutionInTwoVersionsIsOneBadge()
    {
        Assert.Equal(["1080p"], Resolutions(BadgeDisplayMode.All, Version(1920, 1080), Version(1920, 800)));
    }
}
