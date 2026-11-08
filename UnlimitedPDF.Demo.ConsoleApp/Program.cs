using UnlimitedPDF.Demo.ConsoleApp;

Console.WriteLine("Start: Unlimited PDF");

// The output path can be passed as the first argument.
var pdfFilePath = args.Length > 0 ? args[0] : @"D:\\unlimited-pdf-sample.pdf";

// The demo images are copied next to the app at build time.
var assetsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets");

var pdfDocument = DemoDocument.Build(assetsDirectory);
pdfDocument.Save(pdfFilePath);

// The merge demo is written next to the sample, for example unlimited-pdf-sample-merged.pdf.
var mergedFilePath = Path.Combine(
    Path.GetDirectoryName(Path.GetFullPath(pdfFilePath)) ?? AppContext.BaseDirectory,
    Path.GetFileNameWithoutExtension(pdfFilePath) + "-merged.pdf");
MergeDemo.Build(assetsDirectory).Save(mergedFilePath);

Console.WriteLine("End: Unlimited PDF");
Console.ReadLine();
