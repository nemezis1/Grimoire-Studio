// Keyboard and history glue for the header search (Components/GlobalSearch.razor):
// - Ctrl+K / Cmd+K, or "/" outside a text field, opens it;
// - while open, Escape closes, arrows move through the results and Enter opens the marked one;
// - opening adds a history entry, so the phone's back button closes the search instead of leaving the page.

let component = null;
let open = false;
let pushed = false;

function typing(target) {
    return target instanceof HTMLElement
        && (target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName));
}

function onKeyDown(e) {
    if (!component) return;

    if (!open) {
        const shortcut = (e.key === 'k' || e.key === 'K') && (e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey;
        const slash = e.key === '/' && !e.ctrlKey && !e.metaKey && !e.altKey && !typing(e.target);
        if (shortcut || slash) {
            e.preventDefault();
            component.invokeMethodAsync('OpenFromKey');
        }
        return;
    }

    switch (e.key) {
        case 'Escape':
            e.preventDefault();
            component.invokeMethodAsync('CloseFromKey');
            break;
        case 'ArrowDown':
        case 'ArrowUp':
            e.preventDefault();
            component.invokeMethodAsync('Move', e.key === 'ArrowDown' ? 1 : -1);
            break;
        case 'Enter':
            if (e.target instanceof HTMLElement && e.target.classList.contains('search-dialog__input')) {
                e.preventDefault();
                component.invokeMethodAsync('Choose');
            }
            break;
    }
}

function onPopState() {
    if (open && pushed) {
        pushed = false;
        component?.invokeMethodAsync('CloseFromBack');
    }
}

export function init(dotNet) {
    component = dotNet;
    document.addEventListener('keydown', onKeyDown);
    window.addEventListener('popstate', onPopState);
}

export function dispose() {
    component = null;
    document.removeEventListener('keydown', onKeyDown);
    window.removeEventListener('popstate', onPopState);
}

/** Called after the dialog is drawn: focus the field, stop the page behind from scrolling, add the history entry. */
export function opened(input) {
    open = true;
    document.documentElement.classList.add('search-is-open');
    input?.focus();
    if (!pushed) {
        history.pushState(history.state, '', location.href);
        pushed = true;
    }
}

/**
 * Called when the dialog closes. Without a result to open, the extra history entry is taken back; with one, the
 * navigation replaces it (NavigateTo with replace), so Back returns to the page the search was opened on.
 */
export function closed(navigating) {
    open = false;
    document.documentElement.classList.remove('search-is-open');
    if (pushed && !navigating) {
        pushed = false;
        history.back();
    }
    pushed = false;
}

/** Keeps the marked result visible while moving with the arrows. */
export function reveal(selector) {
    document.querySelector(selector)?.scrollIntoView({ block: 'nearest' });
}
