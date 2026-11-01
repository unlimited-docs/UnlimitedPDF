using System.Runtime.CompilerServices;

namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Locates the committed golden PDFs. Golden files are write-once: they were captured from the library before
/// the image feature changed it, and a test never overwrites an existing one.
/// </summary>
internal static class GoldenFiles
{
    #region Fields and Constants

    /// <summary>
    /// Set to <c>1</c> to capture golden files that do not exist yet.
    /// </summary>
    public const string CaptureVariable = "UNLIMITEDPDF_CAPTURE_GOLDEN";

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Writes <paramref name="bytes"/> as the golden file for <paramref name="name"/> when capture is requested and
    /// no golden file exists yet.
    /// </summary>
    public static void CaptureIfRequested(string name, byte[] bytes)
    {
        var path = PathFor(name);
        if (Environment.GetEnvironmentVariable(CaptureVariable) != "1" || File.Exists(path))
            return;

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    /// <summary>
    /// Returns the golden file path for <paramref name="name"/>. The library writes the platform line ending,
    /// so each line-ending flavor has its own file.
    /// </summary>
    public static string PathFor(string name)
    {
        var lineEnding = Environment.NewLine == "\r\n" ? "crlf" : "lf";
        return Path.Combine(ProjectDirectory(), "Golden", $"{name}.{lineEnding}.pdf");
    }

    #endregion

    #region Private Methods

    private static string ProjectDirectory([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, ".."));

    #endregion

    #endregion
}
