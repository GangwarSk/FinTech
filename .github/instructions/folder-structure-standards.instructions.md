---
description: "Use when: creating projects, files, or folders; scaffolding or migrating a solution; moving code - canonical src/Api, src/Shared, src/Web layout, project reference matrix, per-project folder trees, naming conventions."
applyTo: "src/**,tests/**"
---

# Folder Structure Standards

> **Single source of truth** for solution layout. Every agent MUST read this file before generating, migrating, or moving code.
> `<P>` = PascalCase product name (e.g. `Hrms`, `SchoolErp`). Namespaces mirror project names exactly.

---

## 1. Repository Root

```text
<repo-root>/
├── src/
│   ├── Api/
│   │   ├── <P>.Api.Host/                 # Minimal API composition root (ONLY executable backend project)
│   │   ├── <P>.Api.Application/          # Use cases: commands, queries, handlers, validators, mapping
│   │   ├── <P>.Api.Domain/               # Aggregates, entities, value objects, domain events, domain services
│   │   ├── <P>.Api.Abstractions/         # Ports: repository / service / infrastructure interfaces
│   │   ├── <P>.Api.Repositories/         # Hybrid repositories (EF Core command + EF/Dapper query), UnitOfWork
│   │   └── <P>.Api.Infrastructure/       # DbContext, EF config, migrations, security, caching, messaging, integrations
│   │
│   ├── Shared/
│   │   ├── <P>.Shared.Dtos/              # Public API contracts (requests/responses) – no logic
│   │   ├── <P>.Shared.Utilities/         # Pure, stateless helpers & extensions – no business rules
│   │   └── <P>.Shared.Kernels/           # DDD building blocks: Entity, AggregateRoot, ValueObject, Result, Error
│   │
│   └── Web/
│       └── <P>.Web/                      # Angular OR React SPA (exactly one framework per Web app)
│
├── tests/
│   ├── <P>.Api.UnitTests/
│   ├── <P>.Api.IntegrationTests/
│   ├── <P>.Api.ArchitectureTests/
│   └── <P>.Web.E2E/                      # Playwright
│
├── docs/                                 # ADRs, architecture, API, runbooks (see DocumentationAgent)
├── db/                                   # Migration scripts, seed data, SP inventory (see DatabaseAgent)
├── build/                                # CI/CD pipelines, Dockerfiles, IaC
├── <P>.slnx
├── global.json                           # Pinned .NET SDK
├── Directory.Build.props                 # Nullable, ImplicitUsings, TreatWarningsAsErrors, AnalysisLevel
├── Directory.Packages.props              # Central Package Management – NO versions in .csproj
├── .editorconfig
└── README.md
```

Rules:
- `src/` contains **only** the folders above. No `Modules/`, `Common/`, `Core/`, `Helpers/` at root.
- Tests never live inside `src/`.
- One `.csproj` per folder, folder name == project name == root namespace == assembly name.

---

## 2. Project Reference Matrix (enforced by `<P>.Api.ArchitectureTests`)

| Project ↓ may reference → | Host | Application | Domain | Abstractions | Repositories | Infrastructure | Dtos | Utilities | Kernels |
|---|---|---|---|---|---|---|---|---|---|
| **Api.Host**            | –  | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Api.Application**     | ❌ | –  | ✅ | ✅ | ❌ | ❌ | ✅ | ✅ | ✅ |
| **Api.Domain**          | ❌ | ❌ | –  | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ |
| **Api.Abstractions**    | ❌ | ❌ | ✅ | –  | ❌ | ❌ | ✅ | ❌ | ✅ |
| **Api.Repositories**    | ❌ | ❌ | ✅ | ✅ | –  | ✅ | ✅ | ✅ | ✅ |
| **Api.Infrastructure**  | ❌ | ❌ | ✅ | ✅ | ❌ | –  | ✅ | ✅ | ✅ |
| **Shared.Dtos**         | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | –  | ❌ | ❌ |
| **Shared.Utilities**    | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | –  | ❌ |
| **Shared.Kernels**      | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ | –  |

Hard rules:
- **Domain** references only `Shared.Kernels`. No EF Core, no ASP.NET, no NuGet except analyzers.
- **Application** never references EF Core, Dapper, `DbContext`, `HttpContext`, or `Infrastructure`.
- **Shared.\*** reference nothing inside the solution (Kernels/Dtos/Utilities are leaf projects).
- **Host** is the only composition root; every other project exposes `Add<Layer>(this IServiceCollection, IConfiguration)`.
- Circular references are a build-breaking defect.

