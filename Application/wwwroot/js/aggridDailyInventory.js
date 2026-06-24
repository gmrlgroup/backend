// AG Grid (Community) interop for the Daily Inventory comparison page.
// Blazor owns data fetching/paging; this module is just the renderer. AG Grid virtualizes the DOM,
// so thousands of rows stay smooth. .NET pushes rows in; the grid asks .NET for more on scroll.

window.aggridDailyInventory = (function () {
    let api = null;
    let dotNetRef = null;
    let hasMore = false;
    let pending = false;
    let detailNames = [];

    const fmtQty = p => (p.value == null || p.value === 0) ? '—' : Number(p.value).toLocaleString();

    const frontCols = () => ([
        { headerName: 'Item No', field: 'itemNo', minWidth: 130 },
        { headerName: 'Variant', field: 'variantCode', minWidth: 110 },
        { headerName: 'Location', field: 'locationCode', minWidth: 110 },
    ]);

    const tailCols = () => ([
        { headerName: 'Wk 6', field: 'week6Qty', type: 'rightAligned', valueFormatter: fmtQty },
        { headerName: 'Wk 5', field: 'week5Qty', type: 'rightAligned', valueFormatter: fmtQty },
        { headerName: 'Wk 4', field: 'week4Qty', type: 'rightAligned', valueFormatter: fmtQty },
        { headerName: 'Wk 3', field: 'week3Qty', type: 'rightAligned', valueFormatter: fmtQty },
        { headerName: 'Wk 2', field: 'week2Qty', type: 'rightAligned', valueFormatter: fmtQty },
        { headerName: 'Wk 1', field: 'week1Qty', type: 'rightAligned', valueFormatter: fmtQty },
        {
            headerName: 'Stock on Hand', field: 'stockOnHand', type: 'rightAligned', valueFormatter: fmtQty,
            cellStyle: p => ({ fontWeight: 600, color: p.value > 0 ? '#166534' : (p.value < 0 ? '#dc2626' : '#374151') })
        },
    ]);

    function buildCols() {
        // Detail columns live under the nested `details` object on each row (camelCase from .NET).
        const dcols = detailNames.map(n => ({ headerName: n, field: 'details.' + n, minWidth: 120 }));
        return [...frontCols(), ...dcols, ...tailCols()];
    }

    function onScroll() {
        if (!api || pending || !hasMore) return;
        const total = api.getDisplayedRowCount();
        const last = api.getLastDisplayedRow();
        if (total > 0 && last >= total - 100) {
            pending = true;
            dotNetRef.invokeMethodAsync('LoadMore').finally(() => { pending = false; });
        }
    }

    return {
        init(elementId, ref) {
            dotNetRef = ref;
            const el = document.getElementById(elementId);
            if (!el || typeof agGrid === 'undefined') return;

            const options = {
                columnDefs: buildCols(),
                rowData: [],
                defaultColDef: { sortable: true, resizable: true, minWidth: 90 },
                rowHeight: 34,
                headerHeight: 38,
                animateRows: false,
                onBodyScroll: onScroll,
            };
            api = agGrid.createGrid(el, options);
        },

        setRows(rows) {
            if (api) api.setGridOption('rowData', rows || []);
        },

        addRows(rows) {
            if (api && rows && rows.length) api.applyTransaction({ add: rows });
        },

        setDetailColumns(names) {
            detailNames = names || [];
            if (api) api.setGridOption('columnDefs', buildCols());
        },

        setHasMore(value) {
            hasMore = !!value;
        },

        dispose() {
            if (api) { api.destroy(); api = null; }
            dotNetRef = null;
            detailNames = [];
            hasMore = false;
            pending = false;
        }
    };
})();
