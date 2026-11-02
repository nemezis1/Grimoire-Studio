import { test, beforeEach, afterEach } from 'node:test';
import assert from 'node:assert/strict';
import worker, { sha256Hex } from '../src/index.js';

const KEY = 'secret-key-of-kasia';
const SITE = 'https://nemezis1.github.io';

const env = {
    GITHUB_TOKEN: 'token',
    REPOSITORY: 'owner/repo',
    BRANCH: 'main',
    COMMIT_EMAIL: 'author@example.com',
    ALLOWED_ORIGINS: `${SITE},http://localhost:5126`,
};

/** In-memory stand-in for a Cloudflare KV namespace. */
const kvStore = () => {
    const values = new Map();
    return { get: async key => values.get(key) ?? null, put: async (key, value) => void values.set(key, value) };
};

/** In-memory stand-in for the GitHub API: one branch, commits, trees and a compare endpoint. */
let github;
const realFetch = globalThis.fetch;

beforeEach(async () => {
    env.ADMIN_KEYS = JSON.stringify({ Kasia: await sha256Hex(KEY) });
    env.ADMINS = kvStore();
    github = {
        head: 'aaaaaaa1111',
        commits: { aaaaaaa1111: { tree: { sha: 'tree0' } } },
        changedSince: {},
        calls: [],
        refusePatch: false,
        // History for commits?path= listings (newest first) and the files at each commit (raw.githubusercontent.com).
        history: {},
        files: {},
    };
    globalThis.fetch = async (url, init = {}) => {
        const reply = (value, status = 200) => new Response(JSON.stringify(value), { status });
        const raw = /^https:\/\/raw\.githubusercontent\.com\/owner\/repo\/([^/]+)\/(.+)$/.exec(url);
        if (raw) {
            github.calls.push({ method: 'RAW', path: `${raw[1]}/${raw[2]}` });
            const content = github.files[raw[1]]?.[raw[2]];
            return content === undefined ? new Response('404', { status: 404 }) : reply(content);
        }

        const path = url.replace('https://api.github.com/repos/owner/repo/', '');
        const body = init.body ? JSON.parse(init.body) : undefined;
        github.calls.push({ method: init.method, path, body });

        const listing = /^commits\?sha=main&path=([^&]+)&per_page=\d+$/.exec(path);
        if (listing) return reply(github.history[decodeURIComponent(listing[1])] ?? []);

        if (path === 'git/ref/heads/main') return reply({ object: { sha: github.head } });
        if (path.startsWith('git/commits/')) return reply(github.commits[path.slice('git/commits/'.length)]);
        if (path === 'git/blobs') return reply({ sha: `blob-${github.calls.length}` });
        if (path === 'git/trees') return reply({ sha: body.tree.length ? 'tree1' : body.base_tree });
        if (path === 'git/commits') return reply({ sha: 'bbbbbbb2222', html_url: 'https://github.com/owner/repo/commit/bbbbbbb2222' });
        if (path === 'git/refs/heads/main') return github.refusePatch ? reply({ message: 'not fast forward' }, 422) : reply({});
        if (path.startsWith('actions/runs?')) {
            return reply({ workflow_runs: [
                { name: 'Build', head_sha: 'ccccccc3333', head_commit: { message: 'Rycerz\n\nZatwierdził: Kasia', author: { name: 'Kasia' } },
                  status: 'in_progress', conclusion: null, run_started_at: '2026-10-31T17:08:42Z', updated_at: '2026-10-31T17:09:00Z', html_url: 'https://github.com/run/2' },
                { name: 'Other', head_sha: 'x', status: 'completed', conclusion: 'success', html_url: 'https://github.com/run/x' },
                { name: 'Build', head_sha: 'ddddddd4444', head_commit: { message: 'Nowa wersja strony', author: { name: 'Grzegorz' } },
                  status: 'completed', conclusion: 'success', run_started_at: '2026-10-31T17:05:00Z', updated_at: '2026-10-31T17:07:00Z', html_url: 'https://github.com/run/4' },
                { name: 'Build', head_sha: 'eeeeeee5555', head_commit: { message: 'Jinx\n\nZatwierdził: Tomek', author: { name: 'Tomek' } },
                  status: 'completed', conclusion: 'failure', run_started_at: '2026-10-31T17:02:00Z', updated_at: '2026-10-31T17:03:00Z', html_url: 'https://github.com/run/5' },
                { name: 'Build', head_sha: 'aaaaaaa1111', head_commit: { message: 'Zarządca\n\nZatwierdził: Kasia', author: { name: 'Kasia' } },
                  status: 'completed', conclusion: 'success', run_started_at: '2026-10-31T16:57:56Z', updated_at: '2026-10-31T17:00:00Z', html_url: 'https://github.com/run/1' },
                { name: 'Build', head_sha: 'fffffff6666', head_commit: { message: 'Starsze\n\nZatwierdził: Kasia', author: { name: 'Kasia' } },
                  status: 'completed', conclusion: 'success', run_started_at: '2026-10-31T15:00:00Z', updated_at: '2026-10-31T15:02:00Z', html_url: 'https://github.com/run/6' },
            ] });
        }
        if (path.startsWith('compare/')) {
            const base = path.slice('compare/'.length).split('...')[0];
            return base in github.changedSince
                ? reply({ files: github.changedSince[base].map(filename => ({ filename })) })
                : reply({ message: 'Not Found' }, 404);
        }
        return reply({ message: 'unexpected' }, 500);
    };
});

