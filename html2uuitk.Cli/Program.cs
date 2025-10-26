using System.Text.Json;
using html2uuitk.Cli;

if (!CliOptions.TryParse(args, out var options, out var error))
{
    if (!string.IsNullOrWhiteSpace(error))
    {
        var isUsage = error.StartsWith("Usage:", StringComparison.OrdinalIgnoreCase);
        if (isUsage)
        {
            Console.WriteLine(error);
        }
        else
        {
            Console.Error.WriteLine(error);
        }

        return isUsage ? 0 : 1;
    }

    return 1;
}

Config config;
try
{
    config = ConfigLoader.Load(options!.ConfigPath);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to load configuration: {ex.Message}");
    return 1;
}

var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};

List<string> breakingSelectors;
Dictionary<string, UssProperty> ussProperties;

try
{
    var breakingPath = Path.Combine(AppContext.BaseDirectory, "breaking_selectors.json");
    var breakingContent = File.ReadAllText(breakingPath);
    breakingSelectors = JsonSerializer.Deserialize<List<string>>(breakingContent, jsonOptions) ?? new List<string>();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to load breaking selectors: {ex.Message}");
    return 1;
}

try
{
    var ussPath = Path.Combine(AppContext.BaseDirectory, "uss_properties.json");
    var ussContent = File.ReadAllText(ussPath);
    ussProperties = JsonSerializer.Deserialize<Dictionary<string, UssProperty>>(ussContent, jsonOptions)
                    ?? new Dictionary<string, UssProperty>(StringComparer.OrdinalIgnoreCase);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to load USS properties: {ex.Message}");
    return 1;
}

var htmlConverter = new HtmlConverter(config);
var cssConverter = new CssConverter(config, ussProperties, breakingSelectors);

try
{
    Directory.CreateDirectory(options.OutputFolder);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to create output directory '{options.OutputFolder}': {ex.Message}");
    return 1;
}

// Extract CSS from HTML files if needed
var extractedCssFiles = new List<string>();
if (options.ExtractCss && options.CssFiles.Count == 0)
{
    Console.WriteLine("Extracting CSS from HTML files...");

    foreach (var htmlPath in options.InputFiles)
    {
        try
        {
            var htmlContent = File.ReadAllText(htmlPath);
            var extractedCss = CssExtractor.ExtractFromHtml(htmlContent, htmlPath);

            if (extractedCss.HasCss)
            {
                // Generate CSS filename
                var cssFileName = string.IsNullOrWhiteSpace(options.CssOutputName)
                    ? $"{Path.GetFileNameWithoutExtension(htmlPath)}-styles"
                    : options.CssOutputName;

                var cssFilePath = Path.Combine(options.OutputFolder, $"{cssFileName}.css");
                var combinedCss = extractedCss.GetCombinedCss();

                File.WriteAllText(cssFilePath, combinedCss);
                extractedCssFiles.Add(cssFilePath);

                Console.WriteLine($"{Path.GetFileName(cssFilePath)} CSS extracted ✓");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to extract CSS from '{htmlPath}': {ex.Message}");
        }
    }
}

// Combine explicit CSS files with extracted ones
var allCssFiles = new List<string>(options.CssFiles);
allCssFiles.AddRange(extractedCssFiles);

// Process HTML files
foreach (var htmlPath in options.InputFiles)
{
    try
    {
        var content = File.ReadAllText(htmlPath);
        var converted = htmlConverter.Convert(content);

        var fileName = Path.GetFileNameWithoutExtension(htmlPath);
        var targetPath = Path.Combine(options.OutputFolder, $"{fileName}.uxml");
        File.WriteAllText(targetPath, converted);
        Console.WriteLine($"{fileName} UXML written ✓");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to process HTML file '{htmlPath}': {ex.Message}");
    }
}

var cssFiles = new List<string>(allCssFiles);
if (!string.IsNullOrEmpty(options.ResetCss))
{
    cssFiles.Add(options.ResetCss);
}

foreach (var cssPath in cssFiles)
{
    try
    {
        var content = File.ReadAllText(cssPath);
        var converted = cssConverter.Convert(content);

        var fileName = Path.GetFileNameWithoutExtension(cssPath);
        var targetPath = Path.Combine(options.OutputFolder, $"{fileName}.uss");
        File.WriteAllText(targetPath, converted);
        Console.WriteLine($"{fileName} USS written ✓");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to process CSS file '{cssPath}': {ex.Message}");
    }
}

return 0;
