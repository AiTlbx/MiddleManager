using System.Text.Json.Serialization;

namespace Ai.Tlbx.MiddleManager.Services;

[JsonSerializable(typeof(VersionManifest))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class VersionManifestContext : JsonSerializerContext
{
}
