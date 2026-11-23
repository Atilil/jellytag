using Jellyfin.Plugin.JellyTag.Configuration;
using MediaBrowser.Model.Entities;
using SkiaSharp;

namespace Jellyfin.Plugin.JellyTag.Services;

/// <summary>
/// The encoding of a drawn image: format, MIME type and cache file extension.
/// </summary>
internal static class ImageEncoding
{
    /// <summary>
    /// Extensions of every format the cache can hold, for listing and deleting cache files.
    /// </summary>
    internal static readonly string[] CacheExtensions = { ".jpg", ".webp", ".png" };

    /// <summary>
    /// Format of the images drawn for an image type. A logo is a transparent image and JPEG has no
    /// alpha channel, so a logo becomes PNG when the setting is JPEG. WebP keeps the alpha channel.
    /// </summary>
    internal static OutputImageFormat FormatFor(ImageType imageType, OutputImageFormat setting) =>
        imageType == ImageType.Logo && setting == OutputImageFormat.Jpeg ? OutputImageFormat.Png : setting;

    internal static string ContentType(OutputImageFormat format) => format switch
    {
        OutputImageFormat.WebP => "image/webp",
        OutputImageFormat.Png => "image/png",
        _ => "image/jpeg"
    };

    internal static string Extension(OutputImageFormat format) => format switch
    {
        OutputImageFormat.WebP => ".webp",
        OutputImageFormat.Png => ".png",
        _ => ".jpg"
    };

    internal static SKData Encode(SKImage image, OutputImageFormat format, PluginConfiguration config) => format switch
    {
        OutputImageFormat.WebP => image.Encode(SKEncodedImageFormat.Webp, Math.Clamp(config.WebPQuality, 1, 100)),

        // PNG is lossless: Skia ignores the quality.
        OutputImageFormat.Png => image.Encode(SKEncodedImageFormat.Png, 100),
        _ => image.Encode(SKEncodedImageFormat.Jpeg, Math.Clamp(config.JpegQuality, 1, 100))
    };
}
