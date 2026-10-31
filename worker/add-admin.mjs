// Manages the OWNERS of the publishing service (the ADMIN_KEYS secret). Owners manage the other admins in the admin
// panel; this script is only needed to add or change an owner. The list (name and SHA-256 of the key) is kept in
// admins.json next to this script (git-ignored); the ADMIN_KEYS secret is always set from the whole file.
//
//   node add-admin.mjs "Name"                  new admin with a new key (printed once)
//   node add-admin.mjs "Name" --key <key>      add an admin with a key they already have
//   node add-admin.mjs --remove "Name"         take the access away
//
// Then upload the list (PowerShell):  Get-Content admins.json | npx wrangler secret put ADMIN_KEYS
import { randomBytes } from 'node:crypto';
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';
import { sha256Hex } from './src/index.js';

const file = fileURLToPath(new URL('./admins.json', import.meta.url));
const admins = fs.existsSync(file) ? JSON.parse(fs.readFileSync(file, 'utf8')) : {};
const args = process.argv.slice(2);

if (args[0] === '--remove' && args[1]) {
    delete admins[args[1]];
    console.log(`Removed ${args[1]}.`);
} else if (args[0] && !args[0].startsWith('--')) {
    const name = args[0];
    const given = args[1] === '--key' ? args[2] : undefined;
    const key = given ?? randomBytes(24).toString('base64url');
    admins[name] = await sha256Hex(key);
    if (!given) {
        console.log(`Key for ${name} (give it to them, it is not stored anywhere):\n  ${key}\n`);
    }
    console.log(`Added ${name}.`);
} else {
    console.error('Usage: node add-admin.mjs "Name" [--key <key>] | node add-admin.mjs --remove "Name"');
    process.exit(1);
}

fs.writeFileSync(file, JSON.stringify(admins));
console.log(`\nAdmins in admins.json: ${Object.keys(admins).join(', ') || '(none)'}`);
console.log('Upload them (PowerShell):  Get-Content admins.json | npx wrangler secret put ADMIN_KEYS');
