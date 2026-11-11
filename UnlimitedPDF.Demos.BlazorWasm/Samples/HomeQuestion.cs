namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// One "How do I...?" question on the home page and the demo that answers it.
/// </summary>
/// <param name="Text">The question without "How do I", for example "Repeat a table header on every page".</param>
/// <param name="Demo">The demo that answers it.</param>
/// <param name="Route">The page the question opens when it is not the demo's own page, for example one HTML scenario, or null.</param>
public sealed record HomeQuestion(string Text, SampleDemo Demo, string? Route = null)
{
    #region Properties, Indexers

    /// <summary>
    /// Gets the page the question opens.
    /// </summary>
    public string Href => Route ?? Demo.Route;

    #endregion
}
