using System.Xml.Serialization;
using Jellyfin.Plugin.JellyTag.Configuration;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JellyTag.Tests.Configuration;

public class ConfigNormalizerTests
{
    private static BadgePanel Panel(PanelType type) => new() { Type = type };

    private static ZoneLine Line(params PanelType[] types) => new() { Panels = types.Select(Panel).ToList() };

    private static BadgeZone Zone(BadgePosition position, params ZoneLine[] lines) =>
        new() { Position = position, Lines = lines.ToList() };

    private static ProfileImageConfig Image(ImageType type, params BadgeZone[] zones) =>
        new() { ImageType = type, Zones = zones.ToList() };

    private static List<ProfileImageConfig> NormalizeOne(params ProfileImageConfig[] images)
    {
        var profiles = new List<BadgeProfile>
        {
            new() { Id = BadgeProfile.DefaultProfileId, Images = images.ToList() }
        };
        ConfigNormalizer.Normalize(profiles, new List<LibraryAssignment>());
        return profiles[0].Images;
    }

    [Fact]
    public void MissingDefaultProfile_IsAddedFirst()
    {
        var other = new BadgeProfile { Name = "Anime" };
        var profiles = new List<BadgeProfile> { other };

        ConfigNormalizer.Normalize(profiles, new List<LibraryAssignment>());

        Assert.Equal(new[] { BadgeProfile.DefaultProfileId, other.Id }, profiles.Select(p => p.Id));
    }

    [Fact]
    public void ProfilesWithTheSameId_KeepTheFirst()
    {
        var id = Guid.NewGuid();
        var profiles = new List<BadgeProfile>
        {
            new() { Id = BadgeProfile.DefaultProfileId },
            new() { Id = id, Name = "First" },
            new() { Id = id, Name = "Second" }
        };

        ConfigNormalizer.Normalize(profiles, new List<LibraryAssignment>());

        Assert.Equal(new[] { "", "First" }, profiles.Select(p => p.Name));
    }

    [Fact]
    public void ImageTypes_AreTheFourSupportedOnes_MissingOnesDisabled()
    {
        var images = NormalizeOne(
            Image(ImageType.Thumb),
            Image(ImageType.Banner),
            new ProfileImageConfig { ImageType = ImageType.Thumb, Enabled = false });

        Assert.Equal(
            new[] { ImageType.Primary, ImageType.Thumb, ImageType.Backdrop, ImageType.Logo },
            images.Select(i => i.ImageType));
        Assert.Equal(new[] { false, true, false, false }, images.Select(i => i.Enabled));
    }

    [Fact]
    public void SameAs_PointingAtItselfOrAtACopy_IsCleared()
    {
        var images = NormalizeOne(
            new ProfileImageConfig { ImageType = ImageType.Primary },
            new ProfileImageConfig { ImageType = ImageType.Thumb, SameAs = ImageType.Primary },
            new ProfileImageConfig { ImageType = ImageType.Backdrop, SameAs = ImageType.Thumb },
            new ProfileImageConfig { ImageType = ImageType.Logo, SameAs = ImageType.Logo });

        Assert.Equal(
            new ImageType?[] { null, ImageType.Primary, null, null },
            images.Select(i => i.SameAs));
    }

    [Fact]
    public void PanelTypeUsedTwice_KeepsTheFirstAcrossZones()
    {
        var images = NormalizeOne(Image(
            ImageType.Primary,
            Zone(BadgePosition.TopLeft, Line(PanelType.Resolution, PanelType.Hdr)),
            Zone(BadgePosition.BottomRight, Line(PanelType.Hdr, PanelType.Audio))));

        var zones = images[0].Zones;
        Assert.Equal(new[] { PanelType.Resolution, PanelType.Hdr }, zones[0].Lines[0].Panels.Select(p => p.Type));
        Assert.Equal(new[] { PanelType.Audio }, zones[1].Lines[0].Panels.Select(p => p.Type));
    }

    [Fact]
    public void ZonesAtTheSamePosition_AreMerged()
    {
        var images = NormalizeOne(Image(
            ImageType.Primary,
            Zone(BadgePosition.TopLeft, Line(PanelType.Resolution)),
            Zone(BadgePosition.TopLeft, Line(PanelType.Audio))));

        var zone = Assert.Single(images[0].Zones);
        Assert.Equal(
            new[] { PanelType.Resolution, PanelType.Audio },
            zone.Lines.Select(l => l.Panels.Single().Type));
    }

    [Fact]
    public void EmptyLinesAndZones_AreRemoved()
    {
        var images = NormalizeOne(Image(
            ImageType.Primary,
            Zone(BadgePosition.TopLeft, Line(), Line(PanelType.Codec)),
            Zone(BadgePosition.BottomLeft, Line()),
            Zone(BadgePosition.TopRight, Line(PanelType.Codec))));

        var zone = Assert.Single(images[0].Zones);
        Assert.Equal(BadgePosition.TopLeft, zone.Position);
        Assert.Single(zone.Lines);
    }

    [Fact]
    public void Assignments_UnknownProfileGoesToDefault_NoneStays_DuplicateLibraryKeepsTheFirst()
    {
        var anime = new BadgeProfile { Name = "Anime" };
        var profiles = new List<BadgeProfile> { new() { Id = BadgeProfile.DefaultProfileId }, anime };
        var assignments = new List<LibraryAssignment>
        {
            new() { LibraryId = "aaa", ProfileId = anime.Id },
            new() { LibraryId = "bbb", ProfileId = Guid.NewGuid() },
            new() { LibraryId = "ccc", ProfileId = null },
            new() { LibraryId = "AAA", ProfileId = null },
            new() { LibraryId = "" }
        };

        ConfigNormalizer.Normalize(profiles, assignments);

        Assert.Equal(new[] { "aaa", "bbb", "ccc" }, assignments.Select(a => a.LibraryId));
        Assert.Equal(
            new Guid?[] { anime.Id, BadgeProfile.DefaultProfileId, null },
            assignments.Select(a => a.ProfileId));
    }

    // Jellyfin saves the configuration with XmlSerializer: the nullable and nested members must survive it.
    [Fact]
    public void Model_SurvivesAnXmlRoundTrip()
    {
        var profile = new BadgeProfile
        {
            Id = BadgeProfile.DefaultProfileId,
            Images = new List<ProfileImageConfig>
            {
                Image(ImageType.Primary, Zone(BadgePosition.BottomCenter, Line(PanelType.Library, PanelType.Rating))),
                new() { ImageType = ImageType.Thumb, SameAs = ImageType.Primary, SizeReduction = 5 }
            }
        };
        var assignment = new LibraryAssignment { LibraryId = "abc", ProfileId = null };

        var loadedProfile = RoundTrip(profile);
        var loadedAssignment = RoundTrip(assignment);

        Assert.Equal(BadgeProfile.DefaultProfileId, loadedProfile.Id);
        Assert.Equal(
            new[] { PanelType.Library, PanelType.Rating },
            loadedProfile.Images[0].Zones[0].Lines[0].Panels.Select(p => p.Type));
        Assert.Equal(ImageType.Primary, loadedProfile.Images[1].SameAs);
        Assert.Null(loadedProfile.Images[0].SameAs);
        Assert.Null(loadedAssignment.ProfileId);
    }

    private static T RoundTrip<T>(T value)
    {
        var serializer = new XmlSerializer(typeof(T));
        using var writer = new StringWriter();
        serializer.Serialize(writer, value);
        using var reader = new StringReader(writer.ToString());
        return (T)serializer.Deserialize(reader)!;
    }
}
