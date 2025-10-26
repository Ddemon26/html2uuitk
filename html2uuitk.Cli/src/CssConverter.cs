using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ExCSS;

namespace html2uuitk.Cli;

internal sealed class CssConverter
{
    private static readonly Regex LeadingDecimalRegex = new(@"(^|\s)\.(\d)", RegexOptions.Compiled);

    private readonly Config _config;
    private readonly Dictionary<string, UssProperty> _ussProperties;
    private readonly HashSet<string> _breakingSelectors;
    private readonly StylesheetParser _parser = new();

    public CssConverter(
        Config config,
        Dictionary<string, UssProperty> ussProperties,
        IEnumerable<string> breakingSelectors)
    {
        _config = config;
        _ussProperties = new Dictionary<string, UssProperty>(ussProperties, StringComparer.OrdinalIgnoreCase);
        _breakingSelectors = new HashSet<string>(breakingSelectors, StringComparer.OrdinalIgnoreCase);
    }

    public string Convert(string cssContent)
    {
        var stylesheet = _parser.Parse(cssContent);
        return CssToUss(stylesheet.StyleRules);
    }

    private string CssToUss(IEnumerable<IStyleRule> rules)
    {
        var result = new StringBuilder();
        var notImplemented = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unsupported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules)
        {
            var ignoreRule = false;

            // Get selector text using reflection or string representation
            var selectorText = ExCssReflection.GetSelectorText(rule);

            if (string.IsNullOrWhiteSpace(selectorText))
                continue;

            var segments = selectorText.Split(',', StringSplitOptions.RemoveEmptyEntries);

            for (var segIndex = 0; segIndex < segments.Length; segIndex++)
            {
                var selector = segments[segIndex].Trim();
                var selectorParts = selector.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                for (var i = 0; i < selectorParts.Length; i++)
                {
                    var segment = selectorParts[i];

                    // Filter out unsupported pseudo-elements (::before, ::after, etc.)
                    if (HasUnsupportedPseudoElement(segment))
                    {
                        ignoreRule = true;
                        continue;
                    }

                    // Transform HTML tags to Unity UI elements, preserving pseudo-classes
                    var transformed = TransformSelectorWithPseudoClasses(segment);
                    selectorParts[i] = transformed;

                    foreach (var breaking in _breakingSelectors)
                    {
                        if (segment.Contains(breaking, StringComparison.OrdinalIgnoreCase))
                        {
                            ignoreRule = true;
                        }
                    }
                }

                segments[segIndex] = string.Join(' ', selectorParts);
            }

            var finalSelectorText = string.Join(", ", segments);
            var builder = new StringBuilder();

            builder.Append(finalSelectorText.Equals("body", StringComparison.OrdinalIgnoreCase) ? ":root" : finalSelectorText)
                   .Append(" {\n");

            var validDeclarations = 0;

            // Get declarations using reflection
            var declarations = ExCssReflection.GetDeclarations(rule);

            foreach (var declaration in declarations)
            {
                var property = TransformProperty(declaration.Key);
                var value = declaration.Value;

                if (_ussProperties.TryGetValue(property, out var metadata))
                {
                    if (metadata.Native)
                    {
                        var translated = TranslateValue(value, property);
                        // Only include property if it has a value
                        if (!string.IsNullOrWhiteSpace(translated))
                        {
                            builder.Append("    ")
                                   .Append(property)
                                   .Append(": ")
                                   .Append(translated)
                                   .Append(";\n");
                            builder.Append(GetExtras(property, translated));
                            validDeclarations++;
                        }
                    }
                    else
                    {
                        notImplemented.Add(property);
                    }
                }
                else
                {
                    unsupported.Add(property);
                }
            }

            builder.Append("}\n");

            if (ignoreRule || validDeclarations == 0)
            {
                Console.WriteLine($"- Empty/invalid ruleset discarded: {finalSelectorText}");
            }
            else
            {
                result.Append(builder);
            }
        }

        if (unsupported.Count > 0)
        {
            Console.WriteLine($"- UI Toolkit doesn't support: {string.Join(", ", unsupported)}");
        }

        if (notImplemented.Count > 0)
        {
            Console.WriteLine($"- Not implemented yet: {string.Join(", ", notImplemented)}");
        }

