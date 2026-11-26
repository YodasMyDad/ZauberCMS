export function initializeFilePaste(fileDropContainer, inputFile) {

    function onPaste(e) {
        // Disabled while the last pick is being read: new files would make that pick unreadable (ZA-100)
        if (inputFile.disabled) {
            return;
        }
        inputFile.files = e.clipboardData.files;
        const event = new Event('change', { bubbles: true });
        inputFile.dispatchEvent(event);
    }

    fileDropContainer.addEventListener('paste', onPaste);

    return {
        dispose: () => {
            fileDropContainer.removeEventListener('paste', onPaste);
        }
    }
}