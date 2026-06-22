// Thin wrapper over Chart.js (loaded globally in App.razor) for the workbench "Visualize" tab.
// One chart instance per canvas id; render() replaces any existing chart on that canvas.

const charts = {};

export function render(canvasId, config) {
    if (!window.Chart) return false;
    destroy(canvasId);
    const el = document.getElementById(canvasId);
    if (!el) return false;
    charts[canvasId] = new window.Chart(el.getContext('2d'), config);
    return true;
}

export function destroy(canvasId) {
    const c = charts[canvasId];
    if (c) {
        c.destroy();
        delete charts[canvasId];
    }
}
