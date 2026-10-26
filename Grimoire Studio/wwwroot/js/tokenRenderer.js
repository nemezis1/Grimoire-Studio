// Token renderer on Canvas 2D. Ported from the WPF TokenGenerator (MainWindow.Drawing.cs) with automatic layout:
// the description starts below the top leaves and the picture fills the space between the description and the
// curved name.
// All pixel settings are expressed for a 500 px token and scaled to the requested size.

const REFERENCE_SIZE = 500;
const FALLBACK_FONTS = 'Georgia, "Times New Roman", serif';

const imageCache = new Map();
const trimCache = new WeakMap();
let renderSequence = 0;

function loadImage(url) {
    if (!url) {
        return Promise.resolve(null);
    }

    let pending = imageCache.get(url);
    if (!pending) {
        pending = new Promise(resolve => {
            const img = new Image();
            img.onload = () => resolve(img);
            img.onerror = () => resolve(null);
            img.src = url;
        });
        imageCache.set(url, pending);
    }

    return pending;
}

/** Bounding box of the non-transparent pixels, so art with empty padding scales by its visible part. */
function trimmed(img) {
    let box = trimCache.get(img);
    if (box) {
        return box;
    }

    box = { x: 0, y: 0, w: img.width, h: img.height };
    try {
        const canvas = document.createElement('canvas');
        canvas.width = img.width;
        canvas.height = img.height;
        const ctx = canvas.getContext('2d', { willReadFrequently: true });
        ctx.drawImage(img, 0, 0);
        const data = ctx.getImageData(0, 0, img.width, img.height).data;

        let minX = img.width, minY = img.height, maxX = -1, maxY = -1;
        for (let y = 0; y < img.height; y++) {
            for (let x = 0; x < img.width; x++) {
                if (data[(y * img.width + x) * 4 + 3] > 8) {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX >= 0) {
            box = { x: minX, y: minY, w: maxX - minX + 1, h: maxY - minY + 1 };
        }
    } catch {
        // Cross-origin or decoding problem: use the whole image.
    }

    trimCache.set(img, box);
    return box;
}

const fontCss = (weight, px, family) => `${weight} ${px}px "${family}", ${FALLBACK_FONTS}`;

async function loadFont(weight, family) {
    try {
        await document.fonts.load(fontCss(weight, 40, family));
    } catch {
        // Unknown family: the canvas falls back to FALLBACK_FONTS.
    }
}

// Point of the orange (setup) leaf, in pixels of its 539 px art, that stays in place when the leaf is scaled:
// the upper-right edge of the leaf next to the border.
const ORANGE_LEAF_ANCHOR = { x: 488, y: 118 };

function leafNames(s) {
    const names = [];
    if (s.leafOtherNights) names.push(['leaf-right', 'right']);
    if (s.leafFirstNight) names.push(['leaf-left', 'left']);
    if (s.leafSetup) names.push(['leaf-orange', ORANGE_LEAF_ANCHOR]);
    if (s.leafReminders) names.push([`leaf-top${Math.min(5, Math.max(1, s.leafReminderCount))}`, 'top']);
    return names;
}

async function prepare(s) {
    const leaves = s.tokenType === 'player' ? leafNames(s) : [];

    const [background, image, ...leafImages] = await Promise.all([
        loadImage(s.effectiveBackgroundUrl),
        loadImage(s.imageUrl),
        ...leaves.map(([name]) => loadImage(`Assets/${name}.png`)),
        loadFont('bold', s.font),
        loadFont('normal', s.descriptionFont),
    ]);

    return {
        background,
        image,
        leaves: leaves.map(([, direction], i) => ({ img: leafImages[i], direction })).filter(l => l.img),
    };
}

/** Half-width of the circle (radius `radius`, centred at `r`) at height y; 0 outside. */
function halfChord(y, r, radius) {
    const dy = y - r;
    return dy * dy >= radius * radius ? 0 : Math.sqrt(radius * radius - dy * dy);
}

const profileCache = new WeakMap();

/** First and last opaque row of every column (-1 when the column is empty). */
function columnProfile(img) {
    let profile = profileCache.get(img);
    if (profile) {
        return profile;
    }

    profile = { top: new Int32Array(img.width).fill(-1), bottom: new Int32Array(img.width).fill(-1) };
    try {
        const canvas = document.createElement('canvas');
        canvas.width = img.width;
        canvas.height = img.height;
        const ctx = canvas.getContext('2d', { willReadFrequently: true });
        ctx.drawImage(img, 0, 0);
        const data = ctx.getImageData(0, 0, img.width, img.height).data;
        for (let x = 0; x < img.width; x++) {
            for (let y = 0; y < img.height; y++) {
                if (data[(y * img.width + x) * 4 + 3] > 8) {
                    if (profile.top[x] < 0) profile.top[x] = y;
                    profile.bottom[x] = y;
                }
            }
        }
    } catch {
        // Without pixel access the leaves are still drawn; only text avoidance is less precise.
    }

    profileCache.set(img, profile);
    return profile;
}

/** y of the token outline (circle of radius r) above x. */
const outlineTop = (x, r) => r - Math.sqrt(Math.max(0, r * r - (x - r) * (x - r)));

/**
 * Top leaves bend with the token: the art is scaled from the top centre and every thin vertical slice keeps its
 * distance from the outline at its new position. Slices only move down, never rotate, so side leaves keep their
 * direction and still touch the edge. At scale 1 the art is unchanged.
 */
function layoutTopLeaves(img, s, r) {
    const box = trimmed(img);
    const profile = columnProfile(img);
    const unit = (r * 2) / img.width;
    const scale = s.leafScale;
    const step = 2;
    const slices = [];

    for (let sx = box.x; sx < box.x + box.w; sx += step) {
        const sw = Math.min(step, box.x + box.w - sx);
        const x0 = (sx + sw / 2) * unit;
        const x = r + (x0 - r) * scale;
        if (Math.abs(x - r) >= r) {
            continue;
        }

        const dy = outlineTop(x, r) - outlineTop(x0, r) * scale;
        let bottom = -1;
        for (let c = sx; c < sx + sw; c++) bottom = Math.max(bottom, profile.bottom[c]);

        slices.push({
            sx,
            sw,
            x: r + (sx * unit - r) * scale,
            w: sw * unit * scale + 0.5,
            y: box.y * unit * scale + dy,
            h: box.h * unit * scale,
            // Lowest opaque point of the slice, used to keep the description clear of the leaves.
            bottom: bottom < 0 ? -1 : bottom * unit * scale + dy,
        });
    }

    return [{ img, box, direction: 'top', slices }];
}

function drawSlices(ctx, part, r) {
    const { img, box, slices } = part;
    ctx.save();
    clipCircle(ctx, r, r, r);
    for (const slice of slices) {
        ctx.drawImage(img, slice.sx, box.y, slice.sw, box.h, slice.x, slice.y, slice.w, slice.h);
    }
    ctx.restore();
}

/**
 * Leaf art is a full-token overlay; at scale 1 it covers the token exactly. Side leaves stay pinned to their edge,
 * as in the WPF generator; top leaves bend with the outline.
 */
function layoutLeaf({ img, direction }, s, r) {
    if (direction === 'top') {
        return layoutTopLeaves(img, s, r);
    }

    const full = { x: 0, y: 0, w: img.width, h: img.height };
    if (typeof direction === 'object') {
        // Scaled around a fixed anchor point of the art.
        const unit = (r * 2) / img.width;
        const size = r * 2 * s.leafScale;
        const rect = {
            x: direction.x * unit * (1 - s.leafScale),
            y: direction.y * unit * (1 - s.leafScale),
            w: size,
            h: size,
        };
        return [{ img, box: full, rect, direction: 'anchor' }];
    }

    const box = r * 2 * s.leafScale;
    const scale = Math.min(box / img.width, box / img.height);
    const w = img.width * scale;
    const h = img.height * scale;
    const dx = direction === 'right' ? r - w / 2 : direction === 'left' ? -r + w / 2 : 0;
    const rect = { x: r - w / 2 + dx, y: r - h / 2, w, h };

    return [{ img, box: full, rect, direction }];
}

/** Greedy word wrap; maxWidth may be a function of the line index. */
function wrap(ctx, text, maxWidth) {
    const widthOf = typeof maxWidth === 'function' ? maxWidth : () => maxWidth;
    const lines = [];
    for (const paragraph of text.split(/\r?\n/)) {
        let line = '';
        for (const word of paragraph.split(/\s+/).filter(Boolean)) {
            const candidate = line ? `${line} ${word}` : word;
            if (line && ctx.measureText(candidate).width > widthOf(lines.length)) {
                lines.push(line);
                line = word;
            } else {
                line = candidate;
            }
        }
        if (line) lines.push(line);
    }
    return lines;
}

/** Positions of every element for a player token. */
function layoutPlayer(ctx, s, assets, r, k) {
    const inner = r - s.borderThickness * k - 6 * k;
    const leaves = assets.leaves.flatMap(leaf => layoutLeaf(leaf, s, r));
    const slices = leaves.flatMap(l => l.slices ?? []).filter(slice => slice.bottom >= 0);
    const margin = 6 * k;

    // Width available to a text line whose top is at y: inside the border and clear of the bent top leaves.
    // Other leaves (sides, orange) may overlap the text.
    const halfWidthAt = y => {
        let half = halfChord(y, r, inner);
        for (const slice of slices) {
            if (slice.bottom + margin > y) {
                const left = slice.x - margin;
                const right = slice.x + slice.w + margin;
                const distance = left > r ? left - r : right < r ? r - right : 0;
                half = Math.min(half, distance);
            }
        }
        return half;
    };

    // Description: starts below the border and below the leaves around the centre line.
    const descSize = s.descriptionFontSize * k;
    const lineHeight = descSize * 1.2;
    const minHalfWidth = descSize * 2;
    const centreLeaves = slices.filter(slice => slice.x + slice.w > r - minHalfWidth && slice.x < r + minHalfWidth);
    const topLeafBottom = Math.max(0, ...centreLeaves.map(slice => slice.bottom));
    const descTop = Math.max(r - inner + 4 * k, topLeafBottom + margin) + s.descriptionVertical * r;
    let lines = [];
    if (s.description?.trim()) {
        ctx.font = fontCss('normal', descSize, s.descriptionFont);
        // In the upper half the top of a line is its narrowest point.
        lines = wrap(ctx, s.description, i => Math.max(descSize * 4, halfWidthAt(descTop + i * lineHeight) * 1.84));
    }
    const descBottom = descTop + lines.length * lineHeight;

    // Name on the bottom arc.
    const nameSize = s.fontSize * k;
    const base = Math.max(0, r - nameSize * 0.6);
    const nameRadius = Math.max(nameSize, Math.min(base + s.textVertical * (r - base), r - 2));
    const nameTop = s.text?.trim() ? r + nameRadius - nameSize * 0.9 : r + inner * 0.85;

    // Picture: fills the band between the description and the name.
    const bandTop = descBottom + 6 * k;
    const bandBottom = Math.max(bandTop + r * 0.3, nameTop - 4 * k);
    const centerY = (bandTop + bandBottom) / 2;
    const image = assets.image && fitImage(assets.image, s, r, centerY, bandBottom - bandTop, halfChord(centerY, r, inner) * 1.6);

    return { leaves, lines, descTop, lineHeight, nameRadius, image };
}

function layoutReminder(s, assets, r, k) {
    const inner = r - 10 * k;
    const image = assets.image && fitImage(assets.image, s, r, r - inner * 0.18, inner * 1.1, inner * 1.2);
    return { leaves: [], lines: [], image };
}

function fitImage(img, s, r, centerY, maxH, maxW) {
    const box = trimmed(img);
    // Player tokens: 100% is 85% of the free space, which leaves room around the picture.
    const base = s.tokenType === 'player' ? 0.85 : 1;
    const scale = Math.min(maxW / box.w, maxH / box.h) * base * s.imageScale;
    const w = box.w * scale;
    const h = box.h * scale;
    return {
        img,
        box,
        rect: {
            x: r - w / 2 + s.imageHorizontal * r * 2,
            y: centerY - h / 2 + s.imageVertical * r * 2,
            w,
            h,
        },
    };
}

function drawPart(ctx, part, r) {
    if (part.slices) {
        drawSlices(ctx, part, r);
        return;
    }

    const { img, box, rect } = part;
    ctx.drawImage(img, box.x, box.y, box.w, box.h, rect.x, rect.y, rect.w, rect.h);
}

function clipCircle(ctx, cx, cy, r) {
    ctx.beginPath();
    ctx.arc(cx, cy, r, 0, Math.PI * 2);
    ctx.clip();
}

function drawFace(ctx, background, r) {
    ctx.save();
    clipCircle(ctx, r, r, r);
    if (background) {
        const scale = Math.max((r * 2) / background.width, (r * 2) / background.height);
        const w = background.width * scale;
        const h = background.height * scale;
        ctx.drawImage(background, r - w / 2, r - h / 2, w, h);
    } else {
        ctx.fillStyle = '#e6e6e6';
        ctx.fillRect(0, 0, r * 2, r * 2);
    }
    ctx.restore();
}

function drawImage(ctx, image, r, k) {
    if (!image) {
        return;
    }

    ctx.save();
    clipCircle(ctx, r, r, r - 4 * k);
    drawPart(ctx, image);
    ctx.restore();
}

function shade(hex, factor) {
    const n = parseInt(hex.slice(1), 16);
    const channels = [(n >> 16) & 255, (n >> 8) & 255, n & 255];
    const mapped = factor >= 0
        ? channels.map(c => Math.round(c + (255 - c) * factor))
        : channels.map(c => Math.round(c * (1 + factor)));
    return `rgb(${mapped.join(',')})`;
}

function ringGradient(ctx, color, size) {
    // Same stops as the WPF brush: light top-right, dark bottom-left.
    const g = ctx.createLinearGradient(size * 0.8, size * 0.2, size * 0.2, size * 0.8);
    g.addColorStop(0, shade(color, 0.1));
    g.addColorStop(0.3, shade(color, 0.05));
    g.addColorStop(0.5, color);
    g.addColorStop(0.65, shade(color, -0.45));
    g.addColorStop(1, shade(color, -0.35));
    return g;
}

function drawBorder(ctx, s, r, k) {
    const thickness = s.borderThickness * k;
    if (thickness <= 0) {
        return;
    }

    const halves = s.borderColor2
        ? [[0, s.borderColor, r], [r, s.borderColor2, r]]
        : [[0, s.borderColor, r * 2]];

    for (const [x, color, width] of halves) {
        ctx.save();
        ctx.beginPath();
        ctx.rect(x, 0, width, r * 2);
        ctx.clip();
        ctx.beginPath();
        ctx.arc(r, r, Math.max(0, r - thickness / 2), 0, Math.PI * 2);
        ctx.lineWidth = thickness;
        ctx.strokeStyle = ringGradient(ctx, color, r * 2);
        ctx.stroke();
        ctx.restore();
    }
}

function drawDescription(ctx, s, layout, r, k) {
    ctx.font = fontCss('normal', s.descriptionFontSize * k, s.descriptionFont);
    ctx.fillStyle = s.textColor;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'top';

    let y = layout.descTop;
    for (const line of layout.lines) {
        ctx.fillText(line, r, y);
        y += layout.lineHeight;
    }
}

function drawCurvedLabel(ctx, s, text, radius, r, k) {
    const spacing = s.letterSpacing * k;
    const chars = [...text];
    const widths = chars.map(c => ctx.measureText(c).width);
    const length = widths.reduce((a, b) => a + b, 0) + Math.max(0, chars.length - 1) * spacing;

    // Text runs left to right along the bottom of the circle.
    let angle = Math.PI / 2 + length / radius / 2;
    ctx.textAlign = 'center';
    ctx.textBaseline = 'alphabetic';

    chars.forEach((c, i) => {
        const charAngle = widths[i] / radius;
        const mid = angle - charAngle / 2;
        ctx.save();
        ctx.translate(r + radius * Math.cos(mid), r + radius * Math.sin(mid));
        ctx.rotate(mid - Math.PI / 2);
        ctx.fillText(c, 0, 0);
        ctx.restore();
        angle -= charAngle + spacing / radius;
    });
}

function drawStraightLabel(ctx, s, r, k) {
    const size = s.fontSize * k;
    const spacing = s.letterSpacing * k;
    const lineHeight = size * 1.15;
    const lines = wrap(ctx, s.text, r * 1.6);
    let y = r + s.textVertical * r * 2 - (lines.length * lineHeight) / 2;

    ctx.textAlign = 'left';
    ctx.textBaseline = 'top';

    for (const line of lines) {
        const chars = [...line];
        const widths = chars.map(c => ctx.measureText(c).width);
        let x = r - (widths.reduce((a, b) => a + b, 0) + Math.max(0, chars.length - 1) * spacing) / 2;
        chars.forEach((c, i) => {
            ctx.fillText(c, x, y);
            x += widths[i] + spacing;
        });
        y += lineHeight;
    }
}

function drawLabel(ctx, s, layout, r, k) {
    if (!s.text?.trim()) {
        return;
    }

    ctx.font = fontCss('bold', s.fontSize * k, s.font);
    ctx.fillStyle = s.textColor;

    if (s.tokenType === 'player') {
        drawCurvedLabel(ctx, s, s.text.split(/\r?\n/).filter(Boolean).join(' '), layout.nameRadius, r, k);
    } else {
        drawStraightLabel(ctx, s, r, k);
    }
}

function draw(canvas, s, size, assets) {
    canvas.width = size;
    canvas.height = size;

    const ctx = canvas.getContext('2d');
    const r = size / 2;
    const k = size / REFERENCE_SIZE;
    const isPlayer = s.tokenType === 'player';
    const layout = isPlayer ? layoutPlayer(ctx, s, assets, r, k) : layoutReminder(s, assets, r, k);

    ctx.clearRect(0, 0, size, size);
    drawFace(ctx, assets.background, r);
    drawImage(ctx, layout.image, r, k);
    if (isPlayer) {
        drawBorder(ctx, s, r, k);
        layout.leaves.forEach(leaf => drawPart(ctx, leaf, r));
        drawDescription(ctx, s, layout, r, k);
    }
    drawLabel(ctx, s, layout, r, k);
}

// --- PNG with physical size --------------------------------------------------------------------------------------

const crcTable = Array.from({ length: 256 }, (_, n) => {
    let c = n;
    for (let i = 0; i < 8; i++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    return c >>> 0;
});

function crc32(bytes) {
    let c = 0xffffffff;
    for (const b of bytes) c = crcTable[(c ^ b) & 0xff] ^ (c >>> 8);
    return (c ^ 0xffffffff) >>> 0;
}

/** Inserts a pHYs chunk so image viewers and printers use the intended DPI (and thus the size in cm). */
async function withDpi(blob, dpi) {
    const png = new Uint8Array(await blob.arrayBuffer());
    const ppm = Math.round(dpi / 0.0254);
    const chunk = new Uint8Array(21);
    const view = new DataView(chunk.buffer);
    view.setUint32(0, 9);
    chunk.set([0x70, 0x48, 0x59, 0x73], 4); // "pHYs"
    view.setUint32(8, ppm);
    view.setUint32(12, ppm);
    chunk[16] = 1; // unit: metre
    view.setUint32(17, crc32(chunk.subarray(4, 17)));

    const afterHeader = 8 + 25; // signature + IHDR chunk
    return new Blob([png.subarray(0, afterHeader), chunk, png.subarray(afterHeader)], { type: 'image/png' });
}

/** Renders the token into an on-page canvas. Older calls still loading assets are dropped. */
export async function render(canvas, settings, size) {
    const sequence = ++renderSequence;
    const assets = await prepare(settings);
    if (sequence === renderSequence && canvas) {
        draw(canvas, settings, size, assets);
    }
}

/** Renders the token off-screen at the export size and downloads it as PNG tagged with the given DPI. */
export async function downloadPng(settings, size, dpi, fileName) {
    const canvas = document.createElement('canvas');
    draw(canvas, settings, size, await prepare(settings));

    const blob = await withDpi(await new Promise(resolve => canvas.toBlob(resolve, 'image/png')), dpi);
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = fileName;
    link.click();
    setTimeout(() => URL.revokeObjectURL(link.href), 1000);
}

/** Renders the token at the given size and returns an object URL of the PNG (free it with releaseUrl). */
export async function renderToUrl(settings, size) {
    const canvas = document.createElement('canvas');
    draw(canvas, settings, size, await prepare(settings));
    const blob = await new Promise(resolve => canvas.toBlob(resolve, 'image/png'));
    return URL.createObjectURL(blob);
}

export function releaseUrl(url) {
    if (url) {
        URL.revokeObjectURL(url);
    }
}

export function print() {
    window.print();
}

// --- Token sheet (per browser, IndexedDB: uploaded pictures can be too big for localStorage) ---------------------

const SHEET_DB = 'grimoire-studio';
const SHEET_STORE = 'sheet';
const SHEET_KEY = 'current';

function openSheetDb() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(SHEET_DB, 1);
        request.onupgradeneeded = () => request.result.createObjectStore(SHEET_STORE);
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

async function sheetRequest(mode, action) {
    const db = await openSheetDb();
    try {
        return await new Promise((resolve, reject) => {
            const request = action(db.transaction(SHEET_STORE, mode).objectStore(SHEET_STORE));
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
    } finally {
        db.close();
    }
}

/** Returns the saved sheet as JSON, or null when there is none or storage is unavailable. */
export async function loadSheet() {
    try {
        return (await sheetRequest('readonly', store => store.get(SHEET_KEY))) ?? null;
    } catch {
        return null;
    }
}

/** Stores the sheet JSON; returns false when the browser refuses (private mode, quota). */
export async function saveSheet(json) {
    try {
        await sheetRequest('readwrite', store => store.put(json, SHEET_KEY));
        return true;
    } catch {
        return false;
    }
}

// --- Presets (per browser) ---------------------------------------------------------------------------------------

const PRESETS_KEY = 'grimoire-studio.token-presets';

export function loadPresets() {
    try {
        return localStorage.getItem(PRESETS_KEY) ?? '[]';
    } catch {
        return '[]';
    }
}

export function savePresets(json) {
    try {
        localStorage.setItem(PRESETS_KEY, json);
        return true;
    } catch {
        return false;
    }
}
