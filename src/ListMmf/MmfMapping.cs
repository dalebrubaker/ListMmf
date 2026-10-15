namespace BruSoftware.ListMmf;

/// <summary>
/// An immutable snapshot of the pointers into one mapped view of a ListMmf file.
/// A ListMmf publishes a new snapshot each time it remaps, and unmaps the old view only after
/// <see cref="MmfReaders.WaitForReaders"/> shows no thread can still be reading through it.
/// </summary>
internal sealed unsafe class MmfMapping
{
    /// <summary>
    /// Published while a remap is in progress. Readers that see it leave their read section and retry.
    /// </summary>
    public static readonly MmfMapping Remapping = new();

    private MmfMapping()
    {
        IsRemapping = true;
    }

    public MmfMapping(byte* basePointer, long* ptrCount, byte* ptrArray)
    {
        BasePointer = basePointer;
        PtrCount = ptrCount;
        PtrArray = ptrArray;
    }

    public bool IsRemapping { get; }

    /// <summary>
    /// The beginning of the view (Version is at offset 0)
    /// </summary>
    public byte* BasePointer { get; }

    public long* PtrCount { get; }

    public byte* PtrArray { get; }
}
