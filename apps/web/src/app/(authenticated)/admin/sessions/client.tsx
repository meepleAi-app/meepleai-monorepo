/**
 * Sessions Monitoring Client - Gap Analysis Implementation
 *
 * Admin interface for monitoring and managing user sessions:
 * - View all active sessions with details
 * - Filter by user
 * - Revoke individual sessions
 * - Revoke all sessions for a user
 *
 * Backend Integration:
 * - GET /api/v1/admin/sessions
 * - DELETE /api/v1/admin/sessions/{sessionId}
 * - DELETE /api/v1/admin/users/{userId}/sessions
 */

'use client';

import { useState, useCallback, useEffect } from 'react';

import {
  Monitor,
  User,
  Clock,
  Globe,
  Smartphone,
  AlertCircle,
  RefreshCw,
  Trash2,
  Ban,
  Search,
  X,
} from 'lucide-react';

import { AdminAuthGuard } from '@/components/admin';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Spinner } from '@/components/loading';
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
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/overlays/alert-dialog-primitives';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api, type AdminSessionInfo } from '@/lib/api';

// ========== Types ==========

type ToastMessage = {
  id: string;
  type: 'success' | 'error' | 'info';
  message: string;
};

type RevokeAction = {
  type: 'single' | 'all';
  sessionId?: string;
  userId?: string;
  userEmail?: string;
};

// ========== Main Component ==========

