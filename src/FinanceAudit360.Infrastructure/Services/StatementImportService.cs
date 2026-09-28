using System.Text.Json;
using FinanceAudit360.Application.Common.Exceptions;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Application.Common.Mappings;
using FinanceAudit360.Contracts.Statements;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Domain.Enums;
using FinanceAudit360.Domain.ValueObjects;
using FinanceAudit360.Shared.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceAudit360.Infrastructure.Services;

/// <summary>
/// Orchestrates the full upload pipeline: store PDF, detect protection, identify the issuer,
/// parse, reconcile against masters and import the transactions in one transaction.
/// </summary>
public sealed class StatementImportService(
    IApplicationDbContext context,
    IUnitOfWork unitOfWork,
    IFileStorage fileStorage,
    IPdfTextExtractor textExtractor,
    IPdfParserFactory parserFactory,
    IStatementParsingPipeline parsingPipeline,
    IUploadHistoryRepository uploadRepository,
    IStatementRepository statementRepository,
    ICreditCardRepository creditCardRepository,
    IBankAccountRepository bankAccountRepository,
    IBankRepository bankRepository,
    IVendorRepository vendorRepository,
    IPersonRepository personRepository,
    ICurrentUser currentUser,
    IDateTimeProvider dateTime,
    ILogger<StatementImportService> logger) : IStatementImportService
{
    public async Task<UploadStatementResponse> UploadAsync(
        Stream content,
        string fileName,
        long sizeInBytes,
        CancellationToken cancellationToken = default)
    {
        var upload = UploadHistory.Start(fileName, sizeInBytes, currentUser.IpAddress);
        await uploadRepository.AddAsync(upload, cancellationToken);

        try
        {
            var stored = await fileStorage.SaveAsync(content, fileName, "statements", cancellationToken);

            var duplicateFile = await context.StatementFiles
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.ContentHash == stored.ContentHash, cancellationToken);

            var statementFile = StatementFile.Create(
                fileName,
                stored.StoredFileName,
                stored.RelativePath,
                stored.SizeInBytes,
                stored.ContentHash);

            await using var probeStream = await fileStorage.OpenReadAsync(stored.RelativePath, cancellationToken);
            var isProtected = await textExtractor.IsPasswordProtectedAsync(probeStream, cancellationToken);

            statementFile.SetPdfMetadata(isProtected, 0);

            var detectedBank = BankCode.Unknown;
            var detectedKind = StatementKind.Unknown;

            if (!isProtected)
            {
                await using var readStream = await fileStorage.OpenReadAsync(stored.RelativePath, cancellationToken);
                var document = await textExtractor.ExtractAsync(readStream, null, cancellationToken);

                var parser = parserFactory.Resolve(document);
                detectedBank = parser.BankCode;
                detectedKind = parsingPipeline.Run(document, parser).Kind;

                statementFile.SetPdfMetadata(false, document.PageCount);
                statementFile.SetDetection(detectedBank, detectedKind);
            }

            await context.StatementFiles.AddAsync(statementFile, cancellationToken);
            upload.AttachFile(statementFile.Id);

            if (isProtected)
            {
                upload.MarkAwaitingPassword();
            }
            else
            {
                upload.MarkProcessing(detectedBank, detectedKind, null);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            var message = duplicateFile is not null
                ? "This exact PDF has been uploaded before. Processing it again will skip duplicate transactions."
                : null;

            return new UploadStatementResponse(
                upload.Id,
                statementFile.Id,
                upload.Status.ToString(),
                isProtected,
                detectedBank == BankCode.Unknown ? null : detectedBank.ToString(),
                detectedKind == StatementKind.Unknown ? null : detectedKind.ToString(),
                message);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Upload failed for {FileName}.", fileName);
            upload.MarkFailed("upload.failed", exception.Message);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new UploadStatementResponse(upload.Id, null, upload.Status.ToString(), false, null, null, exception.Message);
        }
    }

    public async Task<StatementProcessingResultDto> ProcessAsync(
        ProcessStatementRequest request,
        CancellationToken cancellationToken = default)
    {
        var upload = await context.UploadHistories
            .Include(u => u.StatementFile)
            .FirstOrDefaultAsync(u => u.Id == request.UploadId, cancellationToken)
            ?? throw new NotFoundException(nameof(UploadHistory), request.UploadId);

        if (upload.StatementFile is null)
        {
            throw new ConflictException("upload.no_file", "No stored file is associated with this upload.");
        }

        try
        {
            await using var stream = await fileStorage.OpenReadAsync(upload.StatementFile.RelativePath, cancellationToken);
            var document = await textExtractor.ExtractAsync(stream, request.Password, cancellationToken);

            var parser = await ResolveParserAsync(document, request.OverrideBankId, cancellationToken);
            var parsed = parsingPipeline.Run(document, parser);

            upload.MarkProcessing(parsed.BankCode, parsed.Kind, parsed.ParserName);
            upload.MarkParsed(parsed.Transactions.Count);
            upload.StatementFile.SetPdfMetadata(!string.IsNullOrEmpty(request.Password), document.PageCount);
            upload.StatementFile.SetDetection(parsed.BankCode, parsed.Kind);

            var result = await ImportAsync(upload, parsed, request, cancellationToken);
            return result;
        }
        catch (PdfPasswordRequiredException exception)
        {
            upload.MarkAwaitingPassword();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Failure(upload, "statement.password_required", exception.Message);
        }
        catch (PdfPasswordIncorrectException exception)
        {
            upload.MarkAwaitingPassword();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Failure(upload, "statement.password_incorrect", exception.Message);
        }
        catch (ConflictException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Processing failed for upload {UploadId}.", request.UploadId);
            upload.MarkFailed("statement.processing_failed", exception.Message);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Failure(upload, "statement.processing_failed", exception.Message);
        }
    }

    private async Task<IPdfParser> ResolveParserAsync(
        PdfDocumentText document,
        Guid? overrideBankId,
        CancellationToken cancellationToken)
    {
        if (overrideBankId is null)
        {
            return parserFactory.Resolve(document);
        }

        var bank = await bankRepository.GetByIdAsync(overrideBankId.Value, cancellationToken)
                   ?? throw new NotFoundException(nameof(Bank), overrideBankId.Value);

        return parserFactory.ResolveByBank(bank.Code) ?? parserFactory.Resolve(document);
    }

    private async Task<StatementProcessingResultDto> ImportAsync(
        UploadHistory upload,
        ParsedStatement parsed,
        ProcessStatementRequest request,
        CancellationToken cancellationToken)
    {
        var warnings = parsed.Warnings.ToList();

        var periodStart = parsed.PeriodStart ?? parsed.Transactions.MinBy(t => t.TransactionDate)?.TransactionDate;
        var periodEnd = parsed.PeriodEnd ?? parsed.Transactions.MaxBy(t => t.TransactionDate)?.TransactionDate;

        if (periodStart is null || periodEnd is null)
        {
            upload.MarkFailed("statement.no_period", "The statement period could not be determined and no transactions were found.");
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Failure(upload, "statement.no_period", "The statement period could not be determined.");
        }

        var period = DateRange.Create(periodStart.Value.Date, periodEnd.Value.Date);
        var bank = await ResolveBankAsync(parsed.BankCode, cancellationToken);

        CreditCard? creditCard = null;
        BankAccount? bankAccount = null;
        CreditCardStatement statement;

        if (parsed.Kind == StatementKind.CreditCard)
        {
            var last4 = parsed.CardNumberMasked.Last4();
            if (last4.Length != 4)
            {
                warnings.Add("The card number could not be read; the statement was linked to a placeholder card.");
                last4 = "0000";
            }

            creditCard = await ResolveCreditCardAsync(bank, last4, parsed, cancellationToken);
            statement = CreditCardStatement.CreateForCreditCard(bank.Id, creditCard.Id, creditCard.CardNumber, period);
        }
        else
        {
            var last4 = (parsed.AccountNumberMasked ?? parsed.CardNumberMasked).Last4();
            if (last4.Length != 4)
            {
                warnings.Add("The account number could not be read; the statement was linked to a placeholder account.");
                last4 = "0000";
            }

            bankAccount = await ResolveBankAccountAsync(bank, last4, parsed, cancellationToken);
            statement = CreditCardStatement.CreateForBankAccount(bank.Id, bankAccount.Id, bankAccount.AccountNumberMasked, period);
        }

        var existing = await statementRepository.FindByDedupeKeyAsync(statement.DedupeKey, cancellationToken);
        if (existing is not null && !request.AllowDuplicate)
        {
            upload.MarkDuplicate(existing.Id);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new StatementProcessingResultDto(
                upload.Id, existing.Id, upload.Status.ToString(), bank.Name, parsed.Kind.ToString(), parsed.ParserName,
                parsed.CardNumberMasked, parsed.AccountNumberMasked, parsed.CardHolderName, parsed.CustomerName,
                parsed.CustomerEmail, parsed.CustomerPhone, null, period.From, period.To, parsed.StatementDate,
                parsed.PaymentDueDate, parsed.OpeningBalance, parsed.ClosingBalance, parsed.MinimumDue, parsed.TotalDue,
                parsed.Transactions.Count, 0, parsed.Transactions.Count, upload.DurationMs,
                ["This statement period has already been imported."], upload.ErrorCode, upload.ErrorMessage);
        }

        statement.SetSource(upload.StatementFileId!.Value, parsed.ParserName, parsed.StatementNumber, parsed.Currency);
        statement.SetDates(parsed.StatementDate, parsed.PaymentDueDate);
        statement.SetBalances(
            parsed.OpeningBalance ?? 0m,
            parsed.ClosingBalance ?? 0m,
            parsed.MinimumDue,
            parsed.TotalDue,
            parsed.CreditLimit,
            parsed.AvailableCreditLimit);

        statement.SetHolderDetails(
            parsed.CardHolderName,
            parsed.CustomerName,
            parsed.CustomerEmail,
            parsed.CustomerPhone,
            Address.Create(parsed.AddressLine1, parsed.AddressLine2, parsed.City, parsed.State, parsed.PostalCode));

        await statementRepository.AddAsync(statement, cancellationToken);

        var (imported, skipped) = await ImportTransactionsAsync(statement, parsed, bank, creditCard, bankAccount, cancellationToken);

        statement.MarkImported();

        if (creditCard is not null && parsed.ClosingBalance is not null)
        {
            creditCard.SyncOutstanding(parsed.ClosingBalance.Value, dateTime.UtcNow);
        }

        if (bankAccount is not null && parsed.ClosingBalance is not null)
        {
            bankAccount.SyncBalance(parsed.ClosingBalance.Value, dateTime.UtcNow);
        }

        upload.MarkCompleted(statement.Id, imported, skipped, warnings.Count == 0 ? null : JsonSerializer.Serialize(warnings));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new StatementProcessingResultDto(
            upload.Id,
            statement.Id,
            upload.Status.ToString(),
            bank.Name,
            parsed.Kind.ToString(),
            parsed.ParserName,
            statement.CardNumber?.Masked,
            statement.AccountNumberMasked,
            statement.CardHolderName,
            statement.CustomerName,
            statement.CustomerEmail,
            statement.CustomerPhone,
            statement.BillingAddress.ToDto(),
            statement.PeriodStart,
            statement.PeriodEnd,
            statement.StatementDate,
            statement.PaymentDueDate,
            statement.OpeningBalance,
            statement.ClosingBalance,
            statement.MinimumDue,
            statement.TotalDue,
            parsed.Transactions.Count,
            imported,
            skipped,
            upload.DurationMs,
            warnings,
            null,
            null);
    }

    private async Task<(int Imported, int Skipped)> ImportTransactionsAsync(
        CreditCardStatement statement,
        ParsedStatement parsed,
        Bank bank,
        CreditCard? creditCard,
        BankAccount? bankAccount,
        CancellationToken cancellationToken)
    {
        if (parsed.Transactions.Count == 0)
        {
            return (0, 0);
        }

        var categories = await context.Categories
            .AsNoTracking()
            .Where(c => c.IsActive && c.MatchKeywords != null && c.MatchKeywords != "")
            .Select(c => new { c.Id, c.MatchKeywords })
            .ToListAsync(cancellationToken);

        var imported = 0;
        var skipped = 0;
        var seenHashes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var line in parsed.Transactions)
        {
            var transaction = Transaction.Create(
                line.TransactionDate,
                line.Amount,
                line.Direction,
                line.TransactionType,
                line.Description,
                parsed.Currency);

            transaction.SetPostingDate(line.PostingDate ?? line.TransactionDate);
            transaction.SetSource(TransactionSource.StatementImport);
            transaction.SetInstrument(bank.Id, bankAccount?.Id, creditCard?.Id);
            transaction.SetStatement(statement.Id);
            transaction.SetReference(line.ReferenceNumber, line.MerchantRawText, line.Location);

            if (!seenHashes.Add(transaction.DedupeHash))
            {
                skipped++;
                continue;
            }

            if (await context.Transactions.AnyAsync(t => t.DedupeHash == transaction.DedupeHash, cancellationToken))
            {
                skipped++;
                continue;
            }

            var vendor = await vendorRepository.MatchByNarrationAsync(line.Description, cancellationToken);
            if (vendor is not null)
            {
                transaction.AssignVendor(vendor.Id);

                if (vendor.DefaultCategoryId.HasValue)
                {
                    transaction.AssignCategory(vendor.DefaultCategoryId);
                }
            }

            if (transaction.CategoryId is null)
            {
                var upper = line.Description.ToUpperInvariant();
                var categoryId = categories
                    .Where(c => c.MatchKeywords!
                        .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Any(token => upper.Contains(token, StringComparison.Ordinal)))
                    .Select(c => (Guid?)c.Id)
                    .FirstOrDefault();

                transaction.AssignCategory(categoryId);
            }

            var person = await personRepository.MatchByKeywordAsync(line.Description, cancellationToken);
            if (person is not null)
            {
                transaction.AssignPerson(person.Id);
            }

            statement.AddTransaction(transaction);
            imported++;
        }

        return (imported, skipped);
    }

    private async Task<Bank> ResolveBankAsync(BankCode code, CancellationToken cancellationToken)
    {
        var bank = await bankRepository.FindByCodeAsync(code, cancellationToken);
        if (bank is not null)
        {
            return bank;
        }

        bank = await bankRepository.FindByCodeAsync(BankCode.Other, cancellationToken);
        if (bank is not null)
        {
            return bank;
        }

        var created = Bank.Create(code.ToString(), code.ToString(), code);
        await bankRepository.AddAsync(created, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return created;
    }

    private async Task<CreditCard> ResolveCreditCardAsync(
        Bank bank,
        string last4,
        ParsedStatement parsed,
        CancellationToken cancellationToken)
    {
        var card = await creditCardRepository.FindByLast4Async(bank.Id, last4, cancellationToken);
        if (card is not null)
        {
            return card;
        }

        card = CreditCard.Create(
            bank.Id,
            last4,
            parsed.CardHolderName ?? parsed.CustomerName ?? "Unknown",
            CardNetwork.Unknown,
            $"{bank.ShortName} ****{last4}",
            null,
            parsed.CreditLimit);

        await creditCardRepository.AddAsync(card, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return card;
    }

    private async Task<BankAccount> ResolveBankAccountAsync(
        Bank bank,
        string last4,
        ParsedStatement parsed,
        CancellationToken cancellationToken)
    {
        var account = await bankAccountRepository.FindByLast4Async(bank.Id, last4, cancellationToken);
        if (account is not null)
        {
            return account;
        }

        account = BankAccount.Create(
            bank.Id,
            last4,
            parsed.CustomerName ?? parsed.CardHolderName ?? "Unknown",
            AccountType.Savings,
            $"{bank.ShortName} ****{last4}");

        await bankAccountRepository.AddAsync(account, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return account;
    }

    private static StatementProcessingResultDto Failure(UploadHistory upload, string code, string message) =>
        new(upload.Id, null, upload.Status.ToString(), null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null, null, 0, 0, 0, upload.DurationMs, [], code, message);
}
