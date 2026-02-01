using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;

namespace DumpViewer;


/// <summary>
/// A control that visualizes any object in a tree structure similar to LinqPad's .Dump().
/// </summary>
public class ObjectViewer : TemplatedControl
{
    private TextBox? _searchTextBox;
    private Button? _searchButton;
    private TreeView? _treeView;
    private TextEditor? _textEditor;
    private bool _isSyncingFromTree;
    private bool _isSyncingFromEditor;
    
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
    /// Defines the <see cref="SyntaxHighlighting"/> property (json, yaml, xml, csv).
    /// </summary>
    public static readonly StyledProperty<string?> SyntaxHighlightingProperty =
        AvaloniaProperty.Register<ObjectViewer, string?>(nameof(SyntaxHighlighting));

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
    /// Gets or sets the syntax highlighting format (json, yaml, xml, csv).
    /// </summary>
    public string? SyntaxHighlighting
    {
        get => GetValue(SyntaxHighlightingProperty);
        set => SetValue(SyntaxHighlightingProperty, value);
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
        SourceTextProperty.Changed.AddClassHandler<ObjectViewer>((x, _) => x.OnSourceTextChanged());
        SyntaxHighlightingProperty.Changed.AddClassHandler<ObjectViewer>((x, _) => x.OnSyntaxHighlightingChanged());
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
        if (_textEditor != null)
        {
            _textEditor.TextArea.Caret.PositionChanged -= OnEditorCaretPositionChanged;
        }
        
        // Get new controls
        _searchTextBox = e.NameScope.Find<TextBox>("PART_SearchTextBox");
        _searchButton = e.NameScope.Find<Button>("PART_SearchButton");
        _treeView = e.NameScope.Find<TreeView>("PART_TreeView");
        _textEditor = e.NameScope.Find<TextEditor>("PART_TextEditor");
        
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
        if (_textEditor != null)
        {
            _textEditor.TextArea.Caret.PositionChanged += OnEditorCaretPositionChanged;
            ConfigureTextEditor();
            UpdateTextEditorContent();
        }
    }

    private void ConfigureTextEditor()
    {
        if (_textEditor == null) return;
        
        _textEditor.IsReadOnly = true;
        _textEditor.ShowLineNumbers = true;
        _textEditor.FontFamily = new FontFamily("Consolas, Monaco, 'Courier New', monospace");
        _textEditor.FontSize = 13;
        _textEditor.Background = Brushes.Transparent;
        _textEditor.Foreground = new SolidColorBrush(Color.Parse("#D4D4D4"));
    }

    private void UpdateTextEditorContent()
    {
        if (_textEditor == null) return;
        
        // Set text content using Document for proper rendering
        var text = SourceText ?? string.Empty;
        if (_textEditor.Document == null)
        {
            _textEditor.Document = new TextDocument(text);
        }
        else if (_textEditor.Document.Text != text)
        {
            _textEditor.Document.Text = text;
        }
        
        // Apply syntax highlighting
        var highlighting = SyntaxHighlightingManager.GetHighlightingForFormat(SyntaxHighlighting);
        if (_textEditor.SyntaxHighlighting != highlighting)
        {
            _textEditor.SyntaxHighlighting = highlighting;
        }
    }

    private void OnSourceTextChanged()
    {
        UpdateTextEditorContent();
    }


    private void OnSyntaxHighlightingChanged()
    {
        UpdateTextEditorContent();
    }

    private void OnEditorCaretPositionChanged(object? sender, EventArgs e)
    {
        if (_isSyncingFromTree || _textEditor == null) return;
        
        _isSyncingFromEditor = true;
        try
        {
            var line = _textEditor.TextArea.Caret.Line;
            var node = FindNodeAtLine(Items, line);
            if (node != null && node != SelectedNode)
            {
                SelectedNode = node;
                ExpandToNode(node);
                SelectNodeInTree(node);
            }
        }
        finally
        {
            _isSyncingFromEditor = false;
        }
    }

    private ObjectNode? FindNodeAtLine(IEnumerable<ObjectNode> nodes, int line)
    {
        foreach (var node in nodes)
        {
            if (node.HasSourceLocation && node.StartLine <= line && (node.EndLine ?? node.StartLine) >= line)
            {
                // Check children for a more specific match
                var childMatch = FindNodeAtLine(node.Children, line);
                return childMatch ?? node;
            }
        }
        return null;
    }

    private void ExpandToNode(ObjectNode targetNode)
    {
        // Expand all ancestors
        ExpandAncestors(Items, targetNode);
    }

    private bool ExpandAncestors(IEnumerable<ObjectNode> nodes, ObjectNode target)
    {
        foreach (var node in nodes)
        {
            if (node == target)
                return true;
            
            if (node.Children.Contains(target) || ExpandAncestors(node.Children, target))
            {
                node.IsExpanded = true;
                return true;
            }
        }
        return false;
    }

    private void SelectNodeInTree(ObjectNode node)
    {
        if (_treeView == null) return;
        _treeView.SelectedItem = node;
    }

    private void OnTreeViewSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isSyncingFromEditor) return;
        
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is ObjectNode node)
        {
            SelectedNode = node;
        }
    }

    private void OnSelectedNodeChanged()
    {
        if (_isSyncingFromEditor) return;
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
        
        // Update the text editor selection and scroll into view
        if (_textEditor != null && startOffset >= 0)
        {
            _isSyncingFromTree = true;
            try
            {
                // Select the text range
                _textEditor.Select(startOffset, Math.Max(0, endOffset - startOffset));
                
                // Scroll to make the line visible
                _textEditor.ScrollToLine(startLine);
                
                // Center the line in view
                _textEditor.TextArea.Caret.Line = startLine;
                _textEditor.TextArea.Caret.Column = startCol;
            }
            finally
            {
                _isSyncingFromTree = false;
            }
        }
    }

    private static (int startOffset, int endOffset) GetCharacterOffsets(string text, int startLine, int startCol, int endLine, int endCol)
    {
        int currentLine = 1;
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
