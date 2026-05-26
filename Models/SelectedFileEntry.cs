using SpaceCleaner.Infrastructure;

namespace SpaceCleaner.Models;

/// <summary>One file in the "selected files" list: its full path and size.</summary>
public sealed class SelectedFileEntry
{
    public SelectedFileEntry(string path, long size)
    {
        Path = path;
        Size = size;
    }

    public string Path { get; }
    public long Size { get; }
    public string SizeText => ByteSize.Format(Size);
}
