# Catalog API

Host rejestruje moduł przez `AddModule<CatalogModule>(configuration)` i mapuje wszystkie
moduły przez `MapModules()`. Bootstrap tworzy grupę na podstawie `IModule.RoutePrefix`,
ustawia tag OpenAPI z `IModule.Name` i dodaje informacyjny `GET /` w tej grupie.
Moduł mapuje pozostałe endpointy względem otrzymanej grupy. Przed `RunAsync()` host
wywołuje `InitializeModulesAsync()`, a moduł katalogu stosuje oczekujące migracje bazy.

## Endpointy

- `GET /api/catalog` — informacja o module: `{ "name": "catalog" }`.
- `POST /api/catalog/catalogs` — utworzenie pustego katalogu.

Przykładowe żądanie:

```json
{ "name": "Books" }
```

Sukces zwraca `201 Created`, `{ "id": "..." }` i nagłówek `Location` z adresem
`/api/catalog/catalogs/{id}`. Endpoint odczytu katalogu pod tym adresem jest kolejnym
etapem implementacji.

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
