// Creates a key for a new admin: prints the key to hand over and the entry to add to the ADMIN_KEYS secret.
// Usage: node add-admin.mjs "Name"
import { randomBytes } from 'node:crypto';
import { sha256Hex } from './src/index.js';

const name = process.argv[2];
if (!name) {
    console.error('Usage: node add-admin.mjs "Name"');
    process.exit(1);
}

const key = randomBytes(24).toString('base64url');
console.log(`Key for ${name} (give it to them, it is not stored anywhere):\n  ${key}\n`);
console.log(`Entry for ADMIN_KEYS:\n  ${JSON.stringify({ [name]: await sha256Hex(key) })}`);
