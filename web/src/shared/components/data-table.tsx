import * as React from 'react';
import {
  flexRender,
  getCoreRowModel,
  useReactTable,
  type ColumnDef,
  type SortingState,
  type VisibilityState,
} from '@tanstack/react-table';
import { ArrowDown, ArrowUp, ChevronsUpDown, ChevronUp, Columns3, MoreHorizontal } from 'lucide-react';
import { cn } from '@/shared/lib/utils';
import { Button } from './ui/button';
import { Card, Select } from './ui/primitives';
import { EmptyState, LoadingState } from './common';

export interface DataTableProps<TData> {
  columns: ColumnDef<TData, unknown>[];
  data: TData[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (size: number) => void;
  sorting?: SortingState;
  onSortingChange?: (sorting: SortingState) => void;
  isLoading?: boolean;
  emptyMessage?: string;
  getRowId?: (row: TData) => string;
  onRowClick?: (row: TData) => void;
  enableColumnVisibility?: boolean;
  toolbar?: React.ReactNode;
  /** When supplied, each row gets a pinned "..." toggle that expands this content beneath it. */
  renderRowDetails?: (row: TData) => React.ReactNode;
}

const PAGE_SIZES = [10, 25, 50, 100, 250];

/**
 * Server-driven table: sorting and paging are state only - the caller re-queries the API.
 * TanStack Table is used purely for column definitions, rendering and visibility.
 */
export function DataTable<TData>({
  columns,
  data,
  totalCount,
  pageNumber,
  pageSize,
  onPageChange,
  onPageSizeChange,
  sorting = [],
  onSortingChange,
  isLoading,
  emptyMessage = 'No records matched the current filters.',
  getRowId,
  onRowClick,
  enableColumnVisibility = true,
  toolbar,
  renderRowDetails,
}: DataTableProps<TData>) {
  const [columnVisibility, setColumnVisibility] = React.useState<VisibilityState>({});
  const [showColumnPicker, setShowColumnPicker] = React.useState(false);
  const [expanded, setExpanded] = React.useState<Set<string>>(() => new Set());

  const toggleExpanded = (rowId: string) =>
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(rowId)) next.delete(rowId);
      else next.add(rowId);
      return next;
    });

  const table = useReactTable({
    data,
    columns,
    state: { sorting, columnVisibility },
    manualSorting: true,
    manualPagination: true,
    enableSortingRemoval: false,
    getCoreRowModel: getCoreRowModel(),
    onColumnVisibilityChange: setColumnVisibility,
    getRowId: getRowId ? (row) => getRowId(row) : undefined,
  });

  // A new page or data set means different rows, so stale expansions are dropped.
  React.useEffect(() => setExpanded((prev) => (prev.size ? new Set() : prev)), [data]);

  const scrollRef = React.useRef<HTMLDivElement>(null);
  const [viewportWidth, setViewportWidth] = React.useState(0);

  React.useEffect(() => {
    const element = scrollRef.current;
    if (!element || !renderRowDetails) return;
    const observer = new ResizeObserver(() => setViewportWidth(element.clientWidth));
    observer.observe(element);
    return () => observer.disconnect();
  }, [renderRowDetails]);

  const columnCount = table.getVisibleLeafColumns().length + (renderRowDetails ? 1 : 0);
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const firstRow = totalCount === 0 ? 0 : (pageNumber - 1) * pageSize + 1;
  const lastRow = Math.min(pageNumber * pageSize, totalCount);

  const toggleSort = (columnId: string) => {
    if (!onSortingChange) return;
    const current = sorting[0];
    onSortingChange(
      current?.id === columnId ? [{ id: columnId, desc: !current.desc }] : [{ id: columnId, desc: true }],
    );
  };

  return (
    <Card className="overflow-hidden">
      {(toolbar || enableColumnVisibility) && (
        <div className="flex flex-wrap items-start justify-between gap-2 border-b border-slate-200 p-4 dark:border-slate-700">
          <div className="flex min-w-0 flex-1 flex-wrap items-center gap-2">{toolbar}</div>
          {enableColumnVisibility && (
            <div className="relative">
              <Button variant="outline" size="sm" onClick={() => setShowColumnPicker((prev) => !prev)}>
                <Columns3 className="h-4 w-4" />
                Columns
              </Button>
              {showColumnPicker && (
                <div className="absolute right-0 z-30 mt-1 w-56 rounded-xl border border-slate-200 bg-white p-2 shadow-xl dark:border-slate-700 dark:bg-slate-900">
                  <div className="scrollbar-thin max-h-72 overflow-y-auto">
                    {table.getAllLeafColumns().map((column) => (
                      <label
                        key={column.id}
                        className="flex cursor-pointer items-center gap-2 rounded px-2 py-1.5 text-sm hover:bg-accent"
                      >
                        <input
                          type="checkbox"
                          className="h-4 w-4 accent-primary"
                          checked={column.getIsVisible()}
                          onChange={column.getToggleVisibilityHandler()}
                        />
                        <span className="truncate">
                          {typeof column.columnDef.header === 'string' ? column.columnDef.header : column.id}
                        </span>
                      </label>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      )}

      <div ref={scrollRef} className="scrollbar-thin overflow-x-auto">
        <table className="w-full caption-bottom text-sm">
          <thead className="bg-slate-50 text-xs font-bold uppercase tracking-wide text-slate-500 dark:bg-slate-800/70 dark:text-slate-400">
            {table.getHeaderGroups().map((headerGroup) => (
              <tr key={headerGroup.id} className="border-b border-slate-200 dark:border-slate-700">
                {headerGroup.headers.map((header) => {
                  const sortable = header.column.columnDef.enableSorting !== false && Boolean(onSortingChange);
                  const active = sorting[0]?.id === header.column.id;

                  return (
                    <th
                      key={header.id}
                      className="whitespace-nowrap px-4 py-3 text-left font-bold"
                      style={{ width: header.getSize() === 150 ? undefined : header.getSize() }}
                    >
                      {header.isPlaceholder ? null : sortable ? (
                        <button
                          type="button"
                          onClick={() => toggleSort(header.column.id)}
                          className={cn('inline-flex items-center gap-1 uppercase hover:text-indigo-700 dark:hover:text-white', active && 'text-indigo-700 dark:text-indigo-300')}
                        >
                          {flexRender(header.column.columnDef.header, header.getContext())}
                          {active ? (
                            sorting[0]?.desc ? (
                              <ArrowDown className="h-3.5 w-3.5" />
                            ) : (
                              <ArrowUp className="h-3.5 w-3.5" />
                            )
                          ) : (
                            <ChevronsUpDown className="h-3.5 w-3.5 opacity-40" />
                          )}
                        </button>
                      ) : (
                        flexRender(header.column.columnDef.header, header.getContext())
                      )}
                    </th>
                  );
                })}
                {renderRowDetails && (
                  <th className="sticky right-0 z-10 w-12 bg-slate-50 px-2 py-3 shadow-[-6px_0_8px_-6px_rgba(15,23,42,0.18)] dark:bg-slate-800">
                    <span className="sr-only">Details</span>
                  </th>
                )}
              </tr>
            ))}
          </thead>
          <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
            {isLoading && (
              <tr>
                <td colSpan={columnCount}>
                  <LoadingState />
                </td>
              </tr>
            )}

            {!isLoading && data.length === 0 && (
              <tr>
                <td colSpan={columnCount}>
                  <EmptyState message={emptyMessage} />
                </td>
              </tr>
            )}

            {!isLoading &&
              table.getRowModel().rows.map((row) => {
                const isExpanded = expanded.has(row.id);
                const detailsId = `row-details-${row.id}`;

                return (
                  <React.Fragment key={row.id}>
                    <tr
                      onClick={() => onRowClick?.(row.original)}
                      className={cn(
                        'group text-slate-700 transition-colors hover:bg-indigo-50/40 dark:text-slate-200 dark:hover:bg-slate-800/50',
                        onRowClick && 'cursor-pointer',
                        isExpanded && 'bg-indigo-50/60 dark:bg-slate-800/60',
                      )}
                    >
                      {row.getVisibleCells().map((cell) => (
                        <td key={cell.id} className="px-4 py-3 align-middle">
                          {flexRender(cell.column.columnDef.cell, cell.getContext())}
                        </td>
                      ))}
                      {renderRowDetails && (
                        <td
                          className={cn(
                            'sticky right-0 z-10 w-12 bg-white px-2 py-2 text-center align-middle shadow-[-6px_0_8px_-6px_rgba(15,23,42,0.18)] dark:bg-slate-900',
                            isExpanded && 'bg-indigo-50 dark:bg-slate-800',
                          )}
                        >
                          <button
                            type="button"
                            onClick={(event) => {
                              event.stopPropagation();
                              toggleExpanded(row.id);
                            }}
                            aria-expanded={isExpanded}
                            aria-controls={detailsId}
                            aria-label={isExpanded ? 'Hide row details' : 'Show row details'}
                            title={isExpanded ? 'Hide details' : 'Show all details'}
                            className="inline-grid h-8 w-8 place-items-center rounded-lg text-slate-500 transition hover:bg-indigo-100 hover:text-indigo-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-500 dark:hover:bg-slate-700 dark:hover:text-white"
                          >
                            {isExpanded ? <ChevronUp className="h-4 w-4" /> : <MoreHorizontal className="h-4 w-4" />}
                          </button>
                        </td>
                      )}
                    </tr>
                    {renderRowDetails && isExpanded && (
                      <tr id={detailsId} className="bg-slate-50/80 dark:bg-slate-900/60">
                        <td colSpan={columnCount} className="p-0">
                          {/* Sticky + visible-width sizing keeps the panel in view however far the grid is scrolled. */}
                          <div
                            className="sticky left-0 px-4 py-4"
                            style={{ width: viewportWidth ? `${viewportWidth}px` : '100%' }}
                          >
                            {renderRowDetails(row.original)}
                          </div>
                        </td>
                      </tr>
                    )}
                  </React.Fragment>
                );
              })}
          </tbody>
        </table>
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3 border-t border-slate-200 bg-slate-50/60 px-4 py-3 text-sm dark:border-slate-700 dark:bg-slate-800/40">
        <span className="font-semibold text-slate-600 dark:text-slate-300">
          {totalCount === 0 ? 'No records' : `Showing ${firstRow}-${lastRow} of ${totalCount.toLocaleString('en-IN')}`}
        </span>

        <div className="flex items-center gap-2">
          <Select
            value={String(pageSize)}
            onChange={(event) => onPageSizeChange(Number(event.target.value))}
            className="h-8 w-20"
            aria-label="Rows per page"
          >
            {PAGE_SIZES.map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </Select>

          <Button variant="outline" size="sm" onClick={() => onPageChange(1)} disabled={pageNumber <= 1}>
            First
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={() => onPageChange(pageNumber - 1)}
            disabled={pageNumber <= 1}
          >
            Previous
          </Button>
          <span className="px-1 text-muted-foreground">
            Page {pageNumber} of {totalPages}
          </span>
          <Button
            variant="outline"
            size="sm"
            onClick={() => onPageChange(pageNumber + 1)}
            disabled={pageNumber >= totalPages}
          >
            Next
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={() => onPageChange(totalPages)}
            disabled={pageNumber >= totalPages}
          >
            Last
          </Button>
        </div>
      </div>
    </Card>
  );
}
