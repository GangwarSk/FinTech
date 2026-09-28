import * as React from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate } from 'react-router-dom';
import { CheckCircle2, Eye, FileUp, KeyRound, Loader2, RefreshCw, Trash2, Upload, XCircle } from 'lucide-react';
import type { ColumnDef } from '@tanstack/react-table';
import { ApiRequestError } from '@/core/api/client';
import { statementsApi } from '@/core/api/endpoints';
import { useAuthStore } from '@/core/stores/authStore';
import { cn, formatCurrency, formatDate, formatDateTime, formatFileSize } from '@/shared/lib/utils';
import type {
  StatementFileImpactDto,
  StatementProcessingResultDto,
  UploadHistoryDto,
  UploadStatementResponse,
} from '@/shared/types/masters';
import { Permissions } from '@/shared/types/auth';
import { DataTable } from '@/shared/components/data-table';
import { EmptyState, PageHeader, SearchBar } from '@/shared/components/common';
import { toast } from '@/shared/components/toast';
import { Button } from '@/shared/components/ui/button';
import { ConfirmDialog } from '@/shared/components/ui/dialog';
import { StatementFileDetailDialog } from './StatementFileDetailDialog';
import {
  Badge,
  Card,
  CardContent,
  CardHeader,
  CardTitle,
  Checkbox,
  Input,
  Label,
  Separator,
} from '@/shared/components/ui/primitives';

const MAX_BYTES = 25 * 1024 * 1024;

const SUPPORTED_BANKS = [
  'HDFC',
  'ICICI',
  'SBI',
  'Axis',
  'Kotak',
  'IndusInd',
  'Standard Chartered',
  'American Express',
];

type Stage = 'idle' | 'uploading' | 'awaiting-password' | 'processing' | 'done' | 'failed';

const STAGE_STEPS = [
  'Upload PDF',
  'Check password',
  'Identify statement',
  'Extract metadata',
  'Extract transactions',
  'Store data',
];

