using Jellyfin.Plugin.JellyTag.Configuration;
using Jellyfin.Plugin.JellyTag.Services;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JellyTag.Tests.Services;

public class BadgeSettingsResolverTests
{
    private static readonly BadgeProfile Anime = new() { Name = "Anime" };

    private static PluginConfiguration Config(params LibraryAssignment[] assignments)
    {
        var config = new PluginConfiguration();
        config.Upgrade();
        config.Profiles.Add(Anime);
        config.LibraryAssignments.AddRange(assignments);
        return config;
    }

    private static Guid? ProfileOf(PluginConfiguration config, params string[] libraries) =>
        BadgeSettingsResolver.ResolveProfile(config, libraries)?.Id;

    [Fact]
    public void ItemInNoLibrary_GetsNoProfile()
    {
        Assert.Null(ProfileOf(Config()));
    }

    [Fact]
    public void UnassignedLibrary_UsesTheDefaultProfile()
    {
        Assert.Equal(BadgeProfile.DefaultProfileId, ProfileOf(Config(), "films"));
    }

    [Fact]
    public void AssignedLibrary_UsesItsProfile()
    {
        var config = Config(new LibraryAssignment { LibraryId = "anime", ProfileId = Anime.Id });

        Assert.Equal(Anime.Id, ProfileOf(config, "anime"));
    }

    // Q16: one library assigned to "no profile" removes the badges, whatever the other libraries say.
    [Fact]
    public void OneLibraryWithoutProfile_RemovesTheBadges()
    {
        var config = Config(
            new LibraryAssignment { LibraryId = "anime", ProfileId = Anime.Id },
            new LibraryAssignment { LibraryId = "all", ProfileId = null });

        Assert.Null(ProfileOf(config, "anime", "all"));
    }

    // Q16: otherwise the highest assignment in the table wins, not the order Jellyfin lists the libraries in.
    [Fact]
    public void SeveralLibraries_TheFirstAssignmentOfTheTableWins()
    {
        var config = Config(
            new LibraryAssignment { LibraryId = "anime", ProfileId = Anime.Id },
            new LibraryAssignment { LibraryId = "films", ProfileId = BadgeProfile.DefaultProfileId });

        Assert.Equal(Anime.Id, ProfileOf(config, "films", "anime"));
    }

    [Theory]
    [InlineData(ImageType.Primary, true, ImageType.Thumb)]
    [InlineData(ImageType.Primary, false, ImageType.Primary)]
    [InlineData(ImageType.Thumb, false, ImageType.Thumb)]
    public void EpisodePrimary_UsesTheThumbnailSettings(ImageType requested, bool isEpisode, ImageType expected)
    {
        Assert.Equal(expected, BadgeSettingsResolver.TargetImageType(requested, isEpisode));
    }

    [Fact]
    public void DisabledImageType_GetsNoSettings()
    {
        var profile = Config().Profiles[0];

        Assert.Null(BadgeSettingsResolver.ResolveImage(profile, ImageType.Backdrop));
    }

    [Fact]
    public void SameAs_CopiesTheSourceZones_WithReducedSizes_AndLeavesTheSourceUnchanged()
    {
        var profile = Config().Profiles[0];
        var thumb = profile.Images.Single(i => i.ImageType == ImageType.Thumb);
        thumb.SameAs = ImageType.Primary;
        thumb.SizeReduction = 5;
        var posterPanel = profile.Images[0].Zones[0].Lines[0].Panels[0];

        var resolved = BadgeSettingsResolver.ResolveImage(profile, ImageType.Thumb)!;

        Assert.Equal(ImageType.Thumb, resolved.ImageType);
        Assert.Equal(posterPanel.SizePercent - 5, resolved.Zones[0].Lines[0].Panels[0].SizePercent);
        Assert.Equal(15, posterPanel.SizePercent);
    }

    [Fact]
    public void Fingerprint_ChangesWithTheSettings()
    {
        var image = Config().Profiles[0].Images[0];
        var before = BadgeSettingsResolver.Fingerprint(image);

        image.Zones[0].GroupGapPercent += 1;

        Assert.NotEqual(before, BadgeSettingsResolver.Fingerprint(image));
    }
}
