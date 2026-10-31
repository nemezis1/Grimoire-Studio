// Small browser helpers: localStorage access that never throws, and printing.

export function getItem(key) {
    try {
        return localStorage.getItem(key);
    } catch {
        return null;
    }
}

/** Returns false when the browser refuses to store (private mode, quota). */
export function setItem(key, value) {
    try {
        localStorage.setItem(key, value);
        return true;
    } catch {
        return false;
    }
}

/**
 * Fonts used only in print (e.g. the script title) are never needed on screen, so the browser does not download
 * them and the printout falls back to a default font. Loading them up front fixes that, also for Ctrl+P.
 */
export async function preloadFonts(families) {
    try {
        await Promise.all(families.map(family => document.fonts.load(`32px "${family}"`)));
    } catch {
        // Printing still works with the fallback font.
    }
}

/**
 * Shrinks the content of each print page until it fits: every .print-page__content has a fixed height and its
 * .print-page__inner is zoomed (and widened by the same factor, so lines keep the full page width).
 */
export function fitPages(root) {
    if (!root) {
        return;
    }

    for (const content of root.querySelectorAll('.print-page__content')) {
        const inner = content.querySelector('.print-page__inner');
        if (!inner) {
            continue;
        }

        // A "fill" inner is as tall as the page; it overflows instead of growing, so measure its scroll height.
        const fill = inner.classList.contains('print-page__inner--fill');
        if (fill) {
            inner.style.zoom = 1;
            inner.style.width = `${content.clientWidth}px`;
            inner.style.height = `${content.clientHeight}px`;
            fitNightColumns(inner);
        }

        // Whole-page shrinking: for fill pages only when shrinking the descriptions was not enough.
        let zoom = 1;
        for (let attempt = 0; attempt < 5; attempt++) {
            // Sizes in pixels: percentages inside a zoomed element are not scaled by the zoom.
            inner.style.zoom = zoom;
            inner.style.width = `${content.clientWidth / zoom}px`;
            if (fill) {
                inner.style.height = `${content.clientHeight / zoom}px`;
            }
            const [space, needed] = fill
                ? [inner.clientHeight, inner.scrollHeight]
                : [content.getBoundingClientRect().height, inner.getBoundingClientRect().height];
            // A couple of pixels is rounding, not overflow.
            if (needed <= space + 2) {
                break;
            }
            const ratio = space / Math.max(1, needed);
            zoom = Math.max(0.4, zoom * ratio * 0.99);
        }
        content.classList.toggle('print-page__content--shrunk', zoom < 0.999);
    }
}

/**
 * Night order: each column that is taller than the page gets smaller descriptions (--desc-scale); headings, icons
 * and role names keep their size, and a column that fits is left alone.
 */
function fitNightColumns(inner) {
    for (const grid of inner.querySelectorAll('.print-nights')) {
        const space = grid.clientHeight;
        for (const column of grid.querySelectorAll('.print-night')) {
            let scale = 1;
            column.style.setProperty('--desc-scale', scale);
            while (column.offsetHeight > space + 1 && scale > 0.5) {
                scale = Math.max(0.5, scale - 0.04);
                column.style.setProperty('--desc-scale', scale);
            }
        }
    }
}

/** Scales an image (data URL) down to fit maxSize × maxSize and returns it as a PNG data URL. */
export async function resizeImage(dataUrl, maxSize) {
    // onload rather than decode(): decode() can stay pending while the page is in the background.
    const img = await new Promise((resolve, reject) => {
        const image = new Image();
        image.onload = () => resolve(image);
        image.onerror = () => reject(new Error('Nie można odczytać obrazka.'));
        image.src = dataUrl;
    });
    const scale = Math.min(1, maxSize / Math.max(img.width, img.height));
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(img.width * scale);
    canvas.height = Math.round(img.height * scale);
    canvas.getContext('2d').drawImage(img, 0, 0, canvas.width, canvas.height);
    return canvas.toDataURL('image/png');
}

export async function print(families = []) {
    await preloadFonts(families);
    window.print();
}

// --- Local changes (roles, scripts, teams, jinxes) in IndexedDB: uploaded icons do not fit in localStorage ---------

const LOCAL_DB = 'grimoire-studio-local';
const LOCAL_STORE = 'kv';

function openLocalDb() {
    return new Promise((resolve, reject) => {
        const request = indexedDB.open(LOCAL_DB, 1);
        request.onupgradeneeded = () => request.result.createObjectStore(LOCAL_STORE);
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
}

async function localRequest(mode, action) {
    const db = await openLocalDb();
    try {
        return await new Promise((resolve, reject) => {
            const request = action(db.transaction(LOCAL_STORE, mode).objectStore(LOCAL_STORE));
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
    } finally {
        db.close();
    }
}

/** Returns the stored text, or null when there is none or IndexedDB is unavailable. */
export async function dbGet(key) {
    try {
        return (await localRequest('readonly', store => store.get(key))) ?? null;
    } catch {
        return null;
    }
}

/** Returns false when the browser refuses to store (private mode, quota). */
export async function dbSet(key, value) {
    try {
        await localRequest('readwrite', store => store.put(value, key));
        return true;
    } catch {
        return false;
    }
}

export function removeItem(key) {
    try {
        localStorage.removeItem(key);
    } catch {
        // Nothing to clean up.
    }
}

/** Copies text to the clipboard; false when the browser does not allow it. */
export async function copyText(text) {
    try {
        await navigator.clipboard.writeText(text);
        return true;
    } catch {
        return false;
    }
}

/** Saves text as a file through the browser's download. */
export function downloadText(fileName, text, type = 'application/json') {
    const url = URL.createObjectURL(new Blob([text], { type }));
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/** Saves bytes (a Uint8Array from .NET) as a file through the browser's download. */
export function downloadBytes(fileName, bytes, type) {
    downloadText(fileName, bytes, type);
}

// --- Per-tab session values (sessionStorage): kept while browsing, gone when the tab is closed ---------------------

export function sessionGet(key) {
    try {
        return sessionStorage.getItem(key);
    } catch {
        return null;
    }
}

export function sessionSet(key, value) {
    try {
        if (value === null) {
            sessionStorage.removeItem(key);
        } else {
            sessionStorage.setItem(key, value);
        }
    } catch {
        // Without sessionStorage the value lasts until the page is reloaded.
    }
}
