using System.Runtime.CompilerServices;

namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Locates committed fixture files in the test project's source folder.
/// </summary>
internal static class Fixtures
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the demo app's source Assets folder.
    /// </summary>
    public static string DemoAssets() => Path.GetFullPath(Path.Combine(ProjectDirectory(), "..", "UnlimitedPDF.Demo.ConsoleApp", "Assets"));

    /// <summary>
    /// Returns the path of a JPEG fixture, for example "rgb-baseline.jpg".
    /// </summary>
    public static string Jpeg(string name) => Path.Combine(ProjectDirectory(), "Fixtures", "Jpeg", name);

    #endregion

    #region Private Methods

    private static string ProjectDirectory([CallerFilePath] string sourcePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath)!, ".."));

    #endregion

    #endregion
}
