# Baza danych modułu katalogu

Moduł korzysta ze schematu `catalog` w bazie PostgreSQL `findora`. `CatalogDbContext`
mapuje wyłącznie dane tego modułu. Migracje i tabela `catalog.__EFMigrationsHistory`
są niezależne od przyszłych kontekstów pozostałych modułów.

Pierwsza migracja tworzy:

- `catalog.catalogs`: identyfikator UUID, nazwa i czas utworzenia w UTC.
- `catalog.field_definitions`: definicje pól z kluczem `(catalog_id, name)`, typem,
  informacją o tablicy i wymaganej wartości. Klucz obcy wskazuje katalog.

Nazwy katalogów są globalnie unikalne po normalizacji `lower(btrim(name))` w PostgreSQL.
Migracja `UniqueCatalogName` dodaje generowaną kolumnę `normalized_name` oraz unikalny
indeks `ux_catalogs_normalized_name`. Repozytorium sprawdza zajętość nazwy przed zapisem
i obsługuje naruszenie tego konkretnego indeksu przy równoczesnych zapisach,
zwracając `Catalog.NameAlreadyExists` (HTTP `409`). Pozostałe błędy bazy propagują się.
Tożsamością katalogu pozostaje jego identyfikator. Nazwy pól są case-sensitive.
Migracja `AddCatalogDocuments` tworzy `catalog.documents`: `id` (UUID v7),
`catalog_id` (FK do katalogu), `data` (`jsonb`) i `created_at` (UTC). Indeks
`ix_documents_catalog_id` wspiera odczyt dokumentów katalogu. CHECK wymaga niepustego
obiektu JSON; walidacja typów odbywa się w Core. Outbox pozostaje do implementacji.

`CatalogDocumentRepository` zapisuje dokument oraz wykryte definicje pól w jednej
transakcji ReadCommitted. Najpierw blokuje wiersz katalogu przez `SELECT ... FOR UPDATE`,
a potem odczytuje aktualne definicje i analizuje dokument. Dzięki temu równoczesne
zapisy do jednego katalogu widzą zatwierdzone typy pól. Różne katalogi mogą zapisywać
równolegle. Każda przyszła ścieżka dodawania dokumentów i zmiany schematu, w tym batch,
musi przestrzegać tej samej blokady. Błąd wycofuje zapis dokumentu i nowych pól.
Pojedynczy zapis i `CreateBatchAsync` współdzielą implementację transakcji. Batch
utrzymuje jedną blokadę katalogu, analizuje 1–100 dokumentów, a następnie zapisuje je
wspólnie z definicjami przez jedno wywołanie `SaveChangesAsync`. Zwracane ID odpowiadają
kolejności wejściowej. Błąd walidacji lub zapisu wycofuje całą paczkę. Batch nie wymaga
nowych tabel ani migracji.
Surowy JSON jest mapowany jako string z typem kolumny `jsonb`; Npgsql nie serializuje
go ponownie. JSONB normalizuje reprezentację — formatowanie i kolejność kluczy nie
są zachowywane.

Odczyt dokumentu przez `CatalogDocumentRepository.GetByIdAsync` filtruje jednocześnie
po `catalog_id` i `id` w jednym zapytaniu bez śledzenia encji i bez blokady katalogu.
Projektuje wyłącznie metadane i dane dokumentu, a zapisany JSON parsuje do
niezależnego `JsonElement` (`Clone` przed zwolnieniem `JsonDocument`). Brak pasującego
wiersza zwraca `null`; handler mapuje go na `Document.NotFound` niezależnie od tego,
czy brakuje katalogu, dokumentu, czy dokument należy do innego katalogu.

`CatalogDocumentRepository.GetPageAsync` sprawdza istnienie katalogu przez `AnyAsync`
(brak zwraca `null`), następnie wykonuje `Count` oraz `Skip/Take` wyłącznie dla dokumentów
tego katalogu. Sortuje po `created_at DESC, id DESC`, bez śledzenia encji i blokady katalogu.
Pobiera pełne dokumenty, parsując JSON dopiero po materializacji strony i klonując każdy
`JsonElement`. Offset jest liczony jako `long`; strona poza zakresem zwraca pustą listę
przed rzutowaniem offsetu na `int`. Istniejący pusty katalog ma oba liczniki równe 0.
Zapytania nie mają wspólnego snapshotu, więc równoczesne zapisy mogą zmienić liczniki
i zawartość stron. Wykorzystywany jest istniejący indeks `ix_documents_catalog_id`.

Jeśli istnieją już duplikaty nazw, migracja i start API zatrzymają się. Migracja nie
usuwa ani nie przemianowuje danych. Przed jej zastosowaniem można wykryć kolizje:

```sql
SELECT lower(btrim(name)) AS normalized_name, array_agg(id) AS catalog_ids, count(*)
FROM catalog.catalogs
GROUP BY lower(btrim(name))
HAVING count(*) > 1;
```

Po świadomym nadaniu kolidującym katalogom różnych nazw uruchom API ponownie.

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
Odczyt `GET /api/catalog/catalogs/{id}` korzysta z `GetByIdAsync`, które projektuje
dane na model odczytu `CatalogDetails` bez śledzenia encji. Pobiera definicje pól
wyłącznie wskazanego katalogu i porządkuje je po nazwie porównaniem ordinal.

Lista katalogów korzysta z `GetPageAsync`: zlicza rekordy, a następnie pobiera stronę
przez `Skip/Take`, projektując tylko ID, nazwę i czas utworzenia bez śledzenia encji.
Kolejność jest określona przez `created_at DESC, id DESC`. Liczniki i strona pochodzą
z osobnych zapytań; równoczesne zapisy mogą zmienić wynik pomiędzy nimi oraz przesunąć
elementy między kolejnymi stronami. Rozmiar strony wynosi domyślnie 10, maksymalnie 100.

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
