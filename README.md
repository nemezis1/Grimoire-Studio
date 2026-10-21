# Grimoire Studio

Fanowska wiki i generator tokenów do gry **Blood on the Clocktower**, udostępniana za darmo społeczności.

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
- `data/icons/{id}.png` – ikona każdej roli, `data/editions/` – ikony skryptów.

Id roli to małe litery i cyfry (`scarletwoman`, `pithag`). Każda zmiana danych musi przejść testy:

```bash
dotnet test GrimoireStudio.Data.Tests
```

## Uruchomienie

```bash
dotnet run --project "Grimoire Studio"
```
