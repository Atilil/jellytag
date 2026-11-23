using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JellyTag.Configuration;

/// <summary>
/// Repairs profiles and library assignments so that the rest of the plugin can rely on their rules.
/// Runs after every load, migration and import, because a configuration can be edited by hand.
/// </summary>
public static class ConfigNormalizer
{
    private static readonly ImageType[] SupportedImageTypes =
        { ImageType.Primary, ImageType.Thumb, ImageType.Backdrop, ImageType.Logo };

    public static void Normalize(List<BadgeProfile> profiles, List<LibraryAssignment> assignments)
    {
        var uniqueProfiles = profiles.Where(p => p != null).DistinctBy(p => p.Id).ToList();
        profiles.Clear();
        profiles.AddRange(uniqueProfiles);

        if (profiles.All(p => p.Id != BadgeProfile.DefaultProfileId))
        {
            profiles.Insert(0, new BadgeProfile { Id = BadgeProfile.DefaultProfileId, Name = "Default" });
        }

        foreach (var profile in profiles)
        {
            NormalizeImages(profile.Images);
        }

        var profileIds = profiles.Select(p => p.Id).ToHashSet();
        var uniqueAssignments = assignments
            .Where(a => a != null && !string.IsNullOrEmpty(a.LibraryId))
            .DistinctBy(a => a.LibraryId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var assignment in uniqueAssignments)
        {
            if (assignment.ProfileId is { } id && !profileIds.Contains(id))
            {
                assignment.ProfileId = BadgeProfile.DefaultProfileId;
            }
        }

        assignments.Clear();
        assignments.AddRange(uniqueAssignments);
    }

    private static void NormalizeImages(List<ProfileImageConfig> images)
    {
        var kept = images
            .Where(i => i != null && SupportedImageTypes.Contains(i.ImageType))
            .DistinctBy(i => i.ImageType)
            .ToList();
        foreach (var type in SupportedImageTypes.Where(t => kept.All(i => i.ImageType != t)))
        {
            kept.Add(new ProfileImageConfig { ImageType = type, Enabled = false });
        }

        // Read the original values so the result does not depend on the order of the entries:
        // a reference is valid only when its target keeps its own zones.
        var sameAsByType = kept.ToDictionary(i => i.ImageType, i => i.SameAs);
        foreach (var image in kept)
        {
            if (image.SameAs is { } target
                && (target == image.ImageType || !sameAsByType.ContainsKey(target) || sameAsByType[target] != null))
            {
                image.SameAs = null;
            }

            NormalizeZones(image.Zones);
        }

        images.Clear();
        images.AddRange(kept.OrderBy(i => Array.IndexOf(SupportedImageTypes, i.ImageType)));
    }

    private static void NormalizeZones(List<BadgeZone> zones)
    {
        var merged = new List<BadgeZone>();
        foreach (var zone in zones.Where(z => z != null))
        {
            var existing = merged.FirstOrDefault(z => z.Position == zone.Position);
            if (existing == null)
            {
                merged.Add(zone);
            }
            else
            {
                existing.Lines.AddRange(zone.Lines);
            }
        }

        var seen = new HashSet<PanelType>();
        foreach (var zone in merged)
        {
            foreach (var line in zone.Lines.Where(l => l != null))
            {
                line.Panels.RemoveAll(p => p == null || !seen.Add(p.Type));
            }

            zone.Lines.RemoveAll(l => l == null || l.Panels.Count == 0);
        }

        zones.Clear();
        zones.AddRange(merged.Where(z => z.Lines.Count > 0));
    }
}