---

## 3. `<P>.Api.Host` – Minimal API

```text
<P>.Api.Host/
├── Program.cs                     # < 60 lines: builder → AddXxx() → app.MapXxx() → Run
├── Endpoints/
│   ├── IEndpointGroup.cs          # interface: void Map(IEndpointRouteBuilder app)
│   └── <Feature>/
│       └── <Feature>Endpoints.cs  # MapGroup("/api/v{version:apiVersion}/<feature>") + handlers
├── Middleware/                    # CorrelationId, TenantResolution, RequestLogging
├── ExceptionHandling/             # IExceptionHandler → ProblemDetails (RFC 9457)
├── Filters/                       # IEndpointFilter: validation, idempotency
├── Authentication/                # JWT / OIDC setup
├── Authorization/                 # Policies, permission requirements & handlers
├── OpenApi/                       # Microsoft.AspNetCore.OpenApi transformers + Scalar UI
├── HealthChecks/
├── RateLimiting/
├── Extensions/                    # WebApplication / IServiceCollection wiring only
├── appsettings.json
├── appsettings.Development.json
└── Properties/launchSettings.json
```
- **No Controllers.** Endpoints are static lambdas / static methods delegating to Application handlers.
- Endpoints contain zero business logic: bind → dispatch → map `Result` to `TypedResults`.

## 4. `<P>.Api.Application`

```text
<P>.Api.Application/
├── Abstractions/Messaging/        # ICommand, IQuery, ICommandHandler, IQueryHandler (if in-house)
├── Behaviors/                     # Validation, Logging, Transaction, Performance pipeline behaviors
├── Features/
│   └── <Feature>/                 # e.g. Employees
│       ├── Commands/
│       │   └── Create<Entity>/
│       │       ├── Create<Entity>Command.cs
│       │       ├── Create<Entity>CommandHandler.cs
│       │       └── Create<Entity>CommandValidator.cs
│       ├── Queries/
│       │   └── Get<Entity>ById/
│       │       ├── Get<Entity>ByIdQuery.cs
│       │       └── Get<Entity>ByIdQueryHandler.cs
│       ├── EventHandlers/
│       ├── Mappings/              # Mapperly mappers or explicit extension methods
│       └── Rules/                 # Configurable business rules (from SP extraction)
└── DependencyInjection.cs
```

## 5. `<P>.Api.Domain`

```text
<P>.Api.Domain/
└── <Feature>/                     # one folder per aggregate
    ├── <Aggregate>.cs             # AggregateRoot<TId>, private setters, factory + behavior methods
    ├── <Aggregate>Id.cs           # strongly typed id (readonly record struct)
    ├── Entities/                  # child entities
    ├── ValueObjects/
    ├── Events/                    # <Aggregate><PastTenseVerb>DomainEvent
    ├── Errors/                    # static class <Aggregate>Errors { public static readonly Error NotFound ... }
    ├── Specifications/
    ├── Services/                  # domain services (pure)
    └── Enums/
```

## 6. `<P>.Api.Abstractions`

```text
<P>.Api.Abstractions/
├── Repositories/<Feature>/        # I<Aggregate>Repository, I<Feature>QueryRepository
├── Persistence/                   # IUnitOfWork, ISqlConnectionFactory
├── Services/                      # ICurrentUser, ICurrentTenant, IEmailSender, IFileStorage
├── Caching/                       # ICacheService (wraps HybridCache)
├── Messaging/                     # IEventBus, IOutbox
└── Security/                      # IPermissionService, IPasswordHasher, ITokenService
```
Interfaces only (plus the records they return). No implementations.

## 7. `<P>.Api.Repositories` – Hybrid Repository

```text
<P>.Api.Repositories/
├── Base/
│   ├── RepositoryBase.cs          # internal generic base: GetById, Add, Remove (never exposed as IRepository<T>)
│   └── QueryRepositoryBase.cs     # Dapper / EF AsNoTracking helpers
├── <Feature>/
│   ├── <Aggregate>Repository.cs          # command side – EF Core tracked, aggregate-scoped
│   └── <Feature>QueryRepository.cs       # read side – projections to Shared.Dtos, Dapper for heavy reports
├── Specifications/                # SpecificationEvaluator
├── UnitOfWork.cs
└── DependencyInjection.cs
```

