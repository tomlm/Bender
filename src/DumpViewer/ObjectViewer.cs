using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace DumpViewer;


/// <summary>
/// A control that visualizes any object in a tree structure similar to LinqPad's .Dump().
/// </summary>
public class ObjectViewer : TemplatedControl
{
    private TextBox? _searchTextBox;
    private Button? _searchButton;
    private TreeView? _treeView;
    private TextBox? _rawTextBox;
    
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
    /// Defines the <see cref="SourceText"/> property.
    /// </summary>
    public static readonly StyledProperty<string?> SourceTextProperty =
        AvaloniaProperty.Register<ObjectViewer, string?>(nameof(SourceText));

    /// <summary>
    /// Defines the <see cref="SelectedNode"/> property.
    /// </summary>
    public static readonly StyledProperty<ObjectNode?> SelectedNodeProperty =
        AvaloniaProperty.Register<ObjectViewer, ObjectNode?>(nameof(SelectedNode));

    /// <summary>
    /// Defines the <see cref="Items"/> property.
    /// </summary>
    public static readonly DirectProperty<ObjectViewer, ObservableCollection<ObjectNode>> ItemsProperty =
        AvaloniaProperty.RegisterDirect<ObjectViewer, ObservableCollection<ObjectNode>>(
            nameof(Items),
            o => o.Items);

    /// <summary>
    /// Defines the <see cref="SelectedSourceRange"/> property.
    /// </summary>
    public static readonly DirectProperty<ObjectViewer, SourceRange?> SelectedSourceRangeProperty =
        AvaloniaProperty.RegisterDirect<ObjectViewer, SourceRange?>(
            nameof(SelectedSourceRange),
            o => o.SelectedSourceRange);

    private ObservableCollection<ObjectNode> _items = [];
    private SourceRange? _selectedSourceRange;

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
    /// Gets or sets the raw source text for highlighting.
    /// </summary>
    public string? SourceText
    {
        get => GetValue(SourceTextProperty);
        set => SetValue(SourceTextProperty, value);
    }

