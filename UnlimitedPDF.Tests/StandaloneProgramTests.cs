using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Tests.Support;

namespace UnlimitedPDF.Tests;

/// <summary>
/// The complete console programs each demo's code tab shows (QF-26112501): every demo has one; it starts with the
/// steps to run it, names only files the site serves, and keeps every line to 100 characters so the code tab shows it
/// without sideways scrolling; and run as those steps say (in a folder holding the named files) it writes the PDFs it
/// names, each a valid PDF.
/// </summary>
[Collection(nameof(CurrentDirectoryCollection))]
public class StandaloneProgramTests
{
    #region Fields and Constants

    /// <summary>
    /// The widest line a program may have: the code tab shows about 110 characters at 1,280 px.
    /// </summary>
    public const int MaxLineLength = 100;

    private static readonly Regex _download = new(
        @"https://unlimitedpdf\.dev/(?<path>(?:images|fonts)/[\w.-]+)(?:\s+\(save it as (?<name>[\w.-]+)\))?",
        RegexOptions.CultureInvariant);

    private static readonly Regex _namedFile = new(@"""(\w+(?:Sample|Program)\.cs)""", RegexOptions.CultureInvariant);

    private static readonly Regex _pdfName = new(@"[\w{}*-]+\.pdf\b", RegexOptions.CultureInvariant);

    #endregion

    #region Methods

    #region Public Methods

