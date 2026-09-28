using FinanceAudit360.Api.Configuration;
using FinanceAudit360.Api.Endpoints;
using FinanceAudit360.Api.Middleware;
using FinanceAudit360.Application;
using FinanceAudit360.Infrastructure;
using FinanceAudit360.Infrastructure.Options;
using FinanceAudit360.Persistence;
using FinanceAudit360.Persistence.Seed;
using FinanceAudit360.Shared.Constants;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName());

builder.Services.AddApplicationLayer();
builder.Services.AddPersistenceLayer(builder.Configuration);
builder.Services.AddInfrastructureLayer(builder.Configuration);
builder.Services.AddApiLayer(builder.Configuration);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

app.UseExceptionHandler(_ => { });
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "FinanceAudit360 API v1");
        options.DocumentTitle = "FinanceAudit360 API";
    });
}
else
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseCors(AppConstants.CorsPolicyName);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RequestAuditMiddleware>();

app.MapAuthEndpoints();
app.MapDashboardEndpoints();
app.MapTransactionEndpoints();
app.MapPersonEndpoints();
app.MapStatementEndpoints();
app.MapMasterEndpoints();
app.MapReportEndpoints();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription().AllowAnonymous();

await InitializeDatabaseAsync(app);

app.Run();

static async Task InitializeDatabaseAsync(WebApplication app)
{
    if (!app.Configuration.GetValue("Database:AutoMigrateOnStartup", true))
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var initializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    var security = app.Configuration.GetSection(SecurityOptions.SectionName).Get<SecurityOptions>() ?? new SecurityOptions();

    if (string.IsNullOrWhiteSpace(security.SeedAdminPassword))
    {
        throw new InvalidOperationException(
            "Security:SeedAdminPassword must be configured so the bootstrap administrator can be created.");
    }

    await initializer.MigrateAsync();
    await initializer.SeedAsync(security.SeedAdminPassword);
}

/// <summary>Exposed so the integration tests can spin up the real host through WebApplicationFactory.</summary>
public partial class Program;