    /// <summary>
    /// Gets or sets the currently selected node.
    /// </summary>
    public ObjectNode? SelectedNode
    {
        get => GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    /// <summary>
    /// Gets the source range for the currently selected node.
    /// </summary>
    public SourceRange? SelectedSourceRange
    {
        get => _selectedSourceRange;
        private set => SetAndRaise(SelectedSourceRangeProperty, ref _selectedSourceRange, value);
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
        SelectedNodeProperty.Changed.AddClassHandler<ObjectViewer>((x, _) => x.OnSelectedNodeChanged());
        FocusableProperty.OverrideDefaultValue<ObjectViewer>(true);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        
        // Unsubscribe from old controls
        if (_searchTextBox != null)
        {
            _searchTextBox.KeyDown -= OnSearchTextBoxKeyDown;
        }
        if (_searchButton != null)
        {
            _searchButton.Click -= OnSearchButtonClick;
        }
        if (_treeView != null)
        {
            _treeView.SelectionChanged -= OnTreeViewSelectionChanged;
        }
        
        // Get new controls
        _searchTextBox = e.NameScope.Find<TextBox>("PART_SearchTextBox");
        _searchButton = e.NameScope.Find<Button>("PART_SearchButton");
        _treeView = e.NameScope.Find<TreeView>("PART_TreeView");
        _rawTextBox = e.NameScope.Find<TextBox>("PART_RawTextBox");
        
        // Subscribe to events
        if (_searchTextBox != null)
        {
            _searchTextBox.KeyDown += OnSearchTextBoxKeyDown;
        }
        if (_searchButton != null)
        {
            _searchButton.Click += OnSearchButtonClick;
        }
        if (_treeView != null)
        {
            _treeView.SelectionChanged += OnTreeViewSelectionChanged;
        }
    }

    private void OnTreeViewSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is ObjectNode node)
        {
            SelectedNode = node;
        }
    }

    private void OnSelectedNodeChanged()
    {
        UpdateSelectedSourceRange();
    }

    private void UpdateSelectedSourceRange()
    {
        if (SelectedNode == null || string.IsNullOrEmpty(SourceText) || !SelectedNode.HasSourceLocation)
        {
            SelectedSourceRange = null;
            return;
        }

        var startLine = SelectedNode.StartLine!.Value;
        var startCol = SelectedNode.StartColumn ?? 1;
        var endLine = SelectedNode.EndLine ?? startLine;
        var endCol = SelectedNode.EndColumn ?? startCol;

        // Convert line/column to character offsets
        var (startOffset, endOffset) = GetCharacterOffsets(SourceText, startLine, startCol, endLine, endCol);
        
        SelectedSourceRange = new SourceRange(startLine, startCol, endLine, endCol, startOffset, endOffset);
        
        
        // Update the raw text box selection and scroll into view
        if (_rawTextBox != null && startOffset >= 0 && endOffset >= startOffset)
        {
            // Set caret first to trigger scroll, then set selection
            _rawTextBox.CaretIndex = startOffset;
            _rawTextBox.SelectionStart = startOffset;
            _rawTextBox.SelectionEnd = endOffset;
            
            // Force focus to ensure scroll happens, then we can return focus if needed
            _rawTextBox.Focus();
            
            // Use dispatcher to scroll after layout
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // Get the ScrollViewer inside the TextBox and scroll to the line
                var scrollViewer = FindScrollViewer(_rawTextBox);
                if (scrollViewer != null && !string.IsNullOrEmpty(SourceText))
                {
                    // Calculate approximate line height and scroll position
                    var totalLines = SourceText.Split('\n').Length;
                    if (totalLines > 0)
                    {
                        var lineHeight = scrollViewer.Extent.Height / totalLines;
                        var targetY = (startLine - 1) * lineHeight;
                        
                        // Scroll to show the line near the top with some padding
                        var scrollY = Math.Max(0, targetY - scrollViewer.Viewport.Height / 4);
                        scrollViewer.Offset = new Avalonia.Vector(scrollViewer.Offset.X, scrollY);
                    }
                }
            }, Avalonia.Threading.DispatcherPriority.Background);
        }
    }

    private static ScrollViewer? FindScrollViewer(Control control)
    {
        if (control is ScrollViewer sv)
            return sv;

        // Search in visual children
        var count = Avalonia.VisualTree.VisualExtensions.GetVisualChildren(control).Count();
        foreach (var child in Avalonia.VisualTree.VisualExtensions.GetVisualChildren(control))
        {
            if (child is ScrollViewer foundSv)
                return foundSv;
            if (child is Control childControl)
            {
                var result = FindScrollViewer(childControl);
                if (result != null)
                    return result;
            }
        }
        return null;
    }

    private static (int startOffset, int endOffset) GetCharacterOffsets(string text, int startLine, int startCol, int endLine, int endCol)
    {
        int currentLine = 1;
        int currentOffset = 0;
        int startOffset = -1;
        int endOffset = -1;
        int lineStart = 0;

        for (int i = 0; i <= text.Length; i++)
        {
            if (currentLine == startLine && startOffset < 0)
            {
                startOffset = lineStart + Math.Max(0, startCol - 1);
            }
            if (currentLine == endLine && endOffset < 0)
            {
                endOffset = lineStart + Math.Max(0, endCol - 1);
            }

            if (i < text.Length && text[i] == '\n')
            {
                currentLine++;
                lineStart = i + 1;
            }
        }

        // If end wasn't found, use end of file
        if (endOffset < 0) endOffset = text.Length;
        if (startOffset < 0) startOffset = 0;

        return (Math.Min(startOffset, text.Length), Math.Min(endOffset, text.Length));
    }

    private void OnSearchTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PerformSearch();
            e.Handled = true;
        }
    }

    private void OnSearchButtonClick(object? sender, RoutedEventArgs e)
    {
        PerformSearch();
    }

    private void PerformSearch()
    {
        var searchText = _searchTextBox?.Text;
        // TODO: Implement search functionality
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        
        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            _searchTextBox?.Focus();
            e.Handled = true;
        }
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
