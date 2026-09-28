using System.Text.RegularExpressions;
using FinanceAudit360.Application.Common.Interfaces;

namespace FinanceAudit360.Infrastructure.Pdf.Parsing;

/// <summary>Header captions that locate the columns of a savings/current account transaction table.</summary>
public sealed record BankAccountTableHeader(
    string Date,
    string? Mode,
    string Particulars,
    string Deposits,
    string Withdrawals,
    string Balance);

/// <summary>One reconstructed table row. Amounts are null when the column was blank.</summary>
public sealed record BankAccountTableRow(
    DateTime Date,
    string? Mode,
    string Particulars,
    decimal? Deposit,
    decimal? Withdrawal,
    decimal? Balance,
    int PageNumber);

/// <summary>
/// Rebuilds a "Date | Mode | Particulars | Deposits | Withdrawals | Balance" table from word positions.
/// Reading such statements line by line fails because the particulars cell wraps over several lines while
/// the date and amounts sit on only one of them, and because deposits and withdrawals are told apart only
/// by column, not by a CR/DR marker. Rows are anchored on the dates in the date column, and each run of
/// wrapped particulars lines is given to the date it is vertically centred on.
/// </summary>
public static partial class BankAccountTableReader
{
    private const double BlockGapFactor = 1.45;
    private const double EmptyRunPenalty = 10_000d;

    [GeneratedRegex(@"^\d{2}[-/.]\d{2}[-/.]\d{4}$")]
    private static partial Regex RowDate();

    [GeneratedRegex(@"^\(?-?(?:\d{1,3}(?:,\d{2,3})+|\d+)\.\d{2}\)?(?:\s*(?:CR|DR))?$", RegexOptions.IgnoreCase)]
    private static partial Regex Amount();

    /// <summary>Returns null when no page carries the header, so the caller can fall back to text parsing.</summary>
    public static IReadOnlyList<BankAccountTableRow>? Read(
        IReadOnlyList<IReadOnlyList<PdfWord>> pages,
        BankAccountTableHeader header) =>
        Read(pages, [header]);

    /// <summary>Tries each header variant in turn; the first one found on any page fixes the columns.</summary>
    public static IReadOnlyList<BankAccountTableRow>? Read(
        IReadOnlyList<IReadOnlyList<PdfWord>> pages,
        IReadOnlyList<BankAccountTableHeader> headers)
    {
        if (pages.Count == 0 || headers.Count == 0)
        {
            return null;
        }

        Columns? columns = null;
        var rows = new List<BankAccountTableRow>();

        for (var index = 0; index < pages.Count; index++)
        {
            var words = pages[index];
            if (words.Count == 0)
            {
                continue;
            }

            var lines = GroupIntoLines(words);
            var (pageColumns, headerBottom) = FindHeader(lines, headers);

            if (pageColumns is not null)
            {
                columns = pageColumns;
            }
            else if (columns is null)
            {
                continue;
            }

            var body = words.Where(w => w.Top >= headerBottom).ToList();
            body = CutAtTerminator(body, columns);

            rows.AddRange(ReadPage(body, columns, index + 1));
        }

        return columns is null ? null : rows;
    }

