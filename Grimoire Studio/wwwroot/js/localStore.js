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

export async function print(families = []) {
    await preloadFonts(families);
    window.print();
}
