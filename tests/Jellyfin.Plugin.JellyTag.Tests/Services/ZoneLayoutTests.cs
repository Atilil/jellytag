using Jellyfin.Plugin.JellyTag.Configuration;
using Jellyfin.Plugin.JellyTag.Services;
using SkiaSharp;

namespace Jellyfin.Plugin.JellyTag.Tests.Services;

// Image 1000x1500. Margins 2 % = 20 px, group gap 1 % = 10 px.
// Panels of size 10 % (badge width 100 px) with a gap of 10 % of that width = 10 px.
public class ZoneLayoutTests
{
    private static readonly SKSizeI Badge = new(100, 50);

    private static BadgeZone Zone(BadgePosition position, ZoneArrangement arrangement) => new()
    {
        Position = position, Arrangement = arrangement, MarginXPercent = 2, MarginYPercent = 2, GroupGapPercent = 1
    };

    private static (int, BadgePanel, List<SKSizeI>) Group(int line, params SKSizeI[] sizes) =>
        (line, new BadgePanel { SizePercent = 10, GapPercent = 10 }, sizes.ToList());

    private static List<List<SKPointI>> Layout(BadgeZone zone, params (int, BadgePanel, List<SKSizeI>)[] groups) =>
        ZoneLayout.Layout(1000, 1500, zone, groups);

    [Fact]
    public void Rows_TopLeft_StackLinesDownwards()
    {
        var layout = Layout(Zone(BadgePosition.TopLeft, ZoneArrangement.Rows), Group(0, Badge, Badge), Group(1, Badge));

        Assert.Equal(new[] { new SKPointI(20, 20), new SKPointI(130, 20) }, layout[0]);
        Assert.Equal(new SKPointI(20, 80), layout[1][0]);
    }

    // The first badge of the line stays at the right edge, the next panel follows after the group gap.
    [Fact]
    public void Rows_TopRight_KeepTheFirstBadgeAtTheEdge()
    {
        var layout = Layout(Zone(BadgePosition.TopRight, ZoneArrangement.Rows), Group(0, Badge, Badge), Group(0, Badge));

        Assert.Equal(new[] { new SKPointI(880, 20), new SKPointI(770, 20) }, layout[0]);
        Assert.Equal(new SKPointI(660, 20), layout[1][0]);
    }

    [Fact]
    public void Rows_BottomCenter_StackLinesUpwards_AndIgnoreMarginX()
    {
        var zone = Zone(BadgePosition.BottomCenter, ZoneArrangement.Rows);
        zone.MarginXPercent = 5;

        var layout = Layout(zone, Group(0, Badge), Group(1, Badge));

        Assert.Equal(new SKPointI(450, 1430), layout[0][0]);
        Assert.Equal(new SKPointI(450, 1370), layout[1][0]);
    }

    [Fact]
    public void Columns_TopLeft_PlaceColumnsSideBySide()
    {
        var layout = Layout(Zone(BadgePosition.TopLeft, ZoneArrangement.Columns), Group(0, Badge, Badge), Group(1, Badge));

        Assert.Equal(new[] { new SKPointI(20, 20), new SKPointI(20, 80) }, layout[0]);
        Assert.Equal(new SKPointI(130, 20), layout[1][0]);
    }

    [Fact]
    public void Columns_BottomRight_KeepTheFirstBadgeAtTheBottom()
    {
        var layout = Layout(Zone(BadgePosition.BottomRight, ZoneArrangement.Columns), Group(0, Badge, Badge), Group(1, Badge));

        Assert.Equal(new[] { new SKPointI(880, 1430), new SKPointI(880, 1370) }, layout[0]);
        Assert.Equal(new SKPointI(770, 1430), layout[1][0]);
    }

    [Fact]
    public void Columns_TopCenter_AreCenteredAsOneBlock()
    {
        var layout = Layout(Zone(BadgePosition.TopCenter, ZoneArrangement.Columns), Group(0, Badge), Group(1, Badge));

        Assert.Equal(new SKPointI(395, 20), layout[0][0]);
        Assert.Equal(new SKPointI(505, 20), layout[1][0]);
    }

    // #46: the gap is a share of the configured badge width, not of the measured badge height.
    [Fact]
    public void BadgeGap_DoesNotDependOnTheBadgeHeight()
    {
        var tall = new SKSizeI(100, 80);

        var layout = Layout(Zone(BadgePosition.TopLeft, ZoneArrangement.Columns), Group(0, tall, tall));

        Assert.Equal(new SKPointI(20, 110), layout[0][1]);
    }

    [Fact]
    public void Columns_Right_AlignNarrowBadgesOnTheRightEdge()
    {
        var narrow = new SKSizeI(60, 30);

        var layout = Layout(Zone(BadgePosition.TopRight, ZoneArrangement.Columns), Group(0, Badge), Group(0, narrow));

        Assert.Equal(new SKPointI(880, 20), layout[0][0]);
        Assert.Equal(new SKPointI(920, 80), layout[1][0]);
    }

    [Fact]
    public void Rows_ShorterBadgesAreCenteredOnTheLine()
    {
        var shorter = new SKSizeI(100, 30);

        var layout = Layout(Zone(BadgePosition.TopLeft, ZoneArrangement.Rows), Group(0, Badge, shorter));

        Assert.Equal(new SKPointI(130, 30), layout[0][1]);
    }

    // Two 50 px lines and a 10 px gap: a 110 px block, centered on the 1500 px height.
    [Fact]
    public void Rows_MiddleLeft_CenterTheLinesAsOneBlock()
    {
        var layout = Layout(Zone(BadgePosition.MiddleLeft, ZoneArrangement.Rows), Group(0, Badge), Group(1, Badge));

        Assert.Equal(new SKPointI(20, 695), layout[0][0]);
        Assert.Equal(new SKPointI(20, 755), layout[1][0]);
    }

    // A middle column reads from the top, like a top one: no reversed order.
    [Fact]
    public void Columns_MiddleRight_CenterEachColumnOnItsHeight()
    {
        var layout = Layout(Zone(BadgePosition.MiddleRight, ZoneArrangement.Columns), Group(0, Badge, Badge), Group(1, Badge));

        Assert.Equal(new[] { new SKPointI(880, 695), new SKPointI(880, 755) }, layout[0]);
        Assert.Equal(new SKPointI(770, 725), layout[1][0]);
    }

    [Fact]
    public void MiddleCenter_IgnoresBothMargins()
    {
        var zone = Zone(BadgePosition.MiddleCenter, ZoneArrangement.Rows);
        zone.MarginXPercent = 5;
        zone.MarginYPercent = 5;

        var layout = Layout(zone, Group(0, Badge));

        Assert.Equal(new SKPointI(450, 725), layout[0][0]);
    }

    [Fact]
    public void PanelsWithoutBadges_TakeNoSpace()
    {
        var layout = Layout(Zone(BadgePosition.TopLeft, ZoneArrangement.Rows), Group(0), Group(1, Badge), Group(1));

        Assert.Empty(layout[0]);
        Assert.Equal(new SKPointI(20, 20), layout[1][0]);
        Assert.Empty(layout[2]);
    }
}
