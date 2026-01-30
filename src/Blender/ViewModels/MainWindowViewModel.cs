using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Xml;
using CommunityToolkit.Mvvm.ComponentModel;
using CsvHelper;
using YamlDotNet.RepresentationModel;

namespace Blender.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string? _filePath;

    [ObservableProperty]
    private DataFormat _format = DataFormat.Auto;

    [ObservableProperty]
    private string? _inputData;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    /// <summary>
    /// Gets the deserialized data object based on the detected format.
    /// Can be JsonNode, XmlDocument, YamlStream, or List&lt;dynamic&gt; for CSV.
    /// </summary>
    [ObservableProperty]
    private object? _data;

    public string Greeting { get; } = "Welcome to Avalonia!";

    /// <summary>
    /// Loads data from a file path.
    /// </summary>
    public async Task<bool> LoadFromFileAsync(string filePath, DataFormat format = DataFormat.Auto)
    {
        FilePath = filePath;
        Format = format;

        if (!File.Exists(filePath))
        {
            HasError = true;
            ErrorMessage = $"File not found: {filePath}";
            return false;
        }

        try
        {
            InputData = await File.ReadAllTextAsync(filePath);
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Error reading file: {ex.Message}";
            return false;
        }

        return await ParseAndDeserializeAsync();
    }

    /// <summary>
    /// Loads data from a string.
    /// </summary>
    public async Task<bool> LoadFromStringAsync(string data, DataFormat format = DataFormat.Auto, string? filePath = null)
    {
        InputData = data;
        FilePath = filePath;
        Format = format;

        return await ParseAndDeserializeAsync();
    }

    /// <summary>
    /// Loads data from stdin.
    /// </summary>
    public async Task<bool> LoadFromStdinAsync(DataFormat format = DataFormat.Auto)
    {
        Format = format;

        if (!Console.IsInputRedirected)
        {
            return true; // No stdin data, not an error
        }

        try
        {
            InputData = await Console.In.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Error reading from stdin: {ex.Message}";
            return false;
        }

        return await ParseAndDeserializeAsync();
    }

    private Task<bool> ParseAndDeserializeAsync()
    {
        // Auto-detect format if not specified
        if (Format == DataFormat.Auto && !string.IsNullOrEmpty(InputData))
        {
            Format = DetectFormat(InputData, FilePath);
        }

        // Deserialize the input data based on format
        if (!string.IsNullOrEmpty(InputData) && Format != DataFormat.Auto)
        {
            try
            {
                Data = DeserializeData(InputData, Format);
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Error deserializing {Format} data: {ex.Message}";
                return Task.FromResult(false);
            }
        }

        return Task.FromResult(true);
    }

    private static object? DeserializeData(string data, DataFormat format)
    {
        return format switch
        {
            DataFormat.Json => JsonNode.Parse(data),
            DataFormat.Xml => ParseXml(data),
            DataFormat.Yaml => ParseYaml(data),
            DataFormat.Csv => ParseCsv(data),
            _ => null
        };
    }

    private static XmlDocument ParseXml(string data)
    {
        var doc = new XmlDocument();
        doc.LoadXml(data);
        return doc;
    }

    private static YamlStream ParseYaml(string data)
    {
        var yaml = new YamlStream();
        using var reader = new StringReader(data);
        yaml.Load(reader);
        return yaml;
    }

    private static List<dynamic> ParseCsv(string data)
    {
        using var reader = new StringReader(data);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        return [.. csv.GetRecords<dynamic>()];
    }

    private static DataFormat DetectFormat(string data, string? filePath)
    {
        // First try to detect by file extension
        if (!string.IsNullOrEmpty(filePath))
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            switch (extension)
            {
                case ".xml":
                    return DataFormat.Xml;
                case ".yml" or ".yaml":
                    return DataFormat.Yaml;
                case ".json":
                    return DataFormat.Json;
                case ".csv":
                    return DataFormat.Csv;
            }
        }

        // Then try to detect by content
        var trimmed = data.TrimStart();
        if (trimmed.StartsWith('<'))
        {
            return DataFormat.Xml;
        }
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            return DataFormat.Json;
        }

        // Check for YAML-like structure (key: value patterns)
        if (trimmed.Contains(':') && !trimmed.Contains(','))
        {
            return DataFormat.Yaml;
        }

        // Check for CSV-like structure (comma-separated with consistent columns)
        if (trimmed.Contains(','))
        {
            return DataFormat.Csv;
        }

        return DataFormat.Auto;
    }
}
