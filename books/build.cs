#!/usr/bin/env dotnet
// Renders books/src/<slug>/book.md into the sample documents used by this repo:
//   books/pdf/<slug>.pdf            born-digital PDF (real text, embedded PNG figures)
//   books/pdf/<slug>-scanned.pdf    image-only PDF (every page is a JPEG, no text layer)
//   books/pages/<slug>-hero.pdf/png the single "hero" page used by the comparison samples
// Maintainer tool. Needs Microsoft Edge or Google Chrome (set BROWSER to override).
// You don't need to run it to try the samples; the outputs are committed.
#:package Markdig@0.42.0
#:package PDFtoImage@5.1.1
#:package PdfPig@0.1.11
#:property PublishAot=false
#:property NoWarn=$(NoWarn);CA1416

using System.Diagnostics;
using System.Text.RegularExpressions;
using Markdig;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

string root = Path.GetFullPath(Path.Combine(AppContext.GetData("EntryPointFileDirectoryPath") as string ?? ".", "."));
string srcRoot = Path.Combine(root, "src");
string pdfDir = Directory.CreateDirectory(Path.Combine(root, "pdf")).FullName;
string pagesDir = Directory.CreateDirectory(Path.Combine(root, "pages")).FullName;
string work = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "dx-books")).FullName;
string browser = FindBrowser();
string profile = Directory.CreateDirectory(Path.Combine(work, "profile")).FullName;

// The page that every provider is compared on. Keyed by book slug.
var heroHeadings = new Dictionary<string, string>
{
    ["tokens-explained"] = "Tokenizers in .NET",
    ["tensors-in-ten-pages"] = "Matrix multiply",
    ["agents-propose-tools-decide"] = "Gates and evidence",
};

var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
string css = File.ReadAllText(Path.Combine(root, "book.css"));

foreach (string bookDir in Directory.GetDirectories(srcRoot).Order())
{
    string slug = Path.GetFileName(bookDir);
    Console.WriteLine($"== {slug}");
    var (meta, body) = ReadFrontMatter(File.ReadAllText(Path.Combine(bookDir, "book.md")));

    // Rasterize SVG figures so the PDF carries real images, the way most real documents do.
    string figOut = Directory.CreateDirectory(Path.Combine(work, slug, "figures")).FullName;
    foreach (string svg in Directory.GetFiles(Path.Combine(bookDir, "figures"), "*.svg"))
    {
        string png = Path.Combine(figOut, Path.ChangeExtension(Path.GetFileName(svg), ".png"));
        var (w, h) = SvgSize(File.ReadAllText(svg));
        Run(browser, $"--headless --disable-gpu --user-data-dir=\"{profile}\" --hide-scrollbars --force-device-scale-factor=2 " +
            $"--window-size={w},{h} --screenshot=\"{png}\" \"{new Uri(svg).AbsoluteUri}\"");
    }

    body = Regex.Replace(body, @"\(figures/([^)]+)\.svg\)", "(figures/$1.png)");
    string html = $"""
        <!doctype html><html lang="en"><head><meta charset="utf-8">
        <title>{meta["title"]}</title>
        <style>{css.Replace("{{HEADER}}", meta["header"])}</style></head>
        <body>{Markdown.ToHtml(body, pipeline)}</body></html>
        """;
    string htmlPath = Path.Combine(work, slug, "book.html");
    File.WriteAllText(htmlPath, html);

    string pdfPath = Path.Combine(pdfDir, $"{slug}.pdf");
    Run(browser, $"--headless --disable-gpu --user-data-dir=\"{profile}\" --no-pdf-header-footer " +
        $"--print-to-pdf=\"{pdfPath}\" \"{new Uri(htmlPath).AbsoluteUri}\"");

    byte[] pdf = File.ReadAllBytes(pdfPath);
    using var document = PdfDocument.Open(pdf);
    int pages = document.NumberOfPages;
    Console.WriteLine($"   {pages} pages, {pdf.Length / 1024} KB");

    // Hero page: a one-page PDF plus a PNG of the same page, for image-only providers.
    if (heroHeadings.TryGetValue(slug, out string? heading))
    {
        int hero = document.GetPages().Skip(1).First(p => p.Text.Contains(heading)).Number;
        var single = new PdfDocumentBuilder();
        single.AddPage(document, hero);
        File.WriteAllBytes(Path.Combine(pagesDir, $"{slug}-hero.pdf"), single.Build());
        using var heroPng = File.Create(Path.Combine(pagesDir, $"{slug}-hero.png"));
        PDFtoImage.Conversion.SavePng(heroPng, pdf, hero - 1, options: new(Dpi: 150));
        Console.WriteLine($"   hero page {hero}");
    }

    // Scanned copy: each page becomes a JPEG and nothing else, like a phone or copier scan.
    var scanned = new PdfDocumentBuilder();
    for (int i = 0; i < pages; i++)
    {
        var size = document.GetPage(i + 1);
        var page = scanned.AddPage(size.Width, size.Height);
        using var bitmap = PDFtoImage.Conversion.ToImage(pdf, i, options: new(Dpi: 150));
        using var jpg = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 70);
        page.AddJpeg(jpg.ToArray(), new PdfRectangle(0, 0, size.Width, size.Height));
    }
    string scannedPath = Path.Combine(pdfDir, $"{slug}-scanned.pdf");
    File.WriteAllBytes(scannedPath, scanned.Build());
    Console.WriteLine($"   scanned copy {new FileInfo(scannedPath).Length / 1024} KB");
}

static (Dictionary<string, string> Meta, string Body) ReadFrontMatter(string text)
{
    var m = Regex.Match(text, @"\A---\r?\n(.*?)\r?\n---\r?\n", RegexOptions.Singleline);
    var meta = m.Groups[1].Value.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select(l => l.Split(':', 2))
        .ToDictionary(p => p[0].Trim(), p => p[1].Trim());
    return (meta, text[m.Length..]);
}

static (int W, int H) SvgSize(string svg)
{
    var vb = Regex.Match(svg, @"viewBox=""[\d.]+\s+[\d.]+\s+([\d.]+)\s+([\d.]+)""");
    return vb.Success
        ? ((int)double.Parse(vb.Groups[1].Value), (int)double.Parse(vb.Groups[2].Value))
        : (800, 450);
}

static string FindBrowser()
{
    string[] candidates =
    [
        Environment.GetEnvironmentVariable("BROWSER") ?? "",
        @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
        @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
        @"C:\Program Files\Google\Chrome\Application\chrome.exe",
        "/usr/bin/microsoft-edge", "/usr/bin/google-chrome", "/usr/bin/chromium",
    ];
    return candidates.FirstOrDefault(File.Exists)
        ?? throw new InvalidOperationException("Install Edge or Chrome, or set BROWSER.");
}

static void Run(string file, string args)
{
    var psi = new ProcessStartInfo(file, args) { RedirectStandardError = true, RedirectStandardOutput = true };
    using var p = Process.Start(psi)!;
    p.StandardOutput.ReadToEnd();
    p.StandardError.ReadToEnd();
    p.WaitForExit();
    if (p.ExitCode != 0) throw new InvalidOperationException($"{Path.GetFileName(file)} exited {p.ExitCode}");
}
