using Jellyfin.Plugin.JellyTag.Configuration;
using Jellyfin.Plugin.JellyTag.Services;

namespace Jellyfin.Plugin.JellyTag.Tests.Services;

public class LibraryBadgesTests
{
    private static readonly (string, string) Films = ("AAAA", "Films");
    private static readonly (string, string) Remux = ("bbbb", "4K Remux");
    private static readonly (string, string) All = ("cccc", "Everything");

    [Fact]
    public void Badge_ShowsTheLibraryName_WithAKeyFromItsId()
    {
        var badge = Assert.Single(LibraryBadges.For([Films], new PluginConfiguration()));

        Assert.Equal(BadgeCategory.Library, badge.Category);
        Assert.Equal("library-aaaa", badge.BadgeKey);
        Assert.Equal("badge-library-aaaa.svg", badge.ResourceFileName);
        Assert.Equal("Films", badge.DisplayText);
    }

    [Fact]
    public void CustomText_ReplacesTheName()
    {
        var config = new PluginConfiguration();
        config.CustomBadgeTexts.Add(new BadgeTextOverride { Key = "library-bbbb", Text = "REMUX" });

        Assert.Equal("REMUX", Assert.Single(LibraryBadges.For([Remux], config)).DisplayText);
    }

    // Q17: assigned libraries first, in the order of the table, then the others as Jellyfin lists them.
    [Fact]
    public void Badges_FollowTheAssignmentTable()
    {
        var config = new PluginConfiguration();
        config.LibraryAssignments.Add(new LibraryAssignment { LibraryId = "bbbb", ProfileId = BadgeProfile.DefaultProfileId });
        config.LibraryAssignments.Add(new LibraryAssignment { LibraryId = "aaaa", ProfileId = BadgeProfile.DefaultProfileId });

        var badges = LibraryBadges.For([All, Films, Remux], config);

        Assert.Equal(new[] { "4K Remux", "Films", "Everything" }, badges.Select(b => b.DisplayText));
    }
}
