using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace DumpViewer;

/// <summary>
/// A control that visualizes any object in a tree structure similar to LinqPad's .Dump().
/// </summary>
public class ObjectViewer : TemplatedControl
{
    /// <summary>
    /// Defines the <see cref="Value"/> property.
    /// </summary>
    public static readonly StyledProperty<object?> ValueProperty =
        AvaloniaProperty.Register<ObjectViewer, object?>(nameof(Value));

    /// <summary>
    /// Defines the <see cref="MaxDepth"/> property.
    /// </summary>
    public static readonly StyledProperty<int> MaxDepthProperty =
        AvaloniaProperty.Register<ObjectViewer, int>(nameof(MaxDepth), 10);

    /// <summary>
    /// Defines the <see cref="Items"/> property.
    /// </summary>
    public static readonly DirectProperty<ObjectViewer, ObservableCollection<ObjectNode>> ItemsProperty =
        AvaloniaProperty.RegisterDirect<ObjectViewer, ObservableCollection<ObjectNode>>(
            nameof(Items),
            o => o.Items);

    private ObservableCollection<ObjectNode> _items = [];

    /// <summary>
    /// Gets or sets the object to visualize.
    /// </summary>
    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum depth for recursive visualization.
    /// </summary>
    public int MaxDepth
    {
        get => GetValue(MaxDepthProperty);
        set => SetValue(MaxDepthProperty, value);
    }

    /// <summary>
    /// Gets the collection of root nodes for the TreeView.
    /// </summary>
    public ObservableCollection<ObjectNode> Items
    {
        get => _items;
        private set => SetAndRaise(ItemsProperty, ref _items, value);
    }

    static ObjectViewer()
    {
        ValueProperty.Changed.AddClassHandler<ObjectViewer>((x, _) => x.OnValueChanged());
        MaxDepthProperty.Changed.AddClassHandler<ObjectViewer>((x, _) => x.OnValueChanged());
    }

    private void OnValueChanged()
    {
        _items.Clear();
        if (Value != null)
        {
            _items.Add(new ObjectNode(Value, null, MaxDepth));
        }
    }
}
