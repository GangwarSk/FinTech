using FinanceAudit360.Application.Features.Masters.Accounts;
using FinanceAudit360.Application.Features.Masters.Banks;
using FinanceAudit360.Application.Features.Masters.Cards;
using FinanceAudit360.Application.Features.Masters.Categories;
using FinanceAudit360.Application.Features.Masters.Settings;
using FinanceAudit360.Application.Features.Masters.Users;
using FinanceAudit360.Application.Features.Masters.Vendors;
using FinanceAudit360.Contracts.Masters;
using FinanceAudit360.Shared.Constants;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FinanceAudit360.Api.Endpoints;

public static class MasterEndpoints
{
    public static IEndpointRouteBuilder MapMasterEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapBankEndpoints();
        app.MapBankAccountEndpoints();
        app.MapCreditCardEndpoints();
        app.MapCategoryEndpoints();
        app.MapVendorEndpoints();
        app.MapUserEndpoints();
        app.MapRoleEndpoints();
        app.MapSettingEndpoints();
        app.MapLookupEndpoints();

        return app;
    }

    private static void MapBankEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/masters/banks").WithTags("Masters - Banks");

        group.MapPost("/search", async ([FromBody] MasterFilterRequest filter, ISender sender, CancellationToken ct) =>
                ApiResults.Paged(await sender.Send(new SearchBanksQuery(filter), ct)))
            .RequireAuthorization(Permissions.MastersRead).WithName("SearchBanks");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                ApiResults.Ok(await sender.Send(new GetBankByIdQuery(id), ct)))
            .RequireAuthorization(Permissions.MastersRead).WithName("GetBankById");

        group.MapPost("/", async ([FromBody] SaveBankRequest request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateBankCommand(request), ct);
                return ApiResults.Created($"/api/masters/banks/{result.Id}", result);
            })
            .RequireAuthorization(Permissions.MastersWrite).WithName("CreateBank");

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] SaveBankRequest request, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new UpdateBankCommand(id, request), ct);
                return ApiResults.NoContentEnvelope("Bank updated.");
            })
            .RequireAuthorization(Permissions.MastersWrite).WithName("UpdateBank");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new DeleteBankCommand(id), ct);
                return ApiResults.NoContentEnvelope("Bank deleted.");
            })
            .RequireAuthorization(Permissions.MastersWrite).WithName("DeleteBank");
    }

    private static void MapBankAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/masters/accounts").WithTags("Masters - Bank Accounts");

        group.MapPost("/search", async ([FromBody] MasterFilterRequest filter, ISender sender, CancellationToken ct) =>
                ApiResults.Paged(await sender.Send(new SearchBankAccountsQuery(filter), ct)))
            .RequireAuthorization(Permissions.MastersRead).WithName("SearchBankAccounts");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                ApiResults.Ok(await sender.Send(new GetBankAccountByIdQuery(id), ct)))
            .RequireAuthorization(Permissions.MastersRead).WithName("GetBankAccountById");

        group.MapPost("/", async ([FromBody] SaveBankAccountRequest request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateBankAccountCommand(request), ct);
                return ApiResults.Created($"/api/masters/accounts/{result.Id}", result);
            })
            .RequireAuthorization(Permissions.MastersWrite).WithName("CreateBankAccount");

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] SaveBankAccountRequest request, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new UpdateBankAccountCommand(id, request), ct);
                return ApiResults.NoContentEnvelope("Account updated.");
            })
            .RequireAuthorization(Permissions.MastersWrite).WithName("UpdateBankAccount");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new DeleteBankAccountCommand(id), ct);
                return ApiResults.NoContentEnvelope("Account deleted.");
            })
            .RequireAuthorization(Permissions.MastersWrite).WithName("DeleteBankAccount");
    }

    private static void MapCreditCardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/masters/cards").WithTags("Masters - Credit Cards");

        group.MapPost("/search", async ([FromBody] MasterFilterRequest filter, ISender sender, CancellationToken ct) =>
                ApiResults.Paged(await sender.Send(new SearchCreditCardsQuery(filter), ct)))
            .RequireAuthorization(Permissions.MastersRead).WithName("SearchCreditCards");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                ApiResults.Ok(await sender.Send(new GetCreditCardByIdQuery(id), ct)))
            .RequireAuthorization(Permissions.MastersRead).WithName("GetCreditCardById");

        group.MapPost("/", async ([FromBody] SaveCreditCardRequest request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateCreditCardCommand(request), ct);
                return ApiResults.Created($"/api/masters/cards/{result.Id}", result);
            })
            .RequireAuthorization(Permissions.MastersWrite).WithName("CreateCreditCard");

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] SaveCreditCardRequest request, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new UpdateCreditCardCommand(id, request), ct);
                return ApiResults.NoContentEnvelope("Card updated.");
            })
            .RequireAuthorization(Permissions.MastersWrite).WithName("UpdateCreditCard");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new DeleteCreditCardCommand(id), ct);
                return ApiResults.NoContentEnvelope("Card deleted.");
            })
            .RequireAuthorization(Permissions.MastersWrite).WithName("DeleteCreditCard");
    }

    private static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/masters/categories").WithTags("Masters - Categories");

        group.MapPost("/search", async ([FromBody] MasterFilterRequest filter, ISender sender, CancellationToken ct) =>
                ApiResults.Paged(await sender.Send(new SearchCategoriesQuery(filter), ct)))
            .RequireAuthorization(Permissions.MastersRead).WithName("SearchCategories");

        group.MapPost("/", async ([FromBody] SaveCategoryRequest request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateCategoryCommand(request), ct);
                return ApiResults.Created($"/api/masters/categories/{result.Id}", result);
            })
            .RequireAuthorization(Permissions.MastersWrite).WithName("CreateCategory");

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] SaveCategoryRequest request, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new UpdateCategoryCommand(id, request), ct);
                return ApiResults.NoContentEnvelope("Category updated.");
            })
            .RequireAuthorization(Permissions.MastersWrite).WithName("UpdateCategory");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new DeleteCategoryCommand(id), ct);
                return ApiResults.NoContentEnvelope("Category deleted.");
            })
            .RequireAuthorization(Permissions.MastersWrite).WithName("DeleteCategory");
    }

    private static void MapVendorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/masters/vendors").WithTags("Masters - Vendors");

        group.MapPost("/search", async ([FromBody] MasterFilterRequest filter, ISender sender, CancellationToken ct) =>
                ApiResults.Paged(await sender.Send(new SearchVendorsQuery(filter), ct)))
            .RequireAuthorization(Permissions.MastersRead).WithName("SearchVendors");

        group.MapPost("/", async ([FromBody] SaveVendorRequest request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateVendorCommand(request), ct);
                return ApiResults.Created($"/api/masters/vendors/{result.Id}", result);
            })
            .RequireAuthorization(Permissions.MastersWrite).WithName("CreateVendor");

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] SaveVendorRequest request, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new UpdateVendorCommand(id, request), ct);
                return ApiResults.NoContentEnvelope("Vendor updated.");
            })
            .RequireAuthorization(Permissions.MastersWrite).WithName("UpdateVendor");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new DeleteVendorCommand(id), ct);
                return ApiResults.NoContentEnvelope("Vendor deleted.");
            })
            .RequireAuthorization(Permissions.MastersWrite).WithName("DeleteVendor");
    }

    private static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/masters/users").WithTags("Masters - Users")
            .RequireAuthorization(Permissions.UsersManage);

        group.MapPost("/search", async ([FromBody] MasterFilterRequest filter, ISender sender, CancellationToken ct) =>
                ApiResults.Paged(await sender.Send(new SearchUsersQuery(filter), ct)))
            .WithName("SearchUsers");

        group.MapGet("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
                ApiResults.Ok(await sender.Send(new GetUserByIdQuery(id), ct)))
            .WithName("GetUserById");

        group.MapPost("/", async ([FromBody] CreateUserRequest request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateUserCommand(request), ct);
                return ApiResults.Created($"/api/masters/users/{result.Id}", result);
            })
            .WithName("CreateUser");

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] UpdateUserRequest request, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new UpdateUserCommand(id, request), ct);
                return ApiResults.NoContentEnvelope("User updated.");
            })
            .WithName("UpdateUser");

        group.MapPost("/{id:guid}/reset-password", async (Guid id, [FromBody] ResetPasswordRequest request, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new ResetUserPasswordCommand(id, request), ct);
                return ApiResults.NoContentEnvelope("Password reset.");
            })
            .WithName("ResetUserPassword");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new DeleteUserCommand(id), ct);
                return ApiResults.NoContentEnvelope("User deactivated.");
            })
            .WithName("DeleteUser");
    }

    private static void MapRoleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/masters/roles").WithTags("Masters - Roles")
            .RequireAuthorization(Permissions.UsersManage);

        group.MapGet("/", async (ISender sender, CancellationToken ct) =>
                ApiResults.Ok(await sender.Send(new SearchRolesQuery(), ct)))
            .WithName("GetRoles");

        group.MapGet("/permissions", async (ISender sender, CancellationToken ct) =>
                ApiResults.Ok(await sender.Send(new GetPermissionCatalogQuery(), ct)))
            .WithName("GetPermissionCatalog");

        group.MapPost("/", async ([FromBody] SaveRoleRequest request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateRoleCommand(request), ct);
                return ApiResults.Created($"/api/masters/roles/{result.Id}", result);
            })
            .WithName("CreateRole");

        group.MapPut("/{id:guid}", async (Guid id, [FromBody] SaveRoleRequest request, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new UpdateRoleCommand(id, request), ct);
                return ApiResults.NoContentEnvelope("Role updated.");
            })
            .WithName("UpdateRole");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new DeleteRoleCommand(id), ct);
                return ApiResults.NoContentEnvelope("Role deleted.");
            })
            .WithName("DeleteRole");
    }

    private static void MapSettingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/masters/settings").WithTags("Masters - Settings");

        group.MapGet("/", async ([FromQuery] string? category, ISender sender, CancellationToken ct) =>
                ApiResults.Ok(await sender.Send(new GetSettingsQuery(category), ct)))
            .RequireAuthorization(Permissions.MastersRead).WithName("GetSettings");

        group.MapPost("/", async ([FromBody] SaveSettingRequest request, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new CreateSettingCommand(request), ct);
                return ApiResults.Created($"/api/masters/settings/{result.Id}", result);
            })
            .RequireAuthorization(Permissions.SettingsManage).WithName("CreateSetting");

        group.MapPut("/{key}", async (string key, [FromBody] UpdateSettingValueRequest request, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new UpdateSettingValueCommand(key, request), ct);
                return ApiResults.NoContentEnvelope("Setting updated.");
            })
            .RequireAuthorization(Permissions.SettingsManage).WithName("UpdateSetting");

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
            {
                await sender.Send(new DeleteSettingCommand(id), ct);
                return ApiResults.NoContentEnvelope("Setting deleted.");
            })
            .RequireAuthorization(Permissions.SettingsManage).WithName("DeleteSetting");
    }

    /// <summary>Small, cacheable lists that power the filter panels and dropdowns.</summary>
    private static void MapLookupEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lookups").WithTags("Lookups")
            .RequireAuthorization(Permissions.MastersRead);

        group.MapGet("/banks", async (ISender sender, CancellationToken ct) =>
                ApiResults.Ok(await sender.Send(new GetBankLookupQuery(), ct)))
            .WithName("GetBankLookup");

        group.MapGet("/accounts", async (ISender sender, CancellationToken ct) =>
                ApiResults.Ok(await sender.Send(new GetBankAccountLookupQuery(), ct)))
            .WithName("GetAccountLookup");

        group.MapGet("/cards", async (ISender sender, CancellationToken ct) =>
                ApiResults.Ok(await sender.Send(new GetCreditCardLookupQuery(), ct)))
            .WithName("GetCardLookup");

        group.MapGet("/categories", async (ISender sender, CancellationToken ct) =>
                ApiResults.Ok(await sender.Send(new GetCategoryLookupQuery(), ct)))
            .WithName("GetCategoryLookup");

        group.MapGet("/vendors", async (ISender sender, CancellationToken ct) =>
                ApiResults.Ok(await sender.Send(new GetVendorLookupQuery(), ct)))
            .WithName("GetVendorLookup");

        // Cascades from bank, then year: the client filters files down before the list gets long.
        group.MapGet("/statement-files", async (
                    Guid? bankId,
                    int? year,
                    ISender sender,
                    CancellationToken ct) =>
                ApiResults.Ok(await sender.Send(
                    new Application.Features.Statements.GetStatementFileLookupQuery(bankId, year), ct)))
            .WithName("GetStatementFileLookup")
            .WithSummary("Uploaded files that produced transactions, labelled with their statement period.");

        group.MapGet("/enums", () => ApiResults.Ok(EnumCatalog.Build()))
            .WithName("GetEnumCatalog")
            .WithSummary("Every enum the UI renders as a dropdown, in one call.");
    }
}
