namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// One playground demo: what the sidebar, the home page, and the demo page show about it.
/// </summary>
/// <param name="Title">The demo name, for example "Invoice".</param>
/// <param name="Description">One sentence saying what the demo shows.</param>
/// <param name="Route">The demo's URL path without a leading slash, for example "playground/documents/invoice".</param>
/// <param name="SourceFile">The sample builder file shown in the code tab, for example "InvoiceSample.cs".</param>
/// <param name="FileName">The file name a downloaded PDF gets, for example "invoice.pdf".</param>
/// <param name="Icon">The icon name drawn by the Icon component.</param>
public sealed record SampleDemo(string Title, string Description, string Route, string SourceFile, string FileName, string Icon);
