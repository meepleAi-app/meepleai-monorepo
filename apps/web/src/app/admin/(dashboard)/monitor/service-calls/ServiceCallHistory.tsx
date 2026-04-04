'use client';

/**
 * ServiceCallHistory Component
 * Filterable table of external service call records with pagination and detail modal.
 */

import { useCallback, useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import { RefreshCw } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/overlays/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';
import type {
  ServiceCall,
  ServiceCallFilters,
} from '@/lib/api/schemas/admin/admin-service-calls.schemas';

const SERVICE_NAMES = [
  'OpenRouter',
  'EmbeddingService',
  'Ollama',
  'BggApi',
  'UnstructuredService',
  'SmolDoclingService',
  'HuggingFace',
  'OrchestrationService',
  'Infisical',
] as const;

const PAGE_SIZE = 50;

// ─── Helpers ─────────────────────────────────────────────────────────────────

function formatTimestamp(ts: string): string {
  try {
    return new Date(ts).toLocaleString(undefined, {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false,
    });
  } catch {
    return ts;
  }
}

function latencyClass(ms: number): string {
  if (ms < 500) return 'text-green-700 dark:text-green-400';
  if (ms < 2000) return 'text-amber-700 dark:text-amber-400';
  return 'text-red-700 dark:text-red-400';
}

function formatLatency(ms: number): string {
  if (ms < 1000) return `${Math.round(ms)}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

function methodBadgeClass(method: string): string {
  switch (method.toUpperCase()) {
    case 'GET':
      return 'bg-sky-100 text-sky-800 dark:bg-sky-900/40 dark:text-sky-300';
    case 'POST':
      return 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-300';
    case 'PUT':
      return 'bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300';
    case 'PATCH':
      return 'bg-violet-100 text-violet-800 dark:bg-violet-900/40 dark:text-violet-300';
    case 'DELETE':
      return 'bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-300';
    default:
      return 'bg-zinc-100 text-zinc-700 dark:bg-zinc-800 dark:text-zinc-400';
  }
}

// ─── Detail Dialog ────────────────────────────────────────────────────────────

interface DetailRow {
  label: string;
  value: string | number | boolean | null;
}

function DetailField({ label, value }: DetailRow) {
  if (value === null || value === undefined) return null;
  return (
    <div className="grid grid-cols-[140px_1fr] gap-2 text-xs">
      <span className="font-semibold text-muted-foreground">{label}</span>
      <span className="font-mono break-all">{String(value)}</span>
    </div>
  );
}

interface CallDetailDialogProps {
  call: ServiceCall | null;
  onClose: () => void;
}

function CallDetailDialog({ call, onClose }: CallDetailDialogProps) {
  return (
    <Dialog open={!!call} onOpenChange={open => !open && onClose()}>
      <DialogContent className="max-w-2xl max-h-[80vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Service Call Detail</DialogTitle>
        </DialogHeader>
        {call && (
          <div className="space-y-2 mt-2">
            <DetailField label="ID" value={call.id} />
            <DetailField label="Service" value={call.serviceName} />
            <DetailField label="Method" value={call.httpMethod} />
            <DetailField label="URL" value={call.requestUrl} />
            <DetailField label="Status Code" value={call.statusCode} />
            <DetailField label="Latency" value={formatLatency(call.latencyMs)} />
            <DetailField label="Success" value={call.isSuccess ? 'Yes' : 'No'} />
            <DetailField label="Timestamp" value={formatTimestamp(call.timestampUtc)} />
            <DetailField label="Correlation ID" value={call.correlationId} />
            {call.requestSummary && (
              <div className="space-y-1">
                <div className="text-xs font-semibold text-muted-foreground">Request Summary</div>
                <pre className="rounded bg-muted/50 p-2 text-xs font-mono whitespace-pre-wrap break-all">
                  {call.requestSummary}
                </pre>
              </div>
            )}
            {call.responseSummary && (
              <div className="space-y-1">
                <div className="text-xs font-semibold text-muted-foreground">Response Summary</div>
                <pre className="rounded bg-muted/50 p-2 text-xs font-mono whitespace-pre-wrap break-all">
                  {call.responseSummary}
                </pre>
              </div>
            )}
            {call.errorMessage && (
              <div className="space-y-1">
                <div className="text-xs font-semibold text-red-600 dark:text-red-400">
                  Error Message
                </div>
                <pre className="rounded bg-red-50 p-2 text-xs font-mono text-red-800 whitespace-pre-wrap break-all dark:bg-red-950/40 dark:text-red-300">
                  {call.errorMessage}
                </pre>
              </div>
            )}
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

// ─── Main Component ───────────────────────────────────────────────────────────

export function ServiceCallHistory() {
  const [service, setService] = useState('');
  const [successFilter, setSuccessFilter] = useState('');
  const [correlationId, setCorrelationId] = useState('');
  const [page, setPage] = useState(1);
  const [selectedCall, setSelectedCall] = useState<ServiceCall | null>(null);

  const [appliedFilters, setAppliedFilters] = useState<ServiceCallFilters>({
    page: 1,
    pageSize: PAGE_SIZE,
  });

  const { data, isFetching, refetch } = useQuery({
    queryKey: ['admin', 'service-calls', appliedFilters],
    queryFn: () => api.admin.getServiceCalls(appliedFilters),
    staleTime: 30_000,
  });

  const handleApply = useCallback(() => {
    const newPage = 1;
    setPage(newPage);
    setAppliedFilters({
      service: service || undefined,
      success: successFilter === '' ? undefined : successFilter === 'true',
      correlationId: correlationId || undefined,
      page: newPage,
      pageSize: PAGE_SIZE,
    });
  }, [service, successFilter, correlationId]);

  const handleRefresh = useCallback(() => {
    void refetch();
  }, [refetch]);

  const handlePageChange = useCallback((newPage: number) => {
    setPage(newPage);
    setAppliedFilters(prev => ({ ...prev, page: newPage }));
  }, []);

  const items = data?.items ?? [];
  const totalCount = data?.totalCount ?? 0;
  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE));

  return (
    <div
      className="rounded-xl border bg-white/70 backdrop-blur-md dark:bg-zinc-900/70"
      data-testid="service-call-history"
    >
      {/* Filter bar */}
      <div className="flex flex-wrap items-end gap-2 border-b px-4 py-3">
        <Select value={service || '_all'} onValueChange={v => setService(v === '_all' ? '' : v)}>
          <SelectTrigger className="w-44 text-xs" data-testid="service-filter-select">
            <SelectValue placeholder="All services" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="_all" className="text-xs">
              All services
            </SelectItem>
            {SERVICE_NAMES.map(name => (
              <SelectItem key={name} value={name} className="text-xs">
                {name}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select
          value={successFilter || '_all'}
          onValueChange={v => setSuccessFilter(v === '_all' ? '' : v)}
        >
          <SelectTrigger className="w-32 text-xs" data-testid="success-filter-select">
            <SelectValue placeholder="All results" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="_all" className="text-xs">
              All results
            </SelectItem>
            <SelectItem value="true" className="text-xs">
              Success
            </SelectItem>
            <SelectItem value="false" className="text-xs">
              Error
            </SelectItem>
          </SelectContent>
        </Select>

        <Input
          placeholder="Correlation ID…"
          value={correlationId}
          onChange={e => setCorrelationId(e.target.value)}
          onKeyDown={e => e.key === 'Enter' && handleApply()}
          className="w-44 font-mono text-xs"
          data-testid="correlation-filter-input"
        />

        <Button size="sm" onClick={handleApply} data-testid="apply-filters-btn">
          Apply
        </Button>

        <Button
          size="sm"
          variant="outline"
          onClick={handleRefresh}
          disabled={isFetching}
          data-testid="refresh-btn"
        >
          <RefreshCw className={`mr-1.5 h-3.5 w-3.5 ${isFetching ? 'animate-spin' : ''}`} />
          Refresh
        </Button>

        {totalCount > 0 && (
          <span className="ml-auto text-xs text-muted-foreground">
            {totalCount.toLocaleString()} total
          </span>
        )}
      </div>

      {/* Table */}
      <div className="overflow-auto">
        {isFetching && items.length === 0 ? (
          <div
            className="flex items-center justify-center py-12"
            data-testid="service-calls-loading"
          >
            <div className="h-8 w-8 animate-spin rounded-full border-4 border-primary border-t-transparent" />
          </div>
        ) : items.length === 0 ? (
          <div
            className="py-12 text-center text-sm text-muted-foreground"
            data-testid="service-calls-empty"
          >
            No service calls found.
          </div>
        ) : (
          <table className="w-full text-sm" data-testid="service-calls-table">
            <thead>
              <tr className="border-b bg-muted/30 text-left font-mono text-xs text-muted-foreground">
                <th className="px-3 py-2">Time</th>
                <th className="px-3 py-2">Service</th>
                <th className="px-3 py-2">Method</th>
                <th className="px-3 py-2">URL</th>
                <th className="px-3 py-2">Status</th>
                <th className="px-3 py-2">Latency</th>
              </tr>
            </thead>
            <tbody>
              {items.map(call => (
                <tr
                  key={call.id}
                  className="border-b transition-colors hover:bg-muted/40 cursor-pointer"
                  onClick={() => setSelectedCall(call)}
                  data-testid={`call-row-${call.id}`}
                >
                  <td className="whitespace-nowrap px-3 py-1.5 font-mono text-xs tabular-nums text-muted-foreground">
                    {formatTimestamp(call.timestampUtc)}
                  </td>
                  <td className="px-3 py-1.5 text-xs font-medium">{call.serviceName}</td>
                  <td className="px-3 py-1.5">
                    <Badge
                      className={`text-[10px] font-medium ${methodBadgeClass(call.httpMethod)}`}
                    >
                      {call.httpMethod.toUpperCase()}
                    </Badge>
                  </td>
                  <td className="max-w-[200px] truncate px-3 py-1.5 font-mono text-xs text-muted-foreground">
                    {call.requestUrl}
                  </td>
                  <td className="px-3 py-1.5">
                    {call.isSuccess ? (
                      <Badge className="bg-green-100 text-green-800 text-[10px] font-medium dark:bg-green-900/40 dark:text-green-300">
                        {call.statusCode ?? 'OK'}
                      </Badge>
                    ) : (
                      <Badge className="bg-red-100 text-red-800 text-[10px] font-medium dark:bg-red-900/40 dark:text-red-300">
                        {call.statusCode ?? 'ERR'}
                      </Badge>
                    )}
                  </td>
                  <td
                    className={`px-3 py-1.5 font-mono text-xs tabular-nums ${latencyClass(call.latencyMs)}`}
                  >
                    {formatLatency(call.latencyMs)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between border-t px-4 py-3">
          <span className="text-xs text-muted-foreground">
            Page {page} of {totalPages}
          </span>
          <div className="flex gap-1">
            <Button
              size="sm"
              variant="outline"
              onClick={() => handlePageChange(page - 1)}
              disabled={page <= 1 || isFetching}
              data-testid="prev-page-btn"
            >
              Previous
            </Button>
            <Button
              size="sm"
              variant="outline"
              onClick={() => handlePageChange(page + 1)}
              disabled={page >= totalPages || isFetching}
              data-testid="next-page-btn"
            >
              Next
            </Button>
          </div>
        </div>
      )}

      {/* Detail dialog */}
      <CallDetailDialog call={selectedCall} onClose={() => setSelectedCall(null)} />
    </div>
  );
}