    private static IEnumerable<BankAccountTableRow> ReadPage(List<PdfWord> body, Columns columns, int pageNumber)
    {
        var anchors = OnePerRow(body
            .Where(w => columns.IsDateZone(w) && RowDate().IsMatch(w.Text))
            .Select(w => (Word: w, Date: StatementTextParser.ParseDate(w.Text)))
            .Where(a => a.Date is not null)
            .ToList());

        if (anchors.Count == 0)
        {
            yield break;
        }

        var rowTolerance = Math.Max(2d, Median(body.Select(w => w.Height)) * 0.6);

        var particularLines = GroupIntoLines(body.Where(w => columns.IsParticulars(w)).ToList());
        var modeLines = GroupIntoLines(body.Where(w => columns.IsModeZone(w)).ToList());
        var assignment = AssignLinesToAnchors(particularLines, anchors.Select(a => a.Word.CenterY).ToList());

        var amountWords = body.Where(w => columns.IsAmount(w, Amount())).ToList();

        for (var i = 0; i < anchors.Count; i++)
        {
            var anchor = anchors[i];
            var own = assignment[i];

            var top = own.Count > 0 ? Math.Min(own[0].CenterY, anchor.Word.CenterY) : anchor.Word.CenterY;
            var bottom = own.Count > 0 ? Math.Max(own[^1].CenterY, anchor.Word.CenterY) : anchor.Word.CenterY;

            var mode = string.Join(' ', modeLines
                .Where(l => l.CenterY >= top - rowTolerance && l.CenterY <= bottom + rowTolerance)
                .Select(l => l.Text));

            var amounts = amountWords
                .Where(w => Math.Abs(w.CenterY - anchor.Word.CenterY) <= rowTolerance)
                .OrderBy(w => w.Left)
                .ToList();

            var (deposit, withdrawal, balance) = columns.SplitAmounts(amounts);

            yield return new BankAccountTableRow(
                anchor.Date!.Value,
                string.IsNullOrWhiteSpace(mode) ? null : mode,
                string.Join(' ', own.Select(l => l.Text)).Trim(),
                deposit,
                withdrawal,
                balance,
                pageNumber);
        }
    }

    /// <summary>
    /// Layouts with both a value date and a transaction date print two dates on a row; the left-most one
    /// anchors the row so the row is not read twice.
    /// </summary>
    private static List<(PdfWord Word, DateTime? Date)> OnePerRow(List<(PdfWord Word, DateTime? Date)> candidates)
    {
        var result = new List<(PdfWord Word, DateTime? Date)>();
        foreach (var candidate in candidates.OrderBy(a => a.Word.CenterY).ThenBy(a => a.Word.Left))
        {
            var sameRow = result.Count > 0 &&
                          Math.Abs(result[^1].Word.CenterY - candidate.Word.CenterY) < candidate.Word.Height * 0.5;
            if (!sameRow)
            {
                result.Add(candidate);
            }
        }

        return result;
    }

    /// <summary>
    /// Splits the particulars lines into one contiguous run per anchor. When the blank space between rows
    /// is visibly larger than the line spacing inside a cell, the runs are simply the visual blocks.
    /// Otherwise a small dynamic programme picks the split whose runs are best centred on their dates.
    /// </summary>
    private static List<List<Line>> AssignLinesToAnchors(IReadOnlyList<Line> lines, IReadOnlyList<double> anchors)
    {
        var result = anchors.Select(_ => new List<Line>()).ToList();
        if (lines.Count == 0)
        {
            return result;
        }

        var gaps = new List<double>();
        for (var i = 1; i < lines.Count; i++)
        {
            gaps.Add(lines[i].CenterY - lines[i - 1].CenterY);
        }

        var lineGap = gaps.Count == 0 ? lines[0].Height * 1.2 : Percentile(gaps, 0.3);
        lineGap = Math.Max(lineGap, 0.5);

        var blocks = new List<List<Line>> { new() { lines[0] } };
        for (var i = 1; i < lines.Count; i++)
        {
            if (gaps[i - 1] > lineGap * BlockGapFactor)
            {
                blocks.Add([]);
            }

            blocks[^1].Add(lines[i]);
        }

        var tolerance = lineGap * 0.6;
        var anchorsPerBlock = blocks
            .Select(b => anchors
                .Select((y, index) => (y, index))
                .Where(a => a.y >= b[0].CenterY - tolerance && a.y <= b[^1].CenterY + tolerance)
                .Select(a => a.index)
                .ToList())
            .ToList();

        // Text above the first date or below the last one that no date claims is page furniture.
        var first = anchorsPerBlock.FindIndex(a => a.Count > 0);
        var last = anchorsPerBlock.FindLastIndex(a => a.Count > 0);
        if (first < 0)
        {
            return result;
        }

        var kept = blocks.Skip(first).Take(last - first + 1).ToList();
        var keptAnchors = anchorsPerBlock.Skip(first).Take(last - first + 1).ToList();

        var claimed = keptAnchors.SelectMany(a => a).ToList();
        var isOneToOne = keptAnchors.All(a => a.Count == 1) &&
                         claimed.Count == anchors.Count &&
                         claimed.Distinct().Count() == anchors.Count;

        if (isOneToOne)
        {
            for (var i = 0; i < kept.Count; i++)
            {
                result[keptAnchors[i][0]].AddRange(kept[i]);
            }

            return result;
        }

        var candidates = kept.SelectMany(b => b).ToList();
        var runs = PartitionByCentroid(candidates, anchors, lineGap);
        for (var i = 0; i < runs.Count; i++)
        {
            result[i].AddRange(runs[i]);
        }

        return result;
    }

