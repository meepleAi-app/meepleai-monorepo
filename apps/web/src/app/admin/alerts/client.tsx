/**
 * Alert Management Client (Issue #921)
 *
 * Minimal UI for alert management:
 * - Display active/all alerts in table
 * - Resolve individual alerts
 * - Auto-refresh every 30s
 * - Severity badges
 * - Metadata viewer (Dialog)
 */

'use client';

import { useCallback, useState } from 'react';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { CheckCircle2, XCircle, AlertTriangle, Info, Eye } from 'lucide-react';

import { AdminAuthGuard } from '@/components/admin';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { api, type AlertDto } from '@/lib/api';
import { cn } from '@/lib/utils';

type ToastMessage = {
  id: string;
  type: 'success' | 'error' | 'info';
  message: string;
};

/**
 * Get icon and color for alert severity
 */
function getSeverityDisplay(severity: string) {
  switch (severity.toLowerCase()) {
    case 'critical':
      return {
        icon: XCircle,
        variant: 'destructive' as const,
        color: 'text-red-600',
        bgColor: 'bg-red-50 dark:bg-red-950/20',
      };
    case 'error':
      return {
        icon: XCircle,
        variant: 'destructive' as const,
        color: 'text-red-500',
        bgColor: 'bg-red-50 dark:bg-red-950/20',
      };
    case 'warning':
      return {
        icon: AlertTriangle,
        variant: 'default' as const,
        color: 'text-yellow-600',
        bgColor: 'bg-yellow-50 dark:bg-yellow-950/20',
      };
    case 'info':
      return {
        icon: Info,
        variant: 'secondary' as const,
        color: 'text-blue-600',
        bgColor: 'bg-blue-50 dark:bg-blue-950/20',
      };
    default:
      return {
        icon: Info,
        variant: 'secondary' as const,
        color: 'text-gray-600',
        bgColor: 'bg-gray-50 dark:bg-gray-950/20',
      };
  }
}

/**
 * Format date to locale string
 */
function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleString('it-IT', {
    dateStyle: 'short',
    timeStyle: 'short',
  });
}

/**
 * Metadata viewer dialog
 */