export function SessionsMonitoringClient() {
  const { user: currentUser, loading: authLoading } = useAuthUser();

  // Data state
  const [sessions, setSessions] = useState<AdminSessionInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filter state
  const [limit, setLimit] = useState(50);
  const [userIdFilter, setUserIdFilter] = useState('');
  const [searchInput, setSearchInput] = useState('');

  // Dialog state
  const [revokeAction, setRevokeAction] = useState<RevokeAction | null>(null);
  const [isRevoking, setIsRevoking] = useState(false);

  // Toast management
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  const addToast = useCallback((type: 'success' | 'error' | 'info', message: string) => {
    const id = `toast-${Date.now()}-${Math.random()}`;
    setToasts(prev => [...prev, { id, type, message }]);
    setTimeout(() => {
      setToasts(prev => prev.filter(t => t.id !== id));
    }, 5000);
  }, []);

  // Fetch sessions
  const fetchSessions = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);

      const result = await api.admin.getAdminSessions({
        limit,
        userId: userIdFilter || undefined,
      });

      setSessions(result);
    } catch (err) {
      console.error('Failed to fetch sessions:', err);
      setError('Errore nel caricamento delle sessioni');
    } finally {
      setLoading(false);
    }
  }, [limit, userIdFilter]);

  // Initial load and refresh on filter change
  useEffect(() => {
    fetchSessions();
  }, [fetchSessions]);

  // Handle refresh
  const handleRefresh = async () => {
    await fetchSessions();
    addToast('success', 'Sessioni aggiornate');
  };

  // Handle search
  const handleSearch = () => {
    setUserIdFilter(searchInput.trim());
  };

  // Clear search
  const handleClearSearch = () => {
    setSearchInput('');
    setUserIdFilter('');
  };

  // Revoke single session
  const handleRevokeSession = async () => {
    if (!revokeAction || revokeAction.type !== 'single' || !revokeAction.sessionId) return;

    try {
      setIsRevoking(true);
      await api.admin.revokeSession(revokeAction.sessionId);
      addToast('success', 'Sessione revocata con successo');
      setRevokeAction(null);
      await fetchSessions();
    } catch (err) {
      console.error('Failed to revoke session:', err);
      addToast('error', 'Errore nella revoca della sessione');
    } finally {
      setIsRevoking(false);
    }
  };

  // Revoke all user sessions
  const handleRevokeAllUserSessions = async () => {
    if (!revokeAction || revokeAction.type !== 'all' || !revokeAction.userId) return;

    try {
      setIsRevoking(true);
      await api.admin.revokeAllUserSessions(revokeAction.userId);
      addToast('success', `Tutte le sessioni revocate per ${revokeAction.userEmail}`);
      setRevokeAction(null);
      await fetchSessions();
    } catch (err) {
      console.error('Failed to revoke all user sessions:', err);
      addToast('error', 'Errore nella revoca delle sessioni');
    } finally {
      setIsRevoking(false);
    }
  };

  // Format date helper
  const formatDate = (dateStr: string | null | undefined) => {
    if (!dateStr) return 'N/A';
    return new Date(dateStr).toLocaleDateString('it-IT', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  // Check if session is expired
  const isSessionExpired = (expiresAt: string) => {
    return new Date(expiresAt) < new Date();
  };

  // Check if session is revoked
  const isSessionRevoked = (revokedAt: string | null | undefined) => {
    return !!revokedAt;
  };

  // Get session status badge
  const getSessionStatusBadge = (session: AdminSessionInfo) => {
    if (isSessionRevoked(session.revokedAt)) {
      return <Badge variant="destructive">Revocata</Badge>;
    }
    if (isSessionExpired(session.expiresAt)) {
      return <Badge variant="secondary">Scaduta</Badge>;
    }
    return <Badge variant="default">Attiva</Badge>;
  };

  // Parse user agent for display
  const parseUserAgent = (ua: string | null | undefined) => {
    if (!ua) return 'Sconosciuto';
    // Simple parsing - show first 40 chars
    return ua.length > 40 ? `${ua.substring(0, 40)}...` : ua;
  };

  // Count active sessions
  const activeSessions = sessions.filter(
    s => !isSessionRevoked(s.revokedAt) && !isSessionExpired(s.expiresAt)
  ).length;

  return (
    <AdminAuthGuard loading={authLoading} user={currentUser}>
      <div className="container mx-auto p-6 max-w-7xl space-y-6">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-bold flex items-center gap-2">
              <Monitor className="h-6 w-6" />
              Monitoraggio Sessioni
            </h1>
            <p className="text-muted-foreground">
              Gestisci e monitora le sessioni utente attive
            </p>
          </div>
          <Button
            variant="outline"
            onClick={handleRefresh}
            disabled={loading}
          >
            <RefreshCw className={`h-4 w-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
            Aggiorna
          </Button>
        </div>

        {/* Stats Cards */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium">Sessioni Totali</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold">{sessions.length}</div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium">Sessioni Attive</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold text-green-600">{activeSessions}</div>
            </CardContent>
          </Card>
          <Card>
            <CardHeader className="pb-2">
              <CardTitle className="text-sm font-medium">Sessioni Scadute/Revocate</CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-2xl font-bold text-muted-foreground">
                {sessions.length - activeSessions}
              </div>
            </CardContent>
          </Card>
        </div>

        {/* Filters */}
        <Card>
          <CardHeader>
            <CardTitle className="text-lg">Filtri</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex flex-wrap gap-4 items-end">
              {/* User ID Search */}
              <div className="flex-1 min-w-[250px]">
                <label className="text-sm font-medium mb-1 block">Cerca per User ID</label>
                <div className="flex gap-2">
                  <Input
                    placeholder="Inserisci User ID..."
                    value={searchInput}
                    onChange={(e) => setSearchInput(e.target.value)}
                    onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
                  />
                  <Button variant="outline" onClick={handleSearch}>
                    <Search className="h-4 w-4" />
                  </Button>
                  {userIdFilter && (
                    <Button variant="ghost" onClick={handleClearSearch}>
                      <X className="h-4 w-4" />
                    </Button>
                  )}
                </div>
              </div>

              {/* Limit */}
              <div className="w-[150px]">
                <label className="text-sm font-medium mb-1 block">Limite</label>
                <Select
                  value={limit.toString()}
                  onValueChange={(v) => setLimit(Number(v))}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="25">25</SelectItem>
                    <SelectItem value="50">50</SelectItem>
                    <SelectItem value="100">100</SelectItem>
                    <SelectItem value="200">200</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>

            {userIdFilter && (
              <div className="mt-3">
                <Badge variant="outline" className="gap-1">
                  <User className="h-3 w-3" />
                  Filtro attivo: {userIdFilter}
                </Badge>
              </div>
            )}
          </CardContent>
        </Card>

        {/* Error State */}
        {error && (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {/* Sessions Table */}
        <Card>
          <CardHeader>
            <CardTitle>Sessioni</CardTitle>
            <CardDescription>
              Lista delle sessioni utente con dettagli e azioni
            </CardDescription>
          </CardHeader>
          <CardContent>
            {loading ? (
              <div className="flex justify-center py-12">
                <Spinner size="lg" />
              </div>
            ) : sessions.length === 0 ? (
              <Alert>
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>
                  Nessuna sessione trovata con i filtri attuali.
                </AlertDescription>
              </Alert>
            ) : (
              <div className="rounded-md border overflow-x-auto">
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Stato</TableHead>
                      <TableHead>Utente</TableHead>
                      <TableHead>Creata</TableHead>
                      <TableHead>Scadenza</TableHead>
                      <TableHead>Ultimo Accesso</TableHead>
                      <TableHead>IP</TableHead>
                      <TableHead>Device</TableHead>
                      <TableHead className="text-right">Azioni</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {sessions.map((session) => (
                      <TableRow key={session.id}>
                        <TableCell>{getSessionStatusBadge(session)}</TableCell>
                        <TableCell>
                          <div className="flex items-center gap-2">
                            <User className="h-4 w-4 text-muted-foreground" />
                            <div>
                              <div className="font-medium text-sm">{session.userEmail}</div>
                              <div className="text-xs text-muted-foreground truncate max-w-[150px]">
                                {session.userId}
                              </div>
                            </div>
                          </div>
                        </TableCell>
                        <TableCell>
                          <div className="flex items-center gap-1 text-sm">
                            <Clock className="h-3 w-3 text-muted-foreground" />
                            {formatDate(session.createdAt)}
                          </div>
                        </TableCell>
                        <TableCell>
                          <div className="text-sm">{formatDate(session.expiresAt)}</div>
                        </TableCell>
                        <TableCell>
                          <div className="text-sm">{formatDate(session.lastSeenAt)}</div>
                        </TableCell>
                        <TableCell>
                          <div className="flex items-center gap-1 text-sm">
                            <Globe className="h-3 w-3 text-muted-foreground" />
                            {session.ipAddress || 'N/A'}
                          </div>
                        </TableCell>
                        <TableCell>
                          <div
                            className="flex items-center gap-1 text-sm max-w-[150px]"
                            title={session.userAgent || 'Sconosciuto'}
                          >
                            <Smartphone className="h-3 w-3 text-muted-foreground" />
                            <span className="truncate">
                              {parseUserAgent(session.userAgent)}
                            </span>
                          </div>
                        </TableCell>
                        <TableCell className="text-right">
                          <div className="flex justify-end gap-2">
                            {!isSessionRevoked(session.revokedAt) && !isSessionExpired(session.expiresAt) && (
                              <>
                                <Button
                                  variant="outline"
                                  size="sm"
                                  onClick={() => setRevokeAction({
                                    type: 'single',
                                    sessionId: session.id,
                                  })}
                                  title="Revoca questa sessione"
                                >
                                  <Trash2 className="h-3 w-3" />
                                </Button>
                                <Button
                                  variant="outline"
                                  size="sm"
                                  onClick={() => setRevokeAction({
                                    type: 'all',
                                    userId: session.userId,
                                    userEmail: session.userEmail,
                                  })}
                                  title="Revoca tutte le sessioni dell'utente"
                                >
                                  <Ban className="h-3 w-3" />
                                </Button>
                              </>
                            )}
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            )}
          </CardContent>
        </Card>

        {/* Revoke Single Session Dialog */}
        <AlertDialog
          open={revokeAction?.type === 'single'}
          onOpenChange={(open) => !open && setRevokeAction(null)}
        >
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Conferma Revoca Sessione</AlertDialogTitle>
              <AlertDialogDescription>
                Sei sicuro di voler revocare questa sessione? L&apos;utente verrà disconnesso
                immediatamente.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel disabled={isRevoking}>Annulla</AlertDialogCancel>
              <AlertDialogAction
                onClick={handleRevokeSession}
                disabled={isRevoking}
                className="bg-red-600 hover:bg-red-700"
              >
                {isRevoking ? <Spinner size="sm" /> : 'Revoca'}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>

        {/* Revoke All User Sessions Dialog */}
        <AlertDialog
          open={revokeAction?.type === 'all'}
          onOpenChange={(open) => !open && setRevokeAction(null)}
        >
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Conferma Revoca Tutte le Sessioni</AlertDialogTitle>
              <AlertDialogDescription>
                Sei sicuro di voler revocare tutte le sessioni di{' '}
                <strong>{revokeAction?.userEmail}</strong>? L&apos;utente verrà disconnesso
                da tutti i dispositivi.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel disabled={isRevoking}>Annulla</AlertDialogCancel>
              <AlertDialogAction
                onClick={handleRevokeAllUserSessions}
                disabled={isRevoking}
                className="bg-red-600 hover:bg-red-700"
              >
                {isRevoking ? <Spinner size="sm" /> : 'Revoca Tutte'}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>

        {/* Toast Notifications */}
        <div className="fixed bottom-4 right-4 z-50 space-y-2">
          {toasts.map(toast => (
            <Alert
              key={toast.id}
              variant={toast.type === 'error' ? 'destructive' : 'default'}
              className="w-96"
            >
              <AlertDescription>{toast.message}</AlertDescription>
            </Alert>
          ))}
        </div>
      </div>
    </AdminAuthGuard>
  );
}
