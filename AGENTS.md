# Findora Backend — context for Codex

## Project

Findora is a modular monolith built with C# / .NET 10, Minimal APIs and PostgreSQL.
The first module is `catalog`. Each catalog can define a different set of fields.
Documents are stored as `jsonb`; new field definitions are inferred from incoming
JSON and existing definitions are validated. Catalogs are not limited to products.

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
`GET /api/catalog/catalogs/{id}`, `GET /api/catalog/catalogs`.
`POST /api/catalog/catalogs/{catalogId}/documents` accepts one raw JSON object,
returns 201 with its generated ID and Location from the named `catalog.GetDocument` route, 400 for
invalid input, or 404 for a missing catalog.
`GET /api/catalog/catalogs/{catalogId}/documents/{documentId}` returns `id`, `catalogId`,
`createdAt` (UTC) and `data` as a JSON object. Both IDs must be non-empty GUIDs;
invalid values return 400 with `Catalog.InvalidId` / `Document.InvalidId` and paths
`catalogId` / `documentId`. Missing catalogs, missing documents and documents in another
catalog all return 404 with `Document.NotFound` and path `documentId`. Reads filter by
both IDs in one untracked query without a catalog lock; models live in `Core/Queries/GetDocument`.
Single-document Location includes the application's PathBase. Batch has no Location.
`GET /api/catalog/catalogs/{catalogId}/documents` returns `items` (full documents with
`id`, `catalogId`, `createdAt` and JSON-object `data`), `page`, `pageSize`, `totalCount`
and `totalPages`. Defaults are page 1 and pageSize 10; page must be at least 1 and
pageSize must be 1–100. Sort by createdAt descending, then id descending. Invalid
parameters return 400 with `Catalog.InvalidId`, `Document.InvalidPage` or
`Document.InvalidPageSize` and paths `catalogId`, `page` or `pageSize` respectively.
Missing catalogs return 404 `Catalog.NotFound` at `catalogId`. Empty catalogs return
200 with zero counts; out-of-range pages return 200 with empty items and actual counts.
`GetDocuments` queries use an existence check, Count and Skip/Take without tracking or
locking; concurrent writes may shift pages and counts. No shared snapshot is guaranteed.
`POST /api/catalog/catalogs/{catalogId}/documents/batch` accepts a JSON array of 1–100
documents. It returns 201 with `ids` in input order and `createdCount`, without Location.
The entire batch and inferred fields are atomic. `CatalogDocumentBatchAnalyzer` processes
documents in input order; each valid document's new definitions apply to later documents.
Errors use zero-based paths such as `documents[2].score`; failed batches persist nothing.
The catalog list accepts `page` (default 1, minimum 1) and `pageSize` (default 10, range 1–100).
It returns `items` (id, name, createdAt only), `page`, `pageSize`, `totalCount`,
and `totalPages`. Ordering is createdAt descending, then id descending.
Out-of-range pages return 200 with empty items; invalid parameters return 400 with
error paths `page` / `pageSize`. Empty databases have zero total pages.
Pagination uses Count and Skip/Take queries; concurrent writes can shift page contents.
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
- Documents also use UUID v7 and UTC timestamps. `catalog.documents` contains
  `id`, `catalog_id`, `data` (jsonb) and `created_at`, with a foreign key and catalog index.
- `CatalogDocumentAnalyzer` in Core infers optional fields without mutating the catalog
  and uses `CatalogDocumentValidator` to validate the complete candidate schema.
  Empty objects are rejected. New empty arrays cannot infer a type; known array fields
  accept `[]`. Integer JSON tokens fitting Int32 infer `int`; other supported numbers
  infer `decimal`. Mixed int/decimal arrays infer `decimal[]`. Existing types stay fixed;
  decimal fields accept integers. Reject numbers outside Decimal and null characters.
- `CatalogDocumentRepository` owns the ReadCommitted transaction: lock the catalog row
  with `SELECT ... FOR UPDATE`, then read current definitions, analyze, and save document
  and new fields atomically. Keep the lock until commit/rollback. All future ingestion
  paths must coordinate schema changes with this same catalog lock. Single and batch
  creation share the repository's transaction implementation and hold one lock per request.
  Failures must leave no new fields or document. Different catalogs can write concurrently.
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
