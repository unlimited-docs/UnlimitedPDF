namespace UnlimitedPDF.Models.Internal.Html.Resources;

/// <summary>
/// Loads the images and stylesheets HTML names, inside the PR-018 boundary (WR-038, A01): <c>data:</c> URIs; relative
/// paths only when they resolve inside the base folder and pass through no symbolic link or junction; anything else
/// only through the caller's resolver. The library itself makes no network request. Size limits fail the load (A06).
/// Expected failures become warnings (D-020: not found, blocked, not resolved); every other exception propagates.
/// </summary>
internal sealed class HtmlResourceLoader
{
    #region Fields and Constants

    private readonly string? _baseFolder;
    private readonly HtmlLimits _limits;
    private readonly PdfHtmlLoadOptions? _options;
    private readonly HtmlWarnings _warnings;
    private long _total;

    #endregion

    #region Constructors and Finalizers

    /// <summary>
    /// Creates the loader.
    /// </summary>
    /// <param name="baseFolder">The base folder, or null for none.</param>
    /// <param name="options">The load options, for the resolvers, or null.</param>
    /// <param name="limits">The size limits.</param>
    /// <param name="warnings">Receives resource warnings.</param>
    public HtmlResourceLoader(string? baseFolder, PdfHtmlLoadOptions? options, HtmlLimits limits, HtmlWarnings warnings)
    {
        _baseFolder = baseFolder is null ? null : Path.GetFullPath(baseFolder);
        _options = options;
        _limits = limits;
        _warnings = warnings;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Loads one resource synchronously.
    /// </summary>
    /// <param name="reference">The reference as written in the HTML.</param>
    /// <param name="line">The HTML line, for warnings.</param>
    /// <returns>The bytes, or null after a warning.</returns>
    public byte[]? Load(string reference, int line)
    {
        var plan = Classify(reference, line);
        switch (plan.Kind)
        {
            case PlanKind.Bytes: return Count(plan.Bytes!, reference);
            case PlanKind.File: return Count(ReadFile(plan.Path!, reference), reference);
            case PlanKind.Resolver:
                if (_options?.ResourceResolver is not { } resolver) return NotResolved(reference, line);
                using (var stream = resolver(reference))
                    return stream is null ? NotResolved(reference, line) : Count(ReadStream(stream, reference), reference);
            default: return null;
        }
    }

    /// <summary>
    /// Loads one resource asynchronously, using the asynchronous resolver when one is set.
    /// </summary>
    /// <param name="reference">The reference as written in the HTML.</param>
    /// <param name="line">The HTML line, for warnings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The bytes, or null after a warning.</returns>
    public async Task<byte[]?> LoadAsync(string reference, int line, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var plan = Classify(reference, line);
        switch (plan.Kind)
        {
            case PlanKind.Bytes: return Count(plan.Bytes!, reference);
            case PlanKind.File: return Count(await ReadFileAsync(plan.Path!, reference, cancellationToken).ConfigureAwait(false), reference);
            case PlanKind.Resolver:
                Stream? stream;
                if (_options?.ResourceResolverAsync is { } asyncResolver) stream = await asyncResolver(reference, cancellationToken).ConfigureAwait(false);
                else if (_options?.ResourceResolver is { } resolver) stream = resolver(reference);
                else return NotResolved(reference, line);
                if (stream is null) return NotResolved(reference, line);
                using (stream)
                    return Count(await ReadStreamAsync(stream, reference, cancellationToken).ConfigureAwait(false), reference);
            default: return null;
        }
    }

    #endregion

    #region Private Methods

    private byte[] Count(byte[] data, string reference)
    {
        CheckSize(data.LongLength, reference);
        _total += data.LongLength;
        if (_total > _limits.MaxTotalResourceSize)
            throw new PdfHtmlException(PdfHtmlErrorReason.ResourcesTooLarge, string.Format(CultureInfo.InvariantCulture,
                "The images and stylesheets of the HTML are larger together than the limit of {0} bytes (at \"{1}\").", _limits.MaxTotalResourceSize, Short(reference)));
        return data;
    }

    private void CheckSize(long size, string reference)
    {
        if (size > _limits.MaxResourceSize)
            throw new PdfHtmlException(PdfHtmlErrorReason.ResourceTooLarge, string.Format(CultureInfo.InvariantCulture,
                "The resource \"{0}\" is larger than the limit of {1} bytes.", Short(reference), _limits.MaxResourceSize));
    }

    private static byte[]? DecodeData(string reference)
    {
        int comma = reference.IndexOf(',');
        if (comma < 0) return null;
        string header = reference.Substring(5, comma - 5);
        string payload = reference.Substring(comma + 1);
        if (header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
        {
            string compact = new string(payload.Where(c => !char.IsWhiteSpace(c)).ToArray());
            compact = Uri.UnescapeDataString(compact);
            compact = compact.PadRight(compact.Length + (4 - compact.Length % 4) % 4, '=');
            try { return Convert.FromBase64String(compact); }
            catch (FormatException) { return null; }
        }
        return PercentDecodeBytes(payload);
    }

    private static bool HasScheme(string reference, out string scheme)
    {
        scheme = string.Empty;
        int colon = reference.IndexOf(':');
        if (colon <= 0) return false;
        string candidate = reference.Substring(0, colon);
        if (!char.IsLetter(candidate[0]) || !candidate.All(c => char.IsLetterOrDigit(c) || c is '+' or '-' or '.')) return false;
        scheme = candidate.ToLowerInvariant();
        return true;
    }

    // True when a path below the base folder (the folder itself excluded) is a symbolic link or junction.
    private bool PassesReparsePoint(string fullPath)
    {
        string relative = fullPath.Substring(_baseFolder!.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string current = _baseFolder;
        foreach (var segment in relative.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current)) return false;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) return true;
        }
        return false;
    }