    private static List<List<Line>> PartitionByCentroid(IReadOnlyList<Line> lines, IReadOnlyList<double> anchors, double lineGap)
    {
        var n = lines.Count;
        var m = anchors.Count;

        var sum = new double[n + 1];
        var excessGap = new double[n + 1];
        for (var i = 0; i < n; i++)
        {
            sum[i + 1] = sum[i] + lines[i].CenterY;
            var gap = i == 0 ? 0 : lines[i].CenterY - lines[i - 1].CenterY;
            var excess = Math.Max(0, gap - lineGap * BlockGapFactor);
            excessGap[i + 1] = excessGap[i] + excess * excess * 4;
        }

        double Cost(int from, int to, int anchor)
        {
            var count = to - from;
            if (count == 0)
            {
                return EmptyRunPenalty;
            }

            var mean = (sum[to] - sum[from]) / count;
            var offset = mean - anchors[anchor];

            // Gaps inside the run, not the one leading into it.
            var internalGaps = excessGap[to] - excessGap[from + 1];
            return count * offset * offset + internalGaps;
        }

        var dp = new double[m + 1, n + 1];
        var split = new int[m + 1, n + 1];
        for (var j = 0; j <= m; j++)
        {
            for (var i = 0; i <= n; i++)
            {
                dp[j, i] = double.PositiveInfinity;
            }
        }

        dp[0, 0] = 0;
        for (var j = 1; j <= m; j++)
        {
            for (var i = 0; i <= n; i++)
            {
                for (var k = 0; k <= i; k++)
                {
                    if (double.IsPositiveInfinity(dp[j - 1, k]))
                    {
                        continue;
                    }

                    var value = dp[j - 1, k] + Cost(k, i, j - 1);
                    if (value < dp[j, i])
                    {
                        dp[j, i] = value;
                        split[j, i] = k;
                    }
                }
            }
        }

        var runs = new List<List<Line>>(new List<Line>[m]);
        var end = n;
        for (var j = m; j >= 1; j--)
        {
            var start = split[j, end];
            runs[j - 1] = lines.Skip(start).Take(end - start).ToList();
            end = start;
        }

        return runs;
    }

