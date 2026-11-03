namespace UnlimitedPDF;

/// <summary>
/// Records the first package version that shipped the annotated member.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false)]
public sealed class AddedVersionAttribute : Attribute
{
    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a new instance of the <see cref="AddedVersionAttribute"/> class.
    /// </summary>
    /// <param name="version">The first package version that shipped the member, for example "0.1.0".</param>
    [AddedVersion("0.1.0")]
    [Description("Initializes a new instance of the AddedVersionAttribute class.")]
    public AddedVersionAttribute(string version) => Version = version;

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the first package version that shipped the member.
    /// </summary>
    public string Version { get; }

    #endregion
}
