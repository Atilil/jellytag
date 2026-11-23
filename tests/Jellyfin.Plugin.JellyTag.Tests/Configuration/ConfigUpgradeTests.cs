using System.Text.Json;
using System.Xml.Serialization;
using Jellyfin.Plugin.JellyTag.Configuration;

namespace Jellyfin.Plugin.JellyTag.Tests.Configuration;

public class ConfigUpgradeTests
{
    [Fact]
    public void FreshConfiguration_IsConvertedOnce()
    {
        var config = new PluginConfiguration();

        Assert.True(config.Upgrade());
        Assert.Equal(PluginConfiguration.CurrentConfigVersion, config.ConfigVersion);
        Assert.Equal(BadgeProfile.DefaultProfileId, Assert.Single(config.Profiles).Id);

        Assert.False(config.Upgrade());
        Assert.Single(config.Profiles);
    }

    [Fact]
    public void CurrentFormat_IsOnlyRepaired()
    {
        var config = new PluginConfiguration { ConfigVersion = PluginConfiguration.CurrentConfigVersion };
        config.ExcludedLibraryIds.Add("aaa");

        Assert.False(config.Upgrade());

        // The 3.x fields are no longer read: the exclusion is not turned into an assignment.
        Assert.Empty(config.LibraryAssignments);
        Assert.Equal(BadgeProfile.DefaultProfileId, Assert.Single(config.Profiles).Id);
    }

    // Jellyfin saves with XmlSerializer, which adds the saved items to the lists the constructor creates.
    [Fact]
    public void SavedConfiguration_LoadsWithoutDuplicates()
    {
        var config = new PluginConfiguration();
        config.Upgrade();
        var serializer = new XmlSerializer(typeof(PluginConfiguration));

        using var writer = new StringWriter();
        serializer.Serialize(writer, config);
        using var reader = new StringReader(writer.ToString());
        var loaded = (PluginConfiguration)serializer.Deserialize(reader)!;

        Assert.Equal(PluginConfiguration.CurrentConfigVersion, loaded.ConfigVersion);
        var profile = Assert.Single(loaded.Profiles);
        Assert.Equal(4, profile.Images.Count);
        Assert.False(loaded.Upgrade());
    }

    // The export of the configuration page, imported back.
    [Fact]
    public void Export_ImportsBackUnchanged()
    {
        var config = new PluginConfiguration();
        config.Upgrade();

        var json = JsonSerializer.Serialize(config);
        var imported = JsonSerializer.Deserialize<PluginConfiguration>(json)!;

        Assert.False(imported.Upgrade());
        Assert.Equal(json, JsonSerializer.Serialize(imported));
    }
}
