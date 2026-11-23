using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jellyfin.Plugin.JellyTag.Configuration;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JellyTag.Services;

/// <summary>
/// Finds the settings that apply to one image of one item.
/// </summary>
internal static class BadgeSettingsResolver
{
    /// <summary>
    /// Profile of an item, from the libraries that hold it. Null means no badges: the item is in no
    /// library, or one of its libraries is assigned to no profile. Otherwise the first assignment of
    /// the table that matches one of the libraries wins, and libraries without an assignment use
    /// the default profile.
    /// </summary>
    internal static BadgeProfile? ResolveProfile(PluginConfiguration config, IReadOnlyCollection<string> libraryIds)
    {
        if (libraryIds.Count == 0)
        {
            return null;
        }

        var matches = config.LibraryAssignments
            .Where(a => libraryIds.Contains(a.LibraryId, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (matches.Any(a => a.ProfileId == null))
        {
            return null;
        }

        var id = matches.FirstOrDefault()?.ProfileId ?? BadgeProfile.DefaultProfileId;
        return config.Profiles.FirstOrDefault(p => p.Id == id)
            ?? config.Profiles.FirstOrDefault(p => p.Id == BadgeProfile.DefaultProfileId);
    }

    /// <summary>
    /// The image type whose settings apply. An episode's primary image is a 16:9 still, not a
    /// poster, so it uses the thumbnail settings.
    /// </summary>
    internal static ImageType TargetImageType(ImageType requested, bool isEpisode) =>
        requested == ImageType.Primary && isEpisode ? ImageType.Thumb : requested;

    /// <summary>
    /// Settings to draw for an image type, or null when it gets no badges. An image type that copies
    /// another one gets a copy of that one's zones, with the panel sizes reduced.
    /// </summary>
    internal static ProfileImageConfig? ResolveImage(BadgeProfile profile, ImageType type)
    {
        var entry = profile.Images.FirstOrDefault(i => i.ImageType == type);
        if (entry is not { Enabled: true })
        {
            return null;
        }

        if (entry.SameAs is not { } sourceType)
        {
            return entry;
        }

        var source = profile.Images.FirstOrDefault(i => i.ImageType == sourceType);
        if (source == null)
        {
            return null;
        }

        // A deep copy, because the reduction must not change the saved settings of the source.
        var copy = JsonSerializer.Deserialize<ProfileImageConfig>(JsonSerializer.Serialize(source))!;
        copy.ImageType = entry.ImageType;
        copy.SameAs = null;
        copy.SizeReduction = 0;
        foreach (var panel in copy.Zones.SelectMany(z => z.Lines).SelectMany(l => l.Panels))
        {
            panel.SizePercent = Math.Max(1, panel.SizePercent - Math.Max(0, entry.SizeReduction));
        }

        return copy;
    }

    /// <summary>
    /// Short hash of the settings used to draw an image. It is part of the cache key, so a change to
    /// these settings gives new images, while a change to another image type or profile does not.
    /// </summary>
    internal static string Fingerprint(ProfileImageConfig image)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(image)));
        return Convert.ToHexString(hash)[..16];
    }
}