    private static byte[] PercentDecodeBytes(string text)
    {
        // Unescaped text is encoded in runs, so a surrogate pair stays one character (F-07).
        var bytes = new List<byte>(text.Length);
        int run = 0;
        void Flush(int end)
        {
            if (end > run) bytes.AddRange(Encoding.UTF8.GetBytes(text.Substring(run, end - run)));
        }
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '%' && i + 2 < text.Length && Uri.IsHexDigit(text[i + 1]) && Uri.IsHexDigit(text[i + 2]))
            {
                Flush(i);
                bytes.Add(Convert.ToByte(text.Substring(i + 1, 2), 16));
                i += 2;
                run = i + 1;
            }
        }
        Flush(text.Length);
        return bytes.ToArray();
    }

    private Plan Classify(string reference, int line)
    {
        string trimmed = reference.Trim();
        if (trimmed.Length == 0) return default;

        if (trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var data = DecodeData(trimmed);
            if (data is null)
            {
                _warnings.Add(PdfHtmlWarningKind.InvalidImage, Short(reference), "The data: URI could not be decoded.", line);
                return default;
            }
            return new Plan(PlanKind.Bytes, data, null);
        }

        bool drive = trimmed.Length >= 2 && char.IsLetter(trimmed[0]) && trimmed[1] == ':';
        bool rooted = drive || trimmed.StartsWith("\\\\", StringComparison.Ordinal) || trimmed.StartsWith("/", StringComparison.Ordinal) && !trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith("\\", StringComparison.Ordinal);
        if (!drive && HasScheme(trimmed, out string scheme))
        {
            if (scheme == "file") return Blocked(reference, line, "file: references are never read");
            return new Plan(PlanKind.Resolver, null, null);
        }
        if (trimmed.StartsWith("//", StringComparison.Ordinal)) return new Plan(PlanKind.Resolver, null, null);
        if (rooted) return Blocked(reference, line, "absolute paths are never read");
        if (_baseFolder is null) return new Plan(PlanKind.Resolver, null, null);

        // A relative path: without query or fragment, percent-decoded, and only inside the base folder.
        string path = trimmed;
        int cut = path.IndexOfAny(new[] { '?', '#' });
        if (cut >= 0) path = path.Substring(0, cut);
        path = Encoding.UTF8.GetString(PercentDecodeBytes(path)).Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (path.IndexOf('\0') >= 0 || Path.IsPathRooted(path)) return Blocked(reference, line, "absolute paths are never read");
        // A colon would name an alternate data stream of a file (F-07); file names in HTML never need one.
        if (path.IndexOf(':') >= 0) return Blocked(reference, line, "paths with a colon are never read");
        string full = Path.GetFullPath(Path.Combine(_baseFolder, path));
        string root = _baseFolder.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? _baseFolder : _baseFolder + Path.DirectorySeparatorChar;
        var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!full.StartsWith(root, comparison)) return Blocked(reference, line, "the path leaves the base folder");
        if (PassesReparsePoint(full)) return Blocked(reference, line, "the path passes through a symbolic link or junction");
        if (!File.Exists(full))
        {
            _warnings.Add(PdfHtmlWarningKind.ResourceNotFound, reference, $"\"{reference}\" was not found in the base folder.", line);
            return default;
        }
        return new Plan(PlanKind.File, null, full);
    }

    private Plan Blocked(string reference, int line, string why)
    {
        _warnings.Add(PdfHtmlWarningKind.ResourceBlocked, Short(reference), $"\"{Short(reference)}\" was not read: {why} (PR-018).", line);
        return default;
    }

    private byte[]? NotResolved(string reference, int line)
    {
        _warnings.Add(PdfHtmlWarningKind.ResourceNotResolved, Short(reference),
            $"\"{Short(reference)}\" is not loaded by the library and no resolver supplied it.", line);
        return null;
    }

    private byte[] ReadFile(string path, string reference)
    {
        CheckSize(new FileInfo(path).Length, reference);
        return File.ReadAllBytes(path);
    }

    private async Task<byte[]> ReadFileAsync(string path, string reference, CancellationToken cancellationToken)
    {
        CheckSize(new FileInfo(path).Length, reference);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return await ReadStreamAsync(stream, reference, cancellationToken).ConfigureAwait(false);
    }

    private byte[] ReadStream(Stream stream, string reference)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = stream.Read(chunk, 0, chunk.Length)) > 0)
        {
            buffer.Write(chunk, 0, read);
            CheckSize(buffer.Length, reference);
        }
        return buffer.ToArray();
    }

    private async Task<byte[]> ReadStreamAsync(Stream stream, string reference, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, 0, chunk.Length, cancellationToken).ConfigureAwait(false)) > 0)
        {
            buffer.Write(chunk, 0, read);
            CheckSize(buffer.Length, reference);
        }
        return buffer.ToArray();
    }

    // Long data: URIs are shortened in messages.
    private static string Short(string reference) => reference.Length <= 80 ? reference : reference.Substring(0, 77) + "...";

    #endregion

    #endregion

    #region Nested Types

    private enum PlanKind
    {
        None,
        Bytes,
        File,
        Resolver
    }

    private readonly record struct Plan(PlanKind Kind, byte[]? Bytes, string? Path);

    #endregion
}
