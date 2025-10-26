using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using ExCSS;

namespace html2uuitk.Cli;

internal static class UssLoader
{
    public static IReadOnlyList<UssVariableDefinition> LoadVariables(string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            throw new FileNotFoundException("USS variable metadata file not found.", jsonPath);
        }

        using var stream = File.OpenRead(jsonPath);
        using var document = JsonDocument.Parse(stream);

        var variables = new List<UssVariableDefinition>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            variables.Add(UssMetadataFactory.FromJson(element));
        }

        return variables;
    }
}

internal sealed class UssStylesheetParser
{
    private readonly StylesheetParser _parser = new();

    public UssStylesheet ParseFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("USS file not found.", path);
        }

        var content = File.ReadAllText(path);
        return Parse(content);
    }

    public UssStylesheet Parse(string content)
    {
        var result = new UssStylesheet();
        var stylesheet = _parser.Parse(content);

        foreach (var rule in stylesheet.StyleRules)
        {
            var rawSelectors = ExCssReflection.GetSelectorText(rule);
            if (string.IsNullOrWhiteSpace(rawSelectors))
            {
                continue;
            }

            var ussRule = new UssRule();

            foreach (var selectorText in rawSelectors.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = selectorText.Trim();
                if (string.IsNullOrEmpty(trimmed))
                {
                    continue;
                }

                var selector = new UssSelector
                {
                    Raw = trimmed,
                };

                foreach (var segment in TokenizeSelector(trimmed))
                {
                    selector.Segments.Add(segment);
                }

                ussRule.Selectors.Add(selector);
            }

            if (ussRule.Selectors.Count == 0)
            {
                continue;
            }

            var declarations = ExCssReflection.GetDeclarations(rule);
            foreach (var (property, rawValue) in declarations)
            {
                var declaration = BuildDeclaration(property, rawValue);
                if (declaration is not null)
                {
                    ussRule.Declarations.Add(declaration);
                }
            }

            if (ussRule.Declarations.Count == 0)
            {
                continue;
            }

            result.Rules.Add(ussRule);
        }

        return result;
    }

    private static UssDeclaration? BuildDeclaration(string property, string rawValue)
    {
        if (string.IsNullOrWhiteSpace(property))
        {
            return null;
        }

        var text = rawValue ?? string.Empty;
        var important = false;

        if (text.Contains("!important", StringComparison.OrdinalIgnoreCase))
        {
            important = true;
            text = text.Replace("!important", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        }

        // Handle Unity-specific conversions
        text = ConvertValueForUnity(property, text);

        var fragment = CreateFragment(text);
        var fragments = fragment is null ? Array.Empty<UssValueFragment>() : new[] { fragment };

        return new UssDeclaration
        {
            Property = property,
            Important = important,
            Value = fragments,
        };
    }

    private static string ConvertValueForUnity(string property, string value)
    {
        return property.ToLowerInvariant() switch
        {
            "-unity-font" or "-unity-font-definition" => "resource()", // Unity expects resource references for fonts
            "border-top-left-radius" or "border-top-right-radius" or "border-bottom-right-radius" or "border-bottom-left-radius"
                => ConvertBorderRadius(value),
            "background-image" => ConvertBackgroundImage(value),
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
        // Unity doesn't support CSS gradients, convert to none or simple color
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

    private static UssValueFragment? CreateFragment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var kind = DetermineKind(trimmed, out var unit);

        return new UssValueFragment
        {
            Kind = kind,
            Raw = trimmed,
            Unit = unit,
            Arguments = UssValueFragment.Empty,
        };
    }

    private static UssValueFragmentKind DetermineKind(string value, out string? unit)
    {
        unit = null;
        if (value.StartsWith("var(", StringComparison.OrdinalIgnoreCase))
        {
            return UssValueFragmentKind.VariableReference;
        }

        if (value.StartsWith("resource(", StringComparison.OrdinalIgnoreCase))
        {
            return UssValueFragmentKind.Resource;
        }

        if (value.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
        {
            return UssValueFragmentKind.Url;
        }

        if (value.StartsWith("#", StringComparison.Ordinal) ||
            value.StartsWith("rgb", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("hsl", StringComparison.OrdinalIgnoreCase))
        {
            return UssValueFragmentKind.Color;
        }

        if (value.StartsWith("\"", StringComparison.Ordinal) || value.StartsWith("'", StringComparison.Ordinal))
        {
            return UssValueFragmentKind.String;
        }

        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
        {
            return UssValueFragmentKind.Boolean;
        }

        if (TryParseNumberWithUnit(value, out var unitCandidate))
        {
            unit = unitCandidate;

            if (string.IsNullOrEmpty(unitCandidate))
            {
                return value.Contains('.', StringComparison.Ordinal)
                    ? UssValueFragmentKind.Number
                    : UssValueFragmentKind.Integer;
            }

            if (unitCandidate.Equals("%", StringComparison.Ordinal))
            {
                return UssValueFragmentKind.Percentage;
            }

            return UssValueFragmentKind.Length;
        }

        if (value.Contains('(') && value.EndsWith(")", StringComparison.Ordinal))
        {
            return UssValueFragmentKind.Function;
        }

        return UssValueFragmentKind.Keyword;
    }

    private static bool TryParseNumberWithUnit(string value, out string? unit)
    {
        unit = null;
        var span = value.AsSpan().Trim();
        if (span.IsEmpty)
        {
            return false;
        }

        var index = 0;
        if (span[0] is '+' or '-')
        {
            index++;
        }

        var hasDigits = false;
        while (index < span.Length && char.IsDigit(span[index]))
        {
            hasDigits = true;
            index++;
        }

        if (index < span.Length && span[index] == '.')
        {
            index++;
            while (index < span.Length && char.IsDigit(span[index]))
            {
                hasDigits = true;
                index++;
            }
        }

        if (!hasDigits)
        {
            return false;
        }

        var numberSpan = span[..index];
        if (!double.TryParse(numberSpan, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return false;
        }

        var unitSpan = index < span.Length ? span[index..] : ReadOnlySpan<char>.Empty;
        if (!unitSpan.IsEmpty && char.IsWhiteSpace(unitSpan[0]))
        {
            return false;
        }

        unit = unitSpan.IsEmpty ? null : unitSpan.ToString();
        return true;
    }

    private static IEnumerable<UssSelectorSegment> TokenizeSelector(string selector)
    {
        var index = 0;

        while (index < selector.Length)
        {
            var current = selector[index];

            if (char.IsWhiteSpace(current))
            {
                var start = index;
                while (index < selector.Length && char.IsWhiteSpace(selector[index]))
                {
                    index++;
                }

                yield return new UssSelectorSegment
                {
                    Type = UssSelectorSegmentType.Whitespace,
                    Value = selector[start..index],
                };

                continue;
            }

            if (current is '>' or '+' or '~')
            {
                yield return new UssSelectorSegment
                {
                    Type = UssSelectorSegmentType.Combinator,
                    Value = selector[index++].ToString(),
                };
                continue;
            }

            if (current == '*')
            {
                yield return new UssSelectorSegment
                {
                    Type = UssSelectorSegmentType.Universal,
                    Value = "*",
                };
                index++;
                continue;
            }

            if (current is '.' or '#')
            {
                var start = index;
                index++;
                while (index < selector.Length && IsIdentifierChar(selector[index]))
                {
                    index++;
                }

                var type = current == '.' ? UssSelectorSegmentType.Class : UssSelectorSegmentType.Id;

                yield return new UssSelectorSegment
                {
                    Type = type,
                    Value = selector[start..index],
                };

                continue;
            }

            if (current == ':')
            {
                var start = index;
                var colonCount = 1;
                index++;
                if (index < selector.Length && selector[index] == ':')
                {
                    colonCount++;
                    index++;
                }

                while (index < selector.Length && IsIdentifierChar(selector[index]))
                {
                    index++;
                }

                if (index < selector.Length && selector[index] == '(')
                {
                    var depth = 0;
                    do
                    {
                        if (selector[index] == '(')
                        {
                            depth++;
                        }
                        else if (selector[index] == ')')
                        {
                            depth--;
                        }

                        index++;
                    } while (index < selector.Length && depth > 0);
                }

                yield return new UssSelectorSegment
                {
                    Type = colonCount == 2 ? UssSelectorSegmentType.PseudoElement : UssSelectorSegmentType.PseudoClass,
                    Value = selector[start..index],
                };

                continue;
            }

            if (current == '[')
            {
                var start = index;
                index++;
                while (index < selector.Length && selector[index] != ']')
                {
                    if (selector[index] == '\\' && index + 1 < selector.Length)
                    {
                        index += 2;
                        continue;
                    }

                    index++;
                }

                if (index < selector.Length)
                {
                    index++;
                }

                yield return new UssSelectorSegment
                {
                    Type = UssSelectorSegmentType.Attribute,
                    Value = selector[start..index],
                };

                continue;
            }

            if (IsTypeCharacter(current))
            {
                var start = index;
                while (index < selector.Length && IsTypeCharacter(selector[index]))
                {
                    index++;
                }

                yield return new UssSelectorSegment
                {
                    Type = UssSelectorSegmentType.Type,
                    Value = selector[start..index],
                };

                continue;
            }

            yield return new UssSelectorSegment
            {
                Type = UssSelectorSegmentType.Unknown,
                Value = selector[index++].ToString(),
            };
        }
    }

    private static bool IsIdentifierChar(char value) =>
        char.IsLetterOrDigit(value) || value is '-' or '_' or '\\';

    private static bool IsTypeCharacter(char value) =>
        char.IsLetterOrDigit(value) || value is '-' or '_' or '|' or '\\' or '.';
}
