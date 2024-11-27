window.initializeShadowDOM = (element, stylesheetPath) => {
    if (element) {
        // Attach a shadow root
        const shadowRoot = element.attachShadow({ mode: 'open' });

        // Load the stylesheet
        const link = document.createElement('link');
        link.rel = 'stylesheet';
        link.href = stylesheetPath;

        // Append the stylesheet and content
        shadowRoot.appendChild(link);

        // Move the inner content of the component to the shadow root
        while (element.firstChild) {
            shadowRoot.appendChild(element.firstChild);
        }
    }
};
