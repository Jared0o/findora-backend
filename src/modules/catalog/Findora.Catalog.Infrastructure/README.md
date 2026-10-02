# Baza danych modułu katalogu

Moduł korzysta ze schematu `catalog` w bazie PostgreSQL `findora`. `CatalogDbContext`
mapuje wyłącznie dane tego modułu. Migracje i tabela `catalog.__EFMigrationsHistory`
są niezależne od przyszłych kontekstów pozostałych modułów.

Pierwsza migracja tworzy:

- `catalog.catalogs`: identyfikator UUID, nazwa i czas utworzenia w UTC.
- `catalog.field_definitions`: definicje pól z kluczem `(catalog_id, name)`, typem,
  informacją o tablicy i wymaganej wartości. Klucz obcy wskazuje katalog.

Nazwy katalogów nie mają obecnie ograniczenia unikalności. Tożsamością katalogu
jest jego identyfikator. Nazwy pól są rozróżniane z uwzględnieniem wielkości liter.
Produkty, ich dane `jsonb` i outbox zostaną dodane w kolejnych migracjach.

Modele zapisu znajdują się w `Persistence/Entities`. Model domenowy w `Core`
pozostaje niezależny od EF Core. `CatalogRepository` zapisuje katalog i zwraca
sukces po wykonaniu `SaveChangesAsync`. Nieoczekiwane błędy bazy propagują się
do wywołującego; nie są zamieniane w błędy walidacji.

## Uruchomienie lokalne

Polecenia wykonuj z głównego katalogu repozytorium. Docker musi działać.

```powershell
docker compose up -d postgres
dotnet run --project src/apps/Findora.Api --launch-profile http
```

PostgreSQL jest dostępny na `localhost:54320`. Konfiguracja deweloperska i factory
EF używają bazy `findora`, użytkownika `findora` i hasła `findora_dev`.
Są to wyłącznie dane lokalnego środowiska. Połączenie można nadpisać zmienną
środowiskową `ConnectionStrings__Findora`; w produkcji trzeba je skonfigurować.

Host rejestruje `CatalogModule` przez `AddModule<CatalogModule>()`. Moduł wywołuje
`AddCatalogCore()` i `AddCatalogInfrastructure(connectionString)`.
Kontekst oraz repozytorium mają czas życia `Scoped`. Przed uruchomieniem serwera host
wywołuje `InitializeModulesAsync()`. Każdy moduł inicjalizuje się kolejno we własnym
scope DI. `CatalogModule.InitializeAsync()` wywołuje `Database.MigrateAsync()` przez
Infrastructure, stosując oczekujące migracje w schemacie `catalog`. Kolejne starty
z aktualną bazą nie wykonują ponownie zastosowanych migracji. Błąd połączenia lub
migracji przerywa start API. Po inicjalizacji katalog można utworzyć przez
`POST /api/catalog/catalogs`.

## Kolejne migracje

```powershell
dotnet tool restore
dotnet ef migrations add NazwaZmiany --project src/modules/catalog/Findora.Catalog.Infrastructure --context CatalogDbContext --output-dir Persistence/Migrations
dotnet ef migrations script --idempotent --project src/modules/catalog/Findora.Catalog.Infrastructure --context CatalogDbContext --output catalog-migrations.sql
```

Automatyczne migracje działają w każdym środowisku. Konto używane przez API musi
mieć uprawnienia do zmian schematu wymaganych przez migracje. Schemat organizuje
własność tabel; odseparowanie uprawnień wymaga konfiguracji ról PostgreSQL.

## Testy integracyjne

```powershell
dotnet test --solution findora-backend.slnx
```

Projekt `tests/modules/catalog/Findora.Catalog.Infrastructure.Tests` używa
`Testcontainers.PostgreSql` i obrazu `postgres:18-alpine`. Testy korzystają z osobnej,
tymczasowej bazy, stosują migracje i usuwają kontener po zakończeniu. Nie wymagają
uruchomienia bazy z Compose. Pierwsze uruchomienie może pobrać obraz PostgreSQL.

Sprawdzane scenariusze obejmują zapis komendy przez DI, odczyt z nowego kontekstu,
położenie tabel i historii migracji, niezależność pól katalogów, klucz obcy,
ograniczenie typów, atomowość zapisu oraz równoległe dodanie tej samej nazwy pola.
