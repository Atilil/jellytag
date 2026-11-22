using Jellyfin.Plugin.JellyTag.Configuration;
using Jellyfin.Plugin.JellyTag.Services;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JellyTag.Tests.Services;

public class LanguageFilterTests
{
    private static BadgePanelSettings Panel(LanguageFilterMode mode, params string[] codes) =>
        new() { LanguageFilter = mode, LanguageCodes = codes.ToList() };

    // #6: hide the language most of the library is in.
    [Theory]
    [InlineData("eng", false)]
    [InlineData("jpn", true)]
    [InlineData("vff", true)]
    public void AllExcept(string language, bool shown)
    {
        Assert.Equal(shown, ImageOverlayService.IsLanguageShown(language, Panel(LanguageFilterMode.Except, "eng")));
    }

    // #7: only the languages the household reads; codes match in either ISO 639-2 form.
    [Theory]
    [InlineData("fra", true)]
    [InlineData("vq", true)]
    [InlineData("ger", true)]
    [InlineData("eng", false)]
    public void OnlyThese(string language, bool shown)
    {
        Assert.Equal(shown, ImageOverlayService.IsLanguageShown(language, Panel(LanguageFilterMode.Only, "fre", " DEU ")));
    }

    [Fact]
    public void Undefined_HiddenByDefault()
    {
        Assert.False(ImageOverlayService.IsLanguageShown("und", Panel(LanguageFilterMode.All)));
        Assert.True(ImageOverlayService.IsLanguageShown("und", new BadgePanelSettings { HideUndefinedLanguage = false }));
    }

    [Theory]
    [InlineData("eng", SubtitleLabel.Code, "EN")]
    [InlineData("eng", SubtitleLabel.CcCode, "CC EN")]
    [InlineData("eng", SubtitleLabel.Vost, "VOSTEN")]
    [InlineData("rus", SubtitleLabel.Vost, "VOSTR")]   // label of earlier versions kept as is
    [InlineData("xho", SubtitleLabel.Code, "XHO")]     // no short code known
    public void SubtitleLabels(string language, SubtitleLabel label, string expected)
    {
        Assert.Equal(expected, ImageOverlayService.FormatSubtitleLabel(language, label));
    }

    private static MediaStream Stream(MediaStreamType type, string language) => new() { Type = type, Language = language };

    // #7: every subtitle language is detected; the panel decides, knowing which ones have audio.
    [Fact]
    public void Subtitles_AllDetected_WithTheirAudioMatch()
    {
        var badges = QualityDetectionService.DetectLanguages(
        [
            Stream(MediaStreamType.Audio, "fre"),
            Stream(MediaStreamType.Subtitle, "fra"),
            Stream(MediaStreamType.Subtitle, "eng"),
            Stream(MediaStreamType.Subtitle, "eng")
        ]).Where(b => b.Category == BadgeCategory.Subtitle).ToList();

        Assert.Equal("subeng,subfra", string.Join(",", badges.Select(b => b.BadgeKey)));
        Assert.False(badges[0].HasMatchingAudio);
        Assert.True(badges[1].HasMatchingAudio);   // "fre" audio matches "fra" subtitles
    }
}
