using System.Text.Json.Serialization;

namespace Dotty.Settings;

[JsonSerializable(typeof(AppSettings))]
internal partial class SettingsJsonContext : JsonSerializerContext;
