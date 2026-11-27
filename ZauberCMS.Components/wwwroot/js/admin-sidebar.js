// ZauberCMS admin: the draggable divider between the navigation and the page (SectionLayout.razor, owner request
// 2026-11-27). The width is Radzen's --rz-sidebar-width, set on <html> so it applies before the admin first paints,
// kept per browser in localStorage. Drag the divider, or focus it and use the arrow keys (Shift for bigger steps),
// Home (narrowest) and End (widest); double-click it or press Enter to go back to the theme's width.
// Loaded in <head> by App.razor for admin pages; it listens on the document, so the divider can be re-rendered freely.
(() => {
    const storageKey = 'zauber.admin.sidebarWidth';
    const widthProperty = '--rz-sidebar-width';
    const minWidth = 200;
    const maxWidth = 720;
    const maxShareOfWindow = 0.6;
    const step = 16;
    const bigStep = 64;
    const root = document.documentElement;

    const widest = () => Math.max(minWidth, Math.min(maxWidth, Math.round(window.innerWidth * maxShareOfWindow)));
    const clamp = width => Math.min(widest(), Math.max(minWidth, Math.round(width)));

    const readSaved = () => {
        try {
            const value = parseInt(localStorage.getItem(storageKey) ?? '', 10);
            return Number.isFinite(value) ? value : null;
        } catch {
            return null; // storage blocked: the theme's width
        }
    };

    const save = width => {
        try {
            if (width == null) {
                localStorage.removeItem(storageKey);
            } else {
                localStorage.setItem(storageKey, String(width));
            }
        } catch {
            // storage blocked: the width lasts until the page is left
        }
    };

    const apply = width => {
        if (width == null) {
            root.style.removeProperty(widthProperty);
        } else {
            root.style.setProperty(widthProperty, `${clamp(width)}px`);
        }
    };

    const sidebarOf = handle => handle.closest('.rz-sidebar');

    // The width the sidebar is set to (Radzen animates towards it, so measuring the sidebar can lag behind)
    const widthOf = handle => {
        const value = parseFloat(getComputedStyle(handle).getPropertyValue(widthProperty));
        return Number.isFinite(value) ? value : (sidebarOf(handle)?.getBoundingClientRect().width ?? null);
    };

    // The separator's value for assistive technology
    const describe = handle => {
        const width = widthOf(handle);
        handle.setAttribute('aria-valuemin', String(minWidth));
        handle.setAttribute('aria-valuemax', String(widest()));
        if (width != null) {
            handle.setAttribute('aria-valuenow', String(Math.round(width)));
        }
    };

    const setWidth = (handle, width) => {
        const clamped = width == null ? null : clamp(width);
        save(clamped);
        apply(clamped);
        describe(handle);
    };

    const handleFrom = target => target instanceof Element ? target.closest('.zauber-sidebar-resizer') : null;

    // Before the admin renders: the saved width shows at once
    apply(readSaved());

    // A smaller window can make the saved width too wide; it is kept and applied again when there is room
    window.addEventListener('resize', () => {
        const saved = readSaved();
        if (saved != null) {
            apply(saved);
        }
    });

    let drag = null;

    document.addEventListener('pointerdown', event => {
        const handle = handleFrom(event.target);
        const sidebar = handle && sidebarOf(handle);
        if (!sidebar || event.button !== 0) {
            return;
        }

        event.preventDefault();
        drag = { handle, sidebar, pointerId: event.pointerId, left: sidebar.getBoundingClientRect().left, width: null };
        handle.setPointerCapture(event.pointerId);
        handle.classList.add('is-dragging');
        // Radzen animates the width; while dragging it follows the pointer instead
        sidebar.style.transition = 'none';
        document.body.style.userSelect = 'none';
        document.body.style.cursor = 'col-resize';
    });

    document.addEventListener('pointermove', event => {
        if (!drag || event.pointerId !== drag.pointerId) {
            return;
        }

        if ((event.buttons & 1) === 0) {
            // The button was released where no pointerup reached us (capture lost): the drag is over
            endDrag();
            return;
        }

        drag.width = clamp(event.clientX - drag.left);
        root.style.setProperty(widthProperty, `${drag.width}px`);
        describe(drag.handle);
    });

    const endDrag = () => {
        if (!drag) {
            return;
        }

        if (drag.width != null) {
            save(drag.width);
        }

        drag.handle.classList.remove('is-dragging');
        drag.sidebar.style.transition = '';
        document.body.style.userSelect = '';
        document.body.style.cursor = '';
        drag = null;
    };

    const endDragOf = event => {
        if (drag && event.pointerId === drag.pointerId) {
            endDrag();
        }
    };

    document.addEventListener('pointerup', endDragOf);
    document.addEventListener('pointercancel', endDragOf);
    // The divider was re-rendered or removed mid-drag
    document.addEventListener('lostpointercapture', endDragOf, true);

    document.addEventListener('dblclick', event => {
        const handle = handleFrom(event.target);
        if (handle) {
            setWidth(handle, null);
        }
    });

    document.addEventListener('keydown', event => {
        const handle = handleFrom(event.target);
        if (!handle) {
            return;
        }

        const width = widthOf(handle) ?? minWidth;
        const amount = event.shiftKey ? bigStep : step;
        switch (event.key) {
            case 'ArrowLeft':
                setWidth(handle, width - amount);
                break;
            case 'ArrowRight':
                setWidth(handle, width + amount);
                break;
            case 'Home':
                setWidth(handle, minWidth);
                break;
            case 'End':
                setWidth(handle, widest());
                break;
            case 'Enter':
                setWidth(handle, null);
                break;
            default:
                return;
        }

        event.preventDefault();
    });

    document.addEventListener('focusin', event => {
        const handle = handleFrom(event.target);
        if (handle) {
            describe(handle);
        }
    });
})();
