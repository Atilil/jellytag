using Jellyfin.Plugin.JellyTag.Configuration;
using SkiaSharp;

namespace Jellyfin.Plugin.JellyTag.Services;

/// <summary>
/// Places the badges of one zone on an image.
/// </summary>
internal static class ZoneLayout
{
    /// <summary>
    /// Positions of each group's badges, in the order of <paramref name="groups"/>. A group is a
    /// panel of the zone with the measured sizes of its visible badges; <c>Line</c> is the index of
    /// the zone line that holds it. Groups without badges take no space.
    /// </summary>
    internal static List<List<SKPointI>> Layout(
        int imageWidth, int imageHeight, BadgeZone zone,
        IReadOnlyList<(int Line, BadgePanel Panel, List<SKSizeI> Sizes)> groups)
    {
        var result = groups.Select(g => new SKPointI[g.Sizes.Count]).ToList();

        var position = zone.Position;
        var right = position is BadgePosition.TopRight or BadgePosition.BottomRight;
        var center = position is BadgePosition.TopCenter or BadgePosition.BottomCenter;
        var bottom = position is BadgePosition.BottomLeft or BadgePosition.BottomRight or BadgePosition.BottomCenter;
        var rows = zone.Arrangement == ZoneArrangement.Rows;

        // A centered zone has no side to keep a distance from.
        var marginX = center ? 0 : PercentOf(imageWidth, ClampMargin(zone.MarginXPercent));
        var marginY = PercentOf(imageWidth, ClampMargin(zone.MarginYPercent));
        var groupGap = PercentOf(imageWidth, Math.Max(0f, zone.GroupGapPercent));

        // The first badge of a line sits at the edge of the zone: the right edge for a row on the
        // right, the bottom edge for a column at the bottom. Reading order is reversed there.
        var reverse = rows ? right : bottom;
        var lines = groups
            .Select((g, index) => (g.Line, Index: index, g.Panel, g.Sizes))
            .Where(g => g.Sizes.Count > 0)
            .GroupBy(g => g.Line)
            .OrderBy(l => l.Key)
            .Select(l => BuildLine(l.ToList(), rows, reverse, groupGap, imageWidth))
            .ToList();
        if (lines.Count == 0)
        {
            return result.Select(r => r.ToList()).ToList();
        }

        // Columns of a centered zone are centered as one block.
        var blockWidth = lines.Sum(l => l.Thickness) + (lines.Count - 1) * groupGap;
        var crossOffset = 0;
        foreach (var line in lines)
        {
            if (rows)
            {
                var lineY = bottom
                    ? imageHeight - marginY - crossOffset - line.Thickness
                    : marginY + crossOffset;
                var startX = right ? imageWidth - marginX - line.Length
                    : center ? (imageWidth - line.Length) / 2
                    : marginX;
                foreach (var slot in line.Slots)
                {
                    var y = lineY + (line.Thickness - slot.Size.Height) / 2;
                    result[slot.Group][slot.Badge] = Point(startX + slot.Offset, y);
                }
            }
            else
            {
                var columnX = right ? imageWidth - marginX - crossOffset - line.Thickness
                    : center ? (imageWidth - blockWidth) / 2 + crossOffset
                    : marginX + crossOffset;
                var startY = bottom ? imageHeight - marginY - line.Length : marginY;
                foreach (var slot in line.Slots)
                {
                    // Each badge is aligned on the zone's side of its column.
                    var free = line.Thickness - slot.Size.Width;
                    var x = columnX + (right ? free : center ? free / 2 : 0);
                    result[slot.Group][slot.Badge] = Point(x, startY + slot.Offset);
                }
            }

            crossOffset += line.Thickness + groupGap;
        }

        return result.Select(r => r.ToList()).ToList();
    }

    private sealed record Slot(int Group, int Badge, SKSizeI Size, int Offset);

    /// <summary>
    /// Badges of a line along its axis. Length is measured along the axis, Thickness across it.
    /// </summary>
    private sealed record Line(List<Slot> Slots, int Length, int Thickness);

    private static Line BuildLine(
        List<(int Line, int Index, BadgePanel Panel, List<SKSizeI> Sizes)> groups,
        bool rows, bool reverse, int groupGap, int imageWidth)
    {
        var order = groups
            .SelectMany(g => g.Sizes.Select((size, badge) => (g.Index, Badge: badge, Size: size, Gap: BadgeGap(g.Panel, imageWidth))))
            .ToList();
        if (reverse)
        {
            order.Reverse();
        }

        var slots = new List<Slot>();
        var offset = 0;
        for (var i = 0; i < order.Count; i++)
        {
            if (i > 0)
            {
                // Two badges of one panel use the panel's gap, two panels use the zone's gap.
                offset += order[i].Index == order[i - 1].Index ? order[i].Gap : groupGap;
            }

            slots.Add(new Slot(order[i].Index, order[i].Badge, order[i].Size, offset));
            offset += rows ? order[i].Size.Width : order[i].Size.Height;
        }

        var thickness = rows ? order.Max(o => o.Size.Height) : order.Max(o => o.Size.Width);
        return new Line(slots, offset, thickness);
    }

    /// <summary>
    /// Gap between two badges of a panel, in pixels: a share of the configured badge width, so
    /// that it does not change with the aspect ratio of the badges shown.
    /// </summary>
    private static int BadgeGap(BadgePanel panel, int imageWidth)
    {
        var size = Math.Clamp(panel.SizePercent, ImageOverlayService.MinBadgeSizePercent, ImageOverlayService.MaxBadgeSizePercent);
        var badgeWidth = imageWidth * size / 100f;
        return (int)(badgeWidth * Math.Max(0f, panel.GapPercent) / 100f);
    }

    private static float ClampMargin(float percent) =>
        Math.Clamp(percent, ImageOverlayService.MinBadgeMarginPercent, ImageOverlayService.MaxBadgeMarginPercent);

    private static int PercentOf(int imageWidth, float percent) => (int)(imageWidth * percent / 100f);

    // Same floor at 0 as the 3.x layout: a block larger than the image starts at its edge.
    private static SKPointI Point(int x, int y) => new(Math.Max(0, x), Math.Max(0, y));
}
