// Grimoire Studio publishing service (Cloudflare Worker).
//
// Lets the admins commit approved wiki changes to the repository without GitHub accounts: each admin has a personal
// key, the Worker checks it and commits with the one GitHub token it keeps as a secret. The token never reaches a
// browser.
//
// Environment (see wrangler.toml and README.md):
//   GITHUB_TOKEN     secret: fine-grained token with Contents read & write on the repository
//   ADMIN_KEYS       secret: JSON {"<admin name>": "<sha-256 hex of the admin's key>"}
//   REPOSITORY       "owner/name"
//   BRANCH           branch to commit to, e.g. "main"
//   COMMIT_EMAIL     author e-mail of the commits (the author name is the admin's)
//   ALLOWED_ORIGINS  comma-separated origins allowed to call the Worker (the site, local development)

const MAX_FILES = 60;
const MAX_FILE_BYTES = 2 * 1024 * 1024;
const MAX_MESSAGE_LENGTH = 2000;

// Only files of the data folder can be written: the JSON files, role icons and script pictures.
const ALLOWED_PATH = /^data\/(?:[a-z]+\.json|icons\/[a-z0-9]+\.png|editions\/[a-z0-9-]+\.png)$/;

export default {
    async fetch(request, env) {
        const cors = corsHeaders(request, env);
        if (request.method === 'OPTIONS') {
            return new Response(null, { status: cors ? 204 : 403, headers: cors ?? {} });
        }

        try {
            const response = await route(request, env);
            for (const [name, value] of Object.entries(cors ?? {})) {
                response.headers.set(name, value);
            }
            return response;
        } catch (error) {
            const status = error instanceof HttpError ? error.status : 500;
            const message = error instanceof HttpError ? error.message : 'Błąd serwera.';
            if (!(error instanceof HttpError)) {
                console.error(error);
            }
            return json({ error: message, ...(error.details ?? {}) }, status, cors);
        }
    },
};

async function route(request, env) {
    const { pathname } = new URL(request.url);
    if (request.method === 'GET' && pathname === '/me') {
        return json({ name: await authenticate(request, env) });
    }

    if (request.method === 'POST' && pathname === '/commit') {
        const admin = await authenticate(request, env);
        return json(await commit(await readBody(request), admin, env));
    }

    throw new HttpError(404, 'Nie ma takiego adresu.');
}

// --- Admin keys -------------------------------------------------------------------------------------------------

/** Name of the admin whose key is in the Authorization header. */
export async function authenticate(request, env) {
    const key = /^Bearer (.+)$/.exec(request.headers.get('Authorization') ?? '')?.[1]?.trim();
    if (!key) {
        throw new HttpError(401, 'Brak klucza administratora.');
    }

    const hash = await sha256Hex(key);
    for (const [name, expected] of Object.entries(adminKeys(env))) {
        if (equalHex(hash, String(expected).toLowerCase())) {
            return name;
        }
    }

    throw new HttpError(401, 'Nieprawidłowy klucz administratora.');
}

/** Admin entries from the ADMIN_KEYS secret; a broken secret is reported as such (500), never as a wrong key. */
function adminKeys(env) {
    let keys;
    try {
        keys = JSON.parse(env.ADMIN_KEYS ?? '{}');
    } catch {
        keys = null;
    }

    if (keys === null || typeof keys !== 'object' || Array.isArray(keys)) {
        throw new HttpError(500, 'Usługa zapisu jest źle skonfigurowana: sekret ADMIN_KEYS nie jest poprawnym JSON-em w postaci {"Imię": "skrót"}.');
    }

    return keys;
}

export async function sha256Hex(text) {
    const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(text));
    return [...new Uint8Array(digest)].map(b => b.toString(16).padStart(2, '0')).join('');
}

/** Compares in constant time, so the answer time does not reveal how much of a hash matched. */
function equalHex(a, b) {
    if (a.length !== b.length) {
        return false;
    }

    let difference = 0;
    for (let i = 0; i < a.length; i++) {
        difference |= a.charCodeAt(i) ^ b.charCodeAt(i);
    }

    return difference === 0;
}

// --- Commit -----------------------------------------------------------------------------------------------------

async function readBody(request) {
    try {
        return await request.json();
    } catch {
        throw new HttpError(400, 'Treść żądania nie jest poprawnym JSON.');
    }
}

/**
 * Commits <files> (path + base64 content) on top of the branch. <baseVersion> is the commit (or its prefix) the
 * changes were made on: when the same files changed since then, nothing is committed and 409 lists them.
 */
