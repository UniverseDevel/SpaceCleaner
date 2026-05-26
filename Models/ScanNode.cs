using System.Collections.ObjectModel;
using System.IO;
using SpaceCleaner.Infrastructure;

namespace SpaceCleaner.Models;

public enum NodeKind
{
    Drive,
    Category,
    Directory,
}

/// <summary>
/// One node in the results tree. A drive node aggregates category nodes, each of which aggregates
/// a directory subtree. <see cref="Size"/> is the recursive total of everything beneath the node,
/// so a drive node shows the total reclaimable space for that drive.
/// </summary>
public sealed class ScanNode : ObservableObject
{
    public required NodeKind Kind { get; set; }

    /// <summary>Text shown in the tree (drive label, category name, or directory name). Settable
    /// because grouping may relabel a node when it becomes a child of a combined category.</summary>
    public required string Name { get; set; }

    public required string FullPath { get; init; }

    /// <summary>The catalog category this node belongs to (null only for the drive root).</summary>
    public CleanupCategory? Category { get; init; }

    /// <summary>Total bytes contained in this node and everything under it.</summary>
    public long Size { get; set; }

    /// <summary>Bytes from files stored directly in this directory (not in subdirectories).</summary>
    public long OwnFileSize { get; set; }

    /// <summary>True when deeper subdirectories were summed into <see cref="Size"/> but not shown as
    /// nodes (the tree is capped in depth for responsiveness).</summary>
    public bool ChildrenAggregated { get; set; }

    public ObservableCollection<ScanNode> Children { get; } = new();

    public bool HasChildren => Children.Count > 0;

    /// <summary>Set while building the tree so checkbox state can propagate up to ancestors.</summary>
    public ScanNode? Parent { get; set; }

    // Tri-state selection for the (future) deletion workflow. Checking a node checks its whole
    // subtree; a parent shows indeterminate when only some descendants are checked.
    private bool? _isChecked = false;
    public bool? IsChecked
    {
        get => _isChecked;
        set => SetIsChecked(value, updateChildren: true, updateParent: true);
    }

    private void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
    {
        if (value == _isChecked)
            return;

        _isChecked = value;

        if (updateChildren && _isChecked.HasValue)
        {
            foreach (var child in Children)
                child.SetIsChecked(_isChecked, updateChildren: true, updateParent: false);
        }

        if (updateParent)
            Parent?.VerifyCheckStateFromChildren();

        OnPropertyChanged(nameof(IsChecked));
    }

    private void VerifyCheckStateFromChildren()
    {
        bool? state = null;
        for (int i = 0; i < Children.Count; i++)
        {
            var childState = Children[i].IsChecked;
            if (i == 0)
                state = childState;
            else if (state != childState)
            {
                state = null; // mixed -> indeterminate
                break;
            }
        }

        SetIsChecked(state, updateChildren: false, updateParent: true);
    }

    private bool _isExpanded;
    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetField(ref _isExpanded, value);
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    // ---- Presentation helpers (read once after the node is built) ---------------------------

    public string SizeText => ByteSize.Format(Size);

    public string OwnFileSizeText => ByteSize.Format(OwnFileSize);

    public string KindGlyph => Kind switch
    {
        NodeKind.Drive => "🖴",
        NodeKind.Category => "🧹",
        _ => "📁",
    };

    public bool IsCategory => Kind == NodeKind.Category;
    public bool IsDrive => Kind == NodeKind.Drive;

    public string CategoryName => Category?.Name ?? "Drive total";

    public string WhySafeText => Category?.WhySafe
        ?? "Summary for this drive. Each item below is a well-known location whose contents are safe " +
           "to remove. Expand it to see exactly where the space is and select any folder for details.";

    public string TypicalContentsText => Category?.TypicalContents ?? string.Empty;

    public string? CautionText => Category?.Caution;

    /// <summary>For directory nodes, the path is the most useful subtitle. For category nodes the
    /// resolved location is shown so the user knows where it lives.</summary>
    public string SubtitlePath => Kind == NodeKind.Drive ? FullPath : FullPath;

    public ScanNode SortChildrenBySizeDescending()
    {
        var ordered = Children.OrderByDescending(c => c.Size).ToList();
        Children.Clear();
        foreach (var child in ordered)
            Children.Add(child);
        return this;
    }
}
