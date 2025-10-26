using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace html2uuitk.Cli;

/// <summary>
/// Extracts CSS content from HTML files, handling various embedding patterns
/// </summary>
internal sealed class CssExtractor
{
    private static readonly Regex StyleTagRegex = new(@"<style[^>]*>(.*?)</style>",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex StyleAttributeRegex = new(@"style\s*=\s*[""']([^""']*)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex MediaAttributeRegex = new(@"media\s*=\s*[""']([^""']*)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static ExtractedCss ExtractFromHtml(string htmlContent, string htmlFileName)
    {
        var extractedCss = new ExtractedCss();

        // Extract CSS from <style> tags
        ExtractFromStyleTags(htmlContent, extractedCss);

        // Extract inline styles (optional - can be noisy)
        ExtractInlineStyles(htmlContent, extractedCss);

        return extractedCss;
    }

    private static void ExtractFromStyleTags(string html, ExtractedCss extractedCss)
    {
        var styleMatches = StyleTagRegex.Matches(html);

        foreach (Match match in styleMatches)
        {
            var fullStyleTag = match.Value;
            var cssContent = match.Groups[1].Value;

            // Extract media query if present
            var mediaQuery = ExtractMediaQuery(fullStyleTag);

            // Clean up the CSS content
            var cleanedCss = CleanCssContent(cssContent);

            if (!string.IsNullOrWhiteSpace(cleanedCss))
            {
                var styleBlock = new StyleBlock
                {
                    Css = cleanedCss,
                    MediaQuery = mediaQuery,
                    OriginalTag = fullStyleTag
                };

                extractedCss.StyleBlocks.Add(styleBlock);
            }
        }
    }

    private static void ExtractInlineStyles(string html, ExtractedCss extractedCss)
    {
        var styleMatches = StyleAttributeRegex.Matches(html);
        var generatedClasses = new Dictionary<string, string>();
        var classCounter = 1;

        foreach (Match match in styleMatches)
        {
            var inlineStyle = match.Groups[1].Value;

            if (!string.IsNullOrWhiteSpace(inlineStyle))
            {
                // Generate a unique class name for this inline style
                var className = $"inline-style-{classCounter++}";
                generatedClasses[className] = inlineStyle;
            }
        }

        // Add generated CSS for inline styles
        if (generatedClasses.Count > 0)
        {
            var inlineCss = new StringBuilder();
            foreach (var (className, styles) in generatedClasses)
            {
                inlineCss.AppendLine($".{className} {{ {styles} }}");
            }

            extractedCss.StyleBlocks.Add(new StyleBlock
            {
                Css = inlineCss.ToString(),
                MediaQuery = null,
                OriginalTag = "/* Extracted from inline styles */"
            });
        }
    }

    private static string? ExtractMediaQuery(string styleTag)
    {
        var mediaMatch = MediaAttributeRegex.Match(styleTag);
        return mediaMatch.Success ? mediaMatch.Groups[1].Value : null;
    }

    private static string CleanCssContent(string css)
    {
        if (string.IsNullOrWhiteSpace(css))
            return string.Empty;

        // Remove HTML comments that might be inside the style tag
        css = Regex.Replace(css, @"<!--.*?-->", "", RegexOptions.Singleline);

        // Remove CDATA sections if present
        css = Regex.Replace(css, @"<!\[CDATA\[(.*?)\]\]>", "$1", RegexOptions.Singleline);

        // Format CSS for better readability
        css = FormatCss(css);

        return css.Trim();
    }

    private static string FormatCss(string css)
    {
        // First, add spacing around braces and semicolons to make it easier to parse
        css = Regex.Replace(css, @"{", " {\n    ");
        css = Regex.Replace(css, @";", ";\n    ");
        css = Regex.Replace(css, @"}", "\n}\n\n");

        // Split into lines and format properly
        var lines = css.Split('\n');
        var result = new StringBuilder();
        var inBlock = false;
        var indentLevel = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Handle CSS blocks
            if (trimmed.Contains("{") && !trimmed.Contains("@"))
            {
                result.AppendLine(trimmed);
                inBlock = true;
                indentLevel = 1;
            }
            else if (trimmed == "}" && inBlock)
            {
                result.AppendLine(trimmed);
                inBlock = false;
                indentLevel = 0;
            }
            else if (inBlock && trimmed.Contains(":"))
            {
                // Format CSS declarations with proper indentation
                var parts = trimmed.Split(new[] { ':' }, 2);
                if (parts.Length == 2)
                {
                    var property = parts[0].Trim();
                    var value = parts[1].Trim();
                    result.AppendLine($"    {property}: {value};");
                }
            }
            else
            {
                result.AppendLine(trimmed);
            }
        }

        // Clean up the result
        var formatted = result.ToString();
        formatted = Regex.Replace(formatted, @"\n\s*\n\s*\n", "\n\n");
        formatted = Regex.Replace(formatted, @"^\s+|\s+$", "", RegexOptions.Multiline);
        // Fix double semicolons
        formatted = Regex.Replace(formatted, @";;", ";");

        return formatted.Trim();
    }

      public static string CombineExtractedCss(ExtractedCss extractedCss)
    {
        var combinedCss = new StringBuilder();

        // Add header comment
        combinedCss.AppendLine("/* Extracted CSS from HTML file */");
        combinedCss.AppendLine($"/* Generated on: {DateTime.UtcNow} */");
        combinedCss.AppendLine();

        // Combine all style blocks in order
        foreach (var styleBlock in extractedCss.StyleBlocks)
        {
            if (!string.IsNullOrWhiteSpace(styleBlock.MediaQuery))
            {
                combinedCss.AppendLine($"@media {styleBlock.MediaQuery} {{");
                combinedCss.AppendLine(styleBlock.Css);
                combinedCss.AppendLine("}");
            }
            else
            {
                combinedCss.AppendLine(styleBlock.Css);
            }

            combinedCss.AppendLine();
        }

        return combinedCss.ToString();
    }
}

/// <summary>
/// Represents CSS extracted from an HTML file
/// </summary>
internal sealed class ExtractedCss
{
    public List<StyleBlock> StyleBlocks { get; } = new();

    public bool HasCss => StyleBlocks.Count > 0;

    public string GetCombinedCss() => CssExtractor.CombineExtractedCss(this);
}

/// <summary>
/// Represents a single CSS block extracted from a <style> tag
/// </summary>
internal sealed class StyleBlock
{
    public string Css { get; set; } = string.Empty;
    public string? MediaQuery { get; set; }
    public string OriginalTag { get; set; } = string.Empty;
}