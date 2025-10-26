using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace html2uuitk.Cli;

internal sealed class Config
{
    [JsonPropertyName("assets")]
    public Dictionary<string, AssetConfig> Assets { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("options")]
    public ConfigOptions Options { get; set; } = new();
}

internal sealed class AssetConfig
{
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
}

internal sealed class ConfigOptions
{
    [JsonPropertyName("uppercase")]
    public bool Uppercase { get; set; }

    [JsonPropertyName("focusable")]
    public bool? Focusable { get; set; }
}

internal static class ConfigLoader
{
    public static Config Load(string path)
    {
        using var stream = File.OpenRead(path);
        var config = JsonSerializer.Deserialize<Config>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return config ?? new Config();
    }
}

internal sealed class UssProperty
{
    [JsonPropertyName("native")]
    public bool Native { get; set; }

    [JsonPropertyName("inherited")]
    public bool Inherited { get; set; }

    [JsonPropertyName("animatable")]
    public string? Animatable { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}
