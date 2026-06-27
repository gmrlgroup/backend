// AG Grid (Community) interop for the dataset table grid (/data/view, also aliased as /data/edit).
// Renders a read-only viewer or an editable spreadsheet depending on opts.editable.
// Blazor loads the columns + rows (each row carries its DuckDB rowid under __rowid) and owns saving;
// this module renders an editable grid, tracks edits/inserts/deletes client-side, and hands the change
// set back to .NET on save. Excel-like behaviour comes from AG Grid: click to focus, type / Enter / F2
// to edit, arrow keys to navigate, Enter moves down, and Ctrl+Z / Ctrl+Y undo/redo cell edits.

window.dataGridEditor = (function () {
    let api = null;
    let dotNetRef = null;
    let columns = [];           // [{ name, dataType }]
    let originals = {};         // __key -> snapshot of column values at load time
    let deletedRowIds = [];     // rowids of removed existing rows
    let newCounter = 0;
    let editable = true;        // when false the grid is a read-only viewer (no editing/selection)

    // Normalizes a cell value for comparison/transport: blank/undefined → null, everything else → string.
    const norm = v => (v === null || v === undefined || v === "") ? null : String(v);

    const colFields = () => columns.map(c => c.name);

    function buildColumnDefs() {
        const dataCols = columns.map(c => ({
            headerName: c.name,
            field: c.name,
            editable: editable,
            minWidth: 120,
            headerTooltip: c.dataType || ""
        }));
        if (!editable) return dataCols;

        // Leading checkbox column for row selection (used by Delete Selected) — editor only.
        const selectCol = {
            headerName: "", field: "__sel", width: 44, pinned: "left",
            checkboxSelection: true, headerCheckboxSelection: true,
            editable: false, sortable: false, filter: false, resizable: false,
            floatingFilter: false, suppressMovable: true, lockPosition: true
        };
        return [selectCol, ...dataCols];
    }

    function snapshot(row) {
        const o = {};
        colFields().forEach(f => o[f] = row[f]);
        return o;
    }

    function loadData(rows) {
        originals = {};
        deletedRowIds = [];
        const data = (rows || []).map((r, i) => {
            // Editable rows are keyed by their DuckDB rowid; the read-only viewer has none, so fall back
            // to the row index to keep getRowId unique.
            const key = (r.__rowid != null) ? ("r" + r.__rowid) : ("i" + i);
            r.__key = key;
            r.__isNew = false;
            originals[key] = snapshot(r);
            return r;
        });
        if (api) api.setGridOption("rowData", data);
        notifyDirty();
    }

    // Diffs the current grid state against the load-time snapshot. Updates carry only changed columns;
    // inserts carry the non-empty columns of new rows; deletes are the removed rowids.
    function computeChanges() {
        const updates = [];
        const inserts = [];
        if (api) {
            api.forEachNode(node => {
                const d = node.data;
                if (!d) return;
                if (d.__isNew) {
                    const values = {};
                    let any = false;
                    colFields().forEach(f => {
                        const nv = norm(d[f]);
                        if (nv !== null) { values[f] = nv; any = true; }
                    });
                    if (any) inserts.push({ rowId: null, values });
                } else {
                    const orig = originals[d.__key] || {};
                    const values = {};
                    let any = false;
                    colFields().forEach(f => {
                        if (norm(d[f]) !== norm(orig[f])) { values[f] = norm(d[f]); any = true; }
                    });
                    if (any) updates.push({ rowId: d.__rowid, values });
                }
            });
        }
        return { updates, inserts, deletes: deletedRowIds.slice() };
    }

    function notifyDirty() {
        if (!dotNetRef) return;
        const c = computeChanges();
        const dirty = (c.updates.length + c.inserts.length + c.deletes.length) > 0;
        dotNetRef.invokeMethodAsync("SetDirty", dirty);
    }

    return {
        init(elementId, ref, cols, rows, opts) {
            dotNetRef = ref;
            columns = cols || [];
            newCounter = 0;
            editable = !opts || opts.editable !== false;
            const el = document.getElementById(elementId);
            if (!el || typeof agGrid === "undefined") return;

            const options = {
                columnDefs: buildColumnDefs(),
                rowData: [],
                // cellDataType:false → AG Grid skips type inference and uses the plain text editor for
                // every cell. Inference (the v31 default) discards edits on mixed/null DuckDB columns,
                // which is what made edited values disappear. The backend casts strings to the real type.
                // filter + floatingFilter give the per-column search row, like the rest of the grid UX.
                defaultColDef: {
                    resizable: true, sortable: true, minWidth: 110,
                    cellDataType: false, enableCellChangeFlash: true,
                    filter: true, floatingFilter: true
                },
                rowSelection: "multiple",
                suppressRowClickSelection: true,   // clicking a cell edits it; selection is via checkbox
                singleClickEdit: false,
                stopEditingWhenCellsLoseFocus: true,
                undoRedoCellEditing: editable,
                undoRedoCellEditingLimit: 50,
                enterNavigatesVertically: true,
                enterNavigatesVerticallyAfterEdit: true,
                rowHeight: 32,
                headerHeight: 38,
                getRowId: p => p.data.__key,
                onCellValueChanged: notifyDirty
            };
            api = agGrid.createGrid(el, options);
            loadData(rows);
        },

        addRow() {
            if (!api) return;
            const key = "n" + (++newCounter);
            const res = api.applyTransaction({ add: [{ __key: key, __isNew: true }] });
            const node = res && res.add && res.add[0];
            if (node && columns.length) {
                api.ensureIndexVisible(node.rowIndex);
                api.setFocusedCell(node.rowIndex, columns[0].name);
                api.startEditingCell({ rowIndex: node.rowIndex, colKey: columns[0].name });
            }
            notifyDirty();
        },

        deleteSelected() {
            if (!api) return 0;
            const nodes = api.getSelectedNodes();
            if (!nodes.length) return 0;
            const remove = [];
            nodes.forEach(n => {
                const d = n.data;
                if (!d.__isNew && d.__rowid != null) deletedRowIds.push(d.__rowid);
                remove.push(d);
            });
            api.applyTransaction({ remove });
            notifyDirty();
            return remove.length;
        },

        getChanges() {
            return computeChanges();
        },

        // Replaces the grid contents (e.g. after a successful save or a discard) and resets tracking.
        setData(rows) {
            newCounter = 0;
            loadData(rows);
        },

        // Replaces both the columns and the rows — used by the read-only query results grid, whose
        // column set changes with every query.
        setColumnsAndData(cols, rows) {
            columns = cols || [];
            newCounter = 0;
            if (api) api.setGridOption("columnDefs", buildColumnDefs());
            loadData(rows);
        },

        // Exports the current grid view (respecting filters/sort/column order) as CSV — the data columns
        // only, never the selection checkbox column.
        exportCsv(fileName) {
            if (api) api.exportDataAsCsv({ fileName: fileName || "export.csv", columnKeys: colFields() });
        },

        dispose() {
            if (api) { api.destroy(); api = null; }
            dotNetRef = null;
            columns = [];
            originals = {};
            deletedRowIds = [];
            newCounter = 0;
            editable = true;
        }
    };
})();
