namespace UnlimitedPDF.Models.Internal.Rendering;

/// <summary>
/// Thrown inside the renderer when a page holds content it does not draw, or reaches a work bound. The renderer
/// turns it into a page that is not drawn, with its reason.
/// </summary>
internal sealed class PageRenderException : Exception
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="reason">Why the page is not drawn, in plain words.</param>
    public PageRenderException(string reason)
        : base(reason)
    {
    }

    #endregion
}
