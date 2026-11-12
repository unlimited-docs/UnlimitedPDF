using System.Reflection;

namespace UnlimitedPDF.Demos.BlazorWasm.Services;

/// <summary>
/// The UnlimitedPDF release this site is built with, shown beside the logo and in the Home announcement (QF-26111209).
/// </summary>
public static class LibraryRelease
{
    #region Fields and Constants

    /// <summary>
    /// The repository's release notes.
    /// </summary>
    public const string NotesUrl = "https://github.com/unlimited-docs/UnlimitedPDF/releases";

    /// <summary>
    /// The library version, such as "1.0.0", or empty when the assembly carries none. It is read from the built
    /// library, so it always matches the package version.
    /// </summary>
    public static readonly string Version = ReadVersion(typeof(PdfDocument).Assembly);

    #endregion

    #region Methods

    #region Private Methods

    /// <summary>
    /// Reads an assembly's release version: its informational version (which keeps a pre-release label such as
    /// "-beta.1") without the "+commit" source revision the SDK appends, or else its three-part assembly version.
    /// </summary>
    /// <param name="assembly">The assembly to read.</param>
    /// <returns>The version, such as "1.0.0", or an empty string when the assembly carries none.</returns>
    private static string ReadVersion(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informational))
        {
            int revision = informational.IndexOf('+');
            return (revision < 0 ? informational : informational[..revision]).Trim();
        }

        return assembly.GetName().Version?.ToString(3) ?? string.Empty;
    }

    #endregion

    #endregion
}
