using UnlimitedPDF.Demos.BlazorWasm.Samples;

namespace UnlimitedPDF.Demos.BlazorWasm.Services;

/// <summary>
/// Loads the app's built-in images from its own <c>images/</c> folder, once each. A <see cref="PdfImage"/> is
/// immutable and reusable across documents, so later generations skip both the download and the decode.
/// </summary>
public sealed class SampleImageStore
{
    #region Fields and Constants

    private readonly Dictionary<string, Task<PdfImage>> _cache = new();
    private readonly HttpClient _http;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a new instance of the <see cref="SampleImageStore"/> class.
    /// </summary>
    /// <param name="http">The client for the app's own origin.</param>
    public SampleImageStore(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Gets one built-in image.
    /// </summary>
    /// <param name="fileName">The image file name, for example "dock.jpg".</param>
    /// <returns>The loaded image.</returns>
    public Task<PdfImage> GetAsync(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        if (!_cache.TryGetValue(fileName, out var task))
        {
            task = LoadAsync(fileName);
            _cache[fileName] = task;
        }

        return task;
    }

    /// <summary>
    /// Gets several built-in images at once.
    /// </summary>
    /// <param name="fileNames">The image file names.</param>
    /// <returns>The loaded images.</returns>
    public async Task<SampleImages> GetManyAsync(params string[] fileNames)
    {
        ArgumentNullException.ThrowIfNull(fileNames);
        var tasks = fileNames.Distinct().ToDictionary(name => name, GetAsync);
        await Task.WhenAll(tasks.Values);
        return new SampleImages(tasks.ToDictionary(pair => pair.Key, pair => pair.Value.Result));
    }

    #endregion

    #region Private Methods

    private async Task<PdfImage> LoadAsync(string fileName)
    {
        try
        {
            var bytes = await _http.GetByteArrayAsync($"images/{fileName}");
            return PdfImage.FromBytes(bytes);
        }
        catch
        {
            // Let a later attempt retry instead of caching the failure.
            _cache.Remove(fileName);
            throw;
        }
    }

    #endregion

    #endregion
}
