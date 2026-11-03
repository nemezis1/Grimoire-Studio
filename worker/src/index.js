// Grimoire Studio on Cloudflare Workers: serves the site (the published Blazor app, as static assets) and, under
// /api/, the publishing service.
//
// The publishing service lets the admins commit approved wiki changes to the repository without GitHub accounts: each admin has a personal
// key, the Worker checks it and commits with the one GitHub token it keeps as a secret. The token never reaches a
// browser.
//
// Environment (see wrangler.toml and README.md):
//   GITHUB_TOKEN     secret: fine-grained token with Contents read & write on the repository
//   ADMIN_KEYS       secret: the owners, JSON {"<name>": "<sha-256 hex of the key>"}; only they manage the admins
//   ADMINS           KV namespace: the other admins, added and removed by an owner in the admin panel, and the
//                    proposals sent by visitors (proposal:<id>)
//   REPOSITORY       "owner/name"
//   BRANCH           branch to commit to, e.g. "main"
//   COMMIT_EMAIL     author e-mail of the commits (the author name is the admin's)
//   ALLOWED_ORIGINS  other origins allowed to call the API (e.g. local development); the site itself is same-origin
//   ASSETS           binding to the site's files (wrangler.toml [assets])

const MAX_FILES = 60;
const MAX_FILE_BYTES = 2 * 1024 * 1024;
const MAX_MESSAGE_LENGTH = 2000;

// Only files of the data folder can be written: the JSON files, role icons and script pictures.
const ALLOWED_PATH = /^data\/(?:[a-z]+\.json|icons\/[a-z0-9]+\.png|editions\/[a-z0-9-]+\.png)$/;

const API = '/api/';

// Last line of the commits this Worker makes; it tells wiki changes from other commits.
const APPROVED_BY_LABEL = 'Zatwierdził:';
const APPROVED_BY = /\n\nZatwierdził: [^\n]+$/;

