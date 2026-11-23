using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JellyTag.Configuration;

/// <summary>
/// Badge category of a panel. A panel of each type appears at most once per image type.
/// </summary>
public enum PanelType
{
    Resolution,
    Hdr,
    Codec,
    Audio,
    Language,
    Subtitle,
    Runtime,
    Rating,
    Library
}

/// <summary>
/// How a zone places its lines: rows stacked away from the edge, or columns side by side.
/// </summary>
public enum ZoneArrangement
{
    Rows,
    Columns
}

/// <summary>
/// A named set of badge settings, used by the libraries assigned to it.
/// </summary>
public class BadgeProfile
{
    /// <summary>
    /// Id of the profile that always exists. It is a constant so that an exported configuration
    /// imported on another server still points its libraries at the same profile.
    /// </summary>
    public static readonly Guid DefaultProfileId = new("daeb47a3-cbe4-4455-9dd2-aa166fd7fcb1");

    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public bool ShowOnEpisodes { get; set; } = true;
    public List<ProfileImageConfig> Images { get; set; } = new();
}

/// <summary>
/// Badge settings of one image type (poster, thumbnail, backdrop, logo) inside a profile.
/// </summary>
public class ProfileImageConfig
{
    public ImageType ImageType { get; set; } = ImageType.Primary;
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When set, this image type draws the zones of that image type instead of its own.
    /// </summary>
    public ImageType? SameAs { get; set; }

    /// <summary>
    /// Percentage points removed from each panel size when <see cref="SameAs"/> is set.
    /// </summary>
    public int SizeReduction { get; set; }

    public List<BadgeZone> Zones { get; set; } = new();
}

/// <summary>
/// The panels drawn at one position of the image.
/// </summary>
public class BadgeZone
{
    public BadgePosition Position { get; set; } = BadgePosition.TopLeft;
    public ZoneArrangement Arrangement { get; set; } = ZoneArrangement.Columns;

    // Percentages of the image width, like the size of the badges.
    public float MarginXPercent { get; set; } = 2.5f;
    public float MarginYPercent { get; set; } = 2.5f;

    /// <summary>
    /// Space between two panels of a line and between two lines, in percent of the image width.
    /// A zone mixes panels of different sizes, so no badge width can serve as the unit.
    /// </summary>
    public float GroupGapPercent { get; set; } = 1f;

    public List<ZoneLine> Lines { get; set; } = new();
}

/// <summary>
/// A row or a column of a zone, depending on <see cref="BadgeZone.Arrangement"/>.
/// </summary>
/// <remarks>
/// A class instead of a nested list because XmlSerializer, which Jellyfin uses to save the
/// configuration, does not handle a list of lists cleanly.
/// </remarks>
public class ZoneLine
{
    public List<BadgePanel> Panels { get; set; } = new();
}

/// <summary>
/// What a panel shows and how it looks. Where it goes is decided by the zone that holds it.
/// </summary>
public class BadgePanel
{
    public PanelType Type { get; set; }
    public BadgeDisplayMode ShowMode { get; set; } = BadgeDisplayMode.Highest;
    public int SizePercent { get; set; } = 15;

    /// <summary>
    /// Space between two badges of this panel, in percent of the configured badge width.
    /// </summary>
    public float GapPercent { get; set; } = 5f;

    public BadgeStyle Style { get; set; } = BadgeStyle.Image;

    // Text style settings (used when Style == Text)
    public string TextBgColor { get; set; } = "#000000";
    public int TextBgOpacity { get; set; } = 180;
    public string TextColor { get; set; } = "#FFFFFF";
    public int TextCornerRadius { get; set; } = 25;

    public List<BadgeTypeStyleOverride> BadgeTypeOverrides { get; set; } = new();
    public List<string> EnabledBadges { get; set; } = new();

    // Language and subtitle panels: which languages to show (ISO 639-2 codes, e.g. "eng").
    public LanguageFilterMode LanguageFilter { get; set; } = LanguageFilterMode.All;
    public List<string> LanguageCodes { get; set; } = new();
    public bool HideUndefinedLanguage { get; set; } = true;

    // Subtitle panel only
    public bool SubtitlesWithoutAudioOnly { get; set; } = true;
    public SubtitleLabel SubtitleLabel { get; set; } = SubtitleLabel.Code;
}

/// <summary>
/// Which profile a library uses.
/// </summary>
public class LibraryAssignment
{
    /// <summary>
    /// Collection folder id in the "N" format (32 hex digits, no dashes), as Jellyfin prints it.
    /// </summary>
    public string LibraryId { get; set; } = "";

    /// <summary>
    /// Null means the library gets no badges.
    /// </summary>
    public Guid? ProfileId { get; set; }
}
