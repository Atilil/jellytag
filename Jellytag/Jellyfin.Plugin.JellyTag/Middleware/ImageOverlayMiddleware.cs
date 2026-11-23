using System.Text.RegularExpressions;
using Jellyfin.Plugin.JellyTag.Configuration;
using Jellyfin.Plugin.JellyTag.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Model.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.JellyTag.Middleware;

/// <summary>
/// Middleware that intercepts Jellyfin image requests and adds quality badge overlays.
/// </summary>
public partial class ImageOverlayMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ImageOverlayMiddleware> _logger;

    [GeneratedRegex(@"/Items/([0-9a-f]{32}|[0-9a-f-]{36})/Images/(Primary|Thumb|Backdrop|Logo)(/\d+)?$", RegexOptions.IgnoreCase)]
    private static partial Regex ImagePathRegex();

    public ImageOverlayMiddleware(RequestDelegate next, ILogger<ImageOverlayMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IQualityDetectionService qualityService,
        IImageOverlayService overlayService,
        IImageCacheService cacheService,
        MediaBrowser.Controller.Library.ILibraryManager libraryManager)
    {
        var path = context.Request.Path.Value;
        if (path == null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var match = ImagePathRegex().Match(path);
        if (!match.Success)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var config = Plugin.Instance?.Configuration;
        if (config == null || !config.Enabled)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var itemIdStr = match.Groups[1].Value;
        var imageType = match.Groups[2].Value;

        if (!Guid.TryParse(itemIdStr, out var itemId))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var item = libraryManager.GetItemById(itemId);
        if (item == null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        if (item is not (Movie or Series or Season or Episode or Video))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Items outside every library (intros added by plugins such as Local Intros) get no profile.
        var libraries = libraryManager.GetCollectionFolders(item).Select(f => (Id: f.Id.ToString("N"), f.Name)).ToList();
        var profile = BadgeSettingsResolver.ResolveProfile(config, libraries.Select(l => l.Id).ToList());
        if (profile == null || (item is Episode && !profile.ShowOnEpisodes))
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var requestedType = Enum.Parse<ImageType>(imageType, ignoreCase: true);
        var imageConfig = BadgeSettingsResolver.ResolveImage(
            profile, BadgeSettingsResolver.TargetImageType(requestedType, item is Episode));
        if (imageConfig == null)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        // Detect all badges and filter by config
        var allBadges = qualityService.DetectAllBadges(item).Concat(LibraryBadges.For(libraries, config)).ToList();
        _logger.LogDebug("DetectAllBadges for {Item}: {Count} badges found: {Badges}",
            item.Name, allBadges.Count, string.Join(", ", allBadges.Select(b => $"{b.Category}:{b.BadgeKey}")));

        var visibleBadges = allBadges.Where(b => overlayService.ShouldShowBadge(b, imageConfig)).ToList();
        _logger.LogDebug("Visible badges after filter: {Count}: {Badges}",
            visibleBadges.Count, string.Join(", ", visibleBadges.Select(b => b.BadgeKey)));

        if (visibleBadges.Count == 0)
        {
            await _next(context).ConfigureAwait(false);
            return;
        }

        var badgeKey = string.Join("_", visibleBadges.Select(b => b.BadgeKey));
        _logger.LogInformation("Applying {Count} badges to {Item}: {BadgeKey}", visibleBadges.Count, item.Name, badgeKey);

        var query = context.Request.QueryString.Value ?? string.Empty;
        var tag = context.Request.Query["tag"].FirstOrDefault() ?? item.DateModified.Ticks.ToString();
        // The index tells the backdrops of an item apart when the request carries no tag.
        var imageTag = $"{tag}_{imageType}{match.Groups[3].Value}_{query}_{BadgeSettingsResolver.Fingerprint(imageConfig)}";
        var outputFormat = ImageEncoding.FormatFor(imageConfig.ImageType, config.OutputFormat);

        var cachedImage = await cacheService.GetCachedImageAsync(itemId, badgeKey, imageTag, outputFormat).ConfigureAwait(false);
        if (cachedImage != null)
        {
            await using (cachedImage.ConfigureAwait(false))
            {
                context.Response.ContentType = ImageEncoding.ContentType(outputFormat);
                context.Response.ContentLength = cachedImage.Length;
                await cachedImage.CopyToAsync(context.Response.Body).ConfigureAwait(false);
            }

            return;
        }

        var originalBody = context.Response.Body;
        using var capturedBody = new MemoryStream();
        context.Response.Body = capturedBody;

        try
        {
            await _next(context).ConfigureAwait(false);

            if (context.Response.StatusCode != 200 || capturedBody.Length == 0)
            {
                capturedBody.Position = 0;
                await capturedBody.CopyToAsync(originalBody).ConfigureAwait(false);
                return;
            }

            capturedBody.Position = 0;

            (Stream resultStream, string contentType) result;
            try
            {
                result = await overlayService.AddBadgeOverlaysAsync(capturedBody, visibleBadges, imageConfig).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add badge overlay, serving original image");
                capturedBody.Position = 0;
                await capturedBody.CopyToAsync(originalBody).ConfigureAwait(false);
                return;
            }

            await using (result.resultStream.ConfigureAwait(false))
            {
                result.resultStream.Position = 0;
                await cacheService.CacheImageAsync(itemId, badgeKey, imageTag, result.resultStream, outputFormat).ConfigureAwait(false);

                result.resultStream.Position = 0;
                context.Response.ContentType = result.contentType;
                context.Response.ContentLength = result.resultStream.Length;
                await result.resultStream.CopyToAsync(originalBody).ConfigureAwait(false);
            }
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }
}
