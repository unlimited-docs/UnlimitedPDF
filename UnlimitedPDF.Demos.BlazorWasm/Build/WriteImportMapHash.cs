using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace UnlimitedPDF.Demos.BlazorWasm.Build
{
    /// <summary>
    /// The hash of the site's one inline script, the import map the build writes into index.html, for the
    /// Content-Security-Policy in staticwebapp.config.json (26111701 T-14). The publish step and the tests share this code.
    /// </summary>
    public static class ImportMapHash
    {
        #region Fields and Constants

        /// <summary>
        /// The text in the config's policy that the hash replaces.
        /// </summary>
        public const string Placeholder = "IMPORTMAP-HASH";

        #endregion

        #region Methods

        #region Public Methods

        /// <summary>
        /// Writes the hash into the config template.
        /// </summary>
        /// <param name="template">The config as it is in the source folder, with <see cref="Placeholder"/>.</param>
        /// <param name="hash">The hash from <see cref="Compute"/>.</param>
        /// <returns>The config with the hash in place of the placeholder.</returns>
        /// <exception cref="InvalidOperationException">The template has no placeholder.</exception>
        public static string Apply(string template, string hash)
        {
            if (!template.Contains(Placeholder)) throw new InvalidOperationException($"The config has no {Placeholder} to replace.");
            return template.Replace(Placeholder, hash);
        }

        /// <summary>
        /// Works out the base64 SHA-256 of the import map, as the browser does: over the script's text as parsed, with
        /// every line break as one line feed.
        /// </summary>
        /// <param name="html">The published index.html.</param>
        /// <returns>The hash, without the "sha256-" prefix.</returns>
        /// <exception cref="InvalidOperationException">The page has no import map, or another inline script.</exception>
        public static string Compute(string html)
        {
            var inline = Regex.Matches(html, "<script(?<attributes>[^>]*)>(?<text>.*?)</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase)
                .Cast<Match>()
                .Where(m => !Regex.IsMatch(m.Groups["attributes"].Value, @"\bsrc\s*=", RegexOptions.IgnoreCase))
                .ToList();
            if (inline.Count != 1 || !Regex.IsMatch(inline[0].Groups["attributes"].Value, "type\\s*=\\s*\"importmap\"", RegexOptions.IgnoreCase))
                throw new InvalidOperationException($"index.html must have exactly one inline script, the import map; it has {inline.Count} inline scripts.");

            string text = inline[0].Groups["text"].Value.Replace("\r\n", "\n").Replace('\r', '\n');
            using (var sha = SHA256.Create())
            {
                return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
            }
        }

        #endregion

        #endregion
    }
}

#if !IMPORTMAP_HASH_ONLY
/// <summary>
/// The publish step: reads the published index.html and the source config template, and writes the config with the
/// hash into the publish folder, replacing what an earlier publish left there. The precompressed copies of the config
/// were made before the hash was known, so they are removed and the host serves the config itself.
/// </summary>
public class WriteImportMapHash : Microsoft.Build.Utilities.Task
{
    #region Methods

    #region Public Methods

    /// <inheritdoc />
    public override bool Execute()
    {
        try
        {
            string hash = UnlimitedPDF.Demos.BlazorWasm.Build.ImportMapHash.Compute(File.ReadAllText(Path.Combine(Folder, "index.html")));
            string config = UnlimitedPDF.Demos.BlazorWasm.Build.ImportMapHash.Apply(File.ReadAllText(Template), hash);
            string target = Path.Combine(Folder, "staticwebapp.config.json");
            File.WriteAllText(target, config);
            foreach (string stale in new[] { target + ".gz", target + ".br" }.Where(File.Exists)) File.Delete(stale);
            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            Log.LogError("WriteImportMapHash: " + exception.Message);
            return false;
        }
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the published wwwroot folder.
    /// </summary>
    [Microsoft.Build.Framework.Required] public string Folder { get; set; } = "";

    /// <summary>
    /// Gets or sets the config template in the source folder.
    /// </summary>
    [Microsoft.Build.Framework.Required] public string Template { get; set; } = "";

    #endregion
}
#endif
