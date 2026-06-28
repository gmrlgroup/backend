// AG Grid (Community) interop for dataset grids.
//
// Two modes:
//  • client  (Query Workbench results): rows are pushed in from .NET; read-only; client-side sort/filter.
//  • infinite (/data/view): AG Grid pulls pages from .NET on demand (server-side paging) so tables with
//    millions of rows scroll smoothly. When editable, edits auto-commit per row (the whole sheet isn't
//    in memory, so there's no bulk save): editing a cell saves that row, deleting removes it immediately.
//
// Excel-like editing on loaded rows: click to focus, type / F2 / Enter to edit, arrow keys to navigate.

window.dataGridEditor = (function () {
    let api = null;
    let dotNetRef = null;
    let columns = [];        // [{ name, dataType }]
    let editable = false;
    let infinite = false;

    const colFields = () => columns.map(c => c.name);
    const norm = v => (v === null || v === undefined || v === "") ? null : String(v);

    function buildColumnDefs() {
        const dataCols = columns.map(c => ({
            headerName: c.name,
            field: c.name,
            editable: editable,
            minWidth: 120,
            headerTooltip: c.dataType || ""
        }));
        if (!editable) return dataCols;

        const selectCol = {
            headerName: "", field: "__sel", width: 44, pinned: "left",
            checkboxSelection: true, headerCheckboxSelection: true,
            editable: false, sortable: false, filter: false, resizable: false,
            floatingFilter: false, suppressMovable: true, lockPosition: true
        };
        return [selectCol, ...dataCols];
    }

    // ---- infinite (server-paged) datasource ----
    function makeDataSource() {
        return {
            getRows: async (params) => {
                if (!dotNetRef) { (params.failCallback || params.fail)?.call(params); return; }
                try {
                    const res = await dotNetRef.invokeMethodAsync(
                        "FetchRows",
                        params.startRow,
                        params.endRow,
                        JSON.stringify(params.sortModel || []),
                        JSON.stringify(params.filterModel || {}));
                    const rows = (res && res.rows) || [];
                    const lastRow = (res && typeof res.lastRow === "number") ? res.lastRow : -1;
                    // AG Grid v31 renamed successCallback→success; support both so it works either way.
                    if (typeof params.successCallback === "function") {
                        params.successCallback(rows, lastRow);
                    } else if (typeof params.success === "function") {
                        params.success({ rowData: rows, rowCount: lastRow >= 0 ? lastRow : undefined });
                    }
                } catch (e) {
                    console.error("dataGridEditor: FetchRows failed", e);
                    if (typeof params.failCallback === "function") params.failCallback();
                    else if (typeof params.fail === "function") params.fail();
                }
            }
        };
    }

    async function onCellValueChanged(event) {
        if (!editable || !dotNetRef) return;
        const rowId = event.data ? event.data.__rowid : null;
        if (rowId == null) return;
        await dotNetRef.invokeMethodAsync("UpdateCell", rowId, event.colDef.field, norm(event.newValue));
    }

    return {
        init(elementId, ref, cols, rows, opts) {
            dotNetRef = ref;
            columns = cols || [];
            editable = !!(opts && opts.editable);
            infinite = !!(opts && opts.infinite);
            const el = document.getElementById(elementId);
            if (!el || typeof agGrid === "undefined") return;

            const options = {
                columnDefs: buildColumnDefs(),
                // cellDataType:false → plain text editor for every cell (AG Grid's type inference otherwise
                // discards edits on mixed/null DuckDB columns). filter+floatingFilter give the search row.
                defaultColDef: {
                    resizable: true, sortable: true, minWidth: 110,
                    cellDataType: false, enableCellChangeFlash: true,
                    filter: true, floatingFilter: true
                },
                rowSelection: "multiple",
                suppressRowClickSelection: true,
                singleClickEdit: false,
                stopEditingWhenCellsLoseFocus: true,
                enterNavigatesVertically: true,
                enterNavigatesVerticallyAfterEdit: true,
                rowHeight: 32,
                headerHeight: 38,
                onCellValueChanged: onCellValueChanged
            };

            if (infinite) {
                options.rowModelType = "infinite";
                options.cacheBlockSize = 100;
                options.maxBlocksInCache = 100;
                options.infiniteInitialRowCount = 100;
                options.blockLoadDebounceMillis = 150; // coalesce rapid scroll into fewer server fetches
                options.datasource = makeDataSource();
            } else {
                options.rowData = rows || [];
            }

            api = agGrid.createGrid(el, options);
        },

        // ---- client mode (Query Workbench) ----
        setColumnsAndData(cols, rows) {
            columns = cols || [];
            if (api) {
                api.setGridOption("columnDefs", buildColumnDefs());
                api.setGridOption("rowData", rows || []);
            }
        },

        exportCsv(fileName) {
            if (api) api.exportDataAsCsv({ fileName: fileName || "export.csv", columnKeys: colFields() });
        },

        // ---- infinite mode (data viewer) ----
        // Re-fetches the visible blocks from the server (after an edit/delete/insert, or manual refresh).
        refresh() {
            if (api) api.refreshInfiniteCache();
        },

        async deleteSelected() {
            if (!api || !dotNetRef) return 0;
            const nodes = api.getSelectedNodes();
            const ids = nodes.map(n => n.data && n.data.__rowid).filter(v => v != null);
            if (!ids.length) return 0;
            await dotNetRef.invokeMethodAsync("DeleteRows", ids);
            api.deselectAll();
            api.refreshInfiniteCache();
            return ids.length;
        },

        async addRow() {
            if (!dotNetRef) return false;
            const ok = await dotNetRef.invokeMethodAsync("InsertRow");
            if (ok && api) api.refreshInfiniteCache();
            return ok;
        },

        dispose() {
            if (api) { api.destroy(); api = null; }
            dotNetRef = null;
            columns = [];
            editable = false;
            infinite = false;
        }
    };
})();
