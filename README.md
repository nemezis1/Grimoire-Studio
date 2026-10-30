# Grimoire Studio

Fanowska wiki i generator tokenów do gry **Blood on the Clocktower**, udostępniana za darmo społeczności:
https://nemezis1.github.io/Grimoire-Studio/

> Projekt nieoficjalny, niezwiązany z The Pandemonium Institute. Blood on the Clocktower jest znakiem towarowym The Pandemonium Institute.

## Struktura

| ścieżka | zawartość |
|---|---|
| `data/` | źródło prawdy: role, legendy (fabled), skrypty i jinxy w JSON, ikony ról i edycji |
| `GrimoireStudio.Data/` | modele danych, wczytywanie i walidacja (net8.0, współdzielone z aplikacją Blood) |
| `GrimoireStudio.Data.Tests/` | testy; sprawdzają też spójność plików w `data/` |
| `Grimoire Studio/` | aplikacja webowa (Blazor WebAssembly) |

## Dane

- `data/roles.json` – role z kolejnością nocy (`firstNight`, `otherNight`; 0 = rola się nie budzi), tekstami PL/EN i przypomnieniami.
- `data/fabled.json` – legendy (team `fabled`).
- `data/scripts.json` – skrypty jako listy id ról.
- `data/hatred.json` – jinxy: rola-właściciel i lista ról, z którymi ma specjalną zasadę.
- `data/teams.json` – drużyny: nazwy, kolory, kolejność; `extra` to drużyny spoza liczby postaci (wędrowcy, legendy, loric).
- `data/icons/{id}.png` – ikona każdej roli, `data/editions/` – ikony skryptów.

Pliki JSON mają jeden stały format (zapisuje go `DataFiles` w `GrimoireStudio.Data`), więc zmiana jednej roli to kilka
zmienionych linii. Polskie teksty przetłumaczone maszynowo są w « » – do sprawdzenia i poprawienia (na stronie
„Jinxy” jest filtr „Do sprawdzenia”).

Id roli to małe litery i cyfry (`scarletwoman`, `pithag`). Każda zmiana danych musi przejść testy:

```bash
dotnet test GrimoireStudio.Data.Tests
```

## Uruchomienie

```bash
dotnet run --project "Grimoire Studio"
```

## Zmiany w danych

- Każdy może zmieniać role, skrypty, drużyny i jinxy w przeglądarce („Moje zmiany”) i wysłać je administratorowi jako
  plik eksportu.
- Administrator (`/?admin`) zatwierdza zgłoszenia w panelu, a potem pobiera „Pliki danych (.zip)”: zmienione pliki
  `data/` gotowe do rozpakowania w katalogu repozytorium i zacommitowania.
- Panel zatrzymuje pobieranie, gdy zmiany psują dane (np. skrypt z nieistniejącą rolą, nowa rola bez ikony).

## Wdrożenie

Workflow `.github/workflows/build.yml` przy każdym pushu i pull requeście buduje projekt i uruchamia testy (w tym
walidację `data/`). Push do `main` publikuje stronę na GitHub Pages; wersją danych (`data/version.txt`) jest wtedy
skrót commita. W ustawieniach repozytorium: *Settings → Pages → Source: GitHub Actions*.
