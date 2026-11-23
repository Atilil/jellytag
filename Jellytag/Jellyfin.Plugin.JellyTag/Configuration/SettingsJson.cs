using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Jellyfin.Plugin.JellyTag.Configuration;

/// <summary>
/// JSON options of the configuration export.
/// </summary>
internal static class SettingsJson
{
    /// <summary>
    /// Leaves out empty values and the properties whose ShouldSerialize&lt;Name&gt; method returns
    /// false, so that the export holds the same settings as the saved file.
    /// </summary>
    internal static readonly JsonSerializerOptions Export = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { ApplyShouldSerializeMethods } }
    };

    // System.Text.Json does not know the ShouldSerialize<Name> convention of XmlSerializer.
    private static void ApplyShouldSerializeMethods(JsonTypeInfo type)
    {
        if (type.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (var property in type.Properties)
        {
            var method = type.Type.GetMethod("ShouldSerialize" + property.Name, BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            if (method?.ReturnType == typeof(bool))
            {
                property.ShouldSerialize = (owner, _) => (bool)method.Invoke(owner, null)!;
            }
        }
    }
}
