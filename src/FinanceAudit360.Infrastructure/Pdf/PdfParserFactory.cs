using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace FinanceAudit360.Infrastructure.Pdf;

/// <summary>
/// Picks the parser for a document by probing the registered parsers in priority order.
/// The generic parser is only used when no issuer-specific parser claims the text.
/// </summary>
public sealed class PdfParserFactory : IPdfParserFactory
{
    private readonly IReadOnlyList<IPdfParser> _parsers;
    private readonly ILogger<PdfParserFactory> _logger;

    public PdfParserFactory(IEnumerable<IPdfParser> parsers, ILogger<PdfParserFactory> logger)
    {
        _parsers = parsers.OrderByDescending(p => p.Priority).ToList();
        _logger = logger;
    }

    public IReadOnlyList<IPdfParser> All => _parsers;

    public IPdfParser Resolve(PdfDocumentText document)
    {
        foreach (var parser in _parsers.Where(p => p.BankCode != BankCode.Other))
        {
            if (!parser.CanParse(document))
            {
                continue;
            }

            _logger.LogInformation("Resolved {ParserName} for the uploaded statement.", parser.Name);
            return parser;
        }

        var fallback = _parsers.LastOrDefault(p => p.BankCode == BankCode.Other)
                       ?? throw new InvalidOperationException("No PDF parser is registered.");

        _logger.LogWarning("No issuer-specific parser matched; using {ParserName}.", fallback.Name);
        return fallback;
    }

    public IPdfParser? ResolveByBank(BankCode bankCode) =>
        _parsers.FirstOrDefault(p => p.BankCode == bankCode);
}
