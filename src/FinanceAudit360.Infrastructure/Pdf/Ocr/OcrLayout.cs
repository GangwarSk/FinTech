using FinanceAudit360.Application.Common.Interfaces;

namespace FinanceAudit360.Infrastructure.Pdf.Ocr;

/// <summary>Rebuilds reading-order text from OCR words so text-based parsers see one printed row per line.</summary>
public static class OcrLayout
{
    public static string ToText(IReadOnlyList<PdfWord> words)
    {
        if (words.Count == 0)
        {
            return string.Empty;
        }

        var heights = words.Select(w => w.Height).Order().ToList();
        var tolerance = Math.Max(1d, heights[heights.Count / 2] * 0.5);

        var rows = new List<List<PdfWord>>();
        foreach (var word in words.OrderBy(w => w.CenterY))
        {
            var row = rows.Count == 0 ? null : rows[^1];
            if (row is not null && Math.Abs(row.Average(w => w.CenterY) - word.CenterY) <= tolerance)
            {
                row.Add(word);
            }
            else
            {
                rows.Add([word]);
            }
        }

        return string.Join('\n', rows.Select(r => string.Join(' ', r.OrderBy(w => w.Left).Select(w => w.Text))));
    }
}