afterEach(() => {
    globalThis.fetch = realFetch;
});

const call = (method, path, { key = KEY, body, origin = SITE } = {}) =>
    worker.fetch(new Request(`https://worker.test${path}`, {
        method,
        headers: {
            ...(key ? { Authorization: `Bearer ${key}` } : {}),
            ...(origin ? { Origin: origin } : {}),
            ...(body ? { 'Content-Type': 'application/json' } : {}),
        },
        body: body ? JSON.stringify(body) : undefined,
    }), env);

const file = (path, text = '[]') => ({ path, content: Buffer.from(text).toString('base64') });

test('me returns the admin name for a valid key', async () => {
    const response = await call('GET', '/api/me');
    assert.equal(response.status, 200);
    assert.deepEqual(await response.json(), { name: 'Kasia', owner: true });
    assert.equal(response.headers.get('Access-Control-Allow-Origin'), SITE);
});

test('a wrong or missing key is refused', async () => {
    assert.equal((await call('GET', '/api/me', { key: 'wrong' })).status, 401);
    assert.equal((await call('GET', '/api/me', { key: null })).status, 401);
});

test('preflight is allowed only for listed origins', async () => {
    assert.equal((await call('OPTIONS', '/api/commit', { origin: SITE })).status, 204);
    assert.equal((await call('OPTIONS', '/api/commit', { origin: 'https://evil.example' })).status, 403);
});

test('commit writes the files on top of the branch with the admin as author', async () => {
    const response = await call('POST', '/api/commit', {
        body: { message: 'Nowa rola', baseVersion: 'aaaaaaa', files: [file('data/roles.json'), file('data/icons/chef.png')] },
    });

    assert.equal(response.status, 200);
    const result = await response.json();
    assert.equal(result.sha, 'bbbbbbb2222');
    assert.deepEqual(result.files, ['data/roles.json', 'data/icons/chef.png']);

    const created = github.calls.find(c => c.path === 'git/commits' && c.method === 'POST').body;
    assert.equal(created.author.name, 'Kasia');
    assert.equal(created.author.email, 'author@example.com');
    assert.deepEqual(created.parents, ['aaaaaaa1111']);
    assert.match(created.message, /^Nowa rola\n\nZatwierdził: Kasia$/);

    const update = github.calls.find(c => c.method === 'PATCH');
    assert.deepEqual(update.body, { sha: 'bbbbbbb2222', force: false });
});

test('files outside the data folder are refused', async () => {
    for (const path of ['README.md', '.github/workflows/build.yml', 'data/../README.md', 'data/icons/x.svg']) {
        const response = await call('POST', '/api/commit', { body: { files: [file(path)] } });
        assert.equal(response.status, 400, path);
    }
    assert.ok(!github.calls.some(c => c.method === 'POST'));
});

