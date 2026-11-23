using System.Text.Json;
using Jellyfin.Plugin.JellyTag.Configuration;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JellyTag.Tests.Configuration;

public class ConfigMigrationTests
{
    // A real 3.3 export, read the way JellyTagController.ImportConfig reads it.
    private static PluginConfiguration LoadFixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Configuration", "Fixtures", name);
        var config = JsonSerializer.Deserialize<PluginConfiguration>(File.ReadAllText(path))!;
        config.EnsureSubtitlePanels();
        return config;
    }

    private static ProfileImageConfig Migrated(PluginConfiguration config, ImageType type) =>
        ConfigMigration.FromV3(config).Profiles.Single().Images.Single(i => i.ImageType == type);

    private static PanelType[][] Lines(BadgeZone zone) =>
        zone.Lines.Select(l => l.Panels.Select(p => p.Type).ToArray()).ToArray();

    [Fact]
    public void RealExport_PosterZones()
    {
        var poster = Migrated(LoadFixture("config-3.3.json"), ImageType.Primary);

        Assert.True(poster.Enabled);
        Assert.Equal(
            new[] { BadgePosition.TopLeft, BadgePosition.TopCenter, BadgePosition.BottomCenter },
            poster.Zones.Select(z => z.Position));

        var topLeft = poster.Zones[0];
        Assert.Equal(ZoneArrangement.Rows, topLeft.Arrangement);
        Assert.Equal(
            new[] { new[] { PanelType.Resolution }, new[] { PanelType.Hdr }, new[] { PanelType.Codec }, new[] { PanelType.Audio } },
            Lines(topLeft));
        Assert.Equal(0.5f, topLeft.MarginXPercent);
        Assert.Equal(0.5f, topLeft.MarginYPercent);
        Assert.Equal(0f, topLeft.GroupGapPercent);

        Assert.Equal(ZoneArrangement.Columns, poster.Zones[1].Arrangement);
        Assert.Equal(new[] { new[] { PanelType.Runtime } }, Lines(poster.Zones[1]));
    }

    [Fact]
    public void RealExport_RatingGap_IsHalved()
    {
        var poster = Migrated(LoadFixture("config-3.3.json"), ImageType.Primary);

        var rating = poster.Zones.Single(z => z.Position == BadgePosition.BottomCenter).Lines[0].Panels[0];
        Assert.Equal(PanelType.Rating, rating.Type);
        Assert.Equal(25f, rating.GapPercent);
        Assert.Equal("#ffffff", rating.TextBgColor);
        Assert.Equal(new[] { "rating", "critic" }, rating.EnabledBadges);
    }

    [Fact]
    public void RealExport_ThumbZones_UseTheFirstPanelMargin_AndDropDisabledPanels()
    {
        var thumb = Migrated(LoadFixture("config-3.3.json"), ImageType.Thumb);

        Assert.Null(thumb.SameAs);
        Assert.Equal(8, thumb.SizeReduction);
        Assert.Equal(
            new[] { BadgePosition.TopLeft, BadgePosition.BottomLeft, BadgePosition.BottomRight },
            thumb.Zones.Select(z => z.Position));

        var topLeft = thumb.Zones[0];
        Assert.Equal(
            new[] { new[] { PanelType.Resolution }, new[] { PanelType.Hdr }, new[] { PanelType.Codec } },
            Lines(topLeft));
        Assert.Equal(0f, topLeft.MarginXPercent);

        Assert.DoesNotContain(
            thumb.Zones.SelectMany(z => z.Lines).SelectMany(l => l.Panels),
            p => p.Type is PanelType.Audio or PanelType.Language or PanelType.Subtitle);
        Assert.Equal(ZoneArrangement.Columns, thumb.Zones[2].Arrangement);
    }

    [Fact]
    public void RealExport_OnlyADefaultProfile_AndTheFourImageTypes()
    {
        var (profiles, assignments) = ConfigMigration.FromV3(LoadFixture("config-3.3.json"));

        var profile = Assert.Single(profiles);
        Assert.Equal(BadgeProfile.DefaultProfileId, profile.Id);
        Assert.True(profile.ShowOnEpisodes);
        Assert.Equal(
            new[] { ImageType.Primary, ImageType.Thumb, ImageType.Backdrop, ImageType.Logo },
            profile.Images.Select(i => i.ImageType));
        Assert.False(profile.Images[2].Enabled);
        Assert.False(profile.Images[3].Enabled);
        Assert.Empty(assignments);
    }

    [Fact]
    public void ExcludedLibraries_BecomeNoProfileAssignments()
    {
        var config = new PluginConfiguration { ExcludedLibraryIds = new List<string> { "aaa", "bbb" } };

        var (_, assignments) = ConfigMigration.FromV3(config);

        Assert.Equal(new[] { "aaa", "bbb" }, assignments.Select(a => a.LibraryId));
        Assert.All(assignments, a => Assert.Null(a.ProfileId));
    }

    [Fact]
    public void ThumbnailSameAsPoster_BecomesSameAs_WithThePosterSwitch()
    {
        var config = new PluginConfiguration { ThumbnailSameAsPoster = true, ThumbnailSizeReduction = 5 };
        config.PosterConfig.Enabled = false;

        var thumb = Migrated(config, ImageType.Thumb);

        Assert.Equal(ImageType.Primary, thumb.SameAs);
        Assert.Equal(5, thumb.SizeReduction);
        Assert.False(thumb.Enabled);
        Assert.NotEmpty(thumb.Zones);
    }

    [Fact]
    public void InlineWithPrevious_JoinsAHorizontalLine_Only()
    {
        var config = new PluginConfiguration();
        var poster = config.PosterConfig;
        foreach (var panel in new[] { poster.ResolutionPanel, poster.HdrPanel, poster.CodecPanel, poster.AudioPanel })
        {
            panel.Position = BadgePosition.BottomLeft;
            panel.Layout = BadgeLayout.Horizontal;
        }

        poster.HdrPanel.InlineWithPrevious = true;
        poster.CodecPanel.Layout = BadgeLayout.Vertical;
        poster.AudioPanel.InlineWithPrevious = true;
        poster.LanguagePanel.Enabled = false;
        poster.SubtitlePanel!.Enabled = false;

        var zone = Migrated(config, ImageType.Primary).Zones.Single();

        Assert.Equal(ZoneArrangement.Rows, zone.Arrangement);
        Assert.Equal(
            new[]
            {
                new[] { PanelType.Resolution, PanelType.Hdr },
                new[] { PanelType.Codec },
                new[] { PanelType.Audio }
            },
            Lines(zone));
    }

    [Fact]
    public void FreshInstallDefaults_GiveOneTopLeftColumn()
    {
        var poster = Migrated(new PluginConfiguration(), ImageType.Primary);

        var zone = Assert.Single(poster.Zones);
        Assert.Equal(BadgePosition.TopLeft, zone.Position);
        Assert.Equal(ZoneArrangement.Columns, zone.Arrangement);
        Assert.Equal(
            new[] { PanelType.Resolution, PanelType.Hdr, PanelType.Codec, PanelType.Audio, PanelType.Language, PanelType.Subtitle },
            Lines(zone).Single());
    }
}
