namespace UnlimitedPDF.Models.Internal.Attributes;

/// <summary>
/// Records the first package version that shipped the annotated public member. Documentation generation reads it by
/// reflection; it is internal because consumers never apply or read it themselves (PR-016).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Constructor, Inherited = false)]
internal sealed class AddedVersionAttribute : Attribute
{
    #region Constructors and Finalizers

    /// <summary>
    /// Initializes a new instance of the <see cref="AddedVersionAttribute"/> class.
    /// </summary>
    /// <param name="version">The first package version that shipped the member, for example "1.0.0".</param>
    public AddedVersionAttribute(string version) => Version = version;

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the first package version that shipped the member.
    /// </summary>
    public string Version { get; }

    #endregion
}
