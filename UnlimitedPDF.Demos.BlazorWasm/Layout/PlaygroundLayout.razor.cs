namespace UnlimitedPDF.Demos.BlazorWasm.Layout;

/// <summary>
/// The playground layout: a sidebar grouped by capability beside the current demo. On small screens the sidebar
/// collapses behind a menu button.
/// </summary>
public partial class PlaygroundLayout
{
    #region Fields and Constants

    private bool _menuOpen;

    #endregion

    #region Methods

    #region Private Methods

    private void CloseMenu() => _menuOpen = false;

    private void ToggleMenu() => _menuOpen = !_menuOpen;

    #endregion

    #endregion
}