export async function commit(body, admin, env) {
    const files = validateFiles(body?.files);
    const message = String(body?.message ?? '').trim().slice(0, MAX_MESSAGE_LENGTH) || 'Zmiany wiki';
    const baseVersion = String(body?.baseVersion ?? '').trim();
    const github = new GitHub(env);

    const head = await github.get(`git/ref/heads/${env.BRANCH}`);
    const headSha = head.object.sha;
    await rejectConflicts(github, baseVersion, headSha, files);

    const headCommit = await github.get(`git/commits/${headSha}`);
    const tree = await github.post('git/trees', {
        base_tree: headCommit.tree.sha,
        tree: await Promise.all(files.map(async file => ({
            path: file.path,
            mode: '100644',
            type: 'blob',
            sha: (await github.post('git/blobs', { content: file.content, encoding: 'base64' })).sha,
        }))),
    });

    if (tree.sha === headCommit.tree.sha) {
        return { changed: false, sha: headSha };
    }

    const created = await github.post('git/commits', {
        message: `${message}\n\nZatwierdził: ${admin}`,
        tree: tree.sha,
        parents: [headSha],
        author: { name: admin, email: env.COMMIT_EMAIL, date: new Date().toISOString() },
    });

    // Not forced: if another admin committed in the meantime, GitHub refuses and the admin tries again.
    try {
        await github.patch(`git/refs/heads/${env.BRANCH}`, { sha: created.sha, force: false });
    } catch (error) {
        if (error instanceof HttpError && error.status === 422) {
            throw new HttpError(409, 'Ktoś właśnie zapisał inne zmiany. Spróbuj ponownie.');
        }
        throw error;
    }

    return { changed: true, sha: created.sha, url: created.html_url, files: files.map(f => f.path) };
}

function validateFiles(files) {
    if (!Array.isArray(files) || files.length === 0) {
        throw new HttpError(400, 'Brak plików do zapisania.');
    }

    if (files.length > MAX_FILES) {
        throw new HttpError(400, `Za dużo plików naraz (limit ${MAX_FILES}).`);
    }

    const seen = new Set();
    return files.map(file => {
        const path = String(file?.path ?? '');
        const content = String(file?.content ?? '');
        if (!ALLOWED_PATH.test(path)) {
            throw new HttpError(400, `Nie można zapisać pliku ${path}: dozwolone są tylko pliki folderu data/.`);
        }

        if (seen.has(path)) {
            throw new HttpError(400, `Plik ${path} podany dwa razy.`);
        }

        seen.add(path);
        if (!/^[A-Za-z0-9+/]*={0,2}$/.test(content) || (content.length * 3) / 4 > MAX_FILE_BYTES) {
            throw new HttpError(400, `Plik ${path}: niepoprawna treść albo za duży.`);
        }

        return { path, content };
    });
}

async function rejectConflicts(github, baseVersion, headSha, files) {
    if (!baseVersion || headSha.startsWith(baseVersion)) {
        return;
    }

    let changed;
    try {
        changed = (await github.get(`compare/${baseVersion}...${headSha}`)).files?.map(f => f.filename) ?? [];
    } catch (error) {
        if (error instanceof HttpError && error.status === 404) {
            // A version that is not a commit (e.g. the build date of a local build): nothing to compare with.
            return;
        }
        throw error;
    }

    const conflicts = files.map(f => f.path).filter(path => changed.includes(path));
    if (conflicts.length > 0) {
        throw new HttpError(409, 'Od wczytania strony ktoś zmienił te same pliki. Odśwież stronę, sprawdź zmiany i wyślij je ponownie.', {
            conflicts,
        });
    }
}

class GitHub {
    constructor(env) {
        this.base = `https://api.github.com/repos/${env.REPOSITORY}/`;
        this.headers = {
            Authorization: `Bearer ${env.GITHUB_TOKEN}`,
            Accept: 'application/vnd.github+json',
            'X-GitHub-Api-Version': '2022-11-28',
            'User-Agent': 'grimoire-studio-worker',
        };
    }

    get(path) {
        return this.request('GET', path);
    }

    post(path, body) {
        return this.request('POST', path, body);
    }

    patch(path, body) {
        return this.request('PATCH', path, body);
    }

    async request(method, path, body) {
        const response = await fetch(this.base + path, {
            method,
            headers: body ? { ...this.headers, 'Content-Type': 'application/json' } : this.headers,
            body: body ? JSON.stringify(body) : undefined,
        });

        if (!response.ok) {
            const detail = await response.text();
            console.error(`GitHub ${method} ${path}: ${response.status} ${detail}`);
            throw new HttpError(
                response.status === 404 || response.status === 422 ? response.status : 502,
                'GitHub odrzucił zapis.');
        }

        return response.json();
    }
}

// --- HTTP helpers -----------------------------------------------------------------------------------------------

export class HttpError extends Error {
    constructor(status, message, details) {
        super(message);
        this.status = status;
        this.details = details;
    }
}

function corsHeaders(request, env) {
    const origin = request.headers.get('Origin');
    const allowed = (env.ALLOWED_ORIGINS ?? '').split(',').map(o => o.trim()).filter(Boolean);
    if (!origin || !allowed.includes(origin)) {
        return null;
    }

    return {
        'Access-Control-Allow-Origin': origin,
        'Access-Control-Allow-Methods': 'GET, POST, OPTIONS',
        'Access-Control-Allow-Headers': 'Authorization, Content-Type',
        'Access-Control-Max-Age': '86400',
        Vary: 'Origin',
    };
}

function json(value, status = 200, headers) {
    return new Response(JSON.stringify(value), {
        status,
        headers: { 'Content-Type': 'application/json; charset=utf-8', ...(headers ?? {}) },
    });
}
