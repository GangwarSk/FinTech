using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FinanceAudit360.Infrastructure.Pdf.Pipeline;

/// <summary>
/// Bank detection, bank-specific parser, generic fallback, then the validation layer. The generic parser
/// takes over only when the bank parser recovers fewer rows, and the bank parser's header fields are kept.
/// </summary>
public sealed class StatementParsingPipeline(
    IPdfParserFactory parserFactory,
    IEnumerable<IStatementValidationRule> rules,
    ILogger<StatementParsingPipeline> logger) : IStatementParsingPipeline
{
    private readonly IReadOnlyList<IStatementValidationRule> _rules = rules.OrderBy(r => r.Order).ToList();

    public ParsedStatement Run(PdfDocumentText document, IPdfParser? preferredParser = null)
    {
        var parser = preferredParser ?? parserFactory.Resolve(document);
        var parsed = WithGenericFallback(parser, parser.Parse(document), document);

        foreach (var rule in _rules)
        {
            parsed = rule.Apply(parsed, document);
        }

        if (document.Metadata.TryGetValue(PdfTextExtractor.OcrPagesKey, out var ocrPages))
        {
            document.Metadata.TryGetValue(PdfTextExtractor.OcrConfidenceKey, out var confidence);
            parsed = parsed with
            {
                ParserName = $"{parsed.ParserName}+OCR",
                Warnings =
                [
                    .. parsed.Warnings,
                    $"Scanned page(s) {ocrPages} were read with OCR (confidence {confidence ?? "n/a"}). Review amounts and dates before relying on them."
                ]
            };
        }

        logger.LogInformation(
            "Parsed statement with {ParserName}: {TransactionCount} transaction(s), {WarningCount} warning(s).",
            parsed.ParserName,
            parsed.Transactions.Count,
            parsed.Warnings.Count);

        return parsed;
    }

    private ParsedStatement WithGenericFallback(IPdfParser parser, ParsedStatement parsed, PdfDocumentText document)
    {
        if (parser.BankCode == BankCode.Other)
        {
            return parsed;
        }

        var generic = parserFactory.ResolveByBank(BankCode.Other);
        if (generic is null)
        {
            return parsed;
        }

        var fallback = generic.Parse(document);
        if (fallback.Transactions.Count <= parsed.Transactions.Count)
        {
            return parsed;
        }

        logger.LogWarning(
            "{ParserName} recovered {BankCount} row(s); the generic parser recovered {GenericCount} and is used instead.",
            parser.Name,
            parsed.Transactions.Count,
            fallback.Transactions.Count);

        return parsed with
        {
            Kind = fallback.Kind,
            ParserName = $"{parser.Name}+{generic.Name}",
            PeriodStart = parsed.PeriodStart ?? fallback.PeriodStart,
            PeriodEnd = parsed.PeriodEnd ?? fallback.PeriodEnd,
            OpeningBalance = fallback.OpeningBalance ?? parsed.OpeningBalance,
            ClosingBalance = fallback.ClosingBalance ?? parsed.ClosingBalance,
            AccountNumberMasked = parsed.AccountNumberMasked ?? fallback.AccountNumberMasked,
            CardNumberMasked = parsed.CardNumberMasked ?? fallback.CardNumberMasked,
            Transactions = fallback.Transactions,
            Warnings = [.. fallback.Warnings, $"{parser.Name} layout was not recognised; transactions were read by the generic parser."]
        };
    }
}
