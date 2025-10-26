using System;
using System.IO;
using System.Xml.Serialization;

namespace html2uuitk.Cli;

internal static class UxmlLoader
{
    private static readonly XmlSerializer Serializer = new(typeof(UxmlDocument));

    public static UxmlDocument Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("UXML file not found.", path);
        }

        using var stream = File.OpenRead(path);
        var document = Serializer.Deserialize(stream) as UxmlDocument;

        if (document is null)
        {
            throw new InvalidDataException($"Failed to deserialize UXML document '{path}'.");
        }

        return document;
    }

    public static bool TryLoad(string path, out UxmlDocument? document, out string? error)
    {
        document = null;
        error = null;

        try
        {
            document = Load(path);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
