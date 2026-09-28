using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceAudit360.Contracts.Auth;
using FinanceAudit360.Contracts.Common;
using FinanceAudit360.Contracts.Transactions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinanceAudit360.Tests.Api;

/// <summary>
/// Boots the real API host against a throwaway LocalDB database so routing, authentication,
/// authorization, validation and the response envelope are all exercised together.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = $"FinanceAudit360_Test_{Guid.NewGuid():N}";

    public const string AdminUserName = "admin";
    public const string AdminPassword = "IntegrationTest#2024";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // Environment variables are the last configuration source WebApplication.CreateBuilder adds,
        // so they reliably override appsettings.Development.json inside the test host.
        foreach (var (key, value) in new Dictionary<string, string>
                 {
                     ["ConnectionStrings__DefaultConnection"] =
                         $"Server=(localdb)\\MSSQLLocalDB;Database={_databaseName};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True",
                     ["Jwt__SigningKey"] = "integration-test-signing-key-at-least-32-bytes-long-0001",
                     ["Security__SeedAdminPassword"] = AdminPassword,
                     ["Security__AuthRateLimitPerMinute"] = "10000",
                     ["Security__GlobalRateLimitPerMinute"] = "100000",
                     ["Security__UploadRateLimitPerMinute"] = "10000",
                     ["Security__EnableRequestAuditLogging"] = "false",
                     ["Database__EnableSensitiveDataLogging"] = "false",
                     ["Serilog__MinimumLevel__Default"] = "Warning"
                 })
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(AdminUserName, AdminPassword));
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(JsonOptions);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", payload!.Data!.AccessToken);

        return client;
    }

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public ValueTask InitializeAsync()
    {
        _ = Services;
        return ValueTask.CompletedTask;
    }

    public override async ValueTask DisposeAsync()
    {
        using (var scope = Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<Persistence.ApplicationDbContext>();
            await context.Database.EnsureDeletedAsync();
        }

        await base.DisposeAsync();
    }
}

/// <summary>
/// One host, one database, shared by every API test class. The factory writes process-wide
/// environment variables, so running several factories in parallel would race on them.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}

[Collection(ApiCollection.Name)]
public class AuthEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(ApiFactory.AdminUserName, ApiFactory.AdminPassword));

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(ApiFactory.JsonOptions);

        Assert.True(payload!.Success);
        Assert.False(string.IsNullOrWhiteSpace(payload.Data!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(payload.Data.RefreshToken));
        Assert.Contains("Administrator", payload.Data.User.Roles);
    }

    [Fact]
    public async Task Login_WithBadPassword_Returns401WithoutLeakingWhetherTheUserExists()
    {
        var client = factory.CreateClient();

        var unknownUser = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("nobody", "Whatever#1234"));
        var badPassword = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(ApiFactory.AdminUserName, "Whatever#1234"));

        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, badPassword.StatusCode);

        var first = await unknownUser.Content.ReadFromJsonAsync<ApiResponse<object>>(ApiFactory.JsonOptions);
        var second = await badPassword.Content.ReadFromJsonAsync<ApiResponse<object>>(ApiFactory.JsonOptions);

        Assert.Equal(first!.Error!.Message, second!.Error!.Message);
    }

    [Fact]
    public async Task Login_WithEmptyPayload_Returns400WithValidationErrors()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(string.Empty, string.Empty));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(ApiFactory.JsonOptions);

        Assert.Equal("validation_failed", payload!.Error!.Code);
        Assert.NotEmpty(payload.Error.ValidationErrors!);
    }

    [Fact]
    public async Task Refresh_RotatesTheTokenAndInvalidatesTheOldOne()
    {
        var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(ApiFactory.AdminUserName, ApiFactory.AdminPassword));
        var tokens = (await login.Content.ReadFromJsonAsync<ApiResponse<AuthResponse>>(ApiFactory.JsonOptions))!.Data!;

        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(tokens.RefreshToken));
        refreshed.EnsureSuccessStatusCode();

        var replayed = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, replayed.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/dashboard/summary?period=Last30Days");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_ReturnsTheAuthenticatedUser()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var payload = await client.GetFromJsonAsync<ApiResponse<CurrentUserDto>>("/api/auth/me", ApiFactory.JsonOptions);

        Assert.Equal(ApiFactory.AdminUserName, payload!.Data!.UserName);
        Assert.NotEmpty(payload.Data.Permissions);
    }

    [Fact]
    public async Task Health_IsAnonymous()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SecurityHeaders_ArePresentOnEveryResponse()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.True(response.Headers.Contains("X-Correlation-Id"));
    }
}

[Collection(ApiCollection.Name)]
public class TransactionEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task CreateThenSearch_RoundTripsTheTransaction()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var create = await client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest(
            new DateTime(2024, 5, 2),
            null,
            1250.75m,
            (int)Domain.Enums.TransactionDirection.Debit,
            (int)Domain.Enums.TransactionType.Debit,
            "API TEST MERCHANT",
            "REFAPITEST01",
            null, null, null, null, null, null, null, "INR"));

        create.EnsureSuccessStatusCode();

        var search = await client.PostAsJsonAsync("/api/transactions/search",
            new TransactionFilterRequest { Keyword = "API TEST MERCHANT" });

        search.EnsureSuccessStatusCode();
        var payload = await search.Content.ReadFromJsonAsync<ApiResponse<TransactionSearchResultDto>>(ApiFactory.JsonOptions);

        Assert.Equal(1, payload!.Data!.TotalCount);
        Assert.Equal(1250.75m, payload.Data.Items[0].Amount);
        Assert.Equal(1250.75m, payload.Data.TotalDebit);
    }

    [Fact]
    public async Task Create_WithInvalidPayload_Returns400()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/transactions", new CreateTransactionRequest(
            new DateTime(2024, 5, 2), null, 0m, 99, 999, string.Empty, null,
            null, null, null, null, null, null, null, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(ApiFactory.JsonOptions);
        Assert.Equal("validation_failed", payload!.Error!.Code);
    }

    [Fact]
    public async Task Search_WithOversizedPageSize_IsClampedNotRejected()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/transactions/search",
            new TransactionFilterRequest { PageSize = 100_000 });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ApiResponse<TransactionSearchResultDto>>(ApiFactory.JsonOptions);

        Assert.Equal(500, payload!.Data!.PageSize);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync($"/api/transactions/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Export_ReturnsCsv()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/transactions/export", new TransactionFilterRequest());

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);

        var csv = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("TransactionDate,PostingDate,Description", csv, StringComparison.Ordinal);
    }
}

[Collection(ApiCollection.Name)]
public class LookupEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Banks_AreSeeded()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var payload = await client.GetFromJsonAsync<ApiResponse<List<LookupDto>>>("/api/lookups/banks", ApiFactory.JsonOptions);

        Assert.True(payload!.Data!.Count >= 8);
        Assert.Contains(payload.Data, b => b.Name.Contains("HDFC", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Categories_AreSeeded()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var payload = await client.GetFromJsonAsync<ApiResponse<List<LookupDto>>>("/api/lookups/categories", ApiFactory.JsonOptions);

        Assert.True(payload!.Data!.Count >= 15);
    }

    [Fact]
    public async Task Dashboard_ReturnsAllKpiCards()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var payload = await client.GetFromJsonAsync<ApiResponse<Contracts.Dashboard.DashboardSummaryDto>>(
            "/api/dashboard/summary?period=Last30Days", ApiFactory.JsonOptions);

        Assert.Equal(11, payload!.Data!.Cards.Count);
        Assert.Contains(payload.Data.Cards, c => c.Key == "outstandingAmount");
    }
}
