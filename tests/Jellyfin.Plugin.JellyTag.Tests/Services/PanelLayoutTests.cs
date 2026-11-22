using Jellyfin.Plugin.JellyTag.Configuration;
using Jellyfin.Plugin.JellyTag.Services;
using SkiaSharp;

namespace Jellyfin.Plugin.JellyTag.Tests.Services;

// Image 1000x1500, margin 2 % = 20 px, badges 100x50, gap 10 % of 50 = 5 px.
public class PanelLayoutTests
{
    private static readonly SKSizeI Badge = new(100, 50);

    private static (BadgePanelSettings, List<SKSizeI>) Group(BadgePosition position, bool inline = false, BadgeLayout layout = BadgeLayout.Horizontal) =>
        (new BadgePanelSettings { Position = position, Layout = layout, MarginPercent = 2, GapPercent = 10, InlineWithPrevious = inline }, [Badge]);

    private static List<List<SKPointI>> Layout(params (BadgePanelSettings, List<SKSizeI>)[] groups) =>
        ImageOverlayService.LayoutPanels(1000, 1500, groups);

    [Fact]
    public void NotInline_StartsANewRow()
    {
        var layout = Layout(Group(BadgePosition.TopLeft), Group(BadgePosition.TopLeft));
        Assert.Equal(new SKPointI(20, 75), layout[1][0]);
    }

    // #9: the second panel continues the first one's row.
    [Fact]
    public void Inline_ContinuesTheRow()
    {
        var layout = Layout(Group(BadgePosition.TopLeft), Group(BadgePosition.TopLeft, inline: true));
        Assert.Equal(new SKPointI(125, 20), layout[1][0]);
    }

    // On the right, the row is aligned as a whole and the first panel stays at the edge.
    [Fact]
    public void Inline_RightRowKeepsTheFirstPanelAtTheEdge()
    {
        var layout = Layout(Group(BadgePosition.TopRight), Group(BadgePosition.TopRight, inline: true));
        Assert.Equal(new SKPointI(880, 20), layout[0][0]);
        Assert.Equal(new SKPointI(775, 20), layout[1][0]);
    }

    // A centered row of two panels is centered as one block.
    [Fact]
    public void Inline_CenteredRowIsCenteredAsOneBlock()
    {
        var layout = Layout(Group(BadgePosition.TopCenter), Group(BadgePosition.TopCenter, inline: true));
        Assert.Equal(new SKPointI(397, 20), layout[0][0]);
        Assert.Equal(new SKPointI(502, 20), layout[1][0]);
    }

    // "Previous" means the previous panel at the same position, whatever lies between.
    [Fact]
    public void Inline_JoinsThePreviousPanelAtTheSamePosition()
    {
        var layout = Layout(Group(BadgePosition.TopLeft), Group(BadgePosition.TopRight), Group(BadgePosition.TopLeft, inline: true));
        Assert.Equal(new SKPointI(125, 20), layout[2][0]);
    }

    [Fact]
    public void Inline_WithoutPreviousPanel_StartsItsOwnRow()
    {
        var layout = Layout(Group(BadgePosition.TopRight), Group(BadgePosition.TopLeft, inline: true));
        Assert.Equal(new SKPointI(20, 20), layout[1][0]);
    }

    // A column has no row to continue.
    [Fact]
    public void Inline_AfterAVerticalPanel_StartsANewRow()
    {
        var layout = Layout(Group(BadgePosition.TopLeft, layout: BadgeLayout.Vertical), Group(BadgePosition.TopLeft, inline: true));
        Assert.Equal(new SKPointI(20, 75), layout[1][0]);
    }
}
