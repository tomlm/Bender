using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Xml;
using CommunityToolkit.Mvvm.ComponentModel;
using YamlDotNet.RepresentationModel;

namespace DumpViewer;

/// <summary>
/// Represents a node in the object visualization tree.
/// </summary>
public partial class ObjectNode : ObservableObject
{
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    /// Gets the name/key of this node (property name, index, dictionary key, etc.).
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// Gets the type of the value.
    /// </summary>
    public Type? ValueType { get; }

    /// <summary>
    /// Gets the original value being visualized.
    /// </summary>
    public object? Value { get; }

    /// <summary>
    /// Gets the display string for the value.
    /// </summary>
    public string DisplayValue { get; }

    /// <summary>
    /// Gets the type name to display.
    /// </summary>
    public string TypeName { get; }

    /// <summary>
    /// Gets the kind of node for styling purposes.
    /// </summary>
    public NodeKind Kind { get; }

    /// <summary>
    /// Gets whether this node has children that can be expanded.
    /// </summary>
    public bool HasChildren { get; }

    /// <summary>
    /// Gets whether this node is expandable (has children and is not a primitive).
    /// </summary>
    public bool IsExpandable => HasChildren;

    /// <summary>
    /// Gets the child nodes (lazily populated).
    /// </summary>
    public IReadOnlyList<ObjectNode> Children => _children ??= CreateChildren();

    private IReadOnlyList<ObjectNode>? _children;
    private readonly int _maxDepth;
    private readonly int _currentDepth;
    private readonly HashSet<object> _visited;
    private readonly string? _inferredItemTypeName;

    public ObjectNode(object? value, string? name = null, int maxDepth = 10, int currentDepth = 0, HashSet<object>? visited = null, string? inferredItemTypeName = null)
    {
        _maxDepth = maxDepth;
        _currentDepth = currentDepth;
        _visited = visited ?? new HashSet<object>(ReferenceEqualityComparer.Instance);
        _inferredItemTypeName = inferredItemTypeName;

        Name = name;
        Value = value;
        ValueType = value?.GetType();

        (Kind, DisplayValue, TypeName, HasChildren) = AnalyzeValue(value);

        // Auto-expand first level
        IsExpanded = currentDepth == 0;
    }