    public static TheoryData<string> Programs()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(ProgramPath(""), "*Program.cs").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileName(path));
        }

        return data;
    }

    [Fact]
    public void Every_demo_has_its_program()
    {
        var missing = SampleCatalog.Pages
            .Where(demo => !File.Exists(ProgramPath(demo.ProgramFile)))
            .Select(demo => $"{demo.Route}: {demo.ProgramFile}")
            .ToList();
        Assert.Empty(missing);
    }

    [Fact]
    public void Every_program_a_page_names_exists()
    {
        // Pages that build their PDF more than one way, or share a builder, name the files in their markup.
        var named = Directory.EnumerateFiles(DemosPath("Pages"), "*.razor", SearchOption.AllDirectories)
            .SelectMany(page => _namedFile.Matches(File.ReadAllText(page)).Select(match => match.Groups[1].Value))
            .Select(SampleDemo.ProgramFileOf)
            .Distinct()
            .ToList();
        Assert.Contains("RightToLeftHtmlProgram.cs", named);
        Assert.All(named, file => Assert.True(File.Exists(ProgramPath(file)), $"{file} is missing."));
    }

    [Fact]
    public void The_html_program_takes_the_html_being_edited()
    {
        string program = Read("HtmlToPdfProgram.cs");
        string html = "<p>She said \"\"\"hi\"\"\"</p>\n\n<p>Two</p>";

        string shaped = ProgramText.WithHtml(program, html);

        // One more quote than the HTML's longest run keeps it one string; each line is indented like the quotes.
        Assert.Contains("        string html = \"\"\"\"\n", shaped, StringComparison.Ordinal);
        Assert.Contains(
            "            <p>She said \"\"\"hi\"\"\"</p>\n\n            <p>Two</p>\n            \"\"\"\";",
            shaped,
            StringComparison.Ordinal);
        Assert.DoesNotContain("<title>Invoice", shaped, StringComparison.Ordinal);
        Assert.Equal(program.TrimEnd('\n'), ProgramText.WithHtml(program, ProgramHtml(program)).TrimEnd('\n'));
        Assert.Equal("no html here", ProgramText.WithHtml("no html here", html));
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public void A_program_starts_with_the_steps_to_run_it(string file)
    {
        string text = Read(file);
        string example = ExampleName(file);

        Assert.StartsWith("// ", text, StringComparison.Ordinal);
        Assert.Contains("A complete console program", Header(text), StringComparison.Ordinal);
        Assert.Contains($"dotnet new console -n {example}", Header(text), StringComparison.Ordinal);
        Assert.Contains($"cd {example}", Header(text), StringComparison.Ordinal);
        Assert.Contains("dotnet add package UnlimitedPDF", Header(text), StringComparison.Ordinal);
        Assert.Contains("Replace everything in Program.cs with this code.", Header(text), StringComparison.Ordinal);
        Assert.Contains("dotnet run", Header(text), StringComparison.Ordinal);
        Assert.Contains($"namespace {example};", text, StringComparison.Ordinal);
        Assert.Matches(@"public static (async Task|void) Main\(\)", text);
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public void A_program_fits_the_code_tab_without_sideways_scrolling(string file)
    {
        var lines = Read(file).Split('\n');
        var tooLong = lines
            .Select((line, index) => (Number: index + 1, Line: line.TrimEnd('\r')))
            .Where(entry => entry.Line.Length > MaxLineLength)
            .Select(entry => $"line {entry.Number} ({entry.Line.Length}): {entry.Line}")
            .ToList();
        Assert.Empty(tooLong);
        Assert.DoesNotContain('\t', Read(file));
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public void A_program_names_only_files_the_site_serves(string file)
    {
        foreach (Match match in _download.Matches(Header(Read(file))))
        {
            Assert.True(File.Exists(WwwrootPath(match.Groups["path"].Value)), $"{file}: {match.Value} is not in wwwroot.");
        }
    }

    [Theory]
    [MemberData(nameof(Programs))]
    public async Task A_program_run_as_its_steps_say_writes_valid_pdfs(string file)
    {
        string text = Read(file);
        string folder = Directory.CreateTempSubdirectory("unlimitedpdf-program-").FullName;
        string previous = Environment.CurrentDirectory;
        try
        {
            // Step "Save these files next to Program.cs": the folder holds every file the steps name.
            foreach (Match match in _download.Matches(Header(text)))
            {
                string path = match.Groups["path"].Value;
                string name = match.Groups["name"].Success ? match.Groups["name"].Value : Path.GetFileName(path);
                File.Copy(WwwrootPath(path), Path.Combine(folder, name), overwrite: true);
            }

            Environment.CurrentDirectory = folder;
            var main = MainOf(file);
            var result = main.Invoke(null, null);
            if (result is Task task)
            {
                await task;
            }
        }
        finally
        {
            Environment.CurrentDirectory = previous;
        }

        var written = Directory.GetFiles(folder, "*.pdf", SearchOption.AllDirectories);
        Assert.NotEmpty(written);
        foreach (string pdf in written)
        {
            var errors = PdfStructureChecker.Check(File.ReadAllBytes(pdf));
            Assert.True(errors.Count == 0, $"{file}: {Path.GetFileName(pdf)}: {string.Join("; ", errors)}");
        }

        // Every PDF the steps tell the developer to open is there.
        foreach (Match match in _pdfName.Matches(OpenLine(text)))
        {
            string pattern = match.Value.Replace("{n}", "*", StringComparison.Ordinal);
            Assert.True(Directory.GetFiles(folder, pattern, SearchOption.AllDirectories).Length > 0, $"{file}: {match.Value} was not written.");
        }

        Directory.Delete(folder, recursive: true);
    }

    #endregion

    #region Private Methods

    private static string DemosPath(string relative, [CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "UnlimitedPDF.Demos.BlazorWasm", relative));

    // The HTML between the program's raw string quotes, without their indent.
    private static string ProgramHtml(string program)
    {
        var lines = program.Split('\n');
        int open = Array.IndexOf(lines, ProgramText.HtmlOpening);
        int close = Array.IndexOf(lines, "            \"\"\";", open + 1);
        return string.Join('\n', lines[(open + 1)..close].Select(line => line.Length >= 12 ? line[12..] : line));
    }

    // "InvoiceProgram.cs" -> "InvoiceExample": the console project's name and the program's namespace.
    private static string ExampleName(string file) => file[..^"Program.cs".Length] + "Example";

    // The comment block before the first line of code.
    private static string Header(string text) =>
        string.Join('\n', text.Split('\n').TakeWhile(line => line.StartsWith("//", StringComparison.Ordinal)));

    private static MethodInfo MainOf(string file)
    {
        string typeName = $"{ExampleName(file)}.Program";
        var type = typeof(StandaloneProgramTests).Assembly.GetType(typeName)
            ?? throw new InvalidOperationException($"{typeName} is not compiled into the tests.");
        return type.GetMethod("Main", BindingFlags.Public | BindingFlags.Static, Type.EmptyTypes)
            ?? throw new InvalidOperationException($"{typeName} has no public static Main().");
    }

    // The header's last sentence, "Then open ... in the ... folder.", which may wrap onto following comment lines.
    private static string OpenLine(string text)
    {
        string header = Header(text);
        int start = header.LastIndexOf("Then open", StringComparison.Ordinal);
        Assert.True(start >= 0, "The steps end with \"Then open ...\".");
        return header[start..];
    }

    private static string ProgramPath(string file, [CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "UnlimitedPDF.Demos.BlazorWasm", "Programs", file));

    private static string Read(string file) => File.ReadAllText(ProgramPath(file)).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string WwwrootPath(string relative, [CallerFilePath] string source = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(source)!, "..", "UnlimitedPDF.Demos.BlazorWasm", "wwwroot", relative));

    #endregion

    #endregion
}

/// <summary>
/// Tests that change the process's current folder, as running a console program from its project folder does; they
/// run alone.
/// </summary>
[CollectionDefinition(nameof(CurrentDirectoryCollection), DisableParallelization = true)]
public class CurrentDirectoryCollection
{
}