    /// <summary>The first "Total" line, or any legend/footer line, ends the table on that page.</summary>
    private static List<PdfWord> CutAtTerminator(List<PdfWord> body, Columns columns)
    {
        foreach (var line in GroupIntoLines(body.Where(w => w.Left < columns.AmountsLeft).ToList()))
        {
            var text = line.Text.TrimStart();
            if (Regex.IsMatch(text, @"^TOTAL\b", RegexOptions.IgnoreCase) ||
                text.StartsWith("Legends", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("Closing Balance", StringComparison.OrdinalIgnoreCase))
            {
                return body.Where(w => w.CenterY < line.CenterY - line.Height / 2).ToList();
            }
        }

        return body;
    }

    private const int MaxHeaderLines = 3;

    /// <summary>
    /// Header captions often wrap ("Withdrawal / Amount / (INR)"), so each line is tried together with the
    /// one or two lines directly below it.
    /// </summary>
    private static (Columns? Columns, double HeaderBottom) FindHeader(
        IReadOnlyList<Line> lines,
        IReadOnlyList<BankAccountTableHeader> headers)
    {
        for (var start = 0; start < lines.Count; start++)
        {
            for (var span = 1; span <= MaxHeaderLines && start + span <= lines.Count; span++)
            {
                var band = lines.Skip(start).Take(span).ToList();
                if (span > 1 && band[^1].CenterY - band[0].CenterY > band[0].Height * 2.6 * (span - 1))
                {
                    break;
                }

                var words = band.SelectMany(l => l.Words).OrderBy(w => w.Left).ToList();
                foreach (var header in headers)
                {
                    var columns = TryBuildColumns(words, header);
                    if (columns is not null)
                    {
                        return (columns, words.Max(w => w.Bottom));
                    }
                }
            }
        }

        return (null, 0);
    }

    private static Columns? TryBuildColumns(IReadOnlyList<PdfWord> band, BankAccountTableHeader header)
    {
        var particulars = Find(band, header.Particulars);
        var balance = Find(band, header.Balance);
        var deposits = Find(band, header.Deposits);
        var withdrawals = Find(band, header.Withdrawals);

        if (particulars is null || balance is null || deposits is null || withdrawals is null)
        {
            return null;
        }

        // Captions must read left to right: narration, then the amount columns.
        if (particulars.Right >= Math.Min(Math.Min(deposits.Left, withdrawals.Left), balance.Left))
        {
            return null;
        }

        var date = Find(band, header.Date);
        var mode = header.Mode is null ? null : Find(band, header.Mode);

        return new Columns(
            date is null ? 0 : CellLeft(band, date),
            mode is null ? null : CellLeft(band, mode),
            CellLeft(band, particulars),
            deposits,
            withdrawals,
            balance);
    }

    private static PdfWord? Find(IReadOnlyList<PdfWord> words, string caption) =>
        words.FirstOrDefault(w => w.Text.TrimEnd('*', ':', '.').StartsWith(caption, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Column text is left-aligned to the start of the caption cell, not to the matched word: "Transaction
    /// Remarks" or a stacked "Transaction / Remarks" both start at "Transaction".
    /// </summary>
    private static double CellLeft(IReadOnlyList<PdfWord> band, PdfWord caption)
    {
        var left = caption.Left;
        var gap = Math.Max(1d, caption.Height * 0.8);

        foreach (var word in band.Where(w => w.Right <= caption.Right).OrderByDescending(w => w.Right))
        {
            var sameLine = Math.Abs(word.CenterY - caption.CenterY) < caption.Height * 0.5;
            var joinsInline = sameLine && word.Right <= left && left - word.Right <= gap;
            var stacked = !sameLine && word.Left < caption.Right && word.Right > caption.Left && word.Left >= left - gap * 3;

            if (joinsInline || stacked)
            {
                left = Math.Min(left, word.Left);
            }
        }

        return left;
    }

    private static List<Line> GroupIntoLines(IReadOnlyList<PdfWord> words)
    {
        var lines = new List<Line>();
        if (words.Count == 0)
        {
            return lines;
        }

        var tolerance = Math.Max(1d, Median(words.Select(w => w.Height)) * 0.45);
        var current = new List<PdfWord>();
        var currentCenter = 0d;

        foreach (var word in words.OrderBy(w => w.CenterY).ThenBy(w => w.Left))
        {
            if (current.Count > 0 && Math.Abs(word.CenterY - currentCenter) > tolerance)
            {
                lines.Add(new Line(current));
                current = [];
            }

            current.Add(word);
            currentCenter = current.Average(w => w.CenterY);
        }

        if (current.Count > 0)
        {
            lines.Add(new Line(current));
        }

        return lines;
    }

    private static double Median(IEnumerable<double> values) => Percentile(values.ToList(), 0.5);

    private static double Percentile(IReadOnlyList<double> values, double percentile)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.OrderBy(v => v).ToList();
        var index = (int)Math.Round((sorted.Count - 1) * percentile);
        return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
    }

    private sealed class Line(List<PdfWord> words)
    {
        public IReadOnlyList<PdfWord> Words { get; } = words.OrderBy(w => w.Left).ToList();

        public double CenterY { get; } = words.Average(w => w.CenterY);

        public double Height { get; } = words.Max(w => w.Height);

        public string Text => string.Join(' ', Words.Select(w => w.Text));
    }

    private sealed class Columns(
        double dateLeft,
        double? modeLeft,
        double particularsLeft,
        PdfWord? deposits,
        PdfWord? withdrawals,
        PdfWord balance)
    {
        private const double Slack = 2d;

        public double AmountsLeft { get; } = new[] { deposits?.Left, withdrawals?.Left, balance.Left }
            .Where(v => v.HasValue)
            .Min()!.Value;

        public bool IsDateZone(PdfWord word) =>
            word.Left >= dateLeft - Slack && word.Left < (modeLeft ?? particularsLeft) - Slack;

        public bool IsModeZone(PdfWord word) =>
            modeLeft.HasValue && word.Left >= modeLeft.Value - Slack && word.Left < particularsLeft - Slack;

        /// <summary>Numbers that stay left of the amount columns are part of the narration, not amounts.</summary>
        public bool IsParticulars(PdfWord word) =>
            word.Left >= particularsLeft - Slack && word.Left < AmountsLeft - Slack && word.Right <= AmountsLeft + Slack;

        public bool IsAmount(PdfWord word, Regex amount) =>
            word.Right > AmountsLeft && amount.IsMatch(word.Text);

        /// <summary>
        /// The right-most figure on a row is the running balance. The others go to whichever of the deposit
        /// or withdrawal headers they line up with; amounts are right-aligned, so right edges are compared.
        /// </summary>
        public (decimal? Deposit, decimal? Withdrawal, decimal? Balance) SplitAmounts(IReadOnlyList<PdfWord> amounts)
        {
            decimal? deposit = null, withdrawal = null, balanceValue = null;
            if (amounts.Count == 0)
            {
                return (null, null, null);
            }

            var candidates = new List<(PdfWord? Header, int Column)> { (deposits, 0), (withdrawals, 1), (balance, 2) };

            for (var i = 0; i < amounts.Count; i++)
            {
                var word = amounts[i];
                var value = Parse(word.Text);

                var column = amounts.Count >= 2 && i == amounts.Count - 1
                    ? 2
                    : candidates
                        .Where(c => c.Header is not null)
                        .OrderBy(c => Distance(word, c.Header!))
                        .First().Column;

                switch (column)
                {
                    case 0:
                        deposit ??= value;
                        break;
                    case 1:
                        withdrawal ??= value;
                        break;
                    default:
                        balanceValue ??= value;
                        break;
                }
            }

            return (deposit, withdrawal, balanceValue);
        }

        private static double Distance(PdfWord word, PdfWord header)
        {
            var wordCenter = (word.Left + word.Right) / 2;
            var headerCenter = (header.Left + header.Right) / 2;
            return Math.Min(Math.Abs(word.Right - header.Right), Math.Abs(wordCenter - headerCenter));
        }

        private static decimal? Parse(string text)
        {
            var cleaned = Regex.Replace(text, @"(?i)\s*(CR|DR)$", string.Empty).Trim('(', ')');
            return StatementTextParser.ParseAmount(cleaned);
        }
    }
}