    private (NodeKind kind, string displayValue, string typeName, bool hasChildren) AnalyzeValue(object? value)
    {
        if (value == null)
        {
            return (NodeKind.Null, "null", "", false);
        }

        var type = value.GetType();
        var typeName = GetFriendlyTypeName(type);

        // Check for circular reference
        if (!type.IsValueType && _visited.Contains(value))
        {
            return (NodeKind.CircularReference, "(circular reference)", typeName, false);
        }

        // Check max depth
        if (_currentDepth >= _maxDepth)
        {
            return (NodeKind.MaxDepth, "(max depth reached)", typeName, false);
        }

        // === Special data format handling ===

        // JSON nodes (System.Text.Json)
        if (value is JsonValue jsonValue)
        {
            return AnalyzeJsonValue(jsonValue);
        }
        if (value is JsonObject jsonObj)
        {
            var objTypeName = _inferredItemTypeName ?? "Object";
            return (NodeKind.Object, "", objTypeName, jsonObj.Count > 0);
        }
        if (value is JsonArray jsonArr)
        {
            return (NodeKind.Collection, $"({jsonArr.Count} items)", "Array", jsonArr.Count > 0);
        }

        // YAML nodes
        if (value is YamlScalarNode yamlScalar)
        {
            return AnalyzeYamlScalar(yamlScalar);
        }
        if (value is YamlMappingNode yamlMap)
        {
            var objTypeName = _inferredItemTypeName ?? "Object";
            return (NodeKind.Object, "", objTypeName, yamlMap.Children.Count > 0);
        }
        if (value is YamlSequenceNode yamlSeq)
        {
            return (NodeKind.Collection, $"({yamlSeq.Children.Count} items)", "Array", yamlSeq.Children.Count > 0);
        }
        if (value is YamlStream yamlStream)
        {
            return (NodeKind.Collection, $"({yamlStream.Documents.Count} documents)", "YamlStream", yamlStream.Documents.Count > 0);
        }
        if (value is YamlDocument yamlDoc)
        {
            return (NodeKind.Object, "", "Document", yamlDoc.RootNode != null);
        }

        // XML nodes
        if (value is XmlDocument xmlDoc)
        {
            return (NodeKind.Object, "", "XmlDocument", xmlDoc.DocumentElement != null);
        }
        if (value is XmlElement xmlElement)
        {
            var childElements = xmlElement.ChildNodes.Cast<XmlNode>().Where(n => n is XmlElement).ToList();
            var attrCount = xmlElement.Attributes?.Count ?? 0;
            var textContent = GetXmlTextContent(xmlElement);

            // If element has only text content (no child elements, no attributes), treat as a leaf with the text value
            if (childElements.Count == 0 && attrCount == 0)
            {
                if (string.IsNullOrEmpty(textContent))
                    return (NodeKind.Null, "null", "", false);
                return (NodeKind.String, $"\"{EscapeString(textContent)}\"", "string", false);
            }

            // Check if all child elements have the same name (it's an array container)
            var distinctNames = childElements.Select(e => ((XmlElement)e).Name).Distinct().ToList();
            if (distinctNames.Count == 1 && childElements.Count > 1)
            {
                return (NodeKind.Collection, $"({childElements.Count} items)", "Array", true);
            }

            // Has children or attributes - it's a complex object
            var objTypeName = _inferredItemTypeName ?? xmlElement.Name;
            return (NodeKind.Object, "", objTypeName, true);
        }

        // ExpandoObject (from CsvHelper dynamic records)
        if (value is ExpandoObject expando)
        {
            var expandoDict = (IDictionary<string, object?>)expando;
            var objTypeName = _inferredItemTypeName ?? "Object";
            return (NodeKind.Object, "", objTypeName, expandoDict.Count > 0);
        }

        // === Standard type handling ===

        // Primitives and simple types
        if (IsPrimitive(type))
        {
            return (NodeKind.Primitive, FormatPrimitive(value), typeName, false);
        }

        // Strings
        if (value is string str)
        {
            return (NodeKind.String, $"\"{EscapeString(str)}\"", "string", false);
        }

        // Enums
        if (type.IsEnum)
        {
            return (NodeKind.Enum, value.ToString() ?? "", typeName, false);
        }

        // DateTime
        if (value is DateTime dt)
        {
            return (NodeKind.DateTime, dt.ToString("O"), "DateTime", false);
        }

        // DateTimeOffset
        if (value is DateTimeOffset dto)
        {
            return (NodeKind.DateTime, dto.ToString("O"), "DateTimeOffset", false);
        }

        // TimeSpan
        if (value is TimeSpan ts)
        {
            return (NodeKind.TimeSpan, ts.ToString(), "TimeSpan", false);
        }

        // Guid
        if (value is Guid guid)
        {
            return (NodeKind.Guid, guid.ToString(), "Guid", false);
        }

        // Dictionaries
        if (value is IDictionary dict)
        {
            return (NodeKind.Dictionary, $"({dict.Count} items)", typeName, dict.Count > 0);
        }

        // Collections/Arrays
        if (value is IEnumerable enumerable and not string)
        {
            var count = enumerable.Cast<object?>().Count();
            return (NodeKind.Collection, $"({count} items)", typeName, count > 0);
        }

        // Complex objects
        var properties = GetVisibleProperties(type);
        return (NodeKind.Object, "", typeName, properties.Length > 0);
    }

