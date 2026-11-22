using Jellyfin.Plugin.JellyTag.Services;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.JellyTag.Tests.Services;

public class DetectLanguagesTests
{
    private static MediaStream Audio(string language, bool isDefault = false) =>
        new() { Type = MediaStreamType.Audio, Language = language, IsDefault = isDefault };

    private static string Keys(params MediaStream[] streams) =>
        string.Join(",", QualityDetectionService.DetectLanguages(streams.ToList()).Select(b => b.BadgeKey));

    // #10: the "Highest" mode shows only the first flag, which must be the default track's.
    [Fact]
    public void DefaultTrack_ComesFirst()
    {
        Assert.Equal("jpn,deu", Keys(Audio("deu"), Audio("jpn", isDefault: true)));
    }

    // #23: the other flags do not depend on the track order in the file.
    [Fact]
    public void OtherTracks_AreAlphabetical()
    {
        Assert.Equal("jpn,deu,eng,fra", Keys(Audio("fra"), Audio("eng"), Audio("jpn", isDefault: true), Audio("deu")));
    }

    [Fact]
    public void NoDefaultFlag_FirstTrackComesFirst()
    {
        Assert.Equal("jpn,deu,eng", Keys(Audio("jpn"), Audio("eng"), Audio("deu")));
    }
}
