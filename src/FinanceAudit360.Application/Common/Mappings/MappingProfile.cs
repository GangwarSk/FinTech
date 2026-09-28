using AutoMapper;
using FinanceAudit360.Contracts.Masters;
using FinanceAudit360.Contracts.Persons;
using FinanceAudit360.Contracts.Reports;
using FinanceAudit360.Contracts.Statements;
using FinanceAudit360.Domain.Entities;
using FinanceAudit360.Domain.ValueObjects;

namespace FinanceAudit360.Application.Common.Mappings;

/// <summary>
/// AutoMapper is used for the straight entity-to-DTO shapes. Grid projections that need
/// aggregate counts are hand-written in the feature repositories so they stay a single SQL query.
/// </summary>
public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Address, AddressDto>().ReverseMap();

        CreateMap<Person, PersonDto>()
            .ForCtorParam(nameof(PersonDto.LedgerEntryCount), o => o.MapFrom(s => s.LedgerEntries.Count));

        CreateMap<MoneyLedger, LedgerEntryDto>()
            .ForCtorParam(nameof(LedgerEntryDto.EntryType), o => o.MapFrom(s => s.EntryType.ToString()))
            .ForCtorParam(nameof(LedgerEntryDto.EntryTypeValue), o => o.MapFrom(s => (int)s.EntryType))
            .ForCtorParam(nameof(LedgerEntryDto.BankAccountName), o => o.MapFrom(s => s.BankAccount != null ? s.BankAccount.Nickname : null))
            .ForCtorParam(nameof(LedgerEntryDto.CreditCardName), o => o.MapFrom(s => s.CreditCard != null ? s.CreditCard.Nickname : null));

        CreateMap<Setting, SettingDto>()
            .ForCtorParam(nameof(SettingDto.DataType), o => o.MapFrom(s => (int)s.DataType))
            .ForCtorParam(nameof(SettingDto.DataTypeName), o => o.MapFrom(s => s.DataType.ToString()));

        CreateMap<UploadHistory, UploadHistoryDto>()
            .ForCtorParam(nameof(UploadHistoryDto.Status), o => o.MapFrom(s => s.Status.ToString()))
            .ForCtorParam(nameof(UploadHistoryDto.DetectedBank), o => o.MapFrom(s => s.DetectedBank.ToString()))
            .ForCtorParam(nameof(UploadHistoryDto.DetectedKind), o => o.MapFrom(s => s.DetectedKind.ToString()));

        CreateMap<AuditLog, AuditLogDto>()
            .ForCtorParam(nameof(AuditLogDto.Action), o => o.MapFrom(s => s.Action.ToString()));
    }
}
