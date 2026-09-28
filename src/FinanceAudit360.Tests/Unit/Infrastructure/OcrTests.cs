using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Infrastructure.Options;
using FinanceAudit360.Infrastructure.Pdf;
using FinanceAudit360.Infrastructure.Pdf.Ocr;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ITextPdfDocument = iText.Kernel.Pdf.PdfDocument;
using ITextPdfWriter = iText.Kernel.Pdf.PdfWriter;

namespace FinanceAudit360.Tests.Unit.Infrastructure;

public class OcrTests
{
    [Fact]
    public void InvertShadedBands_TurnsLightCaptionsOnGreyIntoDarkTextOnWhite()
    {
        const int width = 100, height = 40;
        var gray = Enumerable.Repeat((byte)255, width * height).ToArray();

        // Grey header bar on rows 10..29, columns 10..89, with a white "glyph" and a black "glyph" inside it.
        for (var y = 10; y < 30; y++)
        {
            for (var x = 10; x < 90; x++)
            {
                gray[y * width + x] = 165;
            }
        }

        for (var y = 15; y < 25; y++)
        {
            gray[y * width + 20] = 255;
            gray[y * width + 60] = 0;
        }

        OcrImage.InvertShadedBands(gray, width, height, 1d);

        Assert.Equal(0, gray[18 * width + 20]);
        Assert.Equal(0, gray[18 * width + 60]);
        Assert.Equal(255, gray[18 * width + 40]);
        Assert.Equal(255, gray[18 * width + 5]);
        Assert.Equal(255, gray[5 * width + 40]);
    }

    [Fact]
    public void InvertShadedBands_LeavesThinRuleLinesAlone()
    {
        const int width = 50, height = 10;
        var gray = Enumerable.Repeat((byte)255, width * height).ToArray();
        for (var x = 0; x < width; x++)
        {
            gray[5 * width + x] = 200;
        }

        OcrImage.InvertShadedBands(gray, width, height, 3d);

        Assert.Equal(200, gray[5 * width + 10]);
    }

    [Fact]
    public void ToText_GroupsWordsIntoPrintedRowsInReadingOrder()
    {
        PdfWord[] words =
        [
            new("379.00", 400, 430, 101, 109),
            new("27-06-2025", 40, 90, 100, 108),
            new("UPI/SUMIT", 160, 210, 99, 107),
            new("fr/FEDERAL", 160, 215, 111, 119)
        ];

        Assert.Equal("27-06-2025 UPI/SUMIT 379.00\nfr/FEDERAL", OcrLayout.ToText(words));
    }

    [Fact]
    public async Task ExtractAsync_ReadsPagesWithoutATextLayerWithOcr()
    {
        var engine = new FakeOcrEngine();
        var extractor = new PdfTextExtractor(
            NullLogger<PdfTextExtractor>.Instance,
            engine,
            Options.Create(new OcrOptions()));

        var document = await extractor.ExtractAsync(new MemoryStream(BlankPdf(2)), null, TestContext.Current.CancellationToken);

        Assert.Equal([0, 1], engine.RequestedPages);
        Assert.Equal(2, document.PageCount);
        Assert.Equal("OCR", document.Metadata[PdfTextExtractor.TextSourceKey]);
        Assert.Equal("1,2", document.Metadata[PdfTextExtractor.OcrPagesKey]);
        Assert.Contains("ICICI Bank page 1", document.FullText, StringComparison.Ordinal);
        Assert.Single(document.PageWords[1]);
    }

    [Fact]
    public async Task ExtractAsync_KeepsScannedPagesUntouchedWhenOcrIsUnavailable()
    {
        var engine = new FakeOcrEngine { Available = false };
        var extractor = new PdfTextExtractor(NullLogger<PdfTextExtractor>.Instance, engine);

        var document = await extractor.ExtractAsync(new MemoryStream(BlankPdf(1)), null, TestContext.Current.CancellationToken);

        Assert.Empty(engine.RequestedPages);
        Assert.False(document.Metadata.ContainsKey(PdfTextExtractor.TextSourceKey));
    }

    private static byte[] BlankPdf(int pages)
    {
        using var output = new MemoryStream();
        using (var writer = new ITextPdfWriter(output))
        using (var pdf = new ITextPdfDocument(writer))
        {
            for (var i = 0; i < pages; i++)
            {
                pdf.AddNewPage();
            }
        }

        return output.ToArray();
    }

    private sealed class FakeOcrEngine : IPdfOcrEngine
    {
        public bool Available { get; init; } = true;

        public List<int> RequestedPages { get; } = [];

        public bool IsAvailable => Available;

        public IReadOnlyDictionary<int, OcrPage> Recognize(
            byte[] pdf,
            string? password,
            IReadOnlyCollection<int> pageIndexes,
            CancellationToken cancellationToken = default)
        {
            RequestedPages.AddRange(pageIndexes);
            return pageIndexes.ToDictionary(
                i => i,
                i => new OcrPage($"ICICI Bank page {i + 1}", [new PdfWord("ICICI", 10, 40, 10, 18)], 0.9f));
        }
    }
}
