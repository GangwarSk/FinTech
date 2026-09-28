using System.Globalization;
using System.Text;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Infrastructure.Options;
using FinanceAudit360.Infrastructure.Pdf.Ocr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;
using ITextPdfReader = iText.Kernel.Pdf.PdfReader;
using ITextPdfDocument = iText.Kernel.Pdf.PdfDocument;
using ITextReaderProperties = iText.Kernel.Pdf.ReaderProperties;
using ITextExtractor = iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor;
using ITextLocationStrategy = iText.Kernel.Pdf.Canvas.Parser.Listener.LocationTextExtractionStrategy;

namespace FinanceAudit360.Infrastructure.Pdf;

/// <summary>
/// Extracts statement text with PdfPig first (best layout fidelity) and falls back to iText for
/// documents PdfPig cannot open. Both paths accept a user password for protected statements.
/// </summary>
public sealed class PdfTextExtractor(
    ILogger<PdfTextExtractor> logger,
    IPdfOcrEngine? ocrEngine = null,
    IOptions<OcrOptions>? ocrOptions = null) : IPdfTextExtractor
{
    public const string TextSourceKey = "TextSource";
    public const string OcrPagesKey = "OcrPages";
    public const string OcrConfidenceKey = "OcrConfidence";

    private readonly int _minWordsPerPage = ocrOptions?.Value.MinWordsPerPage ?? 5;

    public async Task<bool> IsPasswordProtectedAsync(Stream pdfStream, CancellationToken cancellationToken = default)
    {
        var buffer = await ToMemoryAsync(pdfStream, cancellationToken);

        try
        {
            using var document = PdfDocument.Open(buffer, new ParsingOptions { UseLenientParsing = true });
            _ = document.NumberOfPages;
            return false;
        }
        catch (PdfDocumentEncryptedException)
        {
            return true;
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "PdfPig probe failed; retrying with iText.");
        }

        buffer.Position = 0;

        try
        {
            using var reader = new ITextPdfReader(buffer);
            using var document = new ITextPdfDocument(reader);
            return reader.IsEncrypted();
        }
        catch (iText.Kernel.Exceptions.BadPasswordException)
        {
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to determine whether the PDF is password protected.");
            return false;
        }
    }

    public async Task<PdfDocumentText> ExtractAsync(
        Stream pdfStream,
        string? password,
        CancellationToken cancellationToken = default)
    {
        var buffer = await ToMemoryAsync(pdfStream, cancellationToken);
        var document = ExtractTextLayer(buffer, password);
        return ApplyOcr(document, buffer, password, cancellationToken);
    }

    private PdfDocumentText ExtractTextLayer(MemoryStream buffer, string? password)
    {
        try
        {
            var direct = ExtractWithPdfPig(buffer, password);
            if (HasLayout(direct))
            {
                return direct;
            }

            logger.LogWarning("PdfPig returned no word positions; retrying on an iText-rewritten copy.");
            return TryPdfPigOnRewrittenCopy(buffer, password) ?? direct;
        }
        catch (PdfDocumentEncryptedException)
        {
            // Wrong or missing password - let the iText path confirm before reporting back.
            buffer.Position = 0;
            return TryPdfPigOnRewrittenCopy(buffer, password)
                   ?? ExtractWithITextOrThrow(buffer, password, string.IsNullOrEmpty(password));
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "PdfPig extraction failed; retrying on an iText-rewritten copy.");
            return TryPdfPigOnRewrittenCopy(buffer, password) ?? ExtractWithITextOrThrow(Rewind(buffer), password, false);
        }
    }

    /// <summary>
    /// Scanned statements are page images with no text layer. Those pages are rendered and read with OCR,
    /// and the recognised words replace the empty page so the normal bank parsers can run on them.
    /// </summary>
    private PdfDocumentText ApplyOcr(
        PdfDocumentText document,
        MemoryStream buffer,
        string? password,
        CancellationToken cancellationToken)
    {
        var scanned = Enumerable.Range(0, document.PageCount).Where(i => IsScannedPage(document, i)).ToList();
        if (scanned.Count == 0 || ocrEngine is null)
        {
            return document;
        }

        if (!ocrEngine.IsAvailable)
        {
            logger.LogWarning("{Count} page(s) have no text layer and OCR is unavailable.", scanned.Count);
            return document;
        }

        IReadOnlyDictionary<int, OcrPage> recognised;
        try
        {
            recognised = ocrEngine.Recognize(buffer.ToArray(), password, scanned, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "OCR failed; continuing with the text layer only.");
            return document;
        }

        if (recognised.Count == 0)
        {
            return document;
        }

        var pages = new List<string>(document.PageCount);
        var pageWords = new List<IReadOnlyList<PdfWord>>(document.PageCount);
        for (var i = 0; i < document.PageCount; i++)
        {
            if (recognised.TryGetValue(i, out var page))
            {
                pages.Add(page.Text);
                pageWords.Add(page.Words);
            }
            else
            {
                pages.Add(i < document.Pages.Count ? document.Pages[i] : string.Empty);
                pageWords.Add(i < document.PageWords.Count ? document.PageWords[i] : []);
            }
        }

        var metadata = new Dictionary<string, string>(document.Metadata)
        {
            [TextSourceKey] = recognised.Count == document.PageCount ? "OCR" : "Mixed",
            [OcrPagesKey] = string.Join(',', recognised.Keys.Order().Select(i => i + 1)),
            [OcrConfidenceKey] = recognised.Values.Average(p => p.MeanConfidence).ToString("0.00", CultureInfo.InvariantCulture)
        };

        return document with
        {
            Pages = pages,
            FullText = string.Join("\n", pages),
            Metadata = metadata,
            PageWords = pageWords
        };
    }

    private bool IsScannedPage(PdfDocumentText document, int index)
    {
        var words = index < document.PageWords.Count ? document.PageWords[index].Count : 0;
        var text = index < document.Pages.Count ? document.Pages[index] : string.Empty;
        return words < _minWordsPerPage && text.Count(c => !char.IsWhiteSpace(c)) < _minWordsPerPage * 4;
    }

    private static bool HasLayout(PdfDocumentText document) =>
        string.IsNullOrWhiteSpace(document.FullText) || document.PageWords.Any(words => words.Count > 0);

    /// <summary>
    /// iText opens encryption and structure variants that PdfPig rejects. Writing an unencrypted copy and
    /// reading that with PdfPig keeps the word positions the table parsers depend on; a plain iText text
    /// dump has none, which silently drops wrapped rows and deposit/withdrawal columns.
    /// </summary>
    private PdfDocumentText? TryPdfPigOnRewrittenCopy(MemoryStream buffer, string? password)
    {
        try
        {
            var decrypted = RewriteWithIText(Rewind(buffer), password);
            if (decrypted is null)
            {
                return null;
            }

            var result = ExtractWithPdfPig(decrypted, null);
            return result with { WasPasswordProtected = !string.IsNullOrEmpty(password) };
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "PdfPig could not read the iText-rewritten copy either.");
            return null;
        }
        finally
        {
            buffer.Position = 0;
        }
    }

    private static MemoryStream? RewriteWithIText(MemoryStream source, string? password)
    {
        var properties = new ITextReaderProperties();
        if (!string.IsNullOrEmpty(password))
        {
            properties.SetPassword(Encoding.UTF8.GetBytes(password));
        }

        var output = new MemoryStream();
        try
        {
            using var reader = new ITextPdfReader(source, properties);
            reader.SetUnethicalReading(true);
            reader.SetCloseStream(false);

            using var writer = new iText.Kernel.Pdf.PdfWriter(output);
            writer.SetCloseStream(false);

            using var document = new ITextPdfDocument(reader, writer);
        }
        catch (iText.Kernel.Exceptions.BadPasswordException)
        {
            return null;
        }

        output.Position = 0;
        return output;
    }

    private static MemoryStream Rewind(MemoryStream stream)
    {
        stream.Position = 0;
        return stream;
    }

    private static PdfDocumentText ExtractWithPdfPig(MemoryStream buffer, string? password)
    {
        buffer.Position = 0;

        var options = new ParsingOptions
        {
            UseLenientParsing = true,
            SkipMissingFonts = true,
            Passwords = string.IsNullOrEmpty(password) ? [] : [password]
        };

        using var document = PdfDocument.Open(buffer, options);

        var pages = new List<string>(document.NumberOfPages);
        var pageWords = new List<IReadOnlyList<PdfWord>>(document.NumberOfPages);
        foreach (var page in document.GetPages())
        {
            string text;
            try
            {
                text = ContentOrderTextExtractor.GetText(page, true);
            }
            catch
            {
                text = page.Text;
            }

            pages.Add(text ?? string.Empty);
            pageWords.Add(ExtractWords(page));
        }

        var metadata = new Dictionary<string, string>();
        var info = document.Information;
        AddIfPresent(metadata, "Title", info.Title);
        AddIfPresent(metadata, "Author", info.Author);
        AddIfPresent(metadata, "Producer", info.Producer);
        AddIfPresent(metadata, "Subject", info.Subject);

        return new PdfDocumentText(
            pages,
            string.Join("\n", pages),
            document.NumberOfPages,
            !string.IsNullOrEmpty(password),
            metadata,
            pageWords);
    }

    /// <summary>PdfPig measures from the bottom of the page; flip it so rows read top to bottom.</summary>
    private static IReadOnlyList<PdfWord> ExtractWords(UglyToad.PdfPig.Content.Page page)
    {
        try
        {
            var height = page.Height;
            return page.GetWords()
                .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                .Select(w => new PdfWord(
                    w.Text,
                    w.BoundingBox.Left,
                    w.BoundingBox.Right,
                    height - w.BoundingBox.Top,
                    height - w.BoundingBox.Bottom))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private static PdfDocumentText ExtractWithITextOrThrow(MemoryStream buffer, string? password, bool passwordMissing)
    {
        try
        {
            var properties = new ITextReaderProperties();
            if (!string.IsNullOrEmpty(password))
            {
                properties.SetPassword(Encoding.UTF8.GetBytes(password));
            }

            using var reader = new ITextPdfReader(buffer, properties);
            reader.SetUnethicalReading(true);

            using var document = new ITextPdfDocument(reader);

            var pageCount = document.GetNumberOfPages();
            var pages = new List<string>(pageCount);

            for (var i = 1; i <= pageCount; i++)
            {
                pages.Add(ITextExtractor.GetTextFromPage(document.GetPage(i), new ITextLocationStrategy()));
            }

            return new PdfDocumentText(
                pages,
                string.Join("\n", pages),
                pageCount,
                !string.IsNullOrEmpty(password),
                new Dictionary<string, string>());
        }
        catch (iText.Kernel.Exceptions.BadPasswordException)
        {
            throw passwordMissing
                ? new PdfPasswordRequiredException("This statement is password protected. Supply the password to continue.")
                : new PdfPasswordIncorrectException("The supplied PDF password is incorrect.");
        }
    }

    private static async Task<MemoryStream> ToMemoryAsync(Stream source, CancellationToken cancellationToken)
    {
        if (source is MemoryStream existing)
        {
            existing.Position = 0;
            return existing;
        }

        var buffer = new MemoryStream();
        if (source.CanSeek)
        {
            source.Position = 0;
        }

        await source.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;
        return buffer;
    }

    private static void AddIfPresent(IDictionary<string, string> target, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[key] = value;
        }
    }
}
