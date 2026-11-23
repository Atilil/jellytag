using Jellyfin.Plugin.JellyTag.Configuration;

namespace Jellyfin.Plugin.JellyTag.Services;

/// <summary>
/// Badges naming the libraries that hold an item.
/// </summary>
internal static class LibraryBadges
{
    internal const string KeyPrefix = "library-";

    /// <summary>
    /// One badge per library, best first: in the order of the assignment table, then the libraries
    /// without an assignment in the order Jellyfin gives them. "Highest only" keeps the first one.
    /// </summary>
    /// <param name="libraries">Id ("N" format) and name of each library that holds the item.</param>
    internal static List<BadgeInfo> For(IReadOnlyList<(string Id, string Name)> libraries, PluginConfiguration config)
    {
        int Rank(string id)
        {
            var index = config.LibraryAssignments.FindIndex(a => string.Equals(a.LibraryId, id, StringComparison.OrdinalIgnoreCase));
            return index < 0 ? int.MaxValue : index;
        }

        return libraries
            .OrderBy(l => Rank(l.Id))
            .Select(l =>
            {
                // The key holds the id, not the name, so that a renamed library keeps its custom text and image.
                var key = KeyPrefix + l.Id.ToLowerInvariant();
                var customText = config.CustomBadgeTexts
                    .FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.OrdinalIgnoreCase))?.Text;
                return new BadgeInfo
                {
                    Category = BadgeCategory.Library,
                    BadgeKey = key,

                    // Uploaded images only: no library badge is embedded, so without an upload the
                    // image style falls back to text.
                    ResourceFileName = "badge-" + key + ".svg",
                    DisplayText = string.IsNullOrEmpty(customText) ? l.Name : customText
                };
            })
            .ToList();
    }
}
