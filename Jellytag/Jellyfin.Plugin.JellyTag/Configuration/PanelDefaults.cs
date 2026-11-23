using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JellyTag.Configuration;

/// <summary>
/// Settings a panel and a zone start with when they are added on the configuration page.
/// </summary>
public class PanelDefaults
{
    public BadgeZone Zone { get; set; } = new();
    public Dictionary<PanelType, BadgePanel> Panels { get; set; } = new();

    /// <summary>
    /// The defaults of a fresh installation, converted like any 3.x configuration so that they have a
    /// single source. Panels that are off by default (video length, ratings) are taken too.
    /// </summary>
    public static PanelDefaults For(ImageType imageType)
    {
        var fresh = new PluginConfiguration();
        fresh.EnsureSubtitlePanels();

        // A backdrop is wide like a thumbnail. A logo has no 3.x settings: it takes the poster ones.
        var wide = imageType is ImageType.Thumb or ImageType.Backdrop;
        var old = wide ? fresh.ThumbnailConfig : fresh.PosterConfig;
        var panels = new Dictionary<PanelType, BadgePanel>
        {
            [PanelType.Resolution] = ConfigMigration.ToPanel(PanelType.Resolution, old.ResolutionPanel),
            [PanelType.Hdr] = ConfigMigration.ToPanel(PanelType.Hdr, old.HdrPanel),
            [PanelType.Codec] = ConfigMigration.ToPanel(PanelType.Codec, old.CodecPanel),
            [PanelType.Audio] = ConfigMigration.ToPanel(PanelType.Audio, old.AudioPanel),
            [PanelType.Language] = ConfigMigration.ToPanel(PanelType.Language, old.LanguagePanel),
            [PanelType.Subtitle] = ConfigMigration.ToPanel(PanelType.Subtitle, old.SubtitlePanel!),
            [PanelType.Runtime] = ConfigMigration.ToPanel(PanelType.Runtime, old.RuntimePanel),
            [PanelType.Rating] = ConfigMigration.ToPanel(PanelType.Rating, old.RatingPanel),

            // New in 4.0: a library name is text, the size of the other badges of this image type.
            [PanelType.Library] = new BadgePanel
            {
                Type = PanelType.Library,
                Style = BadgeStyle.Text,
                SizePercent = old.ResolutionPanel.SizePercent
            }
        };

        // The zone that holds the resolution panel in a fresh installation, without its panels.
        var zone = ConfigMigration.FromV3(fresh).Profiles[0].Images
            .First(i => i.ImageType == (wide ? ImageType.Thumb : ImageType.Primary))
            .Zones[0];
        zone.Lines.Clear();

        return new PanelDefaults { Zone = zone, Panels = panels };
    }
}
