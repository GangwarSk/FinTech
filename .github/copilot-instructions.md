# Repository AI Instructions

This repository uses the enterprise agent pack in `.github/agents` and the standards in `.github/instructions`.

## Always

- Target layout is defined in [folder-structure-standards.instructions.md](instructions/folder-structure-standards.instructions.md):
  `src/Api/<P>.Api.{Host,Application,Domain,Abstractions,Repositories,Infrastructure}`, `src/Shared/<P>.Shared.{Dtos,Utilities,Kernels}`, `src/Web/<P>.Web` (Angular or React), tests in `tests/`.
- Backend: .NET 10 Minimal API (no controllers), DDD + Clean Architecture, Hybrid Repository, `Result<T>` + ProblemDetails.
- Follow [architecture-standards.instructions.md](instructions/architecture-standards.instructions.md) and [coding-standards.instructions.md](instructions/coding-standards.instructions.md).
- Never violate the project reference matrix; never place code outside the target layout.
- Ask for the project name `<P>` if it cannot be derived from the existing solution.

## Agent Routing

For multi-step generation or migration work, use **CodingAgent** – it plans and delegates to the specialists:

| Need | Agent |
|---|---|
| Architecture design / review | ArchitectureAgent |
| Legacy ASPX/MVC/WCF → .NET 10 | DddMigrationAgent |
| Minimal API endpoints, scaffolding | DotNetApiAgent |
| Repositories, EF Core, Dapper | HybridRepositoryAgent |
| Stored procedure → .NET | StoredProcedureModernizationAgent |
| Security audit / auth | SecurityAgent |
| Schema design | DatabaseAgent |
| Engine-to-engine data migration | MigrationAgent |
| SQL Server → PostgreSQL | SqlToPostgresAgent |
| Query performance | QueryOptimizationAgent |
| Angular / React app | AngularArchitectAgent / ReactArchitectAgent |
| Angular ↔ React conversion | AngularToReactAgent / ReactToAngularAgent |
| Tests | TestingAgent, UnitTestingAgent, ApiTestingAgent, E2ETestingAgent |
| Documentation | DocumentationAgent |
