namespace UnlimitedPDF.Tests.Support;

/// <summary>
/// Sets the current culture for the duration of a <c>using</c> block and restores it afterwards.
/// </summary>
internal sealed class CultureScope : IDisposable
{
    #region Fields and Constants

    private readonly CultureInfo _culture;
    private readonly CultureInfo _uiCulture;

    #endregion

    #region Constructors and Finalizers

    public CultureScope(string name)
    {
        _culture = CultureInfo.CurrentCulture;
        _uiCulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
    }

    #endregion

    #region Methods

    #region Public Methods

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }

    #endregion

    #endregion
}
