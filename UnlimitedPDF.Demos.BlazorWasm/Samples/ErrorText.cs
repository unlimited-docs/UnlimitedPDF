using System.Text.RegularExpressions;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Turns an exception from a demo into the sentence the demo's error box shows. The WebAssembly build trims the
/// framework's message texts to their resource keys, so an argument error can read "Arg_ParamName_Name, fontSize"; that
/// tail is replaced by the value's name in plain words (WCAG 3.3.1, 3.3.3).
/// </summary>
public static partial class ErrorText
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the plain sentence for an exception.
    /// </summary>
    /// <param name="exception">The exception a demo's builder threw.</param>
    /// <returns>The message without resource keys or parameter details, naming the value that is wrong when known.</returns>
    public static string Plain(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        string message = exception.Message;

        // "(Parameter 'x')" from a full runtime, or "Arg_ParamName_Name, x ..." from a trimmed one.
        int cut = message.IndexOf(" Arg_", StringComparison.Ordinal);
        if (cut < 0) cut = message.IndexOf(" (Parameter '", StringComparison.Ordinal);
        if (cut >= 0) message = message[..cut];
        message = ResourceKey().Replace(message, string.Empty).Trim();

        if (exception is ArgumentException { ParamName: { Length: > 0 } name })
        {
            message = $"{Words(name)}: {char.ToLowerInvariant(message[0])}{message[1..]}";
        }

        if (message.Length == 0) message = "A value isn't valid.";
        return message.EndsWith('.') ? message : message + ".";
    }

    #endregion

    #region Private Methods

    [GeneratedRegex(@"\b[A-Z][A-Za-z]+_[A-Za-z_]+\b,?")]
    private static partial Regex ResourceKey();

    // "fontSize" becomes "Font size", "pagesPerPart" becomes "Pages per part".
    private static string Words(string name)
    {
        string spaced = Regex.Replace(name, "(?<!^)([A-Z])", " $1").ToLowerInvariant();
        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }

    #endregion

    #endregion
}
