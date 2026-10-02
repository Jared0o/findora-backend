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
- `GET /api/catalog/catalogs?page=1&pageSize=10` — stronicowana lista katalogów.

Lista przyjmuje `page` (domyślnie 1, minimum 1) oraz `pageSize` (domyślnie 10,
zakres 1–100). Zwraca `items` z polami `id`, `name`, `createdAt`, a także `page`,
`pageSize`, `totalCount` i `totalPages`. Definicje pól są dostępne w szczegółach katalogu.
Sortowanie: `createdAt` malejąco, następnie `id` malejąco.

Pusta baza ma `totalCount: 0` i `totalPages: 0`. Strona poza zakresem zwraca `200`
z pustym `items` oraz aktualnymi licznikami. Nieprawidłowy format lub zakres parametrów
zwraca `400 ProblemDetails` z błędami `Catalog.InvalidPage` / `Catalog.InvalidPageSize`
i ścieżkami `page` / `pageSize`.

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

Kontrakty HTTP znajdują się w `Contracts`, a endpointy w `Endpoints/Catalogs` i `Endpoints/Documents`.
Logika biznesowa i dostęp do bazy pozostają w Core i Infrastructure.

## Dodawanie dokumentu

`POST /api/catalog/catalogs/{catalogId}/documents` przyjmuje bezpośrednio dokument JSON:

```json
{
  "title": "Article",
  "score": 10.0,
  "published": true,
  "tags": ["news", "world"]
}
```

Sukces zwraca `201` z `{ "id": "..." }`. Endpoint odczytu dokumentu nie jest
jeszcze zaimplementowany, dlatego odpowiedź nie zawiera `Location`.
Niepoprawny dokument lub ID zwraca `400 ProblemDetails` z tablicą błędów `errors`;
brak katalogu zwraca `404` z kodem `Catalog.NotFound`.

Nieznane pola są automatycznie wykrywane i zapisywane jako opcjonalne. Znane pola
muszą pasować do typu ustalonego dla katalogu. Wykryte definicje są widoczne w
`GET /api/catalog/catalogs/{id}`. Dokument i nowe definicje zapisują się atomowo.

Obsługiwane typy: `int`, `decimal`, `string`, `bool` i jednorodne tablice.
Całkowity zapis liczby mieszczącej się w Int32 ustala `int`; zapis `10.0`, wykładniczy
lub liczba poza Int32 ustala `decimal`, o ile mieści się w Decimal.
Pole decimal przyjmuje też liczby całkowite; int nie przyjmuje ułamków.
Mieszana tablica liczb całkowitych i dziesiętnych ustala `decimal[]`.
Typ nie zmienia się automatycznie. Nowe pola nie stają się wymagane.

Odrzucamy `{}`, `null`, obiekty zagnieżdżone, tablice tablic, tablice mieszanych typów,
duplikaty właściwości oraz puste nazwy pól. Znak null (`\u0000`) jest niedozwolony
w nazwach i wartościach tekstowych ze względu na PostgreSQL JSONB.
Nowe pole z `[]` jest niejednoznaczne i zwraca błąd; istniejące pole tablicowe akceptuje `[]`.
Pola opcjonalne można pomijać. Błędy zawierają ścieżkę, np. `tags[2]`.

## Batch dokumentów

`POST /api/catalog/catalogs/{catalogId}/documents/batch` przyjmuje tablicę od 1 do 100
dokumentów, np.:

```json
[
  { "title": "Pierwszy", "score": 10.0 },
  { "title": "Drugi", "score": 20.5, "published": true }
]
```

Cała paczka i nowe definicje pól zapisują się w jednej transakcji — błąd dowolnego
dokumentu odrzuca całość. Poprawne dokumenty analizowane wcześniej ustalają typy
nowych pól dla kolejnych dokumentów w paczce. Kolejność wejściowa ma więc znaczenie:
`1` ustala `int` i późniejsze `1.5` jest błędem; `1.0` ustala `decimal` i przyjmie `1`.
Niepoprawny dokument nie przekazuje swoich nowych definicji do kolejnych dokumentów.

`201` zwraca `{ "ids": ["...", "..."], "createdCount": 2 }`, z ID w kolejności wejścia,
bez `Location`. `400 ProblemDetails` zawiera listę błędów, np. `documents[2].score`
lub `documents[3].tags[1]` (indeksy od zera). Błędy całego dokumentu wskazują np.
`documents[1]`, a błędy rozmiaru lub formatu paczki — `documents`. Brak katalogu to `404`.
Limit rozmiaru oznacza liczbę dokumentów; każdy dokument podlega zasadom pojedynczego zapisu.

## Dokumentacja

W środowisku `Development` host udostępnia:

- `/openapi/v1.json` — dokument generowany przez ASP.NET Core OpenAPI.
- `/scalar` — interaktywny interfejs Scalar.

Dokumentacja jest wspólna dla wszystkich modułów; operacje są pogrupowane tagami.
Uruchomienie wymaga skonfigurowania `ConnectionStrings:Findora` i dostępnego PostgreSQL.
Migracje wykonują się automatycznie przed przyjmowaniem żądań; konto bazy musi mieć
uprawnienia do zmian schematu.
