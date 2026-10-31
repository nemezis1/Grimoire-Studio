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

/** In-memory stand-in for the GitHub API: one branch, commits, trees and a compare endpoint. */
let github;
const realFetch = globalThis.fetch;

beforeEach(async () => {
    env.ADMIN_KEYS = JSON.stringify({ Kasia: await sha256Hex(KEY) });
    github = {
        head: 'aaaaaaa1111',
        commits: { aaaaaaa1111: { tree: { sha: 'tree0' } } },
        changedSince: {},
        calls: [],
        refusePatch: false,
    };
    globalThis.fetch = async (url, init = {}) => {
        const path = url.replace('https://api.github.com/repos/owner/repo/', '');
        const body = init.body ? JSON.parse(init.body) : undefined;
        github.calls.push({ method: init.method, path, body });
        const reply = (value, status = 200) => new Response(JSON.stringify(value), { status });

        if (path === 'git/ref/heads/main') return reply({ object: { sha: github.head } });
        if (path.startsWith('git/commits/')) return reply(github.commits[path.slice('git/commits/'.length)]);
        if (path === 'git/blobs') return reply({ sha: `blob-${github.calls.length}` });
        if (path === 'git/trees') return reply({ sha: body.tree.length ? 'tree1' : body.base_tree });
        if (path === 'git/commits') return reply({ sha: 'bbbbbbb2222', html_url: 'https://github.com/owner/repo/commit/bbbbbbb2222' });
        if (path === 'git/refs/heads/main') return github.refusePatch ? reply({ message: 'not fast forward' }, 422) : reply({});
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
    const response = await call('GET', '/me');
    assert.equal(response.status, 200);
    assert.deepEqual(await response.json(), { name: 'Kasia' });
    assert.equal(response.headers.get('Access-Control-Allow-Origin'), SITE);
});

test('a wrong or missing key is refused', async () => {
    assert.equal((await call('GET', '/me', { key: 'wrong' })).status, 401);
    assert.equal((await call('GET', '/me', { key: null })).status, 401);
});

test('preflight is allowed only for listed origins', async () => {
    assert.equal((await call('OPTIONS', '/commit', { origin: SITE })).status, 204);
    assert.equal((await call('OPTIONS', '/commit', { origin: 'https://evil.example' })).status, 403);
});

test('commit writes the files on top of the branch with the admin as author', async () => {
    const response = await call('POST', '/commit', {
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
        const response = await call('POST', '/commit', { body: { files: [file(path)] } });
        assert.equal(response.status, 400, path);
    }
    assert.ok(!github.calls.some(c => c.method === 'POST'));
});

test('a change to the same file since the base version is a conflict', async () => {
    github.head = 'ccccccc3333';
    github.commits.ccccccc3333 = { tree: { sha: 'tree0' } };
    github.changedSince.aaaaaaa = ['data/roles.json'];

    const response = await call('POST', '/commit', { body: { baseVersion: 'aaaaaaa', files: [file('data/roles.json')] } });

    assert.equal(response.status, 409);
    assert.deepEqual((await response.json()).conflicts, ['data/roles.json']);
    assert.ok(!github.calls.some(c => c.method === 'POST'));
});

test('changes to other files since the base version are fine', async () => {
    github.head = 'ccccccc3333';
    github.commits.ccccccc3333 = { tree: { sha: 'tree0' } };
    github.changedSince.aaaaaaa = ['data/hatred.json'];

    const response = await call('POST', '/commit', { body: { baseVersion: 'aaaaaaa', files: [file('data/roles.json')] } });

    assert.equal(response.status, 200);
});

test('a branch moved during the commit is reported as a conflict', async () => {
    github.refusePatch = true;

    const response = await call('POST', '/commit', { body: { files: [file('data/roles.json')] } });

    assert.equal(response.status, 409);
});

test('without a key nothing reaches GitHub', async () => {
    const response = await call('POST', '/commit', { key: null, body: { files: [file('data/roles.json')] } });

    assert.equal(response.status, 401);
    assert.equal(github.calls.length, 0);
});

test('a broken ADMIN_KEYS secret is reported as a configuration error', async () => {
    env.ADMIN_KEYS = '"3f9a"';

    const response = await call('GET', '/me');

    assert.equal(response.status, 500);
    assert.match((await response.json()).error, /ADMIN_KEYS/);
});