export default {
    async fetch(request, env) {
        // Everything outside /api/ is the site; its unknown paths get index.html (the app's own routes).
        if (!new URL(request.url).pathname.startsWith(API) && env.ASSETS) {
            return env.ASSETS.fetch(request);
        }

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
    if (request.method === 'GET' && pathname === `${API}me`) {
        return json(await authenticate(request, env));
    }

    if (request.method === 'POST' && pathname === `${API}commit`) {
        const admin = await authenticate(request, env);
        return json(await commit(await readBody(request), admin.name, env));
    }

    if (request.method === 'GET' && pathname === `${API}status`) {
        await authenticate(request, env);
        return json(await deployments(env));
    }

    if (request.method === 'GET' && pathname === `${API}changes`) {
        await authenticate(request, env);
        return json(await wikiChanges(env));
    }

    if (request.method === 'GET' && pathname === `${API}item-history`) {
        await authenticate(request, env);
        const params = new URL(request.url).searchParams;
        return json(await itemHistory(params.get('kind'), params.get('id'), params.get('file'), env));
    }

    if (request.method === 'GET' && pathname === `${API}commit-changes`) {
        await authenticate(request, env);
        return json(await commitChanges(new URL(request.url).searchParams.get('sha'), env));
    }

    if (request.method === 'GET' && pathname === `${API}data`) {
        await authenticate(request, env);
        return json(await dataAt(new URL(request.url).searchParams.get('ref'), env));
    }

    // Anyone sends a proposal and reads it back by its id (their receipt); only admins list, decide and remove them.
    if (pathname === `${API}proposals`) {
        if (request.method === 'POST') {
            return json(await submitProposal(request, env), 201);
        }

        if (request.method === 'GET') {
            await authenticate(request, env);
            return json(new URL(request.url).searchParams.has('pending')
                ? await pendingSummary(env)
                : await listProposals(env));
        }
    }

    if (pathname.startsWith(`${API}proposals/`)) {
        const id = decodeURIComponent(pathname.slice(`${API}proposals/`.length));
        if (request.method === 'GET') {
            return json(await getProposal(id, env));
        }

        if (request.method === 'PUT') {
            await authenticate(request, env);
            return json(await saveProposal(id, await readBody(request), env));
        }

        if (request.method === 'DELETE') {
            await authenticate(request, env);
            return json(await deleteProposal(id, env));
        }
    }

    if (pathname === `${API}admins` || pathname.startsWith(`${API}admins/`)) {
        const owner = await authenticate(request, env);
        if (!owner.owner) {
            throw new HttpError(403, 'Tylko właściciel strony zarządza administratorami.');
        }

        const name = decodeURIComponent(pathname.slice(`${API}admins/`.length));
        if (request.method === 'GET' && pathname === `${API}admins`) {
            return json({ admins: await listAdmins(env) });
        }

        if (request.method === 'POST' && pathname === `${API}admins`) {
            return json(await addAdmin((await readBody(request))?.name, owner.name, env));
        }

        if (request.method === 'DELETE' && name) {
            return json(await removeAdmin(name, env));
        }
    }

    throw new HttpError(404, 'Nie ma takiego adresu.');
}

// --- Admins managed by the owners ---------------------------------------------------------------------------------

const ADMINS_KEY = 'admins';
const MAX_NAME_LENGTH = 40;

/** Admins added in the panel: {"<name>": {hash, addedAt, addedBy}}. */
async function storedAdmins(env) {
    if (!env.ADMINS) {
        return {};
    }

    return JSON.parse(await env.ADMINS.get(ADMINS_KEY) ?? '{}');
}

function requireStore(env) {
    if (!env.ADMINS) {
        throw new HttpError(500, 'Usługa zapisu jest źle skonfigurowana: brak magazynu ADMINS (Cloudflare KV).');
    }
}

async function listAdmins(env) {
    return Object.entries(await storedAdmins(env))
        .map(([name, entry]) => ({ name, addedAt: entry.addedAt, addedBy: entry.addedBy }))
        .sort((a, b) => a.name.localeCompare(b.name, 'pl'));
}

/** Adds an admin with a new key; the key is returned only here, the store keeps its hash. */
async function addAdmin(rawName, owner, env) {
    requireStore(env);
    const name = String(rawName ?? '').trim();
    if (!name || name.length > MAX_NAME_LENGTH) {
        throw new HttpError(400, `Podaj imię administratora (do ${MAX_NAME_LENGTH} znaków).`);
    }

    const admins = await storedAdmins(env);
    const taken = [...Object.keys(adminKeys(env)), ...Object.keys(admins)]
        .some(existing => existing.toLowerCase() === name.toLowerCase());
    if (taken) {
        throw new HttpError(409, `Administrator „${name}” już jest.`);
    }

    const key = base64Url(crypto.getRandomValues(new Uint8Array(24)));
    admins[name] = { hash: await sha256Hex(key), addedAt: new Date().toISOString(), addedBy: owner };
    await env.ADMINS.put(ADMINS_KEY, JSON.stringify(admins));
    return { name, key };
}

async function removeAdmin(name, env) {
    requireStore(env);
    const admins = await storedAdmins(env);
    if (!(name in admins)) {
        throw new HttpError(404, `Nie ma administratora „${name}”.`);
    }

    delete admins[name];
    await env.ADMINS.put(ADMINS_KEY, JSON.stringify(admins));
    return { removed: name };
}

function base64Url(bytes) {
    return btoa(String.fromCharCode(...bytes)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

// --- Proposals ----------------------------------------------------------------------------------------------------

// Proposals live in the ADMINS namespace too, as proposal:<id> with {status, author} as metadata. Decided ones expire.
const PROPOSAL_PREFIX = 'proposal:';
const PROPOSAL_ID = /^[A-Za-z0-9_-]{16,64}$/;
const PROPOSAL_STATUSES = ['pending', 'approved', 'rejected'];
const MAX_PROPOSAL_BYTES = 4 * 1024 * 1024;
const MAX_PENDING_PROPOSALS = 100;
const MAX_PROPOSALS_PER_HOUR = 10;
const MAX_LISTED_PROPOSALS = 200;
const DECIDED_PROPOSAL_SECONDS = 180 * 24 * 60 * 60;

/** A proposal from anyone: the Worker gives it its id and time, and keeps only the fields a visitor may set. */
async function submitProposal(request, env) {
    requireStore(env);
    const text = await request.text();
    if (text.length > MAX_PROPOSAL_BYTES) {
        throw new HttpError(413, 'Zgłoszenie jest za duże.');
    }

    let body;
    try {
        body = JSON.parse(text);
    } catch {
        throw new HttpError(400, 'Treść żądania nie jest poprawnym JSON.');
    }

    const author = String(body?.author ?? '').trim();
    if (!author || author.length > MAX_NAME_LENGTH) {
        throw new HttpError(400, `Podaj, kto zgłasza (do ${MAX_NAME_LENGTH} znaków).`);
    }

    const changes = validChanges(body?.changes);
    if ((await pendingProposals(env)).length >= MAX_PENDING_PROPOSALS) {
        throw new HttpError(429, 'Za dużo zgłoszeń czeka na decyzję. Spróbuj za jakiś czas.');
    }

    await limitProposals(request, env);
    const proposal = {
        id: base64Url(crypto.getRandomValues(new Uint8Array(18))),
        author,
        comment: String(body?.comment ?? '').trim().slice(0, MAX_MESSAGE_LENGTH),
        createdAt: new Date().toISOString(),
        changes,
        status: 'pending',
    };
    await putProposal(proposal, env);
    return proposal;
}

/** A change set: an object whose item lists are arrays of objects, and removed items as "role:<id>" etc. */
function validChanges(changes) {
    const lists = ['roles', 'scripts', 'teams', 'jinxes'];
    const removed = changes?.removed;
    const valid = changes && typeof changes === 'object' && !Array.isArray(changes)
        && lists.every(name => changes[name] === undefined
            || (Array.isArray(changes[name]) && changes[name].every(item => item && typeof item === 'object')))
        && (removed === undefined
            || (Array.isArray(removed) && removed.every(key => /^(?:role|script|team):[a-z0-9]+$/.test(String(key)))))
        && [...lists, 'removed'].some(name => changes[name]?.length > 0);
    if (!valid) {
        throw new HttpError(400, 'Zgłoszenie nie zawiera zmian.');
    }

    return changes;
}

/** At most MAX_PROPOSALS_PER_HOUR from one address (kept as a hash, for an hour). */
async function limitProposals(request, env) {
    const address = request.headers.get('CF-Connecting-IP') ?? 'unknown';
    const key = `proposal-rate:${(await sha256Hex(address)).slice(0, 32)}`;
    const count = Number(await env.ADMINS.get(key) ?? 0);
    if (count >= MAX_PROPOSALS_PER_HOUR) {
        throw new HttpError(429, 'Za dużo zgłoszeń w krótkim czasie. Spróbuj za godzinę.');
    }

    await env.ADMINS.put(key, String(count + 1), { expirationTtl: 60 * 60 });
}

function putProposal(proposal, env) {
    const metadata = { status: proposal.status, author: proposal.author };
    return env.ADMINS.put(PROPOSAL_PREFIX + proposal.id, JSON.stringify(proposal),
        proposal.status === 'pending' ? { metadata } : { metadata, expirationTtl: DECIDED_PROPOSAL_SECONDS });
}

async function proposalKeys(env) {
    const keys = [];
    let cursor;
    do {
        const page = await env.ADMINS.list({ prefix: PROPOSAL_PREFIX, cursor });
        keys.push(...page.keys);
        cursor = page.list_complete ? undefined : page.cursor;
    } while (cursor && keys.length < MAX_LISTED_PROPOSALS);
    return keys;
}

/** Ids of the proposals waiting for a decision, from the listing's metadata (no proposal is read). */
async function pendingProposals(env) {
    requireStore(env);
    return (await proposalKeys(env))
        .filter(key => key.metadata?.status === 'pending')
        .map(key => key.name.slice(PROPOSAL_PREFIX.length));
}

/**
 * The pending proposals' number and ids. KV listings lag behind writes (up to a minute), so the ids let the admin's
 * browser correct the number with the decisions it has just made.
 */
async function pendingSummary(env) {
    const ids = await pendingProposals(env);
    return { pending: ids.length, ids };
}

async function listProposals(env) {
    requireStore(env);
    const keys = (await proposalKeys(env)).slice(0, MAX_LISTED_PROPOSALS);
    const values = await Promise.all(keys.map(key => env.ADMINS.get(key.name)));
    return { proposals: values.filter(Boolean).map(value => JSON.parse(value)) };
}

function proposalId(id) {
    if (!PROPOSAL_ID.test(id)) {
        throw new HttpError(400, 'Nieznane zgłoszenie.');
    }

    return id;
}

async function getProposal(id, env) {
    requireStore(env);
    const value = await env.ADMINS.get(PROPOSAL_PREFIX + proposalId(id));
    if (!value) {
        throw new HttpError(404, 'Nie ma takiego zgłoszenia (mogło zostać usunięte).');
    }

    return JSON.parse(value);
}

/** An admin's version of a proposal: a decision, the commit that sent it, or one added from a file. */
async function saveProposal(id, body, env) {
    requireStore(env);
    proposalId(id);
    if (body?.id !== id) {
        throw new HttpError(400, 'Identyfikator zgłoszenia się nie zgadza.');
    }

    const author = String(body.author ?? '').trim();
    if (!author || author.length > MAX_NAME_LENGTH * 4) {
        throw new HttpError(400, 'Zgłoszenie nie ma autora.');
    }

    if (!PROPOSAL_STATUSES.includes(body.status)) {
        throw new HttpError(400, 'Nieznany stan zgłoszenia.');
    }

    validChanges(body.changes);
    const proposal = { ...body, author };
    if (JSON.stringify(proposal).length > MAX_PROPOSAL_BYTES * 2) {
        throw new HttpError(413, 'Zgłoszenie jest za duże.');
    }

    await putProposal(proposal, env);
    return proposal;
}

async function deleteProposal(id, env) {
    requireStore(env);
    await env.ADMINS.delete(PROPOSAL_PREFIX + proposalId(id));
    return { removed: id };
}

// --- Data files at a version ------------------------------------------------------------------------------------

const DATA_FILES = ['roles', 'fabled', 'scripts', 'hatred', 'teams'];

/**
 * The JSON files of the data folder as text at <ref> (a commit, or its prefix), or at the head of the branch without
 * it; a file missing there is null.
 */
export async function dataAt(ref, env) {
    if (ref && !/^[0-9a-f]{7,40}$/.test(ref)) {
        throw new HttpError(400, 'Nieznana wersja danych.');
    }

    const github = new GitHub(env);
    const sha = ref
        ? (await github.get(`commits/${ref}`)).sha
        : (await github.get(`git/ref/heads/${env.BRANCH}`)).object.sha;
    const texts = await Promise.all(DATA_FILES.map(name => github.textAt(sha, `data/${name}.json`)));
    return { sha, files: Object.fromEntries(DATA_FILES.map((name, i) => [`${name}.json`, texts[i]])) };
}

// --- Deployments ------------------------------------------------------------------------------------------------

/**
 * Deployments of wiki changes sent by admins (commits made by this Worker), newest first: those still running or
 * failed, down to the last one deployed. Other commits (e.g. new versions of the site) are left out.
 */
export async function deployments(env) {
    const runs = await new GitHub(env).get(`actions/runs?branch=${encodeURIComponent(env.BRANCH)}&event=push&per_page=30`);
    const wiki = (runs.workflow_runs ?? [])
        .filter(run => run.name === 'Build' && APPROVED_BY.test(run.head_commit?.message ?? ''));
    const lastDeployed = wiki.findIndex(run => run.status === 'completed' && run.conclusion === 'success');
    return {
        runs: (lastDeployed < 0 ? wiki : wiki.slice(0, lastDeployed + 1))
            .map(run => ({
                sha: run.head_sha,
                title: (run.head_commit?.message ?? run.display_title ?? '').split('\n')[0],
                author: run.head_commit?.author?.name ?? '',
                status: run.status,
                conclusion: run.conclusion,
                startedAt: run.run_started_at ?? run.created_at,
                updatedAt: run.updated_at,
                url: run.html_url,
            })),
    };
}

// --- History of the wiki, read from git ---------------------------------------------------------------------------

const CHANGES_LIMIT = 30;
// Per file: one listing, one version per commit and the version before the oldest stay under the subrequest limit.
const ITEM_COMMITS_LIMIT = 15;
const PROPOSALS_LINE = /^Zgłoszenia: (.+)$/m;

function commitSummary(item) {
    const message = item.commit?.message ?? '';
    return {
        sha: item.sha,
        url: item.html_url,
        date: item.commit?.author?.date,
        author: item.commit?.author?.name ?? '',
        title: message.split('\n')[0],
        approvedBy: /\n\nZatwierdził: ([^\n]+)$/.exec(message)?.[1] ?? null,
        proposals: PROPOSALS_LINE.exec(message)?.[1] ?? null,
    };
}

/** Wiki changes sent by admins (commits made by this Worker), newest first. */
export async function wikiChanges(env) {
    const commits = await new GitHub(env).get(
        `commits?sha=${encodeURIComponent(env.BRANCH)}&path=data&per_page=${CHANGES_LIMIT * 2}`);
    return {
        changes: commits
            .filter(item => APPROVED_BY.test(item.commit?.message ?? ''))
            .slice(0, CHANGES_LIMIT)
            .map(commitSummary),
    };
}

// Which data file holds an item of a kind, and how to find the item in it.
const ITEM_KINDS = {
    role: {
        files: ['roles', 'fabled'],
        find: (data, id) => data?.find?.(role => role.id === id) ?? null,
    },
    script: {
        files: ['scripts'],
        find: (data, id) => data?.scripts?.find?.(script => script.id === id) ?? null,
    },
    jinx: {
        files: ['hatred'],
        // id is "<role>/<role>": the jinx may be stored under either of the two.
        find: (data, id) => {
            const [a, b] = id.split('/');
            for (const owner of data ?? []) {
                const other = owner.id === a ? b : owner.id === b ? a : null;
                const jinx = other && owner.hatred?.find(j => j.id === other);
                if (jinx) {
                    return { owner: owner.id, ...jinx };
                }
            }
            return null;
        },
    },
};

/**
 * Commits that changed one role, script or jinx, newest first, with the item before and after each (null when it did
 * not exist). For a role, commits of its icon count too. Only the latest commits of the file are looked at.
 */
export async function itemHistory(kind, id, file, env) {
    const spec = ITEM_KINDS[kind];
    if (!spec || !id || !/^[a-z0-9]+(?:\/[a-z0-9]+)?$/.test(id)) {
        throw new HttpError(400, 'Nieznana pozycja.');
    }

    const name = spec.files.includes(file) ? file : spec.files[0];
    const path = `data/${name}.json`;
    const github = new GitHub(env);
    const branch = encodeURIComponent(env.BRANCH);
    const commits = await github.get(`commits?sha=${branch}&path=${encodeURIComponent(path)}&per_page=${ITEM_COMMITS_LIMIT}`);

    const versions = await Promise.all(commits.map(c => github.fileAt(c.sha, path)));
    const oldest = commits.at(-1)?.parents?.[0]?.sha;
    versions.push(oldest ? await github.fileAt(oldest, path) : null);

    const entries = new Map();
    commits.forEach((c, i) => {
        const after = spec.find(versions[i], id);
        const before = spec.find(versions[i + 1], id);
        if (JSON.stringify(after) !== JSON.stringify(before)) {
            entries.set(c.sha, { ...commitSummary(c), before, after, iconChanged: false });
        }
    });

    if (kind === 'role') {
        const icons = await github.get(`commits?sha=${branch}&path=${encodeURIComponent(`data/icons/${id}.png`)}&per_page=${ITEM_COMMITS_LIMIT}`);
        for (const c of icons) {
            const entry = entries.get(c.sha) ?? { ...commitSummary(c), before: null, after: null };
            entries.set(c.sha, { ...entry, iconChanged: true });
        }
    }

    return {
        entries: [...entries.values()].sort((a, b) => String(b.date).localeCompare(String(a.date))),
        // Older changes may exist beyond the commits looked at.
        complete: commits.length < ITEM_COMMITS_LIMIT,
    };
}

// Items of each data file by key, for comparing two versions of it.
const FILE_ITEMS = {
    roles: data => (data ?? []).map(role => ['role', role.id, role]),
    fabled: data => (data ?? []).map(role => ['role', role.id, role]),
    scripts: data => (data?.scripts ?? []).map(script => ['script', script.id, script]),
    teams: data => (data ?? []).map(team => ['team', team.id, team]),
    hatred: data => (data ?? []).flatMap(owner =>
        (owner.hatred ?? []).map(jinx => ['jinx', `${owner.id}/${jinx.id}`, { owner: owner.id, ...jinx }])),
};

/** Roles, scripts, teams and jinxes one commit changed, with each item before and after (null when it did not exist). */
export async function commitChanges(sha, env) {
    if (!sha || !/^[0-9a-f]{7,40}$/.test(sha)) {
        throw new HttpError(400, 'Nieznany commit.');
    }

    const github = new GitHub(env);
    const detail = await github.get(`commits/${sha}`);
    const parent = detail.parents?.[0]?.sha;
    const items = [];
    for (const file of detail.files ?? []) {
        const name = /^data\/([a-z]+)\.json$/.exec(file.filename)?.[1];
        const icon = /^data\/icons\/([a-z0-9]+)\.png$/.exec(file.filename)?.[1];
        if (icon) {
            items.push({ kind: 'role', id: icon, before: null, after: null, iconChanged: true });
        }
        if (!FILE_ITEMS[name]) {
            continue;
        }

        const [after, before] = await Promise.all([
            github.fileAt(detail.sha, file.filename),
            parent ? github.fileAt(parent, file.filename) : null,
        ]);
        const old = new Map(FILE_ITEMS[name](before).map(([kind, id, item]) => [`${kind}:${id}`, item]));
        const now = new Map(FILE_ITEMS[name](after).map(([kind, id, item]) => [`${kind}:${id}`, item]));
        for (const key of new Set([...old.keys(), ...now.keys()])) {
            if (JSON.stringify(old.get(key)) !== JSON.stringify(now.get(key))) {
                const [kind, id] = [key.slice(0, key.indexOf(':')), key.slice(key.indexOf(':') + 1)];
                items.push({ kind, id, before: old.get(key) ?? null, after: now.get(key) ?? null, iconChanged: false });
            }
        }
    }

    // A role whose data and icon both changed is one item.
    const merged = new Map();
    for (const item of items) {
        const key = `${item.kind}:${item.id}`;
        const known = merged.get(key);
        const data = known?.iconChanged && !item.iconChanged ? item : known ?? item;
        merged.set(key, { ...data, iconChanged: Boolean(known?.iconChanged || item.iconChanged) });
    }
    return { parent: parent ?? null, items: [...merged.values()] };
}

// --- Admin keys -------------------------------------------------------------------------------------------------

/** The admin whose key is in the Authorization header: {name, owner}; owners are those in ADMIN_KEYS. */
export async function authenticate(request, env) {
    const key = /^Bearer (.+)$/.exec(request.headers.get('Authorization') ?? '')?.[1]?.trim();
    if (!key) {
        throw new HttpError(401, 'Brak klucza administratora.');
    }

    const hash = await sha256Hex(key);
    for (const [name, expected] of Object.entries(adminKeys(env))) {
        if (equalHex(hash, String(expected).toLowerCase())) {
            return { name, owner: true };
        }
    }

    for (const [name, entry] of Object.entries(await storedAdmins(env))) {
        if (equalHex(hash, String(entry.hash).toLowerCase())) {
            return { name, owner: false };
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
 * Commits <files> (path + base64 content, or delete: true) on top of the branch. <baseVersion> is the commit (or its prefix) the
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
            // A null sha removes the file.
            sha: file.delete ? null : (await github.post('git/blobs', { content: file.content, encoding: 'base64' })).sha,
        }))),
    });

    if (tree.sha === headCommit.tree.sha) {
        return { changed: false, sha: headSha };
    }

    const created = await github.post('git/commits', {
        message: `${message}\n\n${APPROVED_BY_LABEL} ${admin}`,
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
        const remove = file?.delete === true;
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

        return { path, content, delete: remove };
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
        this.repository = env.REPOSITORY;
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

    /** A file of the repository as it was at <sha>, parsed as JSON; null when it did not exist then. */
    async fileAt(sha, path) {
        const text = await this.textAt(sha, path);
        return text === null ? null : JSON.parse(text);
    }

    /** A file of the repository as it was at <sha>, as text; null when it did not exist then. */
    async textAt(sha, path) {
        const response = await fetch(`https://raw.githubusercontent.com/${this.repository}/${sha}/${path}`, {
            headers: { Authorization: this.headers.Authorization, 'User-Agent': this.headers['User-Agent'] },
        });
        if (response.status === 404) {
            return null;
        }

        if (!response.ok) {
            console.error(`GitHub raw ${sha} ${path}: ${response.status}`);
            throw new HttpError(502, 'Nie udało się odczytać historii z GitHuba.');
        }

        return response.text();
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
        'Access-Control-Allow-Methods': 'GET, POST, PUT, DELETE, OPTIONS',
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