## 8. `<P>.Api.Infrastructure`

```text
<P>.Api.Infrastructure/
├── Persistence/
│   ├── AppDbContext.cs
│   ├── Configurations/<Feature>/<Entity>Configuration.cs   # IEntityTypeConfiguration<T>
│   ├── Interceptors/              # Auditing, SoftDelete, DomainEventsToOutbox, TenantStamp
│   ├── Migrations/
│   ├── Seed/
│   └── SqlConnectionFactory.cs
├── Security/                      # JWT token service, password hashing, permission service
├── Identity/                      # CurrentUser, CurrentTenant (from HttpContext claims)
├── Caching/                       # HybridCache + Redis
├── Messaging/                     # Outbox processor, event bus
├── BackgroundJobs/                # IHostedService / Quartz / Hangfire
├── Storage/                       # Blob / file storage
├── Integrations/<ExternalSystem>/ # Typed HttpClients + Microsoft.Extensions.Http.Resilience
├── Observability/                 # OpenTelemetry, Serilog
└── DependencyInjection.cs
```

## 9. `Shared/*`

```text
<P>.Shared.Kernels/
├── Primitives/        # Entity<TId>, AggregateRoot<TId>, ValueObject, IDomainEvent, StronglyTypedId
├── Results/           # Result, Result<T>, Error, ErrorType
├── Guards/            # Guard.Against.*
├── Auditing/          # IAuditable, ISoftDeletable
└── Tenancy/           # ITenantEntity

<P>.Shared.Dtos/
├── Common/            # PagedRequest, PagedResponse<T>, LookupDto, SortDirection
└── <Feature>/         # <Entity>Response, Create<Entity>Request, Update<Entity>Request (sealed records)

<P>.Shared.Utilities/
├── Extensions/        # string, DateOnly, enumerable, enum extensions
├── Constants/
├── Formatting/
└── Security/          # hashing / masking helpers (stateless)
```

## 10. `src/Web/<P>.Web`

- Angular → see [AngularArchitectAgent](../agents/AngularArchitectAgent.agent.md) §Folder Structure.
- React → see [ReactArchitectAgent](../agents/ReactArchitectAgent.agent.md) §Folder Structure.
- API TypeScript clients are **generated** from the Host OpenAPI document into `src/app/core/api/generated` (Angular) or `src/shared/api/generated` (React). Never hand-write DTO interfaces that already exist in `Shared.Dtos`.
- Both frameworks use `features/<module>/<sub-feature>/` (e.g. `features/hrm/employee/`) with separate `api/` and `models/` folders and one concern per file (`employee.api.ts`, `employee.dto.ts`, `employee.model.ts`, `employee.enums.ts`, `employee.mapper.ts`). Mixing API calls, DTOs, and interfaces in one file is forbidden.

---

## 11. Naming Conventions

| Artifact | Pattern | Example |
|---|---|---|
| Command | `<Verb><Entity>Command` | `ApproveLeaveCommand` |
| Query | `Get<Entity>[By<X>]Query` / `List<Entities>Query` | `ListEmployeesQuery` |
| Handler | `<Message>Handler` | `ApproveLeaveCommandHandler` |
| Validator | `<Message>Validator` | `ApproveLeaveCommandValidator` |
| Domain event | `<Aggregate><PastVerb>DomainEvent` | `LeaveApprovedDomainEvent` |
| Request DTO | `<Verb><Entity>Request` | `CreateEmployeeRequest` |
| Response DTO | `<Entity>Response` / `<Entity>ListItemResponse` | `EmployeeResponse` |
| Endpoint group | `<Feature>Endpoints` | `EmployeeEndpoints` |
| Repository | `<Aggregate>Repository` / `<Feature>QueryRepository` | `EmployeeRepository` |
| EF config | `<Entity>Configuration` | `EmployeeConfiguration` |

## 12. Forbidden

- `Helpers/`, `Misc/`, `Common/` dumping grounds inside layers (use purposeful names).
- Files > 400 lines, classes with > 1 public responsibility.
- Technical-type-first folders at the top of Application (`/Services`, `/DTOs`) – use `Features/<Feature>/`.
- Duplicated DTOs across Application and Shared.Dtos.
