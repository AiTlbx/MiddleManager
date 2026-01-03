using Ai.Tlbx.MidTerm.Settings;
using System.Text.Json.Serialization;

namespace Ai.Tlbx.MidTerm.Settings
{
    [JsonSerializable(typeof(MidTermSettings))]
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        WriteIndented = true,
        UseStringEnumConverter = true)]
    public partial class SettingsJsonContext : JsonSerializerContext
    {
    }
}
