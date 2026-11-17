namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// The Inventory report demo's inputs: how many warehouses and how many items each holds.
/// </summary>
public sealed class InventoryReportInput
{
    #region Fields and Constants

    /// <summary>
    /// The most items a warehouse lists.
    /// </summary>
    public const int MaxItems = 200;

    /// <summary>
    /// The most warehouses the report lists.
    /// </summary>
    public const int MaxWarehouses = 8;

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets how many items each warehouse lists, from 1 to <see cref="MaxItems"/>.
    /// </summary>
    public int ItemsPerWarehouse { get; set; } = 24;

    /// <summary>
    /// Gets or sets the report title.
    /// </summary>
    public string Title { get; set; } = "Stock on hand";

    /// <summary>
    /// Gets or sets how many warehouses the report lists, from 1 to <see cref="MaxWarehouses"/>.
    /// </summary>
    public int Warehouses { get; set; } = 3;

    #endregion
}
