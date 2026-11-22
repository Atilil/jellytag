using Jellyfin.Plugin.JellyTag.Services;

namespace Jellyfin.Plugin.JellyTag.Tests.Services;

public class FormatRuntimeTests
{
    [Theory]
    [InlineData(45, "45m")]
    [InlineData(112, "1h 52m")]
    [InlineData(65, "1h 05m")]
    [InlineData(120, "2h 00m")]
    public void Minutes(int minutes, string expected)
    {
        Assert.Equal(expected, QualityDetectionService.FormatRuntime(TimeSpan.FromMinutes(minutes).Ticks));
    }

    [Fact]
    public void RoundsToTheNearestMinute()
    {
        Assert.Equal("1h 00m", QualityDetectionService.FormatRuntime(TimeSpan.FromSeconds(59 * 60 + 40).Ticks));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(20 * TimeSpan.TicksPerSecond)]
    public void UnknownOrUnderAMinute_IsNull(long? ticks)
    {
        Assert.Null(QualityDetectionService.FormatRuntime(ticks));
    }
}