test('a change to the same file since the base version is a conflict', async () => {
    github.head = 'ccccccc3333';
    github.commits.ccccccc3333 = { tree: { sha: 'tree0' } };
    github.changedSince.aaaaaaa = ['data/roles.json'];

    const response = await call('POST', '/api/commit', { body: { baseVersion: 'aaaaaaa', files: [file('data/roles.json')] } });

    assert.equal(response.status, 409);
    assert.deepEqual((await response.json()).conflicts, ['data/roles.json']);
    assert.ok(!github.calls.some(c => c.method === 'POST'));
});

test('changes to other files since the base version are fine', async () => {
    github.head = 'ccccccc3333';
    github.commits.ccccccc3333 = { tree: { sha: 'tree0' } };
    github.changedSince.aaaaaaa = ['data/hatred.json'];

    const response = await call('POST', '/api/commit', { body: { baseVersion: 'aaaaaaa', files: [file('data/roles.json')] } });

    assert.equal(response.status, 200);
});

test('a branch moved during the commit is reported as a conflict', async () => {
    github.refusePatch = true;

    const response = await call('POST', '/api/commit', { body: { files: [file('data/roles.json')] } });

    assert.equal(response.status, 409);
});

test('without a key nothing reaches GitHub', async () => {
    const response = await call('POST', '/api/commit', { key: null, body: { files: [file('data/roles.json')] } });

    assert.equal(response.status, 401);
    assert.equal(github.calls.length, 0);
});

test('a broken ADMIN_KEYS secret is reported as a configuration error', async () => {
    env.ADMIN_KEYS = '"3f9a"';

    const response = await call('GET', '/api/me');

    assert.equal(response.status, 500);
    assert.match((await response.json()).error, /ADMIN_KEYS/);
});

test('paths outside /api/ are served from the site files', async () => {
    const served = [];
    const response = await worker.fetch(new Request('https://worker.test/roles/chef'), {
        ...env,
        ASSETS: { fetch: request => { served.push(new URL(request.url).pathname); return new Response('site'); } },
    });

    assert.equal(await response.text(), 'site');
    assert.deepEqual(served, ['/roles/chef']);
});

test('an unknown API path is 404', async () => {
    assert.equal((await call('GET', '/api/nothing')).status, 404);
});

test('status lists wiki changes from the running ones down to the last deployed', async () => {
    const response = await call('GET', '/api/status');

    assert.equal(response.status, 200);
    const { runs } = await response.json();
    assert.deepEqual(runs.map(r => [r.sha, r.title, r.author, r.status, r.conclusion]), [
        ['ccccccc3333', 'Rycerz', 'Kasia', 'in_progress', null],
        ['eeeeeee5555', 'Jinx', 'Tomek', 'completed', 'failure'],
        ['aaaaaaa1111', 'Zarządca', 'Kasia', 'completed', 'success'],
    ]);
    assert.match(github.calls[0].path, /^actions\/runs\?branch=main&event=push/);
});

test('status needs an admin key', async () => {
    assert.equal((await call('GET', '/api/status', { key: null })).status, 401);
});

test('an owner adds an admin, whose key then works but cannot manage admins', async () => {
    const added = await call('POST', '/api/admins', { body: { name: 'Tomek' } });
    assert.equal(added.status, 200);
    const { name, key } = await added.json();
    assert.equal(name, 'Tomek');
    assert.match(key, /^[A-Za-z0-9_-]{32}$/);

    const me = await call('GET', '/api/me', { key });
    assert.deepEqual(await me.json(), { name: 'Tomek', owner: false });

    assert.equal((await call('GET', '/api/admins', { key })).status, 403);
    assert.equal((await call('POST', '/api/admins', { key, body: { name: 'Ktoś' } })).status, 403);
    assert.equal((await call('DELETE', '/api/admins/Tomek', { key })).status, 403);
});

test('the owner lists and removes admins; a removed key stops working at once', async () => {
    const { key } = await (await call('POST', '/api/admins', { body: { name: 'Zoja Ś' } })).json();

    const list = await (await call('GET', '/api/admins')).json();
    assert.deepEqual(list.admins.map(a => [a.name, a.addedBy]), [['Zoja Ś', 'Kasia']]);
    assert.ok(!JSON.stringify(list).includes(key));

    assert.equal((await call('DELETE', '/api/admins/' + encodeURIComponent('Zoja Ś'))).status, 200);
    assert.equal((await call('GET', '/api/me', { key })).status, 401);
    assert.equal((await call('DELETE', '/api/admins/Nikt')).status, 404);
});

