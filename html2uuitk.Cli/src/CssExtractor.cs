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

        // Handle CSS @layer rules - flatten them for better compatibility
        css = ProcessCssLayers(css);

        // Handle @property rules - convert to comments with fallback
        css = ProcessPropertyRules(css);

        // Handle @counter-style rules - convert to comments
        css = ProcessCounterStyleRules(css);

        // Handle @supports and @container rules - flatten them
        css = ProcessConditionalRules(css);

        // Clean up excessive whitespace while preserving important formatting
        css = Regex.Replace(css, @"\s+", " ");
        css = Regex.Replace(css, @";\s*}", "; }");
        css = Regex.Replace(css, @"{\s*", " { ");
        css = Regex.Replace(css, @";\s*", "; ");
        css = Regex.Replace(css, @":\s*", ": ");

        // Restore line breaks for better readability (optional)
        css = Regex.Replace(css, @"}", "}\n");
        css = Regex.Replace(css, @";", ";\n");

        return css.Trim();
    }

    private static string ProcessCssLayers(string css)
    {
        // Handle complex @layer rules by extracting content from nested layers
        // This preserves the CSS content within layers but makes it compatible with ExCSS

        // First, handle the layer declaration list (e.g., @layer reset, base, components;)
        var layerDeclPattern = new Regex(@"@layer\s+[^{]*;", RegexOptions.Singleline);
        css = layerDeclPattern.Replace(css, "");

        // Then handle layer blocks (e.g., @layer reset { ... })
        var layerBlockPattern = new Regex(@"@layer\s+[\w\s-]*\{((?:[^{}]*\{[^{}]*\})*[^{}]*)\}", RegexOptions.Singleline);
        css = layerBlockPattern.Replace(css, match =>
        {
            // Extract the content within the layer block
            var content = match.Groups[1].Value;
            return content;
        });

        return css;
    }

    private static string ProcessPropertyRules(string css)
    {
        // Convert @property rules to comments with fallback values
        var propertyPattern = new Regex(@"@property\s+--[\w-]+\s*\{[^}]*initial-value:\s*([^;]+);[^}]*\}", RegexOptions.Singleline);
        return propertyPattern.Replace(css, match =>
        {
            var initialValue = match.Groups[1].Value.Trim();
            return $"/* @property rule - fallback value: {initialValue} */";
        });
    }

    private static string ProcessCounterStyleRules(string css)
    {
        // Convert @counter-style rules to comments
        var counterStylePattern = new Regex(@"@counter-style\s+[^{]+\{[^}]*\}", RegexOptions.Singleline);
        return counterStylePattern.Replace(css, match => $"/* {match.Value} */");
    }

    private static string ProcessConditionalRules(string css)
    {
        // Flatten @supports and @container rules
        var supportsPattern = new Regex(@"@supports\s*\([^)]*\)\s*\{([^}]*)\}", RegexOptions.Singleline);
        css = supportsPattern.Replace(css, "$1");

        var containerPattern = new Regex(@"@container\s*\([^)]*\)\s*\{([^}]*)\}", RegexOptions.Singleline);
        css = containerPattern.Replace(css, "$1");

        return css;
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