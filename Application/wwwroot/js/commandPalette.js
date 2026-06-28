// Global "/" command palette trigger.
// Listens for the "/" key anywhere on the document (except while typing in an
// editable element) and asks the Blazor CommandPalette component to open.
window.commandPalette = {
    _handler: null,

    register: function (dotnetRef) {
        // Avoid stacking listeners across interactive re-renders.
        if (this._handler) {
            document.removeEventListener('keydown', this._handler);
        }

        this._handler = function (e) {
            // "/" opens the page palette, "#" opens the resource search.
            const isTrigger = (e.key === '/' || e.key === '#');
            if (!isTrigger || e.ctrlKey || e.metaKey || e.altKey) return;

            const t = e.target;
            const tag = t && t.tagName ? t.tagName.toLowerCase() : '';
            const isEditable =
                tag === 'input' ||
                tag === 'textarea' ||
                tag === 'select' ||
                (t && t.isContentEditable);

            if (isEditable) return; // let the user type the key in fields

            e.preventDefault();
            dotnetRef.invokeMethodAsync('OpenFromJs', e.key);
        };

        document.addEventListener('keydown', this._handler);
    },

    unregister: function () {
        if (this._handler) {
            document.removeEventListener('keydown', this._handler);
            this._handler = null;
        }
    },

    focus: function (el) {
        if (el) {
            // Wait a tick so the element is in the DOM after the render.
            setTimeout(function () { el.focus(); el.select && el.select(); }, 20);
        }
    }
};
