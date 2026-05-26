using System.IO;
using SpaceCleaner.Infrastructure;

namespace SpaceCleaner.Models;

/// <summary>A fixed/removable drive the user can choose to include in a scan.</summary>
public sealed class DriveItem : ObservableObject
{
    public required string Root { get; init; }          // e.g. "C:\"
    public required string Label { get; init; }         // volume label or drive type
    public long TotalBytes { get; init; }
    public long FreeBytes { get; init; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    /// <summary>Drive letter without trailing slash, e.g. "C:".</summary>
    public string Letter => Root.TrimEnd(Path.DirectorySeparatorChar);

    public string DisplayName => string.IsNullOrWhiteSpace(Label)
        ? Letter
        : $"{Letter} ({Label})";

    public string SpaceSummary => $"{ByteSize.Format(FreeBytes)} free of {ByteSize.Format(TotalBytes)}";
}