test('an admin name cannot repeat an owner or another admin', async () => {
    assert.equal((await call('POST', '/api/admins', { body: { name: 'kasia' } })).status, 409);
    await call('POST', '/api/admins', { body: { name: 'Tomek' } });
    assert.equal((await call('POST', '/api/admins', { body: { name: 'TOMEK' } })).status, 409);
    assert.equal((await call('POST', '/api/admins', { body: { name: '  ' } })).status, 400);
});

test('the store keeps only hashes of the keys', async () => {
    const { key } = await (await call('POST', '/api/admins', { body: { name: 'Tomek' } })).json();
    const stored = await env.ADMINS.get('admins');
    assert.ok(!stored.includes(key));
    assert.ok(stored.includes(await sha256Hex(key)));
});

const commitItem = (sha, date, message, parent) =>
    ({ sha, html_url: `https://github.com/c/${sha}`, commit: { message, author: { name: 'Ktoś', date } }, parents: [{ sha: parent }] });

test('changes list only wiki commits sent by admins, with who approved and whose proposals', async () => {
    github.history.data = [
        commitItem('c3', '2026-11-02T10:00:00Z', 'Rycerz\n\nZgłoszenia: Ola, Tomek\n\nZatwierdził: Kasia', 'c2'),
        commitItem('c2', '2026-11-01T10:00:00Z', 'Update data to the official TPI release', 'c1'),
    ];

    const { changes } = await (await call('GET', '/api/changes')).json();

    assert.deepEqual(changes.map(c => [c.sha, c.title, c.approvedBy, c.proposals]), [['c3', 'Rycerz', 'Kasia', 'Ola, Tomek']]);
});

test('item history keeps only the commits that changed this role, plus its icon', async () => {
    const chef = name => ({ id: 'chef', namePl: name });
    const imp = name => ({ id: 'imp', namePl: name });
    github.history['data/roles.json'] = [
        commitItem('c3', '2026-11-03T10:00:00Z', 'Szef\n\nZatwierdził: Kasia', 'c2'),
        commitItem('c2', '2026-11-02T10:00:00Z', 'Imp', 'c1'),
        commitItem('c1', '2026-11-01T10:00:00Z', 'Kucharz', 'c0'),
    ];
    github.history['data/icons/chef.png'] = [commitItem('c4', '2026-11-04T10:00:00Z', 'Ikona\n\nZatwierdził: Kasia', 'c3')];
    github.files = {
        c3: { 'data/roles.json': [chef('C'), imp('Y')] },
        c2: { 'data/roles.json': [chef('B'), imp('Y')] },
        c1: { 'data/roles.json': [chef('B'), imp('X')] },
        c0: { 'data/roles.json': [chef('A'), imp('X')] },
    };

    const { entries, complete } = await (await call('GET', '/api/item-history?kind=role&id=chef&file=roles')).json();

    assert.deepEqual(entries.map(e => [e.sha, e.before?.namePl ?? null, e.after?.namePl ?? null, e.iconChanged]), [
        ['c4', null, null, true],
        ['c3', 'B', 'C', false],
        ['c1', 'A', 'B', false],
    ]);
    assert.equal(entries[1].approvedBy, 'Kasia');
    assert.equal(complete, true);
});

test('item history finds a jinx stored under either role', async () => {
    github.history['data/hatred.json'] = [commitItem('c2', '2026-11-02T10:00:00Z', 'Jinx', 'c1')];
    github.files = {
        c2: { 'data/hatred.json': [{ id: 'imp', hatred: [{ id: 'chef', reasonPl: 'nowy' }] }] },
        c1: { 'data/hatred.json': [] },
    };

    const { entries } = await (await call('GET', '/api/item-history?kind=jinx&id=chef/imp')).json();

    assert.deepEqual(entries.map(e => [e.sha, e.before, e.after?.owner, e.after?.reasonPl]), [['c2', null, 'imp', 'nowy']]);
});

test('item history refuses unknown kinds and odd ids', async () => {
    assert.equal((await call('GET', '/api/item-history?kind=team&id=imp')).status, 400);
    assert.equal((await call('GET', '/api/item-history?kind=role&id=../x')).status, 400);
    assert.equal((await call('GET', '/api/item-history?kind=role&id=chef', { key: null })).status, 401);
});
