using UnlimitedPDF;
using UnlimitedPDF.Demo.ConsoleApp;

Console.WriteLine("Start: Unlimited PDF");

// The output path can be passed as the first argument; by default the files are written to an "output" folder in the
// working folder, which works on every machine and operating system.
var pdfFilePath = args.Length > 0 ? args[0] : Path.Combine(Environment.CurrentDirectory, "output", "unlimited-pdf-sample.pdf");
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(pdfFilePath))!);

// The demo images are copied next to the app at build time.
var assetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");

var pdfDocument = DemoDocument.Build(assetsDirectory);
pdfDocument.Save(pdfFilePath);

// The merge demo is written next to the sample, for example unlimited-pdf-sample-merged.pdf.
var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(pdfFilePath)) ?? AppContext.BaseDirectory;
var mergedFilePath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(pdfFilePath) + "-merged.pdf");
MergeDemo.Build(assetsDirectory).Save(mergedFilePath);

// The split demo writes one file per page of the sample into a folder next to it, for example
// unlimited-pdf-sample-split\unlimited-pdf-sample-1.pdf.
var splitFolder = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(pdfFilePath) + "-split");
var parts = PdfOperations.Split(PdfFile.FromFile(pdfFilePath), 1, splitFolder);
Console.WriteLine($"Split into {parts.Count} files in {splitFolder}");

// The HTML demo converts the sample invoice HTML next to the sample, for example unlimited-pdf-sample-html.pdf.
var htmlFilePath = Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(pdfFilePath) + "-html.pdf");
var html = HtmlDemo.Write(assetsDirectory, htmlFilePath);
Console.WriteLine($"Converted {HtmlDemo.FileName} to {htmlFilePath} with {html.Warnings.Count} warnings");

Console.WriteLine($"End: Unlimited PDF. The files are in {outputDirectory}");

// Keeps a console window opened by double-click on screen; a redirected or scripted run ends at once.
if (!Console.IsInputRedirected) Console.ReadLine();
