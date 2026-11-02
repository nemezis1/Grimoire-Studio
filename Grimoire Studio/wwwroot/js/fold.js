// Folding rows of controls (e.g. the filters of a list): an element with data-fold-rows="2" gets data-folded while
// its items would take more than that many rows, at any window width. CSS then hides the foldable part behind a
// button (see "Smaller windows and phones" in css/app.css).

const watched = new WeakSet();

/** Number of rows the visible items take when nothing is folded. */
function rowsUnfolded(element) {
    delete element.dataset.folded;
    // The controls themselves (a group of chips wraps inside); items of one row differ in height, so tops within
    // 20 px are one row.
    const tops = [...element.querySelectorAll('input, select, button, summary, .filters__count')]
        .filter(item => item.offsetParent !== null)
        .map(item => item.getBoundingClientRect().top)
        .sort((a, b) => a - b);
    let rows = 0;
    let rowTop = -Infinity;
    for (const top of tops) {
        if (top > rowTop + 20) {
            rows++;
            rowTop = top;
        }
    }
    return rows;
}

function update(element) {
    const max = Number(element.dataset.foldRows) || 2;
    if (rowsUnfolded(element) > max) {
        element.dataset.folded = '';
    }
}

function watch(element) {
    if (watched.has(element)) {
        return;
    }
    watched.add(element);

    let width = 0;
    let pending = false;
    const schedule = force => {
        const current = element.clientWidth;
        if (!force && current === width) {
            return;
        }
        width = current;
        if (!pending) {
            pending = true;
            requestAnimationFrame(() => {
                pending = false;
                update(element);
            });
        }
    };

    // Only a change of width (or of the items) can change the rows; folding itself changes just the height.
    new ResizeObserver(() => schedule(false)).observe(element);
    new MutationObserver(() => schedule(true)).observe(element, { childList: true, subtree: true, characterData: true });
    schedule(true);
}

new MutationObserver(() => document.querySelectorAll('[data-fold-rows]').forEach(watch))
    .observe(document.body, { childList: true, subtree: true });