        return result.ToString();
    }

    private string TranslateValue(string value, string property)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var normalized = LeadingDecimalRegex.Replace(value, "$10.$2");

        // Handle viewport units
        normalized = normalized.Replace("vw", "%", StringComparison.OrdinalIgnoreCase)
                               .Replace("vh", "%", StringComparison.OrdinalIgnoreCase);

        // Handle Unity-specific property conversions
        normalized = ConvertValueForUnity(property, normalized);

        if (string.Equals(property, "-unity-font", StringComparison.OrdinalIgnoreCase))
        {
            var assetPath = GetAssetPath(normalized);
            return assetPath ?? string.Empty; // Return empty to omit the property
        }

        if (string.Equals(property, "letter-spacing", StringComparison.OrdinalIgnoreCase))
        {
            if (normalized.EndsWith("px", StringComparison.OrdinalIgnoreCase)
                && double.TryParse(normalized[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
            {
                var adjusted = Math.Round(numeric * 2, MidpointRounding.AwayFromZero);
                return $"{adjusted.ToString("0", CultureInfo.InvariantCulture)}px";
            }
        }

        // Handle CSS custom properties for Unity
        if (property.StartsWith("--unity-", StringComparison.OrdinalIgnoreCase))
        {
            // Unity USS custom properties - keep as is
            return normalized;
        }

        return normalized;
    }

    private static string ConvertValueForUnity(string property, string value)
    {
        return property.ToLowerInvariant() switch
        {
            "-unity-font-definition" => string.Empty, // Will be handled separately
            "border-top-left-radius" or "border-top-right-radius" or "border-bottom-right-radius" or "border-bottom-left-radius"
                => ConvertBorderRadius(value),
            "background-image" => ConvertBackgroundImage(value),
            "background-color" when value.Contains("gradient", StringComparison.OrdinalIgnoreCase) => "none",
            "font-size" => ConvertEmToPx(value),
            _ => value
        };
    }

    private static string ConvertBorderRadius(string value)
    {
        // Unity USS doesn't support shorthand border-radius with multiple values
        // Take only the first value
        var parts = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[0] : value;
    }

    private static string ConvertBackgroundImage(string value)
    {
        // Unity doesn't support CSS gradients, convert to none
        if (value.Contains("gradient", StringComparison.OrdinalIgnoreCase))
        {
            return "none";
        }
        return value;
    }

    private static string ConvertEmToPx(string value)
    {
        // Convert em units to pixels (assuming 16px = 1em as base)
        if (value.EndsWith("em", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(value[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var emValue))
            {
                var pxValue = emValue * 16; // Standard conversion
                return $"{pxValue:F0}px";
            }
        }
        return value;
    }

    private static string TransformProperty(string property)
    {
        if (string.Equals(property, "background", StringComparison.OrdinalIgnoreCase))
        {
            return "background-color";
        }

        if (string.Equals(property, "font-family", StringComparison.OrdinalIgnoreCase))
        {
            return "-unity-font";
        }

        return property;
    }

    private string GetExtras(string property, string value)
    {
        if (string.Equals(property, "-unity-font", StringComparison.OrdinalIgnoreCase))
        {
            // Only add font-definition if we have an actual font resource
            if (!string.IsNullOrWhiteSpace(GetAssetPath(value)))
            {
                return $"\t-unity-font-definition: {GetAssetPath(value)};\n";
            }
        }

        return string.Empty;
    }

    private string? GetAssetPath(string value)
    {
        var cleaned = value.Trim('\'', '"');

        if (_config.Assets.TryGetValue(cleaned, out var asset)
            && !string.IsNullOrWhiteSpace(asset.Path))
        {
            return asset.Path;
        }

        return null;
    }

    private static string TransformSelectorWithPseudoClasses(string selector)
    {
        // Unity supported pseudo-classes that we want to preserve
        var supportedPseudoClasses = new[]
        {
            ":hover", ":active", ":focus", ":disabled", ":enabled",
            ":checked", ":inactive", ":selected", ":root"
        };

        // Check if the selector contains a pseudo-class
        foreach (var pseudoClass in supportedPseudoClasses)
        {
            var index = selector.IndexOf(pseudoClass, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                // Split into base selector and pseudo-class part
                var baseSelector = selector[..index];
                var pseudoPart = selector[index..];

                // Transform the base selector
                if (TagMappings.GetUiTagForSelector(baseSelector) is { } mapped)
                {
                    return mapped.Replace("ui:", string.Empty, StringComparison.OrdinalIgnoreCase) + pseudoPart;
                }

                return baseSelector + pseudoPart;
            }
        }

        // No pseudo-class found, do normal transformation
        if (TagMappings.GetUiTagForSelector(selector) is { } directMapped)
        {
            return directMapped.Replace("ui:", string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return selector;
    }

    private static bool HasUnsupportedPseudoElement(string selector)
    {
        // Unity doesn't support these pseudo-elements
        var unsupportedPseudoElements = new[]
        {
            "::before", "::after", "::first-letter", "::first-line",
            ":before", ":after", ":first-letter", ":first-line"
        };

        return unsupportedPseudoElements.Any(pseudo =>
            selector.Contains(pseudo, StringComparison.OrdinalIgnoreCase));
    }
}
