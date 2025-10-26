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
    private static readonly Regex LeadingDecimalRegex = new(@"(^|[\s,(])\.(\d)", RegexOptions.Compiled);

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
        // Pre-process CSS to remove unsupported pseudo-classes from multi-selectors
        // This prevents ExCSS from silently failing to parse entire rules
        cssContent = PreprocessUnsupportedPseudoClasses(cssContent);

        var stylesheet = _parser.Parse(cssContent);
        return CssToUss(stylesheet.StyleRules);
    }

    private static string PreprocessUnsupportedPseudoClasses(string css)
    {
        // ExCSS doesn't recognize newer pseudo-classes like :focus-visible
        // and will silently fail to parse ANY rule containing them.
        // So we need to strip them from comma-separated selectors before parsing.

        var unsupportedPseudos = new[]
        {
            ":focus-visible", ":focus-within", ":nth-child", ":nth-of-type",
            ":first-child", ":last-child", ":only-child", ":first-of-type", ":last-of-type"
        };

        // Pattern: find selector groups (comma-separated) followed by opening brace
        var rulePattern = new Regex(@"([^{}]+)\{", RegexOptions.Compiled);

        return rulePattern.Replace(css, match =>
        {
            var selectors = match.Groups[1].Value;
            var segments = selectors.Split(',');
            var filteredSegments = new List<string>();

            foreach (var segment in segments)
            {
                var hasUnsupported = false;
                foreach (var pseudo in unsupportedPseudos)
                {
                    if (segment.Contains(pseudo, StringComparison.OrdinalIgnoreCase))
                    {
                        hasUnsupported = true;
                        break;
                    }
                }

                if (!hasUnsupported)
                {
                    filteredSegments.Add(segment);
                }
            }

            // If we filtered out all selectors, keep one selector but make it invalid
            // so ExCSS will skip it instead of causing a parse error
            if (filteredSegments.Count == 0)
            {
                return "__invalid__ {";
            }

            return string.Join(", ", filteredSegments) + " {";
        });
    }

    private string CssToUss(IEnumerable<IStyleRule> rules)
    {
        var result = new StringBuilder();
        var notImplemented = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unsupported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in rules)
        {
            // Get selector text using reflection or string representation
            var selectorText = ExCssReflection.GetSelectorText(rule);

            if (string.IsNullOrWhiteSpace(selectorText))
                continue;

            var segments = selectorText.Split(',', StringSplitOptions.RemoveEmptyEntries);
            var validSegments = new List<string>();

            for (var segIndex = 0; segIndex < segments.Length; segIndex++)
            {
                var selector = segments[segIndex].Trim();
                var selectorParts = selector.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var isValidSegment = true;

                for (var i = 0; i < selectorParts.Length; i++)
                {
                    var segment = selectorParts[i];

                    // Filter out unsupported pseudo-elements (::before, ::after, etc.)
                    if (HasUnsupportedPseudoElement(segment))
                    {
                        isValidSegment = false;
                        break;
                    }

                    // Filter out unsupported pseudo-classes
                    if (HasUnsupportedPseudoClass(segment))
                    {
                        isValidSegment = false;
                        break;
                    }

                    // Transform HTML tags to Unity UI elements, preserving pseudo-classes
                    var transformed = TransformSelectorWithPseudoClasses(segment);
                    selectorParts[i] = transformed;

                    foreach (var breaking in _breakingSelectors)
                    {
                        if (segment.Contains(breaking, StringComparison.OrdinalIgnoreCase))
                        {
                            isValidSegment = false;
                            break;
                        }
                    }

                    if (!isValidSegment)
                        break;
                }

                if (isValidSegment)
                {
                    validSegments.Add(string.Join(' ', selectorParts));
                }
            }

            // Skip if no valid segments remain
            if (validSegments.Count == 0)
                continue;

            var finalSelectorText = string.Join(", ", validSegments);
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

                // CSS custom properties (variables) - Unity USS supports these
                if (property.StartsWith("--", StringComparison.Ordinal))
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        builder.Append("    ")
                               .Append(property)
                               .Append(": ")
                               .Append(value)
                               .Append(";\n");
                        validDeclarations++;
                    }
                    continue;
                }

                // Try to convert unsupported properties to Unity USS equivalents
                var unityEquivalent = TryConvertToUnityEquivalent(property, value);
                if (unityEquivalent != null)
                {
                    foreach (var (unityProp, unityVal) in unityEquivalent)
                    {
                        builder.Append("    ")
                               .Append(unityProp)
                               .Append(": ")
                               .Append(unityVal)
                               .Append(";\n");
                        validDeclarations++;
                    }
                    continue;
                }

                // Fallback for unsupported properties - try to get basic equivalent
                var fallback = TryGetFallbackProperty(property, value);
                if (fallback != null)
                {
                    foreach (var (fallbackProp, fallbackVal) in fallback)
                    {
                        // Check if fallback property is supported
                        if (_ussProperties.TryGetValue(fallbackProp, out var fallbackMeta) && fallbackMeta.Native)
                        {
                            var translated = TranslateValue(fallbackVal, fallbackProp);
                            if (!string.IsNullOrWhiteSpace(translated))
                            {
                                builder.Append("    ")
                                       .Append(fallbackProp)
                                       .Append(": ")
                                       .Append(translated)
                                       .Append(";\n");
                                validDeclarations++;
                            }
                        }
                    }
                }

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

            if (validDeclarations == 0)
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
            "background-size" => TakeFirstCommaSeparatedValue(value),
            "text-shadow" => TakeFirstCommaSeparatedValue(value),
            "position" when value.Equals("fixed", StringComparison.OrdinalIgnoreCase) => "absolute",
            "font-size" or "padding-top" or "padding-right" or "padding-bottom" or "padding-left"
                or "margin-top" or "margin-right" or "margin-bottom" or "margin-left"
                or "width" or "height" or "top" or "right" or "bottom" or "left"
                => ConvertRemOrEmToPx(value),
            "letter-spacing" => ConvertLetterSpacing(value),
            _ => ConvertRemOrEmToPx(value) // Try converting all values with rem/em
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

    private static string ConvertRemOrEmToPx(string value)
    {
        // Convert rem units to pixels (assuming 16px = 1rem as base)
        if (value.EndsWith("rem", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(value[..^3], NumberStyles.Float, CultureInfo.InvariantCulture, out var remValue))
            {
                var pxValue = remValue * 16; // Standard conversion
                return $"{pxValue:F0}px";
            }
        }

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

    private static string ConvertLetterSpacing(string value)
    {
        // Unity letter-spacing needs pixel values, not em/rem
        var converted = ConvertRemOrEmToPx(value);

        // If it was already in px, double it (Unity letter-spacing works differently)
        if (converted.EndsWith("px", StringComparison.OrdinalIgnoreCase)
            && double.TryParse(converted[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
        {
            var adjusted = Math.Round(numeric * 2, MidpointRounding.AwayFromZero);
            return $"{adjusted.ToString("0", CultureInfo.InvariantCulture)}px";
        }

        return converted;
    }

    private static string TakeFirstCommaSeparatedValue(string value)
    {
        // Unity doesn't support comma-separated multiple values for some properties
        // Take only the first value before comma, but skip commas inside parentheses (like rgba())

        var depth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth--;
            }
            else if (c == ',' && depth == 0)
            {
                // Found a comma at depth 0 (not inside parentheses)
                return value[..i].Trim();
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

    private static List<(string property, string value)>? TryConvertToUnityEquivalent(string property, string value)
    {
        // Convert unsupported CSS properties to Unity USS equivalents where possible
        var lowerProp = property.ToLowerInvariant();

        return lowerProp switch
        {
            // Display properties
            "display" when value.Contains("flex", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("flex-direction", ConvertFlexDirection(value)),
                ("align-items", "center"),
                ("justify-content", "flex-start")
            },
            "display" when value.Contains("grid", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("flex-direction", "row")
            },
            "display" when value.Contains("block", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("flex-direction", "column")
            },
            "display" when value.Contains("inline-block", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("flex-direction", "row")
            },

            // Flex properties
            "flex-direction" => new List<(string, string)>
            {
                ("flex-direction", ConvertFlexDirection(value))
            },
            "justify-content" => new List<(string, string)>
            {
                ("justify-content", ConvertJustifyContent(value))
            },
            "align-items" => new List<(string, string)>
            {
                ("align-items", ConvertAlignItems(value))
            },
            "align-content" => new List<(string, string)>
            {
                ("align-content", ConvertAlignItems(value))
            },
            "flex-wrap" when value.Contains("wrap", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("flex-wrap", "wrap")
            },
            "flex-grow" => new List<(string, string)>
            {
                ("flex-grow", value)
            },
            "flex-shrink" => new List<(string, string)>
            {
                ("flex-shrink", value)
            },
            "flex-basis" => new List<(string, string)>
            {
                ("flex-basis", ConvertRemOrEmToPx(value))
            },
            "align-self" => new List<(string, string)>
            {
                ("align-self", ConvertAlignItems(value))
            },

            // Text properties
            "text-align" => new List<(string, string)>
            {
                ("-unity-text-align", ConvertTextAlign(value))
            },
            "text-decoration" when value.Contains("underline", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("-unity-text-decoration", "underline")
            },
            "text-decoration" when value.Contains("line-through", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("-unity-text-decoration", "line-through")
            },
            "text-transform" when value.Contains("uppercase", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("-unity-font-style", "bold")
            },
            "white-space" when value.Contains("nowrap", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("white-space", "nowrap")
            },
            "line-height" => new List<(string, string)>
            {
                ("-unity-line-height", ConvertRemOrEmToPx(value))
            },
            "word-break" when value.Contains("break", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("white-space", "normal")
            },
            "text-overflow" when value.Contains("ellipsis", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("-unity-text-overflow", "ellipsis")
            },

            // Visual effects
            "opacity" => new List<(string, string)>
            {
                ("opacity", value)
            },
            "visibility" when value.Contains("hidden", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("display", "none")
            },
            "box-shadow" when !string.IsNullOrWhiteSpace(value) && !value.Equals("none", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("-unity-box-shadow", ConvertBoxShadow(value))
            },
            "filter" when value.Contains("blur", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("-unity-background-scale-mode", "stretch-to-fit")
            },

            // Layout properties
            "float" when value.Contains("left", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("position", "absolute"),
                ("left", "0")
            },
            "float" when value.Contains("right", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("position", "absolute"),
                ("right", "0")
            },
            "clear" => new List<(string, string)>
            {
                ("margin-bottom", "0")
            },

            // Sizing and spacing
            "min-width" => new List<(string, string)>
            {
                ("min-width", ConvertRemOrEmToPx(value))
            },
            "max-width" => new List<(string, string)>
            {
                ("max-width", ConvertRemOrEmToPx(value))
            },
            "min-height" => new List<(string, string)>
            {
                ("min-height", ConvertRemOrEmToPx(value))
            },
            "max-height" => new List<(string, string)>
            {
                ("max-height", ConvertRemOrEmToPx(value))
            },
            "box-sizing" when value.Contains("border-box", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("-unity-overflow-clip-box", "border-box")
            },

            // Border properties
            "border-collapse" when value.Contains("collapse", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("border-left-width", "0"),
                ("border-right-width", "0")
            },
            "outline" when !string.IsNullOrWhiteSpace(value) && !value.Equals("none", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("border-left-width", "1px"),
                ("border-right-width", "1px"),
                ("border-top-width", "1px"),
                ("border-bottom-width", "1px")
            },

            // Background properties (enhanced)
            "background-repeat" when value.Contains("no-repeat", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("-unity-background-scale-mode", "scale-to-fit")
            },
            "background-position" => new List<(string, string)>
            {
                ("-unity-background-position", ConvertBackgroundPosition(value))
            },
            "background-attachment" when value.Contains("fixed", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("-unity-background-scale-mode", "stretch-to-fit")
            },

            // User interaction
            "user-select" when value.Contains("none", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("cursor", "default")
            },
            "pointer-events" when value.Contains("none", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("picking-mode", "Ignore")
            },
            "cursor" => new List<(string, string)>
            {
                ("cursor", ConvertCursor(value))
            },
            "resize" when value.Contains("both", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("-unity-overflow-clip-box", "content-box")
            },

            // Transform properties (basic support)
            "transform" when !string.IsNullOrWhiteSpace(value) && !value.Equals("none", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("transform", ConvertTransform(value))
            },
            "transform-origin" => new List<(string, string)>
            {
                ("transform-origin", ConvertTransformOrigin(value))
            },

            // List properties
            "list-style-type" when value.Contains("none", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("-unity-list-style-type", "none")
            },
            "list-style-position" when value.Contains("inside", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("-unity-list-style-position", "inside")
            },

            // Overflow properties
            "overflow-x" or "overflow-y" => new List<(string, string)>
            {
                ("overflow", ConvertOverflow(value))
            },

            // Unity theme variable mappings
            "border-radius" => new List<(string, string)>
            {
                ("-unity-border-radius", ConvertRemOrEmToPx(TakeFirstCommaSeparatedValue(value)))
            },
            "transition" when !string.IsNullOrWhiteSpace(value) => new List<(string, string)>
            {
                ("transition", ConvertTransition(value))
            },

            _ => null
        };
    }

    private static string ConvertTextAlign(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "left" => "upper-left",
            "center" => "upper-center",
            "right" => "upper-right",
            "justify" => "upper-left",
            _ => value
        };
    }

    private static string ConvertFlexDirection(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "row" or "row-reverse" => "row",
            "column" or "column-reverse" => "column",
            _ => "row"
        };
    }

    private static string ConvertJustifyContent(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "flex-start" or "start" => "flex-start",
            "flex-end" or "end" => "flex-end",
            "center" => "center",
            "space-between" => "space-between",
            "space-around" => "space-around",
            "space-evenly" => "space-around",
            _ => "flex-start"
        };
    }

    private static string ConvertAlignItems(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "flex-start" or "start" => "flex-start",
            "flex-end" or "end" => "flex-end",
            "center" => "center",
            "baseline" => "center",
            "stretch" => "stretch",
            _ => "center"
        };
    }

    private static string ConvertBoxShadow(string value)
    {
        // Simple box-shadow conversion - take the first 3 values (x-offset, y-offset, blur)
        var parts = value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var result = new List<string>();
            for (int i = 0; i < Math.Min(3, parts.Length); i++)
            {
                var part = ConvertRemOrEmToPx(parts[i]);
                if (part.EndsWith("px", StringComparison.OrdinalIgnoreCase))
                {
                    // Convert to numeric values only
                    var numeric = part[..^2];
                    if (double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                    {
                        result.Add(num.ToString("F1", CultureInfo.InvariantCulture));
                    }
                }
            }
            if (result.Count >= 2)
            {
                return string.Join(" ", result) + " 2px rgba(0,0,0,0.2)";
            }
        }
        return "2px 2px 4px rgba(0,0,0,0.2)";
    }

    private static string ConvertBackgroundPosition(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "center" or "center center" => "center",
            "top" or "top center" or "center top" => "top",
            "bottom" or "bottom center" or "center bottom" => "bottom",
            "left" or "left center" or "center left" => "left",
            "right" or "right center" or "center right" => "right",
            _ => "center"
        };
    }

    private static string ConvertCursor(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "pointer" => "link",
            "grab" or "grabbing" => "resize-vertical",
            "text" => "text",
            "move" => "move",
            "not-allowed" => "default",
            "wait" => "default",
            "help" => "default",
            "crosshair" => "default",
            "progress" => "default",
            _ => "default"
        };
    }

    private static string ConvertTransform(string value)
    {
        // Very basic transform support - only handle simple translations
        if (value.Contains("translate", StringComparison.OrdinalIgnoreCase))
        {
            // Extract numeric values from translate function
            var match = Regex.Match(value, @"translate\(([^)]+)\)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var coords = match.Groups[1].Value;
                var parts = coords.Split(',');
                if (parts.Length >= 1)
                {
                    var x = ConvertRemOrEmToPx(parts[0].Trim());
                    return $"translate({x}, 0px)";
                }
            }
        }
        return "none";
    }

    private static string ConvertTransformOrigin(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "center" or "center center" => "center",
            "top" or "top center" => "top center",
            "bottom" or "bottom center" => "bottom center",
            "left" or "left center" => "center left",
            "right" or "right center" => "center right",
            _ => "center"
        };
    }

    private static string ConvertOverflow(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "hidden" => "hidden",
            "scroll" => "scroll",
            "auto" => "scroll",
            "visible" => "visible",
            _ => "visible"
        };
    }

    private static string ConvertTransition(string value)
    {
        // Simplified transition - just take the duration and property
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var duration = ConvertRemOrEmToPx(parts[1]);
            return $"all {duration} ease";
        }
        return "all 0.2s ease";
    }

    private static List<(string property, string value)>? TryGetFallbackProperty(string property, string value)
    {
        // Fallback system for completely unsupported properties
        // This provides basic Unity equivalents to prevent empty style classes
        var lowerProp = property.ToLowerInvariant();

        return lowerProp switch
        {
            // Animation fallbacks
            "animation" when !string.IsNullOrWhiteSpace(value) => new List<(string, string)>
            {
                ("opacity", "1") // Basic animation support
            },
            "animation-delay" or "animation-duration" or "animation-fill-mode"
            or "animation-iteration-count" or "animation-name" or "animation-timing-function"
                => new List<(string, string)>
                {
                    ("opacity", "1")
                },

            // Advanced layout fallbacks
            "grid-template-columns" or "grid-template-rows" => new List<(string, string)>
            {
                ("flex-direction", "row")
            },
            "grid-gap" or "grid-column-gap" or "grid-row-gap" => new List<(string, string)>
            {
                ("margin", ConvertRemOrEmToPx(value))
            },
            "grid-area" or "grid-column" or "grid-row" => new List<(string, string)>
            {
                ("flex-grow", "1")
            },

            // Multi-column layout fallbacks
            "column-count" when value.Contains("2", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("flex-direction", "row")
            },
            "column-gap" => new List<(string, string)>
            {
                ("margin-right", ConvertRemOrEmToPx(value))
            },

            // Advanced text fallbacks
            "writing-mode" => new List<(string, string)>
            {
                ("flex-direction", "column")
            },
            "text-indent" => new List<(string, string)>
            {
                ("margin-left", ConvertRemOrEmToPx(value))
            },
            "letter-spacing" when value.Contains("normal", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("letter-spacing", "0px")
            },
            "word-spacing" => new List<(string, string)>
            {
                ("letter-spacing", ConvertRemOrEmToPx(value))
            },

            // Border and outline fallbacks
            "border-style" when !value.Equals("none", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("border-width", "1px")
            },
            "outline-style" when !value.Equals("none", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("border-width", "1px")
            },
            "outline-width" => new List<(string, string)>
            {
                ("border-width", ConvertRemOrEmToPx(value))
            },
            "outline-color" => new List<(string, string)>
            {
                ("border-color", value)
            },
            "outline-offset" => new List<(string, string)>
            {
                ("margin", ConvertRemOrEmToPx(value))
            },

            // Size and dimension fallbacks
            "aspect-ratio" => new List<(string, string)>
            {
                ("width", "100px"),
                ("height", "100px")
            },
            "object-fit" => new List<(string, string)>
            {
                ("-unity-image-size", "scale-to-fit")
            },
            "object-position" => new List<(string, string)>
            {
                ("-unity-image-position", "center")
            },

            // Advanced visual fallbacks
            "backdrop-filter" => new List<(string, string)>
            {
                ("opacity", "0.9")
            },
            "mix-blend-mode" => new List<(string, string)>
            {
                ("opacity", "0.9")
            },
            "isolation" => new List<(string, string)>
            {
                ("opacity", "1")
            },

            // Table properties fallbacks
            "table-layout" => new List<(string, string)>
            {
                ("width", "auto")
            },
            "border-spacing" => new List<(string, string)>
            {
                ("margin", ConvertRemOrEmToPx(value))
            },

            // Advanced positioning fallbacks
            "z-index" => new List<(string, string)>
            {
                ("position", "relative")
            },

            // Clip and mask fallbacks
            "clip" or "clip-path" => new List<(string, string)>
            {
                ("overflow", "hidden")
            },

            // Print and page fallbacks
            "page-break-after" or "page-break-before" => new List<(string, string)>
            {
                ("margin-bottom", "10px")
            },

            // Scroll behavior fallbacks
            "scroll-behavior" when value.Contains("smooth", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("overflow", "scroll")
            },

            // Advanced selection fallbacks
            "::selection" or "::-moz-selection" => new List<(string, string)>
            {
                ("color", "white"),
                ("background-color", "blue")
            },

            // CSS variables fallbacks (if -- prefix is missing but property looks like a variable)
            _ when property.Contains("color", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("color", value)
            },
            _ when property.Contains("size", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("width", ConvertRemOrEmToPx(value)),
                ("height", ConvertRemOrEmToPx(value))
            },
            _ when property.Contains("width", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("width", ConvertRemOrEmToPx(value))
            },
            _ when property.Contains("height", StringComparison.OrdinalIgnoreCase) => new List<(string, string)>
            {
                ("height", ConvertRemOrEmToPx(value))
            },

            _ => null
        };
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

    private static bool HasUnsupportedPseudoClass(string selector)
    {
        // Unity supported pseudo-classes
        var supportedPseudoClasses = new[]
        {
            ":hover", ":active", ":focus", ":disabled", ":enabled",
            ":checked", ":inactive", ":selected", ":root"
        };

        // Check for :nth-child() and similar unsupported pseudo-classes
        if (selector.Contains(":nth-child", StringComparison.OrdinalIgnoreCase) ||
            selector.Contains(":nth-of-type", StringComparison.OrdinalIgnoreCase) ||
            selector.Contains(":first-child", StringComparison.OrdinalIgnoreCase) ||
            selector.Contains(":last-child", StringComparison.OrdinalIgnoreCase) ||
            selector.Contains(":only-child", StringComparison.OrdinalIgnoreCase) ||
            selector.Contains(":first-of-type", StringComparison.OrdinalIgnoreCase) ||
            selector.Contains(":last-of-type", StringComparison.OrdinalIgnoreCase) ||
            selector.Contains(":focus-visible", StringComparison.OrdinalIgnoreCase) ||
            selector.Contains(":focus-within", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check if selector has a supported pseudo-class
        foreach (var pseudoClass in supportedPseudoClasses)
        {
            // Use exact matching: check if the pseudo-class is followed by a word boundary
            // (space, comma, bracket, or end of string)
            var index = selector.IndexOf(pseudoClass, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                var endIndex = index + pseudoClass.Length;
                if (endIndex >= selector.Length ||
                    !char.IsLetterOrDigit(selector[endIndex]) && selector[endIndex] != '-')
                {
                    return false; // Found a supported pseudo-class with proper boundary
                }
            }
        }

        // Check if there's any pseudo-class at all
        var colonIndex = selector.IndexOf(':');
        if (colonIndex >= 0 && colonIndex < selector.Length - 1 && !selector.Contains("::", StringComparison.Ordinal))
        {
            // Has a pseudo-class but not a supported one
            return true;
        }

        return false; // No pseudo-class found
    }
}
