import * as React from 'react';
import { useQuery } from '@tanstack/react-query';
import type { ColumnDef } from '@tanstack/react-table';
import { AlertTriangle, FileText } from 'lucide-react';
import { statementsApi } from '@/core/api/endpoints';
import { formatCurrency, formatDate, formatDateTime, formatFileSize } from '@/shared/lib/utils';
import type { StatementFileSummaryDto } from '@/shared/types/masters';
import type { TransactionDto } from '@/shared/types/transactions';
import { DataTable } from '@/shared/components/data-table';
import { Dialog } from '@/shared/components/ui/dialog';
import { Badge, Separator } from '@/shared/components/ui/primitives';

/**
 * Everything one uploaded PDF produced, in one place. Used from both the upload history and the recycle
 * bin, so it has to read soft-deleted rows too - the API side does that with IgnoreQueryFilters.
 */
export function StatementFileDetailDialog({
  fileId,
  open,
  onClose,
}: {
  fileId: string | null;
  open: boolean;
  onClose: () => void;
}) {
  const [page, setPage] = React.useState(1);
  const [pageSize, setPageSize] = React.useState(25);

  // A different file starts at page one rather than inheriting the previous file's position.
  React.useEffect(() => {
    setPage(1);
  }, [fileId]);

  const detail = useQuery({
    queryKey: ['statement-file', fileId],
    queryFn: () => statementsApi.files.detail(fileId!),
    enabled: open && Boolean(fileId),
  });

  const transactions = useQuery({
    queryKey: ['statement-file-transactions', fileId, page, pageSize],
    queryFn: () =>
      statementsApi.files.transactions(fileId!, {
        pageNumber: page,
        pageSize,
        sortBy: 'TransactionDate',
        sortDirection: 0,
      }),
    enabled: open && Boolean(fileId),
    placeholderData: (previous) => previous,
  });

  const columns = React.useMemo<ColumnDef<TransactionDto, unknown>[]>(
    () => [
      { id: 'date', header: 'Date', cell: ({ row }) => formatDate(row.original.transactionDate) },
      {
        id: 'description',
        header: 'Description',
        cell: ({ row }) => (
          <span className="block max-w-md truncate" title={row.original.description}>
            {row.original.description}
          </span>
        ),
      },
      { id: 'reference', header: 'Reference', cell: ({ row }) => row.original.referenceNumber ?? '-' },
      {
        id: 'amount',
        header: 'Amount',
        cell: ({ row }) => (
          <span className={row.original.direction === 'Credit' ? 'tabular text-success' : 'tabular'}>
            {formatCurrency(row.original.amount)}
          </span>
        ),
      },
      { id: 'direction', header: 'Direction', cell: ({ row }) => row.original.direction },
      { id: 'vendor', header: 'Vendor', cell: ({ row }) => row.original.vendorName ?? '-' },
    ],
    [],
  );

  const file = detail.data?.file;

  return (
    <Dialog open={open} onClose={onClose} title={file?.displayName ?? 'Uploaded file'} className="max-w-5xl">
      {detail.isLoading && <p className="text-sm text-muted-foreground">Loading file details...</p>}

      {detail.isError && (
        <p className="text-sm text-destructive">The file details could not be loaded.</p>
      )}

      {file && (
        <div className="space-y-4">
          {file.isDeleted && (
            <div className="flex items-start gap-2 rounded-md border border-warning/40 bg-warning/10 p-3 text-sm">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
              <span>
                This file is in the recycle bin. It was deleted by {file.deletedBy ?? 'an administrator'} on{' '}
                {formatDateTime(file.deletedOnUtc)}. Everything shown below is deleted with it.
              </span>
            </div>
          )}

          <FileFacts file={file} pageCount={detail.data!.pageCount} protectedPdf={detail.data!.isPasswordProtected} />

          {detail.data!.statements.length > 0 && (
            <>
              <Separator />
              <div>
                <h3 className="mb-2 text-sm font-semibold">Statement</h3>
                <div className="space-y-2">
                  {detail.data!.statements.map((statement) => (
                    <dl
                      key={statement.id}
                      className="grid gap-x-6 gap-y-2 rounded-md border bg-muted/30 p-3 sm:grid-cols-3"
                    >
                      <Fact label="Period" value={`${formatDate(statement.periodStart)} - ${formatDate(statement.periodEnd)}`} />
                      <Fact label="Statement date" value={formatDate(statement.statementDate)} />
                      <Fact label="Card / account" value={statement.cardNumberMasked ?? statement.accountNumberMasked ?? '-'} />
                      <Fact label="Opening balance" value={formatCurrency(statement.openingBalance)} />
                      <Fact label="Closing balance" value={formatCurrency(statement.closingBalance)} />
                      <Fact label="Total due" value={formatCurrency(statement.totalDue)} />
                      <Fact label="Credits" value={formatCurrency(statement.totalCredits)} />
                      <Fact label="Debits" value={formatCurrency(statement.totalDebits)} />
                      <Fact label="Parser" value={statement.parserName ?? '-'} />
                    </dl>
                  ))}
                </div>
              </div>
            </>
          )}

          <Separator />

          <div>
            <h3 className="mb-2 text-sm font-semibold">
              Imported transactions{' '}
              <span className="font-normal text-muted-foreground">
                ({transactions.data?.totalCount ?? file.transactionCount})
              </span>
            </h3>
            <DataTable
              columns={columns}
              data={transactions.data?.items ?? []}
              totalCount={transactions.data?.totalCount ?? 0}
              pageNumber={page}
              pageSize={pageSize}
              onPageChange={setPage}
              onPageSizeChange={(size) => {
                setPageSize(size);
                setPage(1);
              }}
              isLoading={transactions.isLoading}
              getRowId={(row) => row.id}
              enableColumnVisibility={false}
              emptyMessage="This file did not import any transactions."
            />
          </div>
        </div>
      )}
    </Dialog>
  );
}

function FileFacts({
  file,
  pageCount,
  protectedPdf,
}: {
  file: StatementFileSummaryDto;
  pageCount: number;
  protectedPdf: boolean;
}) {
  return (
    <div className="space-y-3">
      <div className="flex items-start gap-2">
        <FileText className="mt-0.5 h-4 w-4 shrink-0 text-muted-foreground" aria-hidden="true" />
        <div className="min-w-0">
          <p className="truncate text-sm font-medium" title={file.originalFileName}>
            {file.originalFileName}
          </p>
          <p className="text-xs text-muted-foreground">
            {formatFileSize(file.sizeInBytes)} · {pageCount || '?'} pages
            {protectedPdf && ' · password protected'}
          </p>
        </div>
      </div>

      <dl className="grid gap-x-6 gap-y-2 sm:grid-cols-3">
        <Fact label="Bank" value={file.bankName ?? file.detectedBank ?? 'Not detected'} />
        <Fact label="Statement type" value={file.detectedKind ?? '-'} />
        <Fact
          label="Upload status"
          value={file.uploadStatus ? <Badge variant="secondary">{file.uploadStatus}</Badge> : '-'}
        />
        <Fact label="Uploaded" value={formatDateTime(file.uploadedOnUtc)} />
        <Fact label="Uploaded by" value={file.uploadedBy ?? '-'} />
        <Fact label="Statements" value={String(file.statementCount)} />
      </dl>
    </div>
  );
}

function Fact({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="min-w-0">
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="truncate text-sm font-medium">{value}</dd>
    </div>
  );
}