export function StatementUploadPage() {
  const queryClient = useQueryClient();
  const fileInputRef = React.useRef<HTMLInputElement>(null);
  const navigate = useNavigate();
  const canDelete = useAuthStore((state) => state.can(Permissions.StatementsDelete));

  const [file, setFile] = React.useState<File | null>(null);
  const [dragging, setDragging] = React.useState(false);
  const [stage, setStage] = React.useState<Stage>('idle');
  const [upload, setUpload] = React.useState<UploadStatementResponse | null>(null);
  const [password, setPassword] = React.useState('');
  const [allowDuplicate, setAllowDuplicate] = React.useState(false);
  const [result, setResult] = React.useState<StatementProcessingResultDto | null>(null);
  const [errorMessage, setErrorMessage] = React.useState<string | null>(null);

  const [historyPage, setHistoryPage] = React.useState(1);
  const [historyPageSize, setHistoryPageSize] = React.useState(10);
  const [historyKeyword, setHistoryKeyword] = React.useState('');

  const [viewFileId, setViewFileId] = React.useState<string | null>(null);
  const [pendingDelete, setPendingDelete] = React.useState<StatementFileImpactDto | null>(null);

  const history = useQuery({
    queryKey: ['upload-history', historyPage, historyPageSize, historyKeyword],
    queryFn: () =>
      statementsApi.history({
        pageNumber: historyPage,
        pageSize: historyPageSize,
        keyword: historyKeyword || null,
        sortBy: 'StartedOnUtc',
        sortDirection: 1,
      }),
  });

  const reset = () => {
    setFile(null);
    setStage('idle');
    setUpload(null);
    setPassword('');
    setAllowDuplicate(false);
    setResult(null);
    setErrorMessage(null);
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  const uploadMutation = useMutation({
    mutationFn: (selected: File) => statementsApi.upload(selected),
    onMutate: () => {
      setStage('uploading');
      setErrorMessage(null);
    },
    onSuccess: (response) => {
      setUpload(response);
      if (response.requiresPassword) {
        setStage('awaiting-password');
        toast.info('Password required', 'This statement is protected. Enter the password to continue.');
      } else {
        setStage('processing');
        processMutation.mutate({ uploadId: response.uploadId, password: null });
      }
    },
    onError: (error) => {
      setStage('failed');
      setErrorMessage(error instanceof ApiRequestError ? error.message : 'The upload failed.');
    },
  });

  const processMutation = useMutation({
    mutationFn: (variables: { uploadId: string; password: string | null }) =>
      statementsApi.process({
        uploadId: variables.uploadId,
        password: variables.password,
        allowDuplicate,
      }),
    onMutate: () => {
      setStage('processing');
      setErrorMessage(null);
    },
    onSuccess: (response) => {
      setResult(response);
      void queryClient.invalidateQueries({ queryKey: ['upload-history'] });
      void queryClient.invalidateQueries({ queryKey: ['transactions'] });
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] });

      if (response.errorCode === 'statement.password_required' || response.errorCode === 'statement.password_incorrect') {
        setStage('awaiting-password');
        setErrorMessage(response.errorMessage ?? 'The password was not accepted.');
        return;
      }

      if (response.errorCode) {
        setStage('failed');
        setErrorMessage(response.errorMessage ?? 'Processing failed.');
        return;
      }

      setStage('done');
      toast.success(
        'Statement imported',
        `${response.transactionsImported} transactions imported, ${response.transactionsSkipped} skipped.`,
      );
    },
    onError: (error) => {
      setStage('failed');
      setErrorMessage(error instanceof ApiRequestError ? error.message : 'Processing failed.');
    },
  });

  // The confirmation has to state what actually goes with the file, so the counts are fetched first.
  const impactMutation = useMutation({
    mutationFn: (statementFileId: string) => statementsApi.files.impact(statementFileId),
    onSuccess: setPendingDelete,
    onError: (error) =>
      toast.error(
        'Could not prepare the delete',
        error instanceof ApiRequestError ? error.message : 'The file details could not be loaded.',
      ),
  });

  const deleteMutation = useMutation({
    mutationFn: (statementFileId: string) => statementsApi.files.remove(statementFileId),
    onSuccess: (_result, statementFileId) => {
      const deleted = pendingDelete;
      setPendingDelete(null);
      if (viewFileId === statementFileId) setViewFileId(null);

      void queryClient.invalidateQueries({ queryKey: ['upload-history'] });
      void queryClient.invalidateQueries({ queryKey: ['statement-files'] });
      void queryClient.invalidateQueries({ queryKey: ['transactions'] });
      void queryClient.invalidateQueries({ queryKey: ['dashboard'] });
      void queryClient.invalidateQueries({ queryKey: ['lookups', 'statement-files'] });

      toast.success(
        'Moved to the recycle bin',
        `${deleted?.originalFileName ?? 'The file'} and its ${deleted?.transactionCount ?? 0} transactions can be restored from the recycle bin.`,
      );
    },
    onError: (error) =>
      toast.error(
        'Delete failed',
        error instanceof ApiRequestError ? error.message : 'The file could not be deleted.',
      ),
  });

  const selectFile = (selected: File | null) => {    if (!selected) return;

    if (!selected.name.toLowerCase().endsWith('.pdf')) {
      toast.error('Unsupported file', 'Only PDF statements can be uploaded.');
      return;
    }

    if (selected.size > MAX_BYTES) {
      toast.error('File too large', `The maximum upload size is ${formatFileSize(MAX_BYTES)}.`);
      return;
    }

    reset();
    setFile(selected);
  };

  const busy = stage === 'uploading' || stage === 'processing';
  const currentStep =
    stage === 'idle' ? 0 : stage === 'uploading' ? 1 : stage === 'awaiting-password' ? 2 : stage === 'processing' ? 4 : 6;

  const historyColumns = React.useMemo<ColumnDef<UploadHistoryDto, unknown>[]>(
    () => [
      {
        id: 'fileName',
        header: 'File',
        accessorKey: 'fileName',
        cell: ({ row }) => (
          <div className="min-w-0">
            <p className="truncate font-medium" title={row.original.fileName}>
              {row.original.fileName}
            </p>
            <p className="text-xs text-muted-foreground">{formatFileSize(row.original.sizeInBytes)}</p>
          </div>
        ),
      },
      {
        id: 'status',
        header: 'Status',
        accessorKey: 'status',
        cell: ({ row }) => <StatusBadge status={row.original.status} />,
      },
      {
        id: 'bank',
        header: 'Bank',
        cell: ({ row }) => row.original.detectedBank ?? '-',
      },
      {
        id: 'parser',
        header: 'Parser',
        cell: ({ row }) => row.original.parserName ?? '-',
      },
      {
        id: 'imported',
        header: 'Imported',
        cell: ({ row }) => (
          <span className="tabular">
            {row.original.transactionsImported} / {row.original.transactionsExtracted}
          </span>
        ),
      },
      {
        id: 'skipped',
        header: 'Skipped',
        cell: ({ row }) => <span className="tabular">{row.original.transactionsSkipped}</span>,
      },
      {
        id: 'startedOn',
        header: 'Started',
        cell: ({ row }) => formatDateTime(row.original.startedOnUtc),
      },
      {
        id: 'duration',
        header: 'Duration',
        cell: ({ row }) => <span className="tabular">{(row.original.durationMs / 1000).toFixed(1)}s</span>,
      },
      {
        id: 'error',
        header: 'Message',
        cell: ({ row }) =>
          row.original.errorMessage ? (
            <span className="text-xs text-destructive" title={row.original.errorMessage}>
              {row.original.errorMessage.slice(0, 60)}
            </span>
          ) : (
            '-'
          ),
      },
      {
        id: 'actions',
        header: 'Actions',
        cell: ({ row }) => {
          // A failed upload can have no stored file, and there is then nothing to view or delete.
          const statementFileId = row.original.statementFileId;
          if (!statementFileId) {
            return <span className="text-xs text-muted-foreground">-</span>;
          }

          return (
            <div className="flex items-center gap-1">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setViewFileId(statementFileId)}
                aria-label={`View data imported from ${row.original.fileName}`}
              >
                <Eye className="h-4 w-4" />
                View
              </Button>
              {canDelete && (
                <Button
                  variant="ghost"
                  size="sm"
                  className="text-destructive hover:text-destructive"
                  onClick={() => impactMutation.mutate(statementFileId)}
                  disabled={impactMutation.isPending}
                  aria-label={`Delete ${row.original.fileName} and its data`}
                >
                  <Trash2 className="h-4 w-4" />
                  Delete
                </Button>
              )}
            </div>
          );
        },
      },
    ],
    [canDelete, impactMutation],
  );

  return (
    <div className="space-y-6">
      <PageHeader
        title="PDF Statement Upload"
        description="Upload a credit card or bank statement. Protected PDFs are unlocked with the password you supply; nothing is stored in plain text."
        actions={
          <>
            {canDelete && (
              <Button variant="outline" onClick={() => navigate('/statements/recycle-bin')}>
                <Trash2 className="h-4 w-4" />
                Recycle bin
              </Button>
            )}
            <Button variant="outline" onClick={() => history.refetch()} disabled={history.isFetching}>
              <RefreshCw className={cn('h-4 w-4', history.isFetching && 'animate-spin')} />
              Refresh history
            </Button>
          </>
        }
      />

      <div className="grid gap-6 lg:grid-cols-3">
        <Card className="lg:col-span-2">
          <CardHeader>
            <CardTitle>Upload a statement</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div
              onDragOver={(event) => {
                event.preventDefault();
                setDragging(true);
              }}
              onDragLeave={() => setDragging(false)}
              onDrop={(event) => {
                event.preventDefault();
                setDragging(false);
                selectFile(event.dataTransfer.files?.[0] ?? null);
              }}
              className={cn(
                'flex flex-col items-center justify-center gap-3 rounded-lg border-2 border-dashed p-8 text-center transition-colors',
                dragging ? 'border-primary bg-primary/5' : 'border-border',
              )}
            >
              <FileUp className="h-8 w-8 text-muted-foreground" aria-hidden="true" />
              <div>
                <p className="text-sm font-medium">Drop a PDF statement here</p>
                <p className="mt-1 text-xs text-muted-foreground">
                  Maximum {formatFileSize(MAX_BYTES)} · PDF only
                </p>
              </div>
              <input
                ref={fileInputRef}
                type="file"
                accept="application/pdf,.pdf"
                className="hidden"
                onChange={(event) => selectFile(event.target.files?.[0] ?? null)}
              />
              <Button variant="outline" onClick={() => fileInputRef.current?.click()} disabled={busy}>
                Choose file
              </Button>
            </div>

            {file && (
              <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border bg-muted/40 p-3">
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium">{file.name}</p>
                  <p className="text-xs text-muted-foreground">{formatFileSize(file.size)}</p>
                </div>
                <div className="flex items-center gap-2">
                  <label className="flex items-center gap-2 text-xs text-muted-foreground">
                    <Checkbox
                      checked={allowDuplicate}
                      onChange={(event) => setAllowDuplicate(event.target.checked)}
                    />
                    Import even if already processed
                  </label>
                  <Button onClick={() => uploadMutation.mutate(file)} disabled={busy || stage === 'done'}>
                    {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Upload className="h-4 w-4" />}
                    Upload &amp; process
                  </Button>
                  <Button variant="ghost" onClick={reset} disabled={busy}>
                    Clear
                  </Button>
                </div>
              </div>
            )}

            {stage !== 'idle' && <WorkflowSteps currentStep={currentStep} failed={stage === 'failed'} />}

            {stage === 'awaiting-password' && upload && (
              <div className="space-y-3 rounded-md border border-warning/40 bg-warning/10 p-4">
                <div className="flex items-center gap-2">
                  <KeyRound className="h-4 w-4" aria-hidden="true" />
                  <p className="text-sm font-medium">This statement is password protected</p>
                </div>
                <p className="text-sm text-muted-foreground">
                  Banks usually use a combination of your name, date of birth or card digits. The password is used
                  once to decrypt the file and is never stored.
                </p>
                <div className="flex flex-wrap items-end gap-2">
                  <div className="flex-1 min-w-[220px]">
                    <Label htmlFor="pdf-password">PDF password</Label>
                    <Input
                      id="pdf-password"
                      type="password"
                      autoComplete="off"
                      value={password}
                      onChange={(event) => setPassword(event.target.value)}
                      onKeyDown={(event) => {
                        if (event.key === 'Enter' && password) {
                          processMutation.mutate({ uploadId: upload.uploadId, password });
                        }
                      }}
                    />
                  </div>
                  <Button
                    onClick={() => processMutation.mutate({ uploadId: upload.uploadId, password })}
                    disabled={!password || busy}
                  >
                    {busy && <Loader2 className="h-4 w-4 animate-spin" />}
                    Unlock &amp; process
                  </Button>
                </div>
              </div>
            )}

            {errorMessage && (
              <div role="alert" className="flex items-start gap-2 rounded-md border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive">
                <XCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden="true" />
                <span>{errorMessage}</span>
              </div>
            )}

            {result && !result.errorCode && <ProcessingResult result={result} />}
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Supported issuers</CardTitle>
          </CardHeader>
          <CardContent className="space-y-3">
            <div className="flex flex-wrap gap-1.5">
              {SUPPORTED_BANKS.map((bank) => (
                <Badge key={bank} variant="secondary">
                  {bank}
                </Badge>
              ))}
            </div>
            <Separator />
            <ol className="space-y-2 text-sm text-muted-foreground">
              {STAGE_STEPS.map((step, index) => (
                <li key={step} className="flex gap-2">
                  <span className="font-medium text-foreground">{index + 1}.</span>
                  {step}
                </li>
              ))}
            </ol>
            <Separator />
            <p className="text-xs text-muted-foreground">
              Statements from other banks still import through the generic parser - pick the bank manually if
              detection is wrong.
            </p>
          </CardContent>
        </Card>
      </div>

      <div className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <h2 className="text-base font-semibold">Upload history</h2>
          <SearchBar
            value={historyKeyword}
            onChange={(value) => {
              setHistoryKeyword(value);
              setHistoryPage(1);
            }}
            placeholder="Search by file name"
            className="w-full sm:w-72"
          />
        </div>

        <DataTable
          columns={historyColumns}
          data={history.data?.items ?? []}
          totalCount={history.data?.totalCount ?? 0}
          pageNumber={historyPage}
          pageSize={historyPageSize}
          onPageChange={setHistoryPage}
          onPageSizeChange={(size) => {
            setHistoryPageSize(size);
            setHistoryPage(1);
          }}
          isLoading={history.isLoading}
          getRowId={(row) => row.id}
          emptyMessage="No statements have been uploaded yet."
        />
      </div>

      <StatementFileDetailDialog
        fileId={viewFileId}
        open={viewFileId !== null}
        onClose={() => setViewFileId(null)}
      />

      <ConfirmDialog
        open={pendingDelete !== null}
        title="Delete this file and its data?"
        message={
          pendingDelete
            ? `Deleting "${pendingDelete.originalFileName}" also removes ${countLabel(pendingDelete.statementCount, 'statement')} and ` +
              `${countLabel(pendingDelete.transactionCount, 'transaction')} imported from it. ` +
              'They move to the recycle bin and can be restored, or permanently deleted from there.'
            : ''
        }
        confirmLabel="Delete file and data"
        destructive
        busy={deleteMutation.isPending}
        onCancel={() => setPendingDelete(null)}
        onConfirm={() => pendingDelete && deleteMutation.mutate(pendingDelete.id)}
      />
    </div>
  );
}

