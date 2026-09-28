using FinanceAudit360.Application.Common.Exceptions;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Application.Features.Auth;
using FinanceAudit360.Contracts.Auth;
using FinanceAudit360.Shared.Constants;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FinanceAudit360.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication")
            .RequireRateLimiting(AppConstants.RateLimitPolicyAuth);

        group.MapPost("/login", async ([FromBody] LoginRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new LoginCommand(request.UserNameOrEmail, request.Password), cancellationToken);
                return ApiResults.Ok(result, "Signed in.");
            })
            .AllowAnonymous()
            .WithName("Login")
            .WithSummary("Authenticates a user and returns an access/refresh token pair.");

        group.MapPost("/refresh", async ([FromBody] RefreshTokenRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(new RefreshTokenCommand(request.RefreshToken), cancellationToken);
                return ApiResults.Ok(result, "Token refreshed.");
            })
            .AllowAnonymous()
            .WithName("RefreshToken")
            .WithSummary("Exchanges a refresh token for a new token pair and rotates the old one.");

        group.MapPost("/logout", async ([FromBody] RevokeTokenRequest request, ISender sender, CancellationToken cancellationToken) =>
            {
                await sender.Send(new RevokeTokenCommand(request.RefreshToken), cancellationToken);
                return ApiResults.NoContentEnvelope("Signed out.");
            })
            .RequireAuthorization()
            .WithName("Logout");

        group.MapGet("/me", async (ICurrentUser currentUser, ISender sender, CancellationToken cancellationToken) =>
            {
                var userId = currentUser.UserId ?? throw new UnauthorizedException();
                var result = await sender.Send(new GetCurrentUserQuery(userId), cancellationToken);
                return ApiResults.Ok(result);
            })
            .RequireAuthorization()
            .WithName("GetCurrentUser");

        group.MapPost("/change-password", async (
                [FromBody] ChangePasswordRequest request,
                ICurrentUser currentUser,
                ISender sender,
                CancellationToken cancellationToken) =>
            {
                var userId = currentUser.UserId ?? throw new UnauthorizedException();
                await sender.Send(new ChangePasswordCommand(userId, request.CurrentPassword, request.NewPassword), cancellationToken);
                return ApiResults.NoContentEnvelope("Password changed. Please sign in again.");
            })
            .RequireAuthorization()
            .WithName("ChangePassword");

        return app;
    }
}
