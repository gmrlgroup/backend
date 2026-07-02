// Chart.js helpers for the Out-of-Stock dashboard (Chart.js is loaded globally in App.razor).
// One chart instance per canvas id; each render() replaces the existing chart on that canvas.

const charts = {};

function applyDefaults() {
    if (!window.Chart) return false;
    window.Chart.defaults.font.family = "'Inter',sans-serif";
    window.Chart.defaults.font.size = 12;
    window.Chart.defaults.color = "#6B6A66";
    return true;
}

export function renderCategoryChart(canvasId, labels, data) {
    if (!applyDefaults()) return false;
    destroy(canvasId);
    const el = document.getElementById(canvasId);
    if (!el) return false;
    charts[canvasId] = new window.Chart(el.getContext('2d'), {
        type: 'bar',
        data: { labels, datasets: [{ data, backgroundColor: '#1F3A5F', borderRadius: 5, maxBarThickness: 34 }] },
        options: {
            responsive: true, maintainAspectRatio: false,
            plugins: { legend: { display: false }, tooltip: { callbacks: { label: c => c.parsed.y + ' SKUs' } } },
            scales: {
                x: { grid: { display: false } },
                y: { beginAtZero: true, ticks: { precision: 0 }, grid: { color: '#EDEBE4' } }
            }
        }
    });
    return true;
}

export function renderTrendChart(canvasId, labels, thisYear, priorYear) {
    if (!applyDefaults()) return false;
    destroy(canvasId);
    const el = document.getElementById(canvasId);
    if (!el) return false;
    charts[canvasId] = new window.Chart(el.getContext('2d'), {
        type: 'line',
        data: {
            labels,
            datasets: [
                {
                    label: 'This year', data: thisYear, borderColor: '#C8372D',
                    backgroundColor: 'rgba(200,55,45,.10)', borderWidth: 2, fill: true, tension: .35,
                    pointRadius: 0, pointHoverRadius: 4, pointBackgroundColor: '#C8372D', order: 1
                },
                {
                    label: 'Prior year', data: priorYear, borderColor: '#9A988F',
                    backgroundColor: 'transparent', borderWidth: 1.5, borderDash: [5, 4], fill: false, tension: .35,
                    pointRadius: 0, pointHoverRadius: 4, pointBackgroundColor: '#9A988F', order: 2
                }
            ]
        },
        options: {
            responsive: true, maintainAspectRatio: false,
            plugins: {
                legend: { display: true, position: 'top', align: 'end', labels: { boxWidth: 14, boxHeight: 2, usePointStyle: false, font: { size: 11.5 } } },
                tooltip: { callbacks: { label: c => c.dataset.label + ': ' + c.parsed.y.toFixed(2) + '%' } }
            },
            scales: {
                x: { grid: { display: false }, ticks: { maxRotation: 0, autoSkip: true, maxTicksLimit: 7 } },
                y: { beginAtZero: true, grid: { color: '#EDEBE4' }, ticks: { callback: v => v + '%' } }
            }
        }
    });
    return true;
}

export function destroy(canvasId) {
    const c = charts[canvasId];
    if (c) { c.destroy(); delete charts[canvasId]; }
}

// Triggers a client-side CSV download from in-browser content (filter-aware export).
export function downloadCsv(filename, content) {
    const blob = new Blob([content], { type: 'text/csv' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = filename;
    a.click();
    URL.revokeObjectURL(a.href);
}
