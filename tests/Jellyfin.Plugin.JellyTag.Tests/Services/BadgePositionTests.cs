using Jellyfin.Plugin.JellyTag.Configuration;
using Jellyfin.Plugin.JellyTag.Services;
using SkiaSharp;

namespace Jellyfin.Plugin.JellyTag.Tests.Services;

public class BadgePositionTests
{
    private const int Width = 1000;
    private const int Height = 1500;
    private const int Margin = 20;

    private static List<SKPointI> Place(BadgePosition position, BadgeLayout layout, int priorExtent, params SKSizeI[] badges) =>
        ImageOverlayService.CalculateStackedPositions(Width, Height, badges.ToList(), position, Margin, 10, layout, priorExtent);

    // #22: a row of two 100 px badges (210 px with the gap) is centered horizontally.
    [Theory]
    [InlineData(BadgePosition.TopCenter, Margin)]
    [InlineData(BadgePosition.BottomCenter, Height - 50 - Margin)]
    public void Horizontal_RowIsCentered(BadgePosition position, int y)
    {
        var points = Place(position, BadgeLayout.Horizontal, 0, new SKSizeI(100, 50), new SKSizeI(100, 50));
        Assert.Equal([new SKPointI(395, y), new SKPointI(505, y)], points);
    }

    // In a column, each badge is centered on its own width.
    [Fact]
    public void Vertical_EachBadgeIsCentered()
    {
        var points = Place(BadgePosition.TopCenter, BadgeLayout.Vertical, 0, new SKSizeI(200, 50), new SKSizeI(100, 50));
        Assert.Equal([new SKPointI(400, Margin), new SKPointI(450, Margin + 60)], points);
    }

    // A second centered panel stacks below (top) or above (bottom) the first one.
    [Theory]
    [InlineData(BadgePosition.TopCenter, Margin + 60)]
    [InlineData(BadgePosition.BottomCenter, Height - 50 - Margin - 60)]
    public void PriorExtent_Stacks(BadgePosition position, int y)
    {
        var points = Place(position, BadgeLayout.Horizontal, 60, new SKSizeI(100, 50));
        Assert.Equal([new SKPointI(450, y)], points);
    }
}
