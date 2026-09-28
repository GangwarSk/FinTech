using System.Text;
using FinanceAudit360.Application.Common.Interfaces;
using FinanceAudit360.Infrastructure.Identity;
using FinanceAudit360.Infrastructure.Jobs;
using FinanceAudit360.Infrastructure.Options;
using FinanceAudit360.Infrastructure.Pdf;
using FinanceAudit360.Infrastructure.Pdf.Ocr;
using FinanceAudit360.Infrastructure.Pdf.Parsers;
using FinanceAudit360.Infrastructure.Pdf.Pipeline;
using FinanceAudit360.Infrastructure.Pdf.Pipeline.Validation;
using FinanceAudit360.Infrastructure.Services;
using FinanceAudit360.Shared.Constants;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Quartz;

namespace FinanceAudit360.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureLayer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.Configure<SecurityOptions>(configuration.GetSection(SecurityOptions.SectionName));

        services.AddHttpContextAccessor();

        services.AddScoped<ICurrentUser, CurrentUserService>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();

        services.Configure<OcrOptions>(configuration.GetSection(OcrOptions.SectionName));
        services.AddMemoryCache();
        services.AddSingleton<IPdfOcrEngine, TesseractPdfOcrEngine>();
        services.AddScoped<IPdfTextExtractor, PdfTextExtractor>();
        services.AddScoped<IStatementImportService, StatementImportService>();

        services.AddScoped<IPdfParser, HdfcPdfParser>();
        services.AddScoped<IPdfParser, IciciPdfParser>();
        services.AddScoped<IPdfParser, CitiPdfParser>();
        services.AddScoped<IPdfParser, SbiPdfParser>();
        services.AddScoped<IPdfParser, AxisPdfParser>();
        services.AddScoped<IPdfParser, KotakPdfParser>();
        services.AddScoped<IPdfParser, IndusIndPdfParser>();
        services.AddScoped<IPdfParser, IdfcFirstPdfParser>();
        services.AddScoped<IPdfParser, SouthIndianBankPdfParser>();
        services.AddScoped<IPdfParser, UniCardPdfParser>();
        services.AddScoped<IPdfParser, SlicePdfParser>();
        services.AddScoped<IPdfParser, PunjabNationalBankPdfParser>();
        services.AddScoped<IPdfParser, UttarPradeshGraminBankPdfParser>();
        services.AddScoped<IPdfParser, AirtelPaymentsBankPdfParser>();
        services.AddScoped<IPdfParser, PaytmPostPaidPdfParser>();
        services.AddScoped<IPdfParser, StandardCharteredPdfParser>();
        services.AddScoped<IPdfParser, AmexPdfParser>();
        services.AddScoped<IPdfParser, GenericPdfParser>();
        services.AddScoped<IPdfParserFactory, PdfParserFactory>();
        services.AddScoped<IStatementValidationRule, BalanceRowFilterRule>();
        services.AddScoped<IStatementValidationRule, RunningBalanceDirectionRule>();
        services.AddScoped<IStatementValidationRule, ReconciliationRule>();
        services.AddScoped<IStatementParsingPipeline, StatementParsingPipeline>();

        services.AddJwtAuthentication(configuration);
        services.AddQuartzScheduling();

        return services;
    }

    private static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(jwt.SigningKey) || Encoding.UTF8.GetByteCount(jwt.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey must be configured with at least 32 bytes. Use user secrets or an environment variable.");
        }

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.SaveToken = false;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
                    NameClaimType = System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName,
                    RoleClaimType = System.Security.Claims.ClaimTypes.Role
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Roles.Administrator, policy => policy.RequireRole(Roles.Administrator))
            .AddDefaultPolicy("default", policy => policy.RequireAuthenticatedUser());

        // One policy per permission keeps endpoint declarations declarative and additive.
        foreach (var permission in Permissions.All)
        {
            services.AddAuthorizationBuilder()
                .AddPolicy(permission, policy => policy.RequireAssertion(ctx =>
                    ctx.User.HasClaim(AuthClaims.Permission, permission) ||
                    ctx.User.IsInRole(Roles.Administrator)));
        }

        return services;
    }

    private static IServiceCollection AddQuartzScheduling(this IServiceCollection services)
    {
        services.AddQuartz(quartz =>
        {
            quartz.AddJob<StatementRetentionJob>(job => job.WithIdentity(StatementRetentionJob.JobKey));
            quartz.AddTrigger(trigger => trigger
                .ForJob(StatementRetentionJob.JobKey)
                .WithIdentity($"{StatementRetentionJob.JobKey}-trigger")
                .WithCronSchedule("0 30 2 * * ?"));

            quartz.AddJob<RecalculateBalancesJob>(job => job.WithIdentity(RecalculateBalancesJob.JobKey));
            quartz.AddTrigger(trigger => trigger
                .ForJob(RecalculateBalancesJob.JobKey)
                .WithIdentity($"{RecalculateBalancesJob.JobKey}-trigger")
                .WithCronSchedule("0 0 * * * ?"));

            quartz.AddJob<AuditLogCleanupJob>(job => job.WithIdentity(AuditLogCleanupJob.JobKey));
            quartz.AddTrigger(trigger => trigger
                .ForJob(AuditLogCleanupJob.JobKey)
                .WithIdentity($"{AuditLogCleanupJob.JobKey}-trigger")
                .WithCronSchedule("0 0 3 ? * SUN"));
        });

        services.AddQuartzHostedService(options => options.WaitForJobsToComplete = true);

        return services;
    }
}
