import * as React from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import type { ColumnDef } from '@tanstack/react-table';
import { ArrowLeft, Eye, RefreshCw, RotateCcw, Trash2 } from 'lucide-react';
import { ApiRequestError } from '@/core/api/client';
import { statementsApi } from '@/core/api/endpoints';
import { cn, formatDate, formatDateTime, formatFileSize } from '@/shared/lib/utils';
import type { StatementFileSummaryDto } from '@/shared/types/masters';
import { DataTable } from '@/shared/components/data-table';
import { PageHeader, SearchBar } from '@/shared/components/common';
import { toast } from '@/shared/components/toast';
import { Button } from '@/shared/components/ui/button';
import { ConfirmDialog } from '@/shared/components/ui/dialog';
import { StatementFileDetailDialog } from './StatementFileDetailDialog';

/**
 * Deleted uploads, one row per file. Restore puts a file and everything it imported back; permanent
 * delete removes it from every table and deletes the stored PDF.
 */
export function RecycleBinPage() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const [page, setPage] = React.useState(1);
  const [pageSize, setPageSize] = React.useState(10);
  const [keyword, setKeyword] = React.useState('');

  const [viewFileId, setViewFileId] = React.useState<string | null>(null);
  const [pendingPurge, setPendingPurge] = React.useState<StatementFileSummaryDto | null>(null);

  const files = useQuery({
    queryKey: ['statement-files', 'recycle-bin', page, pageSize, keyword],
    queryFn: () =>
      statementsApi.files.recycleBin({
        pageNumber: page,
        pageSize,
        keyword: keyword || null,
        deletedOnly: true,
        sortBy: 'DeletedOnUtc',
        sortDirection: 1,
      }),
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ['statement-files'] });
    void queryClient.invalidateQueries({ queryKey: ['upload-history'] });
    void queryClient.invalidateQueries({ queryKey: ['transactions'] });
    void queryClient.invalidateQueries({ queryKey: ['dashboard'] });
    void queryClient.invalidateQueries({ queryKey: ['lookups', 'statement-files'] });
  };

  const restoreMutation = useMutation({
    mutationFn: (id: string) => statementsApi.files.restore(id),
    onSuccess: () => {
      invalidate();
      toast.success('File restored', 'The file and its imported data are available again.');
    },
    onError: (error) =>
      toast.error(
        'Restore failed',
        error instanceof ApiRequestError ? error.message : 'The file could not be restored.',
      ),
  });

  const purgeMutation = useMutation({
    mutationFn: (id: string) => statementsApi.files.purge(id),
    onSuccess: (_result, id) => {
      setPendingPurge(null);
      if (viewFileId === id) setViewFileId(null);
      invalidate();
      toast.success('Permanently deleted', 'The file and its data were removed for good.');
    },
    onError: (error) => {
      setPendingPurge(null);
      toast.error(
        'Permanent delete failed',
        error instanceof ApiRequestError ? error.message : 'The file could not be deleted.',
      );
    },
  });

  const columns = React.useMemo<ColumnDef<StatementFileSummaryDto, unknown>[]>(
    () => [
      {
        id: 'file',
        header: 'File',
        cell: ({ row }) => (
          <div className="min-w-0">
            <p className="truncate font-medium" title={row.original.originalFileName}>
              {row.original.originalFileName}
            </p>
            <p className="text-xs text-muted-foreground">{formatFileSize(row.original.sizeInBytes)}</p>
          </div>
        ),
      },
      {
        id: 'bank',
        header: 'Bank',
        cell: ({ row }) => row.original.bankName ?? row.original.detectedBank ?? '-',
      },
      {
        id: 'period',
        header: 'Statement period',
        cell: ({ row }) =>
          row.original.periodStart && row.original.periodEnd
            ? `${formatDate(row.original.periodStart)} - ${formatDate(row.original.periodEnd)}`
            : '-',
      },
      {
        id: 'statements',
        header: 'Statements',
        cell: ({ row }) => <span className="tabular">{row.original.statementCount}</span>,
      },
      {
        id: 'transactions',
        header: 'Transactions',
        cell: ({ row }) => <span className="tabular">{row.original.transactionCount}</span>,
      },
      {
        id: 'deletedOn',
        header: 'Deleted',
        cell: ({ row }) => (
          <div className="min-w-0">
            <p>{formatDateTime(row.original.deletedOnUtc)}</p>
            <p className="text-xs text-muted-foreground">by {row.original.deletedBy ?? 'unknown'}</p>
          </div>
        ),
      },
      {
        id: 'actions',
        header: 'Actions',
        cell: ({ row }) => (
          <div className="flex items-center gap-1">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setViewFileId(row.original.id)}
              aria-label={`View data deleted with ${row.original.originalFileName}`}
            >
              <Eye className="h-4 w-4" />
              View
            </Button>
            <Button
              variant="ghost"
              size="sm"
              onClick={() => restoreMutation.mutate(row.original.id)}
              disabled={restoreMutation.isPending}
              aria-label={`Restore ${row.original.originalFileName}`}
            >
              <RotateCcw className="h-4 w-4" />
              Restore
            </Button>
            <Button
              variant="ghost"
              size="sm"
              className="text-destructive hover:text-destructive"
              onClick={() => setPendingPurge(row.original)}
              aria-label={`Permanently delete ${row.original.originalFileName}`}
            >
              <Trash2 className="h-4 w-4" />
              Delete permanently
            </Button>
          </div>
        ),
      },
    ],
    [restoreMutation],
  );

  return (
    <div className="space-y-6">
      <PageHeader
        title="Recycle Bin"
        description="Deleted statement files and everything imported from them. Restore a file to bring its transactions back, or delete it permanently to remove it from the database and disk."
        actions={
          <>
            <Button variant="outline" onClick={() => navigate('/statements')}>
              <ArrowLeft className="h-4 w-4" />
              Back to uploads
            </Button>
            <Button variant="outline" onClick={() => files.refetch()} disabled={files.isFetching}>
              <RefreshCw className={cn('h-4 w-4', files.isFetching && 'animate-spin')} />
              Refresh
            </Button>
          </>
        }
      />

      <div className="flex flex-wrap items-center justify-between gap-3">
        <SearchBar
          value={keyword}
          onChange={(value) => {
            setKeyword(value);
            setPage(1);
          }}
          placeholder="Search by file name"
          className="w-full sm:w-72"
        />
      </div>

      <DataTable
        columns={columns}
        data={files.data?.items ?? []}
        totalCount={files.data?.totalCount ?? 0}
        pageNumber={page}
        pageSize={pageSize}
        onPageChange={setPage}
        onPageSizeChange={(size) => {
          setPageSize(size);
          setPage(1);
        }}
        isLoading={files.isLoading}
        getRowId={(row) => row.id}
        emptyMessage="The recycle bin is empty."
      />

      <StatementFileDetailDialog
        fileId={viewFileId}
        open={viewFileId !== null}
        onClose={() => setViewFileId(null)}
      />

      <ConfirmDialog
        open={pendingPurge !== null}
        title="Permanently delete this file?"
        message={
          pendingPurge
            ? `"${pendingPurge.originalFileName}", its ${pendingPurge.statementCount} statement(s) and ` +
              `${pendingPurge.transactionCount} transaction(s) will be removed from the database, and the stored PDF ` +
              'will be deleted from disk. This cannot be undone.'
            : ''
        }
        confirmLabel="Delete permanently"
        destructive
        busy={purgeMutation.isPending}
        onCancel={() => setPendingPurge(null)}
        onConfirm={() => pendingPurge && purgeMutation.mutate(pendingPurge.id)}
      />
    </div>
  );
}
