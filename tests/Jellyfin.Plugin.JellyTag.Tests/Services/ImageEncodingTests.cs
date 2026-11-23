using Jellyfin.Plugin.JellyTag.Configuration;
using Jellyfin.Plugin.JellyTag.Services;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JellyTag.Tests.Services;

// Q14: a clear logo is a transparent PNG and JPEG has no alpha channel. The pixels themselves are
// checked by the integration tests: drawing needs the native Skia library of a Jellyfin server.
public class ImageEncodingTests
{
    [Theory]
    [InlineData(ImageType.Logo, OutputImageFormat.Jpeg, OutputImageFormat.Png)]
    [InlineData(ImageType.Logo, OutputImageFormat.WebP, OutputImageFormat.WebP)]
    [InlineData(ImageType.Primary, OutputImageFormat.Jpeg, OutputImageFormat.Jpeg)]
    [InlineData(ImageType.Backdrop, OutputImageFormat.WebP, OutputImageFormat.WebP)]
    public void OnlyALogo_SwitchesJpegToPng(ImageType type, OutputImageFormat setting, OutputImageFormat expected)
    {
        Assert.Equal(expected, ImageEncoding.FormatFor(type, setting));
    }

    [Theory]
    [InlineData(OutputImageFormat.Jpeg, "image/jpeg", ".jpg")]
    [InlineData(OutputImageFormat.WebP, "image/webp", ".webp")]
    [InlineData(OutputImageFormat.Png, "image/png", ".png")]
    public void ContentTypeAndExtension_MatchTheFormat(OutputImageFormat format, string contentType, string extension)
    {
        Assert.Equal(contentType, ImageEncoding.ContentType(format));
        Assert.Equal(extension, ImageEncoding.Extension(format));
        Assert.Contains(extension, ImageEncoding.CacheExtensions);
    }
}
