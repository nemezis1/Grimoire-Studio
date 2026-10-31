# Usługa zapisu (Cloudflare Worker)

Pozwala administratorom zapisywać zatwierdzone zmiany wiki prosto do repozytorium, bez kont na GitHubie.

- Każdy administrator ma własny klucz. Worker sprawdza klucz i robi commit jednym tokenem GitHuba, który trzyma jako
  sekret – token nigdy nie trafia do przeglądarki.
- Commit ma jako autora imię administratora (i adres `COMMIT_EMAIL`), w opisie „Zatwierdził: …”.
- Zapisać można tylko pliki `data/` (JSON, ikony ról, obrazki skryptów).
- Jeśli od wczytania strony ktoś zmienił te same pliki, Worker nic nie zapisuje i zwraca listę plików (konflikt).

Po commicie workflow `Build` sam wdraża stronę na GitHub Pages (kilka minut).

## Uruchomienie (jednorazowo)

1. **Token GitHuba** (konto właściciela repozytorium): *Settings → Developer settings → Fine-grained tokens →
   Generate new token*. Repository access: tylko `nemezis1/Grimoire-Studio`; Permissions → Repository → *Contents:
   Read and write*. Token ma datę ważności – przed jej upływem trzeba wygenerować nowy i podmienić sekret.
2. **Konto Cloudflare** (darmowe) i zalogowanie narzędzia:
   ```bash
   cd worker
   npx wrangler login
   ```
3. **Sekrety:**
   ```bash
   npx wrangler secret put GITHUB_TOKEN
   npx wrangler secret put ADMIN_KEYS
   ```
   `ADMIN_KEYS` to JSON z wpisami administratorów, np. `{"Kasia": "…", "Tomek": "…"}` (patrz niżej).
4. **Wdrożenie:**
   ```bash
   npx wrangler deploy
   ```
   Wrangler poda adres, np. `https://grimoire-studio.botc-wiki.workers.dev`.
5. **Adres w aplikacji:** wpisz go w `Grimoire Studio/wwwroot/appsettings.json` jako `PublishApi` i zrób commit.
   Bez tego panel administratora nie pokazuje logowania ani wysyłania.

## Administratorzy

Nowy administrator:

```bash
node add-admin.mjs "Kasia"
```

Skrypt wypisuje klucz (przekaż go tej osobie; nigdzie nie jest zapisany) i wpis do `ADMIN_KEYS` – skrót klucza.
Dopisz wpis do JSON-a i ustaw sekret ponownie (`npx wrangler secret put ADMIN_KEYS`). Odebranie dostępu: usuń wpis i
ustaw sekret jeszcze raz.

Administrator otwiera stronę z `?admin`, w panelu wpisuje klucz („Zapis do repozytorium”) i po zatwierdzeniu zmian
klika „Wyślij do repozytorium”.

## Testy

```bash
node --test
```

Testy zastępują GitHuba atrapą; uruchamia je też workflow `Build`.
