using System.Xml.Serialization;
using Jellyfin.Plugin.JellyTag.Configuration;

namespace Jellyfin.Plugin.JellyTag.Tests.Configuration;

public class SubtitlePanelMigrationTests
{
    // Shape of a configuration saved by 3.1.0: VOST settings, no subtitle panel.
    private const string Saved310 = """
        <?xml version="1.0" encoding="utf-8"?>
        <PluginConfiguration xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:xsd="http://www.w3.org/2001/XMLSchema">
          <Enabled>true</Enabled>
          <PosterConfig>
            <LanguagePanel>
              <Position>BottomRight</Position>
              <Order>2</Order>
              <SizePercent>12</SizePercent>
            </LanguagePanel>
            <ShowVostIndicator>false</ShowVostIndicator>
            <VostBgColor>#123456</VostBgColor>
            <VostCornerRadius>7</VostCornerRadius>
          </PosterConfig>
        </PluginConfiguration>
        """;

    private static PluginConfiguration Load(string xml)
    {
        using var reader = new StringReader(xml);
        return (PluginConfiguration)new XmlSerializer(typeof(PluginConfiguration)).Deserialize(reader)!;
    }

    [Fact]
    public void Saved310_GetsASubtitlePanelLookingLikeItsVostIndicator()
    {
        var config = Load(Saved310);
        Assert.Null(config.PosterConfig.SubtitlePanel);

        config.EnsureSubtitlePanels();

        var panel = config.PosterConfig.SubtitlePanel!;
        Assert.False(panel.Enabled);
        Assert.Equal(BadgePosition.BottomRight, panel.Position);
        Assert.Equal(2, panel.Order);
        Assert.Equal(12, panel.SizePercent);
        Assert.Equal("#123456", panel.TextBgColor);
        Assert.Equal(7, panel.TextCornerRadius);
        Assert.Equal(SubtitleLabel.Vost, panel.SubtitleLabel);
        Assert.True(config.PosterConfig.LanguagePanel.HideUndefinedLanguage);
    }

    [Fact]
    public void NewConfiguration_ShowsLanguageCodes()
    {
        var config = new PluginConfiguration();
        config.EnsureSubtitlePanels();

        Assert.Equal(SubtitleLabel.Code, config.PosterConfig.SubtitlePanel!.SubtitleLabel);
    }
}
