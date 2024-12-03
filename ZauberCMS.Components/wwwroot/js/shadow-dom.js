window.cachedStylesheets = window.cachedStylesheets || new Map();

window.initializeShadowDOMWithMultipleStylesheets = (element, stylesheetPaths) => {
    if (!element || typeof element.attachShadow !== 'function') {
        return;
    }

    if (!Array.isArray(stylesheetPaths)) {
        return;
    }

    const shadowRoot = element.attachShadow({ mode: 'open' });

    stylesheetPaths.forEach((stylesheetPath) => {
        if (cachedStylesheets.has(stylesheetPath)) {
            // Reuse cached style
            shadowRoot.appendChild(cachedStylesheets.get(stylesheetPath).cloneNode(true));
        } else {
            // Load new stylesheet
            const link = document.createElement('link');
            link.rel = 'stylesheet';
            link.href = stylesheetPath;

            // Cache the stylesheet
            cachedStylesheets.set(stylesheetPath, link);
            shadowRoot.appendChild(link);
        }
    });

    // Move the inner content of the component to the shadow root
    while (element.firstChild) {
        shadowRoot.appendChild(element.firstChild);
    }
};
