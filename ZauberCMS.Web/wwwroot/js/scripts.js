// Register an afterWebStarted initializer for SSR
Blazor.addEventListener('afterWebStarted', () => {
    
});

// For initial page load
document.addEventListener('DOMContentLoaded', () => {
    initializeAllScripts();
});

// Listen for enhanced navigation updates
Blazor.addEventListener('enhancedload', () => {
    initializeAllScripts();
});

window.initializeAllScripts = () => {

    window.initializeMainNavScript();
    
    // Add more scripts here
    
    console.log('Initialised all scripts');
};

window.initializeMainNavScript = () => {
    const mainNav = document.getElementById('mainNav');
    if (!mainNav) {
        console.warn('mainNav element not found');
        return;
    }

    let scrollPos = 0;
    const headerHeight = mainNav.clientHeight;

    // Remove any existing scroll listeners to prevent duplicates
    const existingHandler = mainNav.getAttribute('data-scroll-initialized');
    if (existingHandler === 'true') {
        return;
    }
    mainNav.setAttribute('data-scroll-initialized', 'true');

    window.addEventListener('scroll', function () {
        const currentTop = document.body.getBoundingClientRect().top * -1;
        if (currentTop < scrollPos) {
            // Scrolling Up
            if (currentTop > 0 && mainNav.classList.contains('is-fixed')) {
                mainNav.classList.add('is-visible');
            } else {
                mainNav.classList.remove('is-visible', 'is-fixed');
            }
        } else {
            // Scrolling Down
            mainNav.classList.remove('is-visible'); // Fixed: removed array brackets
            if (currentTop > headerHeight && !mainNav.classList.contains('is-fixed')) {
                mainNav.classList.add('is-fixed');
            }
        }
        scrollPos = currentTop;
    });
};