// JS-interop wrapper around the Monaco editor for the SQL workbench.
// The Monaco AMD loader (loader.min.js) is loaded globally in App.razor; we lazily configure it
// and load the editor on first use. One editor instance per element id.

let _monacoReady = null;

function ensureMonaco() {
    if (_monacoReady) return _monacoReady;
    _monacoReady = new Promise((resolve, reject) => {
        if (window.monaco) { resolve(window.monaco); return; }
        if (!window.require) { reject(new Error('Monaco loader not available')); return; }
        try {
            window.require.config({
                paths: { vs: 'https://cdnjs.cloudflare.com/ajax/libs/monaco-editor/0.40.0/min/vs' }
            });
            window.require(['vs/editor/editor.main'], () => resolve(window.monaco), reject);
        } catch (e) {
            reject(e);
        }
    });
    return _monacoReady;
}

const editors = {};

export async function init(elementId, dotnetRef, value) {
    const monaco = await ensureMonaco();
    const el = document.getElementById(elementId);
    if (!el) return;

    const editor = monaco.editor.create(el, {
        value: value || '',
        language: 'sql',
        theme: 'vs',
        automaticLayout: true,
        minimap: { enabled: false },
        scrollBeyondLastLine: false,
        fontSize: 13,
        lineNumbers: 'on',
        tabSize: 2,
    });

    editor.onDidChangeModelContent(() => {
        dotnetRef.invokeMethodAsync('OnValueChanged', editor.getValue());
    });

    editors[elementId] = editor;
}

export function setValue(elementId, value) {
    const editor = editors[elementId];
    if (editor && editor.getValue() !== (value || '')) {
        editor.setValue(value || '');
    }
}

export function getValue(elementId) {
    const editor = editors[elementId];
    return editor ? editor.getValue() : '';
}

export function dispose(elementId) {
    const editor = editors[elementId];
    if (editor) {
        editor.dispose();
        delete editors[elementId];
    }
}
