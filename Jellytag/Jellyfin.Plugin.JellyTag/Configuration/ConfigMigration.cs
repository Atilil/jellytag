using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JellyTag.Configuration;

/// <summary>
/// Converts a 3.x configuration (one set of panels per image type, placement stored in each panel)
/// to profiles and zones.
/// </summary>
public static class ConfigMigration
{
    /// <summary>
    /// Builds the profiles and library assignments of a 3.x configuration. Run
    /// <see cref="PluginConfiguration.MigrateFromLegacy"/> and <see cref="PluginConfiguration.EnsureSubtitlePanels"/>
    /// first: this method reads only the 3.x panels.
    /// </summary>
    public static (List<BadgeProfile> Profiles, List<LibraryAssignment> Assignments) FromV3(PluginConfiguration config)
    {
        var primary = ToImage(ImageType.Primary, config.PosterConfig);
        var thumb = ToImage(ImageType.Thumb, config.ThumbnailConfig);
        thumb.SizeReduction = config.ThumbnailSizeReduction;
        if (config.ThumbnailSameAsPoster)
        {
            // 3.x drew thumbnails from the poster settings, including the poster's Enabled switch.
            // The thumbnail zones are kept so that unchecking "same as poster" restores them.
            thumb.SameAs = ImageType.Primary;
            thumb.Enabled = primary.Enabled;
        }

        var profiles = new List<BadgeProfile>
        {
            new()
            {
                Id = BadgeProfile.DefaultProfileId,
                Name = "Default",
                ShowOnEpisodes = config.ShowOnEpisodes,
                Images = new List<ProfileImageConfig> { primary, thumb }
            }
        };
        var assignments = config.ExcludedLibraryIds
            .Select(id => new LibraryAssignment { LibraryId = id, ProfileId = null })
            .ToList();

        ConfigNormalizer.Normalize(profiles, assignments);
        return (profiles, assignments);
    }

    private static ProfileImageConfig ToImage(ImageType type, ImageTypeConfig? old)
    {
        var image = new ProfileImageConfig { ImageType = type, Enabled = old?.Enabled ?? false };
        if (old == null)
        {
            return image;
        }

        var panels = OrderedEnabledPanels(old);
        foreach (var corner in panels.GroupBy(p => p.Settings.Position).OrderBy(g => g.Key))
        {
            image.Zones.Add(ToZone(corner.Key, corner.ToList()));
        }

        return image;
    }

    // Same list and same stable sort as ImageOverlayService.GetOrderedPanels in 3.x, so that the
    // panels of a corner keep the order in which they were drawn.
    private static List<(PanelType Type, BadgePanelSettings Settings)> OrderedEnabledPanels(ImageTypeConfig old)
    {
        var panels = new List<(PanelType Type, BadgePanelSettings? Settings)>
        {
            (PanelType.Resolution, old.ResolutionPanel),
            (PanelType.Hdr, old.HdrPanel),
            (PanelType.Codec, old.CodecPanel),
            (PanelType.Audio, old.AudioPanel),
            (PanelType.Language, old.LanguagePanel),
            (PanelType.Subtitle, old.SubtitlePanel),
            (PanelType.Runtime, old.RuntimePanel),
            (PanelType.Rating, old.RatingPanel)
        };

        return panels
            .Where(p => p.Settings is { Enabled: true })
            .Select(p => (p.Type, p.Settings!))
            .OrderBy(p => p.Item2.Order)
            .ToList();
    }

    private static BadgeZone ToZone(BadgePosition position, List<(PanelType Type, BadgePanelSettings Settings)> panels)
    {
        var first = panels[0].Settings;
        var zone = new BadgeZone
        {
            Position = position,
            Arrangement = first.Layout == BadgeLayout.Horizontal ? ZoneArrangement.Rows : ZoneArrangement.Columns,
            MarginXPercent = first.MarginPercent,
            MarginYPercent = first.MarginPercent,

            // 3.x spaced the lines of a corner by the first panel's gap, in percent of the badge
            // height. A text badge is half as high as it is wide, and its width is SizePercent of
            // the image width, so this gives the same space in percent of the image width.
            GroupGapPercent = first.GapPercent * first.SizePercent / 200f
        };

        if (zone.Arrangement == ZoneArrangement.Columns)
        {
            // 3.x stacked the vertical panels of a corner one under the other: one column.
            zone.Lines.Add(new ZoneLine { Panels = panels.Select(p => ToPanel(p.Type, p.Settings)).ToList() });
            return zone;
        }

        ZoneLine? line = null;
        BadgePanelSettings? lineStart = null;
        foreach (var (type, settings) in panels)
        {
            // Same condition as ImageOverlayService.JoinsLine in 3.x.
            var joins = settings.InlineWithPrevious && lineStart is { Layout: BadgeLayout.Horizontal };
            if (!joins)
            {
                line = new ZoneLine();
                lineStart = settings;
                zone.Lines.Add(line);
            }

            line!.Panels.Add(ToPanel(type, settings));
        }

        return zone;
    }

    internal static BadgePanel ToPanel(PanelType type, BadgePanelSettings old) => new()
    {
        Type = type,
        ShowMode = old.ShowMode,
        SizePercent = old.SizePercent,

        // From percent of the badge height to percent of the badge width: exact for text badges,
        // which are half as high as they are wide.
        GapPercent = old.GapPercent / 2f,
        Style = old.Style,
        TextBgColor = old.TextBgColor,
        TextBgOpacity = old.TextBgOpacity,
        TextColor = old.TextColor,
        TextCornerRadius = old.TextCornerRadius,
        BadgeTypeOverrides = old.BadgeTypeOverrides,
        EnabledBadges = old.EnabledBadges,
        LanguageFilter = old.LanguageFilter,
        LanguageCodes = old.LanguageCodes,
        HideUndefinedLanguage = old.HideUndefinedLanguage,
        SubtitlesWithoutAudioOnly = old.SubtitlesWithoutAudioOnly,
        SubtitleLabel = old.SubtitleLabel
    };
}