/** "1 statement" / "2 statements" - the confirmation reads as a sentence, not a stat line. */
function countLabel(count: number, noun: string) {
  return `${count} ${noun}${count === 1 ? '' : 's'}`;
}

function WorkflowSteps({ currentStep, failed }: { currentStep: number; failed: boolean }) {
  return (
    <ol className="flex flex-wrap gap-2" aria-label="Processing progress">
      {STAGE_STEPS.map((step, index) => {
        const complete = index < currentStep;
        const active = index === currentStep;
        return (
          <li
            key={step}
            className={cn(
              'flex items-center gap-1.5 rounded-full border px-3 py-1 text-xs',
              complete && !failed && 'border-success/40 bg-success/10 text-success',
              active && !failed && 'border-primary/40 bg-primary/10 text-primary',
              failed && index === currentStep && 'border-destructive/40 bg-destructive/10 text-destructive',
              !complete && !active && 'text-muted-foreground',
            )}
          >
            {complete && !failed ? <CheckCircle2 className="h-3 w-3" /> : <span>{index + 1}</span>}
            {step}
          </li>
        );
      })}
    </ol>
  );
}

function ProcessingResult({ result }: { result: StatementProcessingResultDto }) {
  const rows: [string, React.ReactNode][] = [
    ['Bank', result.bankName ?? '-'],
    ['Statement type', result.statementKind ?? '-'],
    ['Parser', result.parserName ?? '-'],
    ['Card number', result.cardNumberMasked ?? '-'],
    ['Account number', result.accountNumberMasked ?? '-'],
    ['Card holder', result.cardHolderName ?? '-'],
    ['Customer name', result.customerName ?? '-'],
    ['Email', result.customerEmail ?? '-'],
    ['Phone', result.customerPhone ?? '-'],
    ['Address', result.billingAddress?.line1 ?? '-'],
    ['Statement period', `${formatDate(result.periodStart)} - ${formatDate(result.periodEnd)}`],
    ['Statement date', formatDate(result.statementDate)],
    ['Payment due date', formatDate(result.paymentDueDate)],
    ['Opening balance', formatCurrency(result.openingBalance)],
    ['Closing balance', formatCurrency(result.closingBalance)],
    ['Minimum due', formatCurrency(result.minimumDue)],
    ['Total due', formatCurrency(result.totalDue)],
  ];

  return (
    <div className="space-y-4 rounded-md border border-success/40 bg-success/5 p-4">
      <div className="flex items-center gap-2">
        <CheckCircle2 className="h-5 w-5 text-success" aria-hidden="true" />
        <div>
          <p className="text-sm font-medium">Statement processed in {(result.durationMs / 1000).toFixed(1)}s</p>
          <p className="text-sm text-muted-foreground">
            {result.transactionsExtracted} rows extracted · {result.transactionsImported} imported ·{' '}
            {result.transactionsSkipped} skipped as duplicates
          </p>
        </div>
      </div>

      <dl className="grid gap-x-6 gap-y-2 sm:grid-cols-2 lg:grid-cols-3">
        {rows.map(([label, value]) => (
          <div key={label} className="min-w-0">
            <dt className="text-xs text-muted-foreground">{label}</dt>
            <dd className="truncate text-sm font-medium">{value}</dd>
          </div>
        ))}
      </dl>

      {result.warnings.length > 0 && (
        <div className="rounded-md border border-warning/40 bg-warning/10 p-3">
          <p className="text-sm font-medium">Warnings</p>
          <ul className="mt-1 list-inside list-disc text-sm text-muted-foreground">
            {result.warnings.map((warning) => (
              <li key={warning}>{warning}</li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

function StatusBadge({ status }: { status: string }) {
  const variant =
    status === 'Completed'
      ? 'success'
      : status === 'Failed'
        ? 'destructive'
        : status === 'Duplicate' || status === 'AwaitingPassword'
          ? 'warning'
          : 'secondary';

  return <Badge variant={variant}>{status}</Badge>;
}

export { EmptyState };
