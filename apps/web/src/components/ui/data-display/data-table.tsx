'use client';

import * as React from 'react';

import {
  columnVisibilityFeature,
  createSortedRowModel,
  flexRender,
  rowSelectionFeature,
  rowSortingFeature,
  tableFeatures,
  useTable,
  type ColumnDef as RtColumnDef,
  type ColumnVisibilityState,
  type OnChangeFn,
  type Row as RtRow,
  type RowData,
  type RowSelectionState,
  type SortingState,
} from '@tanstack/react-table';
import { ArrowUpDown, ArrowUp, ArrowDown } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';

import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from './table';

/**
 * Le feature che questa tabella usa, registrate esplicitamente (#3893).
 *
 * In react-table v9 le feature non sono piu' incluse d'ufficio: una API
 * assente significa quasi sempre una feature non registrata, non una API
 * rimossa. Qui ne servono tre — ordinamento, selezione righe, visibilita'
 * colonne — piu' lo slot del sorted row model. Il core row model e'
 * automatico e non va piu' passato.
 *
 * NON si usa `stockFeatures`: bundlerebbe anche paginazione, filtri,
 * raggruppamento, faceting, pinning e resizing, che questa tabella non ha
 * mai offerto.
 */
const features = tableFeatures({
  columnVisibilityFeature,
  rowSelectionFeature,
  rowSortingFeature,
  sortedRowModel: createSortedRowModel(),
});

type Features = typeof features;

/**
 * I consumatori non devono nominare il set di feature: e' una scelta di
 * questo wrapper, non loro. Questi alias tengono la firma a un solo
 * generico come in v8 (`ColumnDef<Riga>`), cosi' i quattro call-site
 * cambiano solo la sorgente dell'import — e smettono di dipendere
 * direttamente da @tanstack/react-table, che era anche un errore di
 * stratificazione.
 */
export type ColumnDef<TData extends RowData, TValue = unknown> = RtColumnDef<
  Features,
  TData,
  TValue
>;
export type Row<TData extends RowData> = RtRow<Features, TData>;
/** v9 ha rinominato `VisibilityState` in `ColumnVisibilityState`. */
export type VisibilityState = ColumnVisibilityState;
export type { SortingState, RowSelectionState };

// Shift selection context for range selection support.
//
// #3893: il contesto non e' piu' generico. Del `Row` gli servono soltanto
// `id` e `toggleSelected`, e in v9 i generici di `Row` sono invarianti —
// tenerlo generico costringeva a un `any` (con relativo eslint-disable) e
// comunque non tipava nulla. Un tipo strutturale minimo dice esattamente cosa
// serve e toglie l'`any`.
interface ShiftSelectableRow {
  id: string;
  toggleSelected: () => void;
}

interface ShiftSelectionContextValue {
  enabled: boolean;
  handleRowClick?: (row: ShiftSelectableRow, event: React.MouseEvent) => void;
}

const ShiftSelectionContext = React.createContext<ShiftSelectionContextValue>({
  enabled: false,
});

/**
 * #3893: il generico `TValue` e' sparito dalla firma pubblica. In v9
 * `useTable` vuole un array omogeneo con `TValue = unknown`, e tutti i
 * call-site scrivevano gia' `ColumnDef<Riga, unknown>`: l'alias `ColumnDef`
 * qui sopra mantiene i due parametri con `unknown` come default, quindi le
 * annotazioni esistenti continuano a compilare senza modifiche.
 */
export interface DataTableProps<TData extends RowData> {
  columns: ColumnDef<TData>[];
  data: TData[];
  sorting?: SortingState;
  onSortingChange?: OnChangeFn<SortingState>;
  rowSelection?: RowSelectionState;
  onRowSelectionChange?: OnChangeFn<RowSelectionState>;
  columnVisibility?: VisibilityState;
  onColumnVisibilityChange?: OnChangeFn<VisibilityState>;
  onRowClick?: (row: TData) => void;
  getRowId?: (row: TData) => string;
  isLoading?: boolean;
  emptyMessage?: string;
  enableShiftSelection?: boolean;
}

