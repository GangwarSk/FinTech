using FinanceAudit360.Application.Features.Persons;
using FinanceAudit360.Contracts.Persons;
using FinanceAudit360.Shared.Constants;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FinanceAudit360.Api.Endpoints;

public static class PersonEndpoints
{
    public static IEndpointRouteBuilder MapPersonEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/persons").WithTags("Persons");

        group.MapPost("/search", async (
                [FromBody] PersonFilterRequest filter,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new SearchPersonsQuery(filter), cancellationToken);
                return ApiResults.Paged(result);
            })
            .RequireAuthorization(Permissions.PersonsRead)
            .WithName("SearchPersons");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetPersonByIdQuery(id), cancellationToken);
                return ApiResults.Ok(result);
            })
            .RequireAuthorization(Permissions.PersonsRead)
            .WithName("GetPersonById");

        group.MapGet("/{id:guid}/audit", async (
                Guid id,
                [FromQuery] DateTime? from,
                [FromQuery] DateTime? to,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetPersonAuditQuery(id, from, to), cancellationToken);
                return ApiResults.Ok(result);
            })
            .RequireAuthorization(Permissions.PersonsRead)
            .WithName("GetPersonAudit")
            .WithSummary("Given/taken totals, balance, recent entries and the monthly timeline.");

        group.MapPost("/{id:guid}/ledger/search", async (
                Guid id,
                [FromBody] PersonLedgerFilterRequest filter,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new GetPersonLedgerQuery(id, filter), cancellationToken);
                return ApiResults.Paged(result);
            })
            .RequireAuthorization(Permissions.PersonsRead)
            .WithName("SearchPersonLedger");

        group.MapPost("/", async (
                [FromBody] CreatePersonRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new CreatePersonCommand(request), cancellationToken);
                return ApiResults.Created($"/api/persons/{result.Id}", result);
            })
            .RequireAuthorization(Permissions.PersonsWrite)
            .WithName("CreatePerson");

        group.MapPut("/{id:guid}", async (
                Guid id,
                [FromBody] UpdatePersonRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                await sender.Send(new UpdatePersonCommand(id, request), cancellationToken);
                return ApiResults.NoContentEnvelope("Person updated.");
            })
            .RequireAuthorization(Permissions.PersonsWrite)
            .WithName("UpdatePerson");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new DeletePersonCommand(id), cancellationToken);
                return ApiResults.NoContentEnvelope("Person deleted.");
            })
            .RequireAuthorization(Permissions.PersonsWrite)
            .WithName("DeletePerson");

        group.MapPost("/{id:guid}/ledger", async (
                Guid id,
                [FromBody] CreateLedgerEntryRequest request,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new AddLedgerEntryCommand(id, request), cancellationToken);
                return ApiResults.Created($"/api/persons/{id}/ledger/{result.Id}", result);
            })
            .RequireAuthorization(Permissions.PersonsWrite)
            .WithName("AddLedgerEntry");

        group.MapDelete("/{id:guid}/ledger/{entryId:guid}", async (
                Guid id,
                Guid entryId,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                await sender.Send(new DeleteLedgerEntryCommand(id, entryId), cancellationToken);
                return ApiResults.NoContentEnvelope("Ledger entry removed.");
            })
            .RequireAuthorization(Permissions.PersonsWrite)
            .WithName("DeleteLedgerEntry");

        group.MapPost("/{id:guid}/ledger/{entryId:guid}/settle", async (
                Guid id,
                Guid entryId,
                [FromQuery] DateTime? settledOn,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                await sender.Send(new SettleLedgerEntryCommand(id, entryId, settledOn ?? DateTime.UtcNow), cancellationToken);
                return ApiResults.NoContentEnvelope("Ledger entry settled.");
            })
            .RequireAuthorization(Permissions.PersonsWrite)
            .WithName("SettleLedgerEntry");

        return app;
    }
}
