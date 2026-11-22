namespace UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

/// <summary>
/// One file the visitor added to a tool.
/// </summary>
/// <typeparam name="T">The loaded content, for example a <c>PdfFile</c>.</typeparam>
/// <param name="Id">A number unique within the set, used as the element key.</param>
/// <param name="Name">The file's name as the visitor's device gave it.</param>
/// <param name="Size">The file's size, in bytes.</param>
/// <param name="Value">The loaded content.</param>
public sealed record ToolFile<T>(int Id, string Name, long Size, T Value);

/// <summary>
/// The files a tool works on, in the visitor's order, within the tools' file count and total size (OWASP A06). A file
/// is checked before it is read, so a file that would pass a limit is refused without being loaded, and the files
/// already added stay.
/// </summary>
/// <typeparam name="T">The loaded content of each file.</typeparam>
public sealed class ToolFileSet<T>
{
    #region Fields and Constants

    private readonly List<ToolFile<T>> _files = [];
    private readonly int _maxFiles;
    private readonly long _maxTotalBytes;
    private int _nextId;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolFileSet{T}"/> class.
    /// </summary>
    /// <param name="maxFiles">The most files the set holds.</param>
    /// <param name="maxTotalBytes">The most bytes the set holds in total.</param>
    public ToolFileSet(int maxFiles = ToolLimits.MaxFiles, long maxTotalBytes = ToolLimits.MaxTotalBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxFiles, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTotalBytes, 1);
        _maxFiles = maxFiles;
        _maxTotalBytes = maxTotalBytes;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Adds a loaded file at the end.
    /// </summary>
    /// <param name="name">The file's name.</param>
    /// <param name="size">The file's size, in bytes.</param>
    /// <param name="value">The loaded content.</param>
    /// <returns>The added file.</returns>
    /// <exception cref="InvalidOperationException">The file would pass a limit; call <see cref="CheckRoomFor"/> first.</exception>
    public ToolFile<T> Add(string name, long size, T value)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (CheckRoomFor(size) is { } error) throw new InvalidOperationException(error);
        var file = new ToolFile<T>(++_nextId, name, size, value);
        _files.Add(file);
        return file;
    }

    /// <summary>
    /// Returns why a file of <paramref name="size"/> bytes can't be added, or null when it can.
    /// </summary>
    /// <param name="size">The file's size, in bytes.</param>
    /// <returns>The message to show, or null.</returns>
    public string? CheckRoomFor(long size)
    {
        if (_files.Count >= _maxFiles)
        {
            return $"One run takes up to {_maxFiles} files.";
        }

        long total = TotalBytes + size;
        return total > _maxTotalBytes
            ? $"These files add up to {ToolLimits.Megabytes(total)}. One run takes up to {ToolLimits.Megabytes(_maxTotalBytes)} in total."
            : null;
    }

    /// <summary>
    /// Removes every file.
    /// </summary>
    public void Clear() => _files.Clear();

    /// <summary>
    /// Moves the file at <paramref name="index"/> one place earlier; the first file stays.
    /// </summary>
    /// <param name="index">The file's position.</param>
    public void MoveEarlier(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _files.Count);
        if (index > 0) (_files[index - 1], _files[index]) = (_files[index], _files[index - 1]);
    }

    /// <summary>
    /// Moves the file at <paramref name="index"/> one place later; the last file stays.
    /// </summary>
    /// <param name="index">The file's position.</param>
    public void MoveLater(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _files.Count);
        if (index < _files.Count - 1) (_files[index + 1], _files[index]) = (_files[index], _files[index + 1]);
    }

    /// <summary>
    /// Moves the file at <paramref name="from"/> to <paramref name="to"/>, for drag and drop.
    /// </summary>
    /// <param name="from">The file's position.</param>
    /// <param name="to">Its new position.</param>
    public void Move(int from, int to)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(from);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(from, _files.Count);
        ArgumentOutOfRangeException.ThrowIfNegative(to);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(to, _files.Count);
        var file = _files[from];
        _files.RemoveAt(from);
        _files.Insert(to, file);
    }

    /// <summary>
    /// Removes the file at <paramref name="index"/>.
    /// </summary>
    /// <param name="index">The file's position.</param>
    public void RemoveAt(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _files.Count);
        _files.RemoveAt(index);
    }

    /// <summary>
    /// Sorts the files by name, A to Z, ignoring case.
    /// </summary>
    public void SortByName()
    {
        var sorted = _files.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Id).ToList();
        _files.Clear();
        _files.AddRange(sorted);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets how many files the set holds.
    /// </summary>
    public int Count => _files.Count;

    /// <summary>
    /// Gets the files, in the visitor's order.
    /// </summary>
    public IReadOnlyList<ToolFile<T>> Files => _files;

    /// <summary>
    /// Gets the files' total size, in bytes.
    /// </summary>
    public long TotalBytes => _files.Sum(f => f.Size);

    #endregion
}
