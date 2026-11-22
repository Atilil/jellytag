using Jellyfin.Plugin.JellyTag.Services;
using MediaBrowser.Controller.Entities.Movies;

namespace Jellyfin.Plugin.JellyTag.Tests.Services;

// #11: ratings Jellyfin already stores, shown as plain text.
public class DetectRatingsTests
{
    private static string Texts(float? community, float? critic) =>
        string.Join(",", QualityDetectionService.DetectRatings(new Movie { CommunityRating = community, CriticRating = critic })
            .Select(b => $"{b.BadgeKey}={b.DisplayText}"));

    [Fact]
    public void BothRatings()
    {
        Assert.Equal("rating=7.8,critic=92%", Texts(7.8f, 92f));
    }

    // Named like the badge images, so an uploaded custom badge replaces the icon.
    [Fact]
    public void Icons_FollowTheBadgeKeys()
    {
        var icons = QualityDetectionService.DetectRatings(new Movie { CommunityRating = 7.8f, CriticRating = 92f }).Select(b => b.IconFileName);
        Assert.Equal(["badge-rating.svg", "badge-critic.svg"], icons);
    }

    // One decimal always, with a dot whatever the server culture.
    [Theory]
    [InlineData(8f, "rating=8.0")]
    [InlineData(7.86f, "rating=7.9")]
    public void CommunityRating_OneDecimal(float rating, string expected)
    {
        Assert.Equal(expected, Texts(rating, null));
    }

    [Fact]
    public void CriticRating_Rounded()
    {
        Assert.Equal("critic=87%", Texts(null, 86.6f));
    }

    // Missing or zero: Jellyfin writes 0 when a provider has no rating.
    [Theory]
    [InlineData(null, null)]
    [InlineData(0f, 0f)]
    public void NoRating_NoBadge(float? community, float? critic)
    {
        Assert.Equal("", Texts(community, critic));
    }
}
