# Strona i usługa zapisu (Cloudflare Worker)

Worker serwuje stronę (opublikowaną aplikację, jako pliki statyczne) pod https://grimoire-studio.botc-wiki.workers.dev/,
a pod `/api/` usługę zapisu. Usługa zapisu pozwala administratorom zapisywać zatwierdzone zmiany wiki prosto do repozytorium, bez kont na GitHubie.

- Każdy administrator ma własny klucz. Worker sprawdza klucz i robi commit jednym tokenem GitHuba, który trzyma jako
  sekret – token nigdy nie trafia do przeglądarki.
- Commit ma jako autora imię administratora (i adres `COMMIT_EMAIL`), w opisie „Zatwierdził: …”.
- Zapisać można tylko pliki `data/` (JSON, ikony ról, obrazki skryptów).
- Jeśli od wczytania strony ktoś zmienił te same pliki, Worker nic nie zapisuje i zwraca listę plików (konflikt).

Każdy push do `main` – także commit administratora – uruchamia workflow `Build`, który buduje stronę i wdraża Workera
(kilka minut).

## Uruchomienie (jednorazowo)

1. **Token GitHuba** (konto właściciela repozytorium): *Settings → Developer settings → Fine-grained tokens →
   Generate new token*. Repository access: tylko `nemezis1/Grimoire-Studio`; Permissions → Repository → *Contents:
   Read and write*. Token ma datę ważności – przed jej upływem trzeba wygenerować nowy i podmienić sekret.
2. **Konto Cloudflare** (darmowe) i zalogowanie narzędzia:
   ```bash
   cd worker
   npx wrangler login
   ```
3. **Magazyn administratorów (KV):**
   ```bash
   npx wrangler kv namespace create ADMINS
   ```
   Wypisane `id` wpisz w `wrangler.toml` w sekcji `[[kv_namespaces]]`.
   **Sekrety:**
   ```bash
   npx wrangler secret put GITHUB_TOKEN
   npx wrangler secret put ADMIN_KEYS
   ```
   `ADMIN_KEYS` to JSON z wpisami administratorów, np. `{"Kasia": "…", "Tomek": "…"}` (patrz niżej).
4. **Wdrażanie z GitHuba:** w Cloudflare *My Profile → API Tokens → Create Token → szablon „Edit Cloudflare
   Workers”* (konto: swoje, strefy: wszystkie lub żadna). Na GitHubie w repozytorium *Settings → Secrets and variables
   → Actions* dodaj sekrety `CLOUDFLARE_API_TOKEN` (ten token) i `CLOUDFLARE_ACCOUNT_ID` (ID konta z panelu
   Cloudflare, *Workers & Pages* → prawa kolumna).
5. **Adres usługi w aplikacji:** `PublishApi` w `Grimoire Studio/wwwroot/appsettings.json`
   (`https://grimoire-studio.botc-wiki.workers.dev/api`). Pusty – panel nie pokazuje logowania ani wysyłania.

Ręczne wdrożenie (np. bez GitHuba): `dotnet publish "Grimoire Studio/Grimoire Studio.csproj" -c Release -o publish`
w katalogu repozytorium, potem `npx wrangler deploy` w `worker/`.

## Administratorzy

- **Właściciele** – klucze z sekretu `ADMIN_KEYS`. Tylko oni zarządzają administratorami; dodać lub zmienić
  właściciela można wyłącznie przez Cloudflare (poniżej), nie ze strony.
- **Administratorzy** – dodawani przez właściciela w panelu administratora (sekcja „Administratorzy”: imię → „Dodaj”,
  klucz pokazuje się raz; „Odbierz dostęp” działa od razu). Ich lista jest w Cloudflare KV (`ADMINS`), tylko ze
  skrótami kluczy. Mogą wysyłać zmiany wiki, ale nie widzą listy administratorów i nie mogą nią zarządzać.

### Właściciele (rzadko)

Lista właścicieli jest w `worker/admins.json` – tylko na tym komputerze, poza gitem. Sekret ustawia się z całego pliku.

```bash
node add-admin.mjs "Grzegorz" --key KLUCZ  # właściciel z kluczem, który już ma
node add-admin.mjs "Imię"                  # nowy właściciel z nowym kluczem
node add-admin.mjs --remove "Imię"
```

```powershell
Get-Content admins.json | npx wrangler secret put ADMIN_KEYS
```

Administrator otwiera stronę z `?admin`, w panelu wpisuje klucz („Zapis do repozytorium”) i po zatwierdzeniu zmian
klika „Wyślij do repozytorium”.

## Testy

```bash
node --test
```

Testy zastępują GitHuba atrapą; uruchamia je też workflow `Build`.
