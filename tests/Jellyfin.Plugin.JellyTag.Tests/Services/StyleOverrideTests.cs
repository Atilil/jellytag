using Jellyfin.Plugin.JellyTag.Configuration;
using Jellyfin.Plugin.JellyTag.Services;

namespace Jellyfin.Plugin.JellyTag.Tests.Services;

public class StyleOverrideTests
{
    private static BadgePanelSettings Panel(params BadgeTypeStyleOverride[] overrides) =>
        new() { BadgeTypeOverrides = overrides.ToList() };

    private static BadgeTypeStyleOverride Override(string key, string color) =>
        new() { BadgeKey = key, BgColor = color };

    [Theory]
    [InlineData("7.1")]
    [InlineData("5.1")]
    [InlineData("stereo")]
    public void ChannelEntry_AppliesToEachLayout(string badgeKey)
    {
        var panel = Panel(Override("channel", "#FF0000"));

        Assert.Equal("#FF0000", ImageOverlayService.FindStyleOverride(badgeKey, panel)?.BgColor);
    }

    [Fact]
    public void OwnKey_WinsOverChannelEntry()
    {
        var panel = Panel(Override("channel", "#FF0000"), Override("5.1", "#00FF00"));

        Assert.Equal("#00FF00", ImageOverlayService.FindStyleOverride("5.1", panel)?.BgColor);
    }

    [Fact]
    public void ChannelEntry_IgnoredByOtherBadges()
    {
        var panel = Panel(Override("channel", "#FF0000"));

        Assert.Null(ImageOverlayService.FindStyleOverride("atmos", panel));
    }
}
