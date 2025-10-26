using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
            var selectorText = GetSelectorText(rule);

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

                    if (TagMappings.GetUiTagForSelector(segment) is { } mapped)
                    {
                        selectorParts[i] = mapped.Replace("ui:", string.Empty, StringComparison.OrdinalIgnoreCase);
                    }

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
            var declarations = GetDeclarations(rule);

            foreach (var declaration in declarations)
            {
                var property = TransformProperty(declaration.Key);
                var value = declaration.Value;

                if (_ussProperties.TryGetValue(property, out var metadata))
                {
                    if (metadata.Native)
                    {
                        var translated = TranslateValue(value, property);
                        builder.Append("    ")
                               .Append(property)
                               .Append(": ")
                               .Append(translated)
                               .Append(";\n");
                        builder.Append(GetExtras(property, translated));
                        validDeclarations++;
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

    private string GetSelectorText(IStyleRule rule)
    {
        try
        {
            // Try to get SelectorText property
            var selectorTextProperty = rule.GetType().GetProperty("SelectorText");
            if (selectorTextProperty != null)
            {
                return selectorTextProperty.GetValue(rule)?.ToString() ?? string.Empty;
            }

            // Try to get Selectors collection and join them
            var selectorsProperty = rule.GetType().GetProperty("Selectors");
            if (selectorsProperty != null && selectorsProperty.GetValue(rule) is IEnumerable<object> selectors)
            {
                return string.Join(", ", selectors.Select(s => s.ToString()));
            }

            // Fallback to string representation
            var ruleString = rule.ToString();
            if (string.IsNullOrEmpty(ruleString))
                return ruleString;
            var match = Regex.Match(ruleString, @"^\s*([^{\s]+)");
            return match.Success ? match.Groups[1].Value : ruleString;
        }
        catch
        {
            return string.Empty;
        }
    }

    private Dictionary<string, string> GetDeclarations(IStyleRule rule)
    {
        var declarations = new Dictionary<string, string>();

        try
        {
            // Try to get Style property
            var styleProperty = rule.GetType().GetProperty("Style");
            if (styleProperty != null && styleProperty.GetValue(rule) is IEnumerable<object> styleDeclarations)
            {
                foreach (var declaration in styleDeclarations)
                {
                    var nameProperty = declaration.GetType().GetProperty("Name");
                    var valueProperty = declaration.GetType().GetProperty("Value") ?? declaration.GetType().GetProperty("Term");

                    if (nameProperty != null && valueProperty != null)
                    {
                        var name = nameProperty.GetValue(declaration)?.ToString();
                        var value = valueProperty.GetValue(declaration)?.ToString();

                        if (!string.IsNullOrEmpty(name))
                        {
                            declarations[name] = value ?? string.Empty;
                        }
                    }
                }
                return declarations;
            }

            // Try to get Declarations property
            var declarationsProperty = rule.GetType().GetProperty("Declarations");
            if (declarationsProperty != null && declarationsProperty.GetValue(rule) is IEnumerable<object> declarationsList)
            {
                foreach (var declaration in declarationsList)
                {
                    var nameProperty = declaration.GetType().GetProperty("Name");
                    var valueProperty = declaration.GetType().GetProperty("Value") ?? declaration.GetType().GetProperty("Term");

                    if (nameProperty != null && valueProperty != null)
                    {
                        var name = nameProperty.GetValue(declaration)?.ToString();
                        var value = valueProperty.GetValue(declaration)?.ToString();

                        if (!string.IsNullOrEmpty(name))
                        {
                            declarations[name] = value ?? string.Empty;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not extract CSS declarations: {ex.Message}");
        }

        return declarations;
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

        if (string.Equals(property, "-unity-font", StringComparison.OrdinalIgnoreCase))
        {
            return GetAssetPath(normalized) ?? normalized;
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
            return "\t-unity-font-definition: none;\n";
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
}