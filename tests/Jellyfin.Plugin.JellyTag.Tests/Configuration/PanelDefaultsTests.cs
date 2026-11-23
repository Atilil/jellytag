using Jellyfin.Plugin.JellyTag.Configuration;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JellyTag.Tests.Configuration;

public class PanelDefaultsTests
{
    [Theory]
    [InlineData(ImageType.Primary, 15)]
    [InlineData(ImageType.Thumb, 10)]
    [InlineData(ImageType.Backdrop, 10)]
    [InlineData(ImageType.Logo, 15)]
    public void EveryCategory_HasADefault_SizedForTheImageType(ImageType imageType, int size)
    {
        var defaults = PanelDefaults.For(imageType);

        Assert.Equal(Enum.GetValues<PanelType>().Order(), defaults.Panels.Keys.Order());
        Assert.All(defaults.Panels, p => Assert.Equal(p.Key, p.Value.Type));
        Assert.Equal(size, defaults.Panels[PanelType.Resolution].SizePercent);
        Assert.Equal(size, defaults.Panels[PanelType.Library].SizePercent);
    }

    // Panels that are off in a fresh installation still get their own defaults.
    [Fact]
    public void OptionalPanels_KeepTheirOwnDefaults()
    {
        var panels = PanelDefaults.For(ImageType.Primary).Panels;

        Assert.Equal(BadgeStyle.Text, panels[PanelType.Runtime].Style);
        Assert.Equal(new[] { "runtime" }, panels[PanelType.Runtime].EnabledBadges);
        Assert.Equal(BadgeDisplayMode.All, panels[PanelType.Rating].ShowMode);
        Assert.Equal(BadgeStyle.Text, panels[PanelType.Library].Style);
    }

    [Fact]
    public void Zone_IsTheFreshInstallationZone_WithoutPanels()
    {
        var zone = PanelDefaults.For(ImageType.Thumb).Zone;

        Assert.Equal(ZoneArrangement.Columns, zone.Arrangement);
        Assert.Equal(2.5f, zone.MarginXPercent);
        Assert.Empty(zone.Lines);
    }
}
