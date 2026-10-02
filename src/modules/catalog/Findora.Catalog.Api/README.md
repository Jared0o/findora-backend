# Catalog API

Host rejestruje moduł przez `AddModule<CatalogModule>(configuration)` i mapuje wszystkie
moduły przez `MapModules()`. Bootstrap tworzy grupę na podstawie `IModule.RoutePrefix`,
ustawia tag OpenAPI z `IModule.Name` i dodaje informacyjny `GET /` w tej grupie.
Moduł mapuje pozostałe endpointy względem otrzymanej grupy. Przed `RunAsync()` host
wywołuje `InitializeModulesAsync()`, a moduł katalogu stosuje oczekujące migracje bazy.

## Endpointy

- `GET /api/catalog` — informacja o module: `{ "name": "catalog" }`.
- `POST /api/catalog/catalogs` — utworzenie pustego katalogu.
- `GET /api/catalog/catalogs/{id}` — metadane katalogu i definicje pól.

Przykładowe żądanie:

```json
{ "name": "Books" }
```

Nazwy katalogów są unikalne w całej bazie, bez rozróżniania wielkości liter
i spacji na początku oraz końcu. `Books`, `books` i ` Books ` wskazują tę samą
nazwę. POST zwraca `409 Conflict` (ProblemDetails z tablicą `errors`),
kod `Catalog.NameAlreadyExists` i ścieżkę `name`, gdy nazwa jest już zajęta.
Oryginalna wielkość liter pozostaje zachowana w zapisanej nazwie.

Sukces zwraca `201 Created`, `{ "id": "..." }` i nagłówek `Location` z adresem
`/api/catalog/catalogs/{id}`, generowanym z nazwanej trasy `catalog.GetCatalog`.
Adres uwzględnia również `PathBase` aplikacji.

Odczyt zwraca `200 OK` z polami `id`, `name`, `createdAt` (UTC) oraz `fields`.
Definicje pól zawierają `name`, `type` (`int`, `decimal`, `string`, `bool`),
`isArray` i `isRequired`. Pola są sortowane po nazwie porównaniem ordinal;
nowo utworzony katalog ma `fields: []`.

Błędny format identyfikatora lub pusty GUID zwraca `400` z kodem `Catalog.InvalidId`.
Nieistniejący katalog zwraca `404` z kodem `Catalog.NotFound`. Obie odpowiedzi
używają ProblemDetails z tablicą `errors`, a ścieżka błędu to `id`.

Walidacja wykorzystuje validator komendy z Core. Nieprawidłowa nazwa zwraca
`400 Bad Request` w formacie `application/problem+json`, z tablicą `errors`
zawierającą `code`, `message` i `path`. Nieoczekiwane wyjątki obsługuje middleware
hosta z odpowiedzią `500` w formacie ProblemDetails.

Kontrakty HTTP znajdują się w `Contracts`, a endpointy w `Endpoints/Catalogs`.
Logika biznesowa i dostęp do bazy pozostają w Core i Infrastructure.

## Dokumentacja

W środowisku `Development` host udostępnia:

- `/openapi/v1.json` — dokument generowany przez ASP.NET Core OpenAPI.
- `/scalar` — interaktywny interfejs Scalar.

Dokumentacja jest wspólna dla wszystkich modułów; operacje są pogrupowane tagami.
Uruchomienie wymaga skonfigurowania `ConnectionStrings:Findora` i dostępnego PostgreSQL.
Migracje wykonują się automatycznie przed przyjmowaniem żądań; konto bazy musi mieć
uprawnienia do zmian schematu.
