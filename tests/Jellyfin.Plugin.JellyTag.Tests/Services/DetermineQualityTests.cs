using Jellyfin.Plugin.JellyTag.Services;

namespace Jellyfin.Plugin.JellyTag.Tests.Services;

public class DetermineQualityTests
{
    [Theory]
    [InlineData(3840, 2160, VideoQuality.UHD4K)]
    [InlineData(3840, 1600, VideoQuality.UHD4K)]    // 4K scope
    [InlineData(1920, 1080, VideoQuality.FHD1080p)]
    [InlineData(1920, 800, VideoQuality.FHD1080p)]  // 1080p scope
    [InlineData(1280, 720, VideoQuality.HD720p)]
    [InlineData(960, 720, VideoQuality.HD720p)]     // 720p 4:3
    [InlineData(720, 576, VideoQuality.SD)]
    [InlineData(640, 480, VideoQuality.SD)]
    [InlineData(1080, 1920, VideoQuality.FHD1080p)] // portrait
    [InlineData(0, 0, VideoQuality.Unknown)]
    public void CommonResolutions(int width, int height, VideoQuality expected)
    {
        Assert.Equal(expected, QualityDetectionService.DetermineQuality(width, height));
    }

    // Narrower-than-16:9 1080p encodes, reported as 720p in #32 (4:3 shows) and #12.
    [Theory]
    [InlineData(1440, 1080)]
    [InlineData(1456, 1080)]
    [InlineData(1480, 1080)]
    [InlineData(1792, 1080)]
    [InlineData(1808, 1080)]
    public void Narrow1080p_IsFullHd(int width, int height)
    {
        Assert.Equal(VideoQuality.FHD1080p, QualityDetectionService.DetermineQuality(width, height));
    }
}
