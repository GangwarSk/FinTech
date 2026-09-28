# Build and publish the API. Run from the repository root:
#   docker build -f src/FinanceAudit360.Api/Dockerfile -t financeaudit360-api .
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the layer is cached until a project or package file changes.
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/FinanceAudit360.Shared/FinanceAudit360.Shared.csproj src/FinanceAudit360.Shared/
COPY src/FinanceAudit360.Contracts/FinanceAudit360.Contracts.csproj src/FinanceAudit360.Contracts/
COPY src/FinanceAudit360.Domain/FinanceAudit360.Domain.csproj src/FinanceAudit360.Domain/
COPY src/FinanceAudit360.Application/FinanceAudit360.Application.csproj src/FinanceAudit360.Application/
COPY src/FinanceAudit360.Persistence/FinanceAudit360.Persistence.csproj src/FinanceAudit360.Persistence/
COPY src/FinanceAudit360.Infrastructure/FinanceAudit360.Infrastructure.csproj src/FinanceAudit360.Infrastructure/
COPY src/FinanceAudit360.Api/FinanceAudit360.Api.csproj src/FinanceAudit360.Api/
RUN dotnet restore src/FinanceAudit360.Api/FinanceAudit360.Api.csproj

COPY src/ src/
RUN dotnet publish src/FinanceAudit360.Api/FinanceAudit360.Api.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Storage and logs are bind-mounted in compose so uploads survive a container rebuild.
RUN mkdir -p /app/App_Data/storage /app/App_Data/logs && \
    adduser --disabled-password --gecos "" --uid 10001 appuser && \
    chown -R appuser:appuser /app
USER appuser

HEALTHCHECK --interval=30s --timeout=5s --start-period=40s --retries=3 \
    CMD ["dotnet", "--info"]

ENTRYPOINT ["dotnet", "FinanceAudit360.Api.dll"]
