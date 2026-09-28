using System.Collections.Concurrent;
using System.Security.Cryptography;
using Docnet.Core;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Infrastructure.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tesseract;

namespace FinanceAudit360.Infrastructure.Pdf.Ocr;

/// <summary>One OCR-recognised page. Word coordinates use the same top-left point space as PdfPig words.</summary>
public sealed record OcrPage(string Text, IReadOnlyList<PdfWord> Words, float MeanConfidence);

/// <summary>Recognises text on scanned statement pages that carry no text layer.</summary>
public interface IPdfOcrEngine
{
    bool IsAvailable { get; }

    IReadOnlyDictionary<int, OcrPage> Recognize(
        byte[] pdf,
        string? password,
        IReadOnlyCollection<int> pageIndexes,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Renders pages with PDFium (Docnet) and reads them with Tesseract. Bank statements print column captions
/// in light text on grey bars, which Tesseract skips, so those bars are inverted before recognition.
/// </summary>
public sealed class TesseractPdfOcrEngine(
    IOptions<OcrOptions> options,
    IMemoryCache cache,
    ILogger<TesseractPdfOcrEngine> logger) : IPdfOcrEngine
{
    // PDFium is not thread-safe; rendering is serialised while recognition runs in parallel.
    private static readonly Lock RenderLock = new();

    private readonly OcrOptions _options = options.Value;
    private bool? _available;

    public bool IsAvailable => _available ??= Probe();

    public IReadOnlyDictionary<int, OcrPage> Recognize(
        byte[] pdf,
        string? password,
        IReadOnlyCollection<int> pageIndexes,
        CancellationToken cancellationToken = default)
    {
        if (pageIndexes.Count == 0 || !IsAvailable)
        {
            return new Dictionary<int, OcrPage>();
        }

        var key = $"ocr:{Convert.ToHexString(SHA256.HashData(pdf))}:{string.Join(',', pageIndexes.Order())}";
        if (cache.TryGetValue(key, out IReadOnlyDictionary<int, OcrPage>? cached) && cached is not null)
        {
            return cached;
        }

        var started = DateTime.UtcNow;
        var scale = Math.Clamp(_options.RenderScale, 1d, 6d);
        var results = new ConcurrentDictionary<int, OcrPage>();

        using (var reader = OpenReader(pdf, password, scale))
        {
            var parallel = new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Clamp(_options.MaxDegreeOfParallelism, 1, Environment.ProcessorCount)
            };

            Parallel.ForEach(
                pageIndexes.Where(i => i >= 0 && i < reader.GetPageCount()),
                parallel,
                CreateEngine,
                (index, _, engine) =>
                {
                    results[index] = RecognizePage(engine, reader, index, scale);
                    return engine;
                },
                engine => engine.Dispose());
        }

        IReadOnlyDictionary<int, OcrPage> recognised = new Dictionary<int, OcrPage>(results);
        logger.LogInformation(
            "OCR read {Pages} page(s) in {Seconds:F1}s (mean confidence {Confidence:P0}).",
            recognised.Count,
            (DateTime.UtcNow - started).TotalSeconds,
            recognised.Count == 0 ? 0 : recognised.Values.Average(p => p.MeanConfidence));

        cache.Set(key, recognised, TimeSpan.FromMinutes(Math.Max(1, _options.CacheMinutes)));
        return recognised;
    }

    private static IDocReader OpenReader(byte[] pdf, string? password, double scale)
    {
        lock (RenderLock)
        {
            var dimensions = new PageDimensions(scale);
            return string.IsNullOrEmpty(password)
                ? DocLib.Instance.GetDocReader(pdf, dimensions)
                : DocLib.Instance.GetDocReader(pdf, password, dimensions);
        }
    }

    private OcrPage RecognizePage(TesseractEngine engine, IDocReader reader, int index, double scale)
    {
        byte[] bitmap;
        int width;
        int height;

        lock (RenderLock)
        {
            using var page = reader.GetPageReader(index);
            width = page.GetPageWidth();
            height = page.GetPageHeight();
            var gray = OcrImage.ToGray(page.GetImage(), width, height);
            OcrImage.InvertShadedBands(gray, width, height, scale);
            bitmap = OcrImage.ToBitmap(gray, width, height, 72d * scale);
        }

        using var pix = Pix.LoadFromMemory(bitmap);
        using var recognised = engine.Process(pix, PageSegMode.Auto);

        var words = new List<PdfWord>();
        using (var iterator = recognised.GetIterator())
        {
            iterator.Begin();
            do
            {
                var text = iterator.GetText(PageIteratorLevel.Word)?.Trim();
                if (string.IsNullOrEmpty(text) || !iterator.TryGetBoundingBox(PageIteratorLevel.Word, out var box))
                {
                    continue;
                }

                words.Add(new PdfWord(text, box.X1 / scale, box.X2 / scale, box.Y1 / scale, box.Y2 / scale));
            }
            while (iterator.Next(PageIteratorLevel.Word));
        }

        return new OcrPage(OcrLayout.ToText(words), words, recognised.GetMeanConfidence());
    }

    private TesseractEngine CreateEngine()
    {
        var engine = new TesseractEngine(ResolveTessDataPath(), _options.Language, EngineMode.LstmOnly);
        engine.SetVariable("user_defined_dpi", ((int)(72d * _options.RenderScale)).ToString());
        return engine;
    }

    private bool Probe()
    {
        if (!_options.Enabled)
        {
            return false;
        }

        var model = Path.Combine(ResolveTessDataPath(), $"{_options.Language}.traineddata");
        if (!File.Exists(model))
        {
            logger.LogWarning("OCR disabled: language data {Model} was not found.", model);
            return false;
        }

        try
        {
            using var engine = CreateEngine();
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "OCR disabled: the Tesseract engine could not be loaded on this machine.");
            return false;
        }
    }

    private string ResolveTessDataPath() =>
        Path.IsPathRooted(_options.TessDataPath)
            ? _options.TessDataPath
            : Path.Combine(AppContext.BaseDirectory, _options.TessDataPath);
}
