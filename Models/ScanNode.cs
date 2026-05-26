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

    /// <summary>True for a grouped category node whose <see cref="FullPath"/> is the common base of
    /// several real scan paths (its children). Such a node must not be enumerated directly.</summary>
    public bool IsGroupRoot { get; set; }

    public ObservableCollection<ScanNode> Children { get; } = new();

    public bool HasChildren => Children.Count > 0;

    /// <summary>Set while building the tree so checkbox state can propagate up to ancestors.</summary>
    public ScanNode? Parent { get; set; }

    /// <summary>Distance from the drive root (drive = 0). Used to indent rows by margin so the size
    /// column can line up at a fixed position regardless of depth.</summary>
    public int Depth
    {
        get
        {
            int depth = 0;
            for (var p = Parent; p is not null; p = p.Parent)
                depth++;
            return depth;
        }
    }

    // Tri-state selection for the (future) deletion workflow. Checking a node checks its whole
    // subtree; a parent shows indeterminate when only some descendants are checked.
    private bool? _isChecked = false;
    public bool? IsChecked
    {
        get => _isChecked;
        set => SetIsChecked(value, updateChildren: true, updateParent: true);
    }

    /// <summary>Raised (on the UI thread) whenever any node's checked state changes, so the view model
    /// can recompute the selected total. Subscribers should debounce — one user action can fire this
    /// many times as the change propagates through the subtree.</summary>
    public static event Action? AnyIsCheckedChanged;

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
        AnyIsCheckedChanged?.Invoke();
    }

    /// <summary>Bytes selected within this node: its full size if checked, 0 if unchecked, or the sum
    /// of selected descendants if partially checked. Counts each selected region exactly once.</summary>
    public long SelectedSize => _isChecked switch
    {
        true => Size,
        false => 0,
        _ => Children.Sum(c => c.SelectedSize),
    };

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

    /// <summary>Safety rating for this node (inherited from its category). Drive nodes have none.</summary>
    public SafetyLevel SafetyLevel => Category?.SafetyLevel ?? SafetyLevel.Safe;

    /// <summary>Drive nodes don't show a safety rating; everything under a category does.</summary>
    public bool ShowSafety => Category is not null;

    public string SafetyText => SafetyLevel switch
    {
        SafetyLevel.Safe => "Safe to remove",
        SafetyLevel.Caution => "Safe — with a cost",
        SafetyLevel.Review => "Review before removing",
        _ => string.Empty,
    };

    public string SafetyBlurb => SafetyLevel switch
    {
        SafetyLevel.Safe => "Regenerated automatically — removing it has no real downside.",
        SafetyLevel.Caution => "Fine to remove, but it will be re-downloaded or rebuilt, which takes time and possibly bandwidth.",
        SafetyLevel.Review => "May contain data you still want, or affect a Windows rollback. Check the contents before removing.",
        _ => string.Empty,
    };

    /// <summary>Why this node has its safety rating (inherited from its category).</summary>
    public string SafetyRationale => Category?.SafetyRationale ?? string.Empty;

    /// <summary>What could go wrong if this location is removed.</summary>
    public string PotentialRisks => Category?.PotentialRisks ?? string.Empty;

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