    private static (NodeKind kind, string displayValue, string typeName, bool hasChildren) AnalyzeJsonValue(JsonValue jsonValue)
    {
        var element = jsonValue.GetValue<System.Text.Json.JsonElement>();
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.String => (NodeKind.String, $"\"{EscapeString(element.GetString() ?? "")}\"", "string", false),
            System.Text.Json.JsonValueKind.Number => (NodeKind.Primitive, element.GetRawText(), "number", false),
            System.Text.Json.JsonValueKind.True => (NodeKind.Primitive, "true", "boolean", false),
            System.Text.Json.JsonValueKind.False => (NodeKind.Primitive, "false", "boolean", false),
            System.Text.Json.JsonValueKind.Null => (NodeKind.Null, "null", "", false),
            _ => (NodeKind.String, element.GetRawText(), "unknown", false)
        };
    }

    private static (NodeKind kind, string displayValue, string typeName, bool hasChildren) AnalyzeYamlScalar(YamlScalarNode scalar)
    {
        var value = scalar.Value ?? "";
        
        // Try to infer type from YAML scalar style and content
        if (scalar.Style == YamlDotNet.Core.ScalarStyle.Plain)
        {
            if (value is "true" or "false")
                return (NodeKind.Primitive, value, "boolean", false);
            if (value == "null" || value == "~" || string.IsNullOrEmpty(value))
                return (NodeKind.Null, "null", "", false);
            if (double.TryParse(value, out _))
                return (NodeKind.Primitive, value, "number", false);
        }
        
        return (NodeKind.String, $"\"{EscapeString(value)}\"", "string", false);
    }

    private IReadOnlyList<ObjectNode> CreateChildren()
    {
        if (!HasChildren || Value == null)
        {
            return Array.Empty<ObjectNode>();
        }

        var type = Value.GetType();

        // Track this object to detect circular references
        var newVisited = new HashSet<object>(_visited, ReferenceEqualityComparer.Instance);
        if (!type.IsValueType)
        {
            newVisited.Add(Value);
        }

        var children = new List<ObjectNode>();

        // === Special data format handling ===

        // JSON Object
        if (Value is JsonObject jsonObj)
        {
            foreach (var prop in jsonObj.OrderBy(p => p.Key))
            {
                // Infer item type name if the value is an array
                string? itemTypeName = prop.Value is JsonArray ? Singularize(prop.Key) : null;
                children.Add(new ObjectNode(prop.Value, prop.Key, _maxDepth, _currentDepth + 1, newVisited, itemTypeName));
            }
            return children;
        }

        // JSON Array
        if (Value is JsonArray jsonArr)
        {
            int index = 0;
            foreach (var item in jsonArr)
            {
                children.Add(new ObjectNode(item, $"[{index}]", _maxDepth, _currentDepth + 1, newVisited, _inferredItemTypeName));
                index++;
            }
            return children;
        }

        // YAML Mapping
        if (Value is YamlMappingNode yamlMap)
        {
            foreach (var entry in yamlMap.Children.OrderBy(e => e.Key.ToString()))
            {
                var key = (entry.Key as YamlScalarNode)?.Value ?? entry.Key.ToString();
                string? itemTypeName = entry.Value is YamlSequenceNode ? Singularize(key) : null;
                children.Add(new ObjectNode(entry.Value, key, _maxDepth, _currentDepth + 1, newVisited, itemTypeName));
            }
            return children;
        }

        // YAML Sequence
        if (Value is YamlSequenceNode yamlSeq)
        {
            int index = 0;
            foreach (var item in yamlSeq.Children)
            {
                children.Add(new ObjectNode(item, $"[{index}]", _maxDepth, _currentDepth + 1, newVisited, _inferredItemTypeName));
                index++;
            }
            return children;
        }

        // YAML Stream
        if (Value is YamlStream yamlStream)
        {
            int index = 0;
            foreach (var doc in yamlStream.Documents)
            {
                children.Add(new ObjectNode(doc.RootNode, $"Document[{index}]", _maxDepth, _currentDepth + 1, newVisited));
                index++;
            }
            return children;
        }

        // YAML Document
        if (Value is YamlDocument yamlDoc && yamlDoc.RootNode != null)
        {
            children.Add(new ObjectNode(yamlDoc.RootNode, "Root", _maxDepth, _currentDepth + 1, newVisited));
            return children;
        }

        // XML Document
        if (Value is XmlDocument xmlDoc && xmlDoc.DocumentElement != null)
        {
            children.Add(new ObjectNode(xmlDoc.DocumentElement, xmlDoc.DocumentElement.Name, _maxDepth, _currentDepth + 1, newVisited));
            return children;
        }

        // XML Element
        if (Value is XmlElement xmlElement)
        {
            // Add attributes first
            if (xmlElement.Attributes != null)
            {
                foreach (XmlAttribute attr in xmlElement.Attributes)
                {
                    children.Add(new ObjectNode(attr.Value, $"@{attr.Name}", _maxDepth, _currentDepth + 1, newVisited));
                }
            }

            // Group child elements by name to detect arrays
            var childElements = xmlElement.ChildNodes.Cast<XmlNode>()
                .Where(n => n is XmlElement)
                .Cast<XmlElement>()
                .ToList();

            // Check if all child elements have the same name (it's an array)
            var distinctNames = childElements.Select(e => e.Name).Distinct().ToList();
            
            if (distinctNames.Count == 1 && childElements.Count > 0)
            {
                // All children have the same name - treat as array with indexes
                var itemTypeName = Singularize(distinctNames[0]);
                int index = 0;
                foreach (var childElement in childElements)
                {
                    children.Add(new ObjectNode(childElement, $"[{index}]", _maxDepth, _currentDepth + 1, newVisited, itemTypeName));
                    index++;
                }
            }
            else
            {
                // Mixed children - use element names
                foreach (var childElement in childElements)
                {
                    children.Add(new ObjectNode(childElement, childElement.Name, _maxDepth, _currentDepth + 1, newVisited));
                }
            }
            return children;
        }

        // ExpandoObject (from CsvHelper)
        if (Value is ExpandoObject expandoObj)
        {
            var expandoDict = (IDictionary<string, object?>)expandoObj;
            foreach (var kvp in expandoDict.OrderBy(k => k.Key))
            {
                children.Add(new ObjectNode(kvp.Value, kvp.Key, _maxDepth, _currentDepth + 1, newVisited));
            }
            return children;
        }

        // === Standard type handling ===

        // Dictionary
        if (Value is IDictionary dict)
        {
            foreach (DictionaryEntry entry in dict)
            {
                var keyStr = entry.Key?.ToString() ?? "null";
                children.Add(new ObjectNode(entry.Value, keyStr, _maxDepth, _currentDepth + 1, newVisited));
            }
            return children;
        }

        // Collection/Array
        if (Value is IEnumerable enumerable and not string)
        {
            int index = 0;
            foreach (var item in enumerable)
            {
                children.Add(new ObjectNode(item, $"[{index}]", _maxDepth, _currentDepth + 1, newVisited, _inferredItemTypeName));
                index++;
            }
            return children;
        }

        // Complex object - show properties
        var properties = GetVisibleProperties(type);
        foreach (var prop in properties)
        {
            try
            {
                var propValue = prop.GetValue(Value);
                children.Add(new ObjectNode(propValue, prop.Name, _maxDepth, _currentDepth + 1, newVisited));
            }
            catch (Exception ex)
            {
                children.Add(new ObjectNode($"<error: {ex.Message}>", prop.Name, _maxDepth, _currentDepth + 1, newVisited));
            }
        }

        return children;
    }

    private static PropertyInfo[] GetVisibleProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.Name)
            .ToArray();
    }

    private static bool IsPrimitive(Type type)
    {
        return type.IsPrimitive || 
               type == typeof(decimal) || 
               type == typeof(byte[]);
    }

    private static string FormatPrimitive(object value)
    {
        return value switch
        {
            bool b => b ? "true" : "false",
            byte[] bytes => $"byte[{bytes.Length}]",
            float f => f.ToString("G9"),
            double d => d.ToString("G17"),
            decimal m => m.ToString("G"),
            _ => value.ToString() ?? ""
        };
    }

    private static string EscapeString(string str)
    {
        if (str.Length > 1000)
        {
            str = str[..1000] + "...";
        }
        return str.Replace("\\", "\\\\")
                  .Replace("\"", "\\\"")
                  .Replace("\n", "\\n")
                  .Replace("\r", "\\r")
                  .Replace("\t", "\\t");
    }

    private static string GetXmlTextContent(XmlElement element)
    {
        return string.Join("", element.ChildNodes.Cast<XmlNode>()
            .Where(n => n is XmlText or XmlCDataSection)
            .Select(n => n.Value?.Trim() ?? ""))
            .Trim();
    }

    private static string GetFriendlyTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            var genericDef = type.GetGenericTypeDefinition();
            var baseName = genericDef.Name;
            var tickIndex = baseName.IndexOf('`');
            if (tickIndex > 0)
            {
                baseName = baseName[..tickIndex];
            }

            var typeArgs = string.Join(", ", type.GetGenericArguments().Select(GetFriendlyTypeName));
            return $"{baseName}<{typeArgs}>";
        }

        if (type.IsArray)
        {
            return $"{GetFriendlyTypeName(type.GetElementType()!)}[]";
        }

        return type.Name;
    }

    /// <summary>
    /// Attempts to singularize a plural word (e.g., "users" ? "User", "categories" ? "Category").
    /// </summary>
    private static string Singularize(string plural)
    {
        if (string.IsNullOrEmpty(plural))
            return "Item";

        // Convert to PascalCase for display
        var result = char.ToUpperInvariant(plural[0]) + plural[1..];

        // Handle common plural patterns
        if (result.EndsWith("ies", StringComparison.OrdinalIgnoreCase) && result.Length > 3)
        {
            // categories ? Category, companies ? Company
            return result[..^3] + "y";
        }
        if (result.EndsWith("ses", StringComparison.OrdinalIgnoreCase) || 
            result.EndsWith("xes", StringComparison.OrdinalIgnoreCase) ||
            result.EndsWith("zes", StringComparison.OrdinalIgnoreCase) ||
            result.EndsWith("ches", StringComparison.OrdinalIgnoreCase) ||
            result.EndsWith("shes", StringComparison.OrdinalIgnoreCase))
        {
            // classes ? Class, boxes ? Box, matches ? Match
            return result[..^2];
        }
        if (result.EndsWith("ves", StringComparison.OrdinalIgnoreCase) && result.Length > 3)
        {
            // leaves ? Leaf, wives ? Wife (approximation)
            return result[..^3] + "f";
        }
        if (result.EndsWith("s", StringComparison.OrdinalIgnoreCase) && 
            !result.EndsWith("ss", StringComparison.OrdinalIgnoreCase) &&
            !result.EndsWith("us", StringComparison.OrdinalIgnoreCase) &&
            !result.EndsWith("is", StringComparison.OrdinalIgnoreCase))
        {
            // users ? User, items ? Item
            return result[..^1];
        }

        return result;
    }
}

/// <summary>
/// The kind of node for styling purposes.
/// </summary>
public enum NodeKind
{
    Null,
    Primitive,
    String,
    Enum,
    DateTime,
    TimeSpan,
    Guid,
    Collection,
    Dictionary,
    Object,
    CircularReference,
    MaxDepth
}

/// <summary>
/// Reference equality comparer for cycle detection.
/// </summary>
internal sealed class ReferenceEqualityComparer : IEqualityComparer<object>
{
    public static ReferenceEqualityComparer Instance { get; } = new();

    private ReferenceEqualityComparer() { }

    public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
    public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
}
