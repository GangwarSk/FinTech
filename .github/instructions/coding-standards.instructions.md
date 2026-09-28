---
description: "Use when: writing or reviewing C#, TypeScript, Angular, React, SQL, or tests - toolchain, C# rules, Result pattern, Minimal API template, data access, security, frontend, testing, git conventions."
applyTo: "**/*.cs,**/*.csproj,**/*.ts,**/*.tsx,**/*.html,**/*.scss,**/*.css,**/*.sql"
---

# Coding Standards

> Mandatory for all developers, AI agents, and reviewers. Layout: [FolderStructureStandards](folder-structure-standards.instructions.md). Architecture: [ArchitectureStandards](architecture-standards.instructions.md).

---

## 1. Core Principles

SOLID · DRY (for knowledge, not for coincidental code) · KISS · YAGNI · Fail fast · Explicit over magic · Composition over inheritance · Make illegal states unrepresentable.

## 2. Baseline Toolchain

| Area | Standard |
|---|---|
| Runtime | .NET 10 (LTS), C# 14, `global.json` pinned |
| Build | `Directory.Build.props`: `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `<AnalysisLevel>latest-recommended</AnalysisLevel>`, `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` |
| Packages | Central Package Management (`Directory.Packages.props`); no floating versions |
| Solution | `.slnx` format |
| Analyzers | Built-in .NET analyzers, `Meziantou.Analyzer`, `SonarAnalyzer.CSharp` |
| Format | `dotnet format` clean; `.editorconfig` committed |
| Frontend | TypeScript `strict: true`, ESLint (flat config) + Prettier, no `any` |

## 3. C# Rules

- File-scoped namespaces; one public type per file; file name == type name.
- `sealed` by default; `internal` by default outside public contracts.
- Primary constructors for DI; `required` / `init` for DTOs.
- DTOs and messages are `sealed record`.
- `async` all the way; every async method accepts and forwards `CancellationToken`; suffix `Async`.
- No `.Result`, `.Wait()`, `async void` (except event handlers).
- Use `TimeProvider`, `IOptions<T>`, `ILogger<T>` – no statics for time, config, or logging.
- Collections: return `IReadOnlyList<T>` / `IReadOnlyCollection<T>`; accept the narrowest interface.
- Pattern matching and switch expressions over if-chains; guard clauses over nesting (max nesting depth 3).
- Methods ≤ 30 lines, classes ≤ 300 lines, cyclomatic complexity ≤ 10.
- No magic numbers/strings – constants, enums, or configurable rules.
- Logging: structured templates (`logger.LogInformation("Leave {LeaveId} approved", id)`), prefer `[LoggerMessage]` source-gen on hot paths; never log secrets/PII.
- Nullability warnings are errors; no `!` suppression without a comment.

## 4. Result Pattern

```csharp
public sealed record Error(string Code, string Message, ErrorType Type);

public static class EmployeeErrors
{
    public static readonly Error NotFound = new("Employee.NotFound", "Employee was not found.", ErrorType.NotFound);
}

// Handler
if (employee is null) return Result.Failure<EmployeeResponse>(EmployeeErrors.NotFound);

// Endpoint
return result.Match(TypedResults.Ok, ProblemDetailsMapper.ToProblem);
```
- Error codes: `<Aggregate>.<Reason>`; `ErrorType` → HTTP status mapping lives only in Host.

## 5. Minimal API Endpoint Template

```csharp
internal sealed class EmployeeEndpoints : IEndpointGroup
{
    public void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v{version:apiVersion}/employees")
            .WithTags("Employees")
            .RequireAuthorization();

        group.MapGet("/{id:guid}", GetById)
            .WithName("GetEmployeeById")
            .RequireAuthorization(Permissions.Employees.Read)
            .Produces<EmployeeResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static async Task<Results<Ok<EmployeeResponse>, ProblemHttpResult>> GetById(
        Guid id, IQueryHandler<GetEmployeeByIdQuery, EmployeeResponse> handler, CancellationToken ct)
        => (await handler.HandleAsync(new GetEmployeeByIdQuery(id), ct))
            .Match(r => TypedResults.Ok(r), ProblemDetailsMapper.ToProblem);
}
```

## 6. Data Access Rules

- Never concatenate SQL. Dapper: parameters only. EF: `FromSql` (interpolated, parameterized) – never `FromSqlRaw` with user input.
- Reads: `AsNoTracking()` + `Select` projection to DTO; never load entities to map afterwards.
- Always paginate list endpoints (`PagedRequest` max page size 100).
- Avoid N+1: projections or `AsSplitQuery()` for multi-collection includes.
- `decimal(18,2)` or explicit precision for money; `DateTimeOffset` for timestamps; `DateOnly` for dates.
- Concurrency tokens (`rowversion` / `xmin`) on aggregates edited by multiple users.

## 7. Security Rules (non-negotiable)

- Every endpoint `RequireAuthorization(...)` unless explicitly `AllowAnonymous()` with justification comment.
- Validate all input at the boundary (FluentValidation) and invariants in domain.
- No secrets in code, config files, or logs. Use user-secrets locally, Key Vault in cloud.
- Output encoding in frontend; never `innerHTML` / `dangerouslySetInnerHTML` with untrusted data.
- Tokens: access token in memory, refresh via HttpOnly Secure SameSite cookie (BFF preferred). Never `localStorage`.
- OWASP Top 10 + ASVS L2 as the baseline. See [SecurityAgent](../agents/SecurityAgent.agent.md).

## 8. Frontend Rules (shared)

- Feature-first folders; smart (container) vs presentational components.
- No business logic in templates/JSX; derive state, don't duplicate it.
- API access only through generated clients + a thin feature data layer.
- Accessibility WCAG 2.2 AA: semantic HTML, labels, keyboard nav, focus management, color contrast.
- i18n-ready: no hard-coded user-facing strings in components.
- Performance budgets: initial JS ≤ 250 KB gzip, LCP ≤ 2.5 s, CLS ≤ 0.1, INP ≤ 200 ms.

## 9. Testing Rules

- Test pyramid: unit (domain + application) > integration (Testcontainers + WebApplicationFactory) > E2E (Playwright, critical journeys).
- Naming: `Method_Scenario_ExpectedResult`; AAA structure.
- Coverage gates: Domain ≥ 90 %, Application ≥ 85 %, overall ≥ 80 %; mutation score (Stryker) ≥ 70 % for Domain.
- No test depends on another test, wall clock, or network (except Testcontainers).

## 10. Git & Review

- Conventional Commits (`feat(employees): add promotion workflow`).
- Small PRs (< 400 changed lines where possible), each with tests and updated docs.
- Definition of Done: builds with zero warnings, all tests green, architecture tests green, OpenAPI updated, docs updated, security checklist passed.

## 11. Comments

- Comment **why**, not what. Public contracts in `Shared.Dtos` and `Abstractions` get XML docs (feeds OpenAPI).
- No commented-out code; no TODO without a tracked issue id.