function MetadataDialog({ alert }: { alert: AlertDto }) {
  return (
    <Dialog>
      <DialogTrigger asChild>
        <Button variant="ghost" size="sm">
          <Eye className="h-4 w-4" />
        </Button>
      </DialogTrigger>
      <DialogContent className="max-w-2xl max-h-[80vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Alert Details: {alert.alertType}</DialogTitle>
          <DialogDescription>Metadata and channel delivery status</DialogDescription>
        </DialogHeader>
        <div className="space-y-4 mt-4">
          {/* Basic Info */}
          <div>
            <h4 className="font-semibold mb-2">Basic Information</h4>
            <dl className="space-y-1 text-sm">
              <div className="flex gap-2">
                <dt className="font-medium">ID:</dt>
                <dd className="text-muted-foreground">{alert.id}</dd>
              </div>
              <div className="flex gap-2">
                <dt className="font-medium">Type:</dt>
                <dd className="text-muted-foreground">{alert.alertType}</dd>
              </div>
              <div className="flex gap-2">
                <dt className="font-medium">Triggered:</dt>
                <dd className="text-muted-foreground">{formatDate(alert.triggeredAt)}</dd>
              </div>
              {alert.resolvedAt && (
                <div className="flex gap-2">
                  <dt className="font-medium">Resolved:</dt>
                  <dd className="text-muted-foreground">{formatDate(alert.resolvedAt)}</dd>
                </div>
              )}
            </dl>
          </div>

          {/* Metadata */}
          {alert.metadata && Object.keys(alert.metadata).length > 0 && (
            <div>
              <h4 className="font-semibold mb-2">Metadata</h4>
              <pre className="bg-muted p-3 rounded-md text-xs overflow-x-auto">
                {JSON.stringify(alert.metadata, null, 2)}
              </pre>
            </div>
          )}

          {/* Channel Delivery Status */}
          {alert.channelSent && Object.keys(alert.channelSent).length > 0 && (
            <div>
              <h4 className="font-semibold mb-2">Delivery Status</h4>
              <div className="space-y-2">
                {Object.entries(alert.channelSent).map(([channel, success]) => (
                  <div key={channel} className="flex items-center gap-2">
                    {success ? (
                      <CheckCircle2 className="h-4 w-4 text-green-600" />
                    ) : (
                      <XCircle className="h-4 w-4 text-red-600" />
                    )}
                    <span className="text-sm">
                      <span className="font-medium">{channel}:</span>{' '}
                      <span className={success ? 'text-green-600' : 'text-red-600'}>
                        {success ? 'Sent' : 'Failed'}
                      </span>
                    </span>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}

export function AlertsPageClient() {
  const { user, loading: authLoading } = useAuthUser();
  const [showActiveOnly, setShowActiveOnly] = useState(true);
  const [toasts, setToasts] = useState<ToastMessage[]>([]);
  const queryClient = useQueryClient();

  // Toast management
  const addToast = useCallback((type: 'success' | 'error' | 'info', message: string) => {
    const id = `toast-${Date.now()}-${Math.random()}`;
    setToasts(prev => [...prev, { id, type, message }]);
    setTimeout(() => {
      setToasts(prev => prev.filter(t => t.id !== id));
    }, 5000);
  }, []);

  const removeToast = useCallback((id: string) => {
    setToasts(prev => prev.filter(t => t.id !== id));
  }, []);

  // Fetch alerts with auto-refresh
  const {
    data: alerts = [],
    isLoading,
    error,
  } = useQuery({
    queryKey: ['admin-alerts', showActiveOnly],
    queryFn: () => api.alerts.getAlerts(showActiveOnly),
    refetchInterval: 30000, // Auto-refresh every 30s
    staleTime: 25000,
  });

  // Resolve alert mutation
  const resolveMutation = useMutation({
    mutationFn: (alertType: string) => api.alerts.resolveAlert(alertType),
    onSuccess: (_, alertType) => {
      addToast('success', `Alert "${alertType}" resolved successfully`);
      queryClient.invalidateQueries({ queryKey: ['admin-alerts'] });
    },
    onError: (error: Error, alertType) => {
      addToast('error', `Failed to resolve "${alertType}": ${error.message}`);
    },
  });

  const handleResolve = useCallback(
    (alert: AlertDto) => {
      if (!alert.isActive) {
        addToast('info', 'Alert is already resolved');
        return;
      }
      resolveMutation.mutate(alert.alertType);
    },
    [resolveMutation, addToast]
  );

  // Stats
  const stats = {
    total: alerts.length,
    active: alerts.filter(a => a.isActive).length,
    critical: alerts.filter(a => a.severity === 'critical' && a.isActive).length,
    warnings: alerts.filter(a => a.severity === 'warning' && a.isActive).length,
  };

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="space-y-6">
        {/* Header */}
        <div>
          <h1 className="text-3xl font-bold">Alert Management</h1>
          <p className="text-muted-foreground mt-1">
            Monitor and resolve system alerts from Prometheus AlertManager
          </p>
        </div>

        {/* Stats Cards */}
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-sm font-medium">Total Alerts</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">{stats.total}</div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-sm font-medium">Active</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold text-blue-600">{stats.active}</div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-sm font-medium">Critical</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold text-red-600">{stats.critical}</div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-sm font-medium">Warnings</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold text-yellow-600">{stats.warnings}</div>
            </CardContent>
          </Card>
        </div>

        {/* Filters */}
        <div className="flex gap-2">
          <Button
            variant={showActiveOnly ? 'default' : 'outline'}
            onClick={() => setShowActiveOnly(true)}
          >
            Active Only
          </Button>
          <Button
            variant={!showActiveOnly ? 'default' : 'outline'}
            onClick={() => setShowActiveOnly(false)}
          >
            All (7 days)
          </Button>
        </div>

        {/* Alerts Table */}
        <Card>
          <CardHeader>
            <CardTitle>Alerts</CardTitle>
            <CardDescription>
              {showActiveOnly ? 'Currently active alerts' : 'Alert history (last 7 days)'}
            </CardDescription>
          </CardHeader>
          <CardContent>
            {error && (
              <Alert variant="destructive" className="mb-4">
                <AlertDescription>
                  Failed to load alerts: {(error as Error).message}
                </AlertDescription>
              </Alert>
            )}

            {isLoading ? (
              <div className="text-center py-8 text-muted-foreground">Loading alerts...</div>
            ) : alerts.length === 0 ? (
              <div className="text-center py-8 text-muted-foreground">
                <CheckCircle2 className="h-12 w-12 mx-auto mb-2 text-green-600" />
                <p className="text-lg font-medium">No alerts found</p>
                <p className="text-sm">
                  {showActiveOnly ? 'All systems operational' : 'No alerts in the last 7 days'}
                </p>
              </div>
            ) : (
              <div className="overflow-x-auto">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Severity</TableHead>
                      <TableHead>Type</TableHead>
                      <TableHead>Message</TableHead>
                      <TableHead>Triggered</TableHead>
                      <TableHead>Status</TableHead>
                      <TableHead className="text-right">Actions</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {alerts.map(alert => {
                      const severityDisplay = getSeverityDisplay(alert.severity);
                      const SeverityIcon = severityDisplay.icon;

                      return (
                        <TableRow key={alert.id} className={cn(!alert.isActive && 'opacity-50')}>
                          <TableCell>
                            <Badge variant={severityDisplay.variant} className="gap-1">
                              <SeverityIcon className="h-3 w-3" />
                              {alert.severity}
                            </Badge>
                          </TableCell>
                          <TableCell className="font-medium">{alert.alertType}</TableCell>
                          <TableCell className="max-w-md truncate">{alert.message}</TableCell>
                          <TableCell className="text-sm text-muted-foreground">
                            {formatDate(alert.triggeredAt)}
                          </TableCell>
                          <TableCell>
                            {alert.isActive ? (
                              <Badge
                                variant="outline"
                                className="bg-yellow-50 text-yellow-700 border-yellow-300"
                              >
                                Active
                              </Badge>
                            ) : (
                              <Badge
                                variant="outline"
                                className="bg-green-50 text-green-700 border-green-300"
                              >
                                Resolved
                              </Badge>
                            )}
                          </TableCell>
                          <TableCell className="text-right space-x-2">
                            <MetadataDialog alert={alert} />
                            {alert.isActive && (
                              <Button
                                variant="outline"
                                size="sm"
                                onClick={() => handleResolve(alert)}
                                disabled={resolveMutation.isPending}
                              >
                                Resolve
                              </Button>
                            )}
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </div>
            )}
          </CardContent>
        </Card>

        {/* Toast Notifications */}
        <div className="fixed bottom-4 right-4 space-y-2 z-50">
          {toasts.map(toast => (
            <Alert
              key={toast.id}
              variant={toast.type === 'error' ? 'destructive' : 'default'}
              className="w-96 shadow-lg"
            >
              <AlertDescription className="flex items-center justify-between">
                <span>{toast.message}</span>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => removeToast(toast.id)}
                  className="h-6 w-6 p-0"
                >
                  ×
                </Button>
              </AlertDescription>
            </Alert>
          ))}
        </div>
      </div>
    </AdminAuthGuard>
  );
}