export function DataTable<TData extends RowData>({
  columns,
  data,
  sorting,
  onSortingChange,
  rowSelection,
  onRowSelectionChange,
  columnVisibility,
  onColumnVisibilityChange,
  onRowClick,
  getRowId,
  isLoading = false,
  emptyMessage = 'No results.',
  enableShiftSelection = false,
}: DataTableProps<TData>) {
  // Internal state when external state is not provided
  const [internalSorting, setInternalSorting] = React.useState<SortingState>([]);
  const [internalRowSelection, setInternalRowSelection] = React.useState<RowSelectionState>({});
  const [internalColumnVisibility, setInternalColumnVisibility] = React.useState<VisibilityState>(
    {}
  );
  const [lastSelectedIndex, setLastSelectedIndex] = React.useState<number | null>(null);

  const table = useTable({
    features,
    data,
    columns,
    onSortingChange: onSortingChange ?? setInternalSorting,
    onRowSelectionChange: onRowSelectionChange ?? setInternalRowSelection,
    onColumnVisibilityChange: onColumnVisibilityChange ?? setInternalColumnVisibility,
    getRowId,
    state: {
      sorting: sorting ?? internalSorting,
      rowSelection: rowSelection ?? internalRowSelection,
      columnVisibility: columnVisibility ?? internalColumnVisibility,
    },
  });

  // Shift+click handler for range selection
  const handleShiftClick = React.useCallback(
    (row: ShiftSelectableRow, event: React.MouseEvent) => {
      if (!enableShiftSelection || !event.shiftKey || lastSelectedIndex === null) {
        // Normal click - just toggle selection
        row.toggleSelected();
        const rows = table.getRowModel().rows;
        const currentIndex = rows.findIndex(r => r.id === row.id);
        setLastSelectedIndex(currentIndex);
        return;
      }

      // Shift+click - range selection
      event.preventDefault(); // Prevent text selection
      const rows = table.getRowModel().rows;
      const currentIndex = rows.findIndex(r => r.id === row.id);
      const start = Math.min(lastSelectedIndex, currentIndex);
      const end = Math.max(lastSelectedIndex, currentIndex);

      // Select all rows in range
      const newSelection: RowSelectionState = { ...(rowSelection ?? internalRowSelection) };
      for (let i = start; i <= end; i++) {
        newSelection[rows[i].id] = true;
      }

      // Update selection
      const handler = onRowSelectionChange ?? setInternalRowSelection;
      if (typeof handler === 'function') {
        handler(newSelection);
      }

      setLastSelectedIndex(currentIndex);
    },
    [
      enableShiftSelection,
      lastSelectedIndex,
      table,
      rowSelection,
      internalRowSelection,
      onRowSelectionChange,
    ]
  );

  // Shift selection context value
  const shiftSelectionValue = React.useMemo<ShiftSelectionContextValue>(
    () => ({
      enabled: enableShiftSelection,
      handleRowClick: enableShiftSelection ? handleShiftClick : undefined,
    }),
    [enableShiftSelection, handleShiftClick]
  );

  return (
    <ShiftSelectionContext.Provider value={shiftSelectionValue}>
      <div className="rounded-md border">
        <Table>
          <TableHeader>
            {table.getHeaderGroups().map(headerGroup => (
              <TableRow key={headerGroup.id}>
                {headerGroup.headers.map(header => (
                  <TableHead key={header.id}>
                    {header.isPlaceholder
                      ? null
                      : flexRender(header.column.columnDef.header, header.getContext())}
                  </TableHead>
                ))}
              </TableRow>
            ))}
          </TableHeader>
          <TableBody>
            {isLoading ? (
              <TableRow>
                <TableCell colSpan={columns.length} className="h-24 text-center">
                  Loading...
                </TableCell>
              </TableRow>
            ) : table.getRowModel().rows?.length ? (
              table.getRowModel().rows.map(row => (
                <TableRow
                  key={row.id}
                  data-state={row.getIsSelected() && 'selected'}
                  onClick={() => onRowClick?.(row.original)}
                  className={onRowClick ? 'cursor-pointer' : undefined}
                >
                  {row.getVisibleCells().map(cell => (
                    <TableCell key={cell.id}>
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : (
              <TableRow>
                <TableCell colSpan={columns.length} className="h-24 text-center">
                  {emptyMessage}
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>
    </ShiftSelectionContext.Provider>
  );
}

// Helper to create a sortable column header
export interface SortableHeaderProps {
  column: {
    getIsSorted: () => false | 'asc' | 'desc';
    toggleSorting: (desc?: boolean) => void;
  };
  children: React.ReactNode;
}

export function SortableHeader({ column, children }: SortableHeaderProps) {
  const sorted = column.getIsSorted();

  return (
    <Button
      variant="ghost"
      onClick={() => column.toggleSorting(sorted === 'asc')}
      className="-ml-4 h-8 data-[state=open]:bg-accent"
    >
      {children}
      {sorted === 'asc' ? (
        <ArrowUp className="ml-2 h-4 w-4" />
      ) : sorted === 'desc' ? (
        <ArrowDown className="ml-2 h-4 w-4" />
      ) : (
        <ArrowUpDown className="ml-2 h-4 w-4" />
      )}
    </Button>
  );
}

// Checkbox cell component with Shift+click support
function SelectCheckboxCell<TData extends RowData>({ row }: { row: Row<TData> }) {
  const shiftContext = React.useContext(ShiftSelectionContext);

  const handleClick = (e: React.MouseEvent) => {
    e.stopPropagation();

    if (shiftContext.enabled && shiftContext.handleRowClick && e.shiftKey) {
      // Use Shift+click handler from context
      shiftContext.handleRowClick(row, e);
    } else {
      // Normal click - toggle selection
      row.toggleSelected();
    }
  };

  return (
    <Checkbox
      checked={row.getIsSelected()}
      onCheckedChange={value => row.toggleSelected(!!value)}
      aria-label="Select row"
      className="translate-y-[2px]"
      onClick={handleClick}
    />
  );
}

// Helper to create a checkbox selection column
export function createSelectColumn<TData extends RowData>(): ColumnDef<TData> {
  return {
    id: 'select',
    header: ({ table }) => (
      <Checkbox
        checked={
          table.getIsAllPageRowsSelected() || (table.getIsSomePageRowsSelected() && 'indeterminate')
        }
        onCheckedChange={value => table.toggleAllPageRowsSelected(!!value)}
        aria-label="Select all"
        className="translate-y-[2px]"
      />
    ),
    cell: ({ row }) => <SelectCheckboxCell row={row} />,
    enableSorting: false,
    enableHiding: false,
  };
}
