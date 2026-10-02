# Findora Backend — context for Codex

## Project

Findora is a modular monolith built with C# / .NET 10, Minimal APIs and PostgreSQL.
The first module is `catalog`. Each catalog can define a different set of fields.
Product data will eventually be stored as `jsonb` and validated against its catalog's
field definitions. Product persistence using `jsonb` is not implemented yet.

Communicate with the user in Polish. Use English for code symbols and contracts.
Prefer simple solutions and existing project mechanisms. Avoid adding large libraries
for functionality that can reasonably be implemented locally.

## Architecture and layout

- `src/apps/Findora.Api` — host, configuration, module bootstrap, OpenAPI and Scalar.
- `src/modules/<module>/Findora.<Module>.Api` — `IModule` implementation, HTTP contracts
  in `Contracts` and endpoints in `Endpoints`, grouped by resource.
- `src/modules/<module>/Findora.<Module>.Core` — domain models, commands, handlers,
  validation and repository interfaces. Domain models remain independent of EF.
- `src/modules/<module>/Findora.<Module>.Infrastructure` — EF Core, persistence entities,
  mapping configurations, migrations and repository implementations.
- `src/shared/Findora.Shared.Abstraction` — shared contracts, `IModule`, `Result`,
  `Result<T>` and `Error`. This project references the `Microsoft.AspNetCore.App` framework.
- `src/shared/Findora.Shared.Infrastructure` — module bootstrap and Result-to-HTTP mapping.
- `tests` — mirrors the `src` structure, for example
  `tests/modules/catalog/Findora.Catalog.Api.Tests` and `tests/shared/...`.

Note: Shared.Infrastructure still uses the `Findora.Shated.Infrastructure` namespace,
and its tests are located in `tests/shared/Findora.Shated.Infrastructure.Tests`.
This is an existing naming inconsistency; use the actual paths and namespaces
when adding references.

## Modules and API

- The host registers modules through `AddModule<TModule>(configuration)`.
- `IModule` exposes `Name`, `RoutePrefix`, `RegisterServices`, `MapEndpoints`
  and an optional `InitializeAsync` implementation.
- `MapModules()` creates a route group from `RoutePrefix`, adds an OpenAPI tag
  and automatically maps an informational `GET /` introducing the module.
  Map module endpoints relative to the supplied group.
- Before starting the server, the host calls `InitializeModulesAsync()`. Modules
  initialize sequentially, each within its own DI scope. Initialization failures
  prevent the application from starting.
- FluentValidation command validators live in Core and are registered through
  `AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), ServiceLifetime.Transient)`.
  Handlers, repositories and DbContext use `Scoped` lifetimes.
- Use the existing Result pattern for expected failures. The `Errors` collection
  contains `Code`, `Message` and an optional `Path`. HTTP validation failures return
  ProblemDetails (`400`) with an `errors` array. Unexpected database exceptions
  propagate to exception handling; do not convert them into validation errors.

Current routes: `GET /`, `GET /api/catalog`, `POST /api/catalog/catalogs`,
`GET /api/catalog/catalogs/{id}`.
Creation accepts `{ "name": "Books" }` and returns `201` with an ID and `Location`.
Catalog names are globally unique, ignoring case and surrounding spaces. Creation
checks for duplicates in the repository and enforces uniqueness in PostgreSQL via
the generated `normalized_name = lower(btrim(name))` column and a unique index.
Both the preliminary check and the database use PostgreSQL normalization.
Name conflicts return `409` with `Catalog.NameAlreadyExists` and path `name`.
Existing duplicate names must be resolved before the uniqueness migration can run.
Creation generates `Location` from the named `catalog.GetCatalog` route.
The read endpoint returns catalog metadata and field definitions ordered by name
using ordinal comparison. Field types are strings: `int`, `decimal`, `string`, `bool`.
Malformed or empty GUIDs return `400`; missing catalogs return `404` with
`Catalog.NotFound`. Read queries and their models live in `Core/Queries/GetCatalog`;
repository reads project to these models without tracking EF entities.
In Development, documentation is available at `/scalar` and `/openapi/v1.json`.

## Database and catalogs

- Each module owns its PostgreSQL schema, DbContext and migration history.
  Catalog uses the `catalog` schema and `catalog.__EFMigrationsHistory`.
  Do not introduce direct dependencies between different modules' contexts.
- Catalog migrations live in `Persistence/Migrations`; persistence models live in
  `Persistence/Entities`. Mapping changes require a new migration and a snapshot
  update through EF tools.
- Migrations run automatically at startup in every environment. PostgreSQL must
  be available, and the application account must have permission to change the schema.
- Generate new catalog identifiers with `Guid.CreateVersion7()`.
- Field definitions support `int` (Int32), `decimal`, `string`, `bool` and homogeneous
  arrays of these types. Reject nested objects, nested arrays and `null`.
  Optional fields can be omitted. The validator rejects unknown and duplicate fields,
  checks required fields and collects all errors. Field names are case-sensitive.

## Commands and verification

Run commands from the repository root. `global.json` selects the SDK;
`Directory.Build.props` defines the target framework and shared settings.
Package versions are managed centrally in `Directory.Packages.props`; update the
corresponding `packages.lock.json` files through restore after dependency changes.

```powershell
dotnet build findora-backend.slnx
dotnet test --solution findora-backend.slnx
dotnet test --project tests/modules/catalog/Findora.Catalog.Api.Tests
docker compose up -d postgres
dotnet run --project src/apps/Findora.Api --launch-profile http
dotnet tool restore
dotnet ef migrations add ChangeName --project src/modules/catalog/Findora.Catalog.Infrastructure --context CatalogDbContext --output-dir Persistence/Migrations
```

Tests use xUnit v3 and Microsoft.Testing.Platform, so use the
`dotnet test --solution` / `--project` syntax. Add new projects to `findora-backend.slnx`.
Database tests use Testcontainers.PostgreSql (`postgres:18-alpine`), require Docker
and run against temporary databases. HTTP tests use TestHost.
Verify behavior relevant to the change; do not run test migrations against the user's
database. Report code failures separately from environment limitations.

The `http` profile starts the API at `http://localhost:5170`. Compose exposes PostgreSQL
at `localhost:54320`. The connection string is in `appsettings.Development.json` and
can be overridden with `ConnectionStrings__Findora`. Compose credentials are for local
development only.

## Documentation

See `src/modules/catalog/Findora.Catalog.Api/README.md` and
`src/modules/catalog/Findora.Catalog.Infrastructure/README.md` for details.
The neighboring repository `../findora-docs` contains early project documentation
(`Analiza_architektury_wyszukiwarki.docx`); consult it when planning architecture
if available. Distinguish planned functionality from features implemented in code.
Update this file and the relevant README files when the conventions described here change.
