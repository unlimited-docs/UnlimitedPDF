namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// One of a demo's variants, chosen on its page and kept in the address.
/// </summary>
/// <param name="Value">The value in the address, for example "quotation"; the first variant's address has none.</param>
/// <param name="Label">The label the choice shows, for example "Quotation".</param>
/// <param name="Address">The variant's path without a leading slash, for example "playground/documents/invoice?type=quotation".</param>
public sealed record DemoVariant(string Value, string Label, string Address);
