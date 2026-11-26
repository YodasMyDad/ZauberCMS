// Block previews in the Block List editor (BlockPreview.razor). The preview is static HTML rendered on the server;
// it goes into a shadow root with the editor's stylesheets, so the site's CSS styles the preview and nothing else.
// Blazor only owns the empty host element, never the nodes inside the shadow root.
window.zauberSetBlockPreview = (host, stylesheetPaths, html) => {
    if (!host || typeof host.attachShadow !== 'function') {
        return;
    }

    let root = host.shadowRoot;
    let container;
    if (!root) {
        root = host.attachShadow({ mode: 'open' });
        (Array.isArray(stylesheetPaths) ? stylesheetPaths : []).forEach((href) => {
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = href;
            link.addEventListener('error', () => {
                console.error('[Zauber] Failed to load block preview stylesheet:', href);
            });
            root.appendChild(link);
        });
        container = document.createElement('div');
        container.setAttribute('data-zauber-block-preview', '');
        root.appendChild(container);
    } else {
        container = root.querySelector('[data-zauber-block-preview]');
    }

    container.innerHTML = html;
};
