'use client';

/**
 * Pending Deletes Client Component - Issue #2372
 *
 * Admin-only page for managing delete requests from Editors.
 * Features:
 * - View pending delete requests
 * - Approve with optional comment
 * - Reject with required reason
 * - Link to game details
 */

import { useCallback, useEffect, useState } from 'react';

import {
  AlertTriangle,
  ArrowLeft,
  Check,
  Clock,
  ExternalLink,
  RefreshCw,
  Trash2,
  X,
} from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { AdminAuthGuard } from '@/components/admin';
import { useAuthUser } from '@/components/auth/AuthProvider';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { Textarea } from '@/components/ui/textarea';
import { useApiClient } from '@/lib/api/context';
import { type DeleteRequest } from '@/lib/api/schemas/shared-games.schemas';

export function PendingDeletesClient() {
  const router = useRouter();
  const { sharedGames } = useApiClient();
  const { user, loading: authLoading } = useAuthUser();

  // Early return if no user
  if (!user) return null;

  // State
  const [requests, setRequests] = useState<DeleteRequest[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(10);
  const [loading, setLoading] = useState(true);

  // Dialog state
  const [approveDialogOpen, setApproveDialogOpen] = useState(false);
  const [rejectDialogOpen, setRejectDialogOpen] = useState(false);
  const [selectedRequest, setSelectedRequest] = useState<DeleteRequest | null>(null);
  const [approveComment, setApproveComment] = useState('');
  const [rejectReason, setRejectReason] = useState('');
  const [processing, setProcessing] = useState(false);

  // Fetch pending delete requests
  const fetchRequests = useCallback(async () => {
    setLoading(true);
    try {
      const result = await sharedGames.getPendingDeletes({ page, pageSize });
      setRequests(result.items);
      setTotal(result.total);
    } catch (error) {
      console.error('Failed to fetch pending deletes:', error);
      toast.error('Errore nel caricamento delle richieste');
    } finally {
      setLoading(false);
    }
  }, [sharedGames, page, pageSize]);

  useEffect(() => {
    fetchRequests();
  }, [fetchRequests]);

  // Handle approve
  const handleApprove = async () => {
    if (!selectedRequest) return;

    setProcessing(true);
    try {
      await sharedGames.approveDelete(
        selectedRequest.id,
        approveComment ? { comment: approveComment } : undefined
      );
      toast.success('Richiesta di eliminazione approvata');
      setApproveDialogOpen(false);
      setSelectedRequest(null);
      setApproveComment('');
      fetchRequests();
    } catch (error) {
      console.error('Failed to approve delete:', error);
      toast.error("Errore nell'approvazione della richiesta");
    } finally {
      setProcessing(false);
    }
  };

  // Handle reject
  const handleReject = async () => {
    if (!selectedRequest || !rejectReason.trim()) {
      toast.error('Inserisci un motivo per il rifiuto');
      return;
    }

    setProcessing(true);
    try {
      await sharedGames.rejectDelete(selectedRequest.id, { reason: rejectReason });
      toast.success('Richiesta di eliminazione rifiutata');
      setRejectDialogOpen(false);
      setSelectedRequest(null);
      setRejectReason('');
      fetchRequests();
    } catch (error) {
      console.error('Failed to reject delete:', error);
      toast.error('Errore nel rifiuto della richiesta');
    } finally {
      setProcessing(false);
    }
  };

  // Open approve dialog
  const openApproveDialog = (request: DeleteRequest) => {
    setSelectedRequest(request);
    setApproveComment('');
    setApproveDialogOpen(true);
  };

  // Open reject dialog
  const openRejectDialog = (request: DeleteRequest) => {
    setSelectedRequest(request);
    setRejectReason('');
    setRejectDialogOpen(true);
  };

  // Format date
  const formatDate = (dateString: string) => {
    return new Date(dateString).toLocaleDateString('it-IT', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  // Calculate pagination
  const totalPages = Math.ceil(total / pageSize);

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto py-8 px-4">
        {/* Header */}
        <div className="mb-8">
          <Button
            variant="ghost"
            size="sm"
            onClick={() => router.push('/admin/shared-games')}
            className="mb-4"
          >
            <ArrowLeft className="h-4 w-4 mr-2" />
            Torna al Catalogo
          </Button>

          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="h-12 w-12 rounded-lg bg-destructive/10 flex items-center justify-center">
                <Trash2 className="h-6 w-6 text-destructive" />
              </div>
              <div>
                <h1 className="text-3xl font-bold tracking-tight">Eliminazioni in Attesa</h1>
                <p className="text-muted-foreground">
                  Gestisci le richieste di eliminazione degli Editor
                </p>
              </div>
            </div>

            <Button variant="outline" onClick={fetchRequests} disabled={loading}>
              <RefreshCw className={`h-4 w-4 mr-2 ${loading ? 'animate-spin' : ''}`} />
              Aggiorna
            </Button>
          </div>
        </div>

        {/* Stats Card */}
        <Card className="mb-6">
          <CardHeader className="pb-3">
            <CardTitle className="text-lg">Riepilogo</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="flex items-center gap-6">
              <div className="flex items-center gap-2">
                <Clock className="h-5 w-5 text-amber-500" />
                <span className="text-2xl font-bold">{total}</span>
                <span className="text-muted-foreground">
                  {total === 1 ? 'richiesta in attesa' : 'richieste in attesa'}
                </span>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Requests Table */}
        <Card>
          <CardHeader>
            <CardTitle>Richieste di Eliminazione</CardTitle>
            <CardDescription>
              Approva o rifiuta le richieste di eliminazione inviate dagli Editor
            </CardDescription>
          </CardHeader>
          <CardContent>
            {loading ? (
              <div className="flex items-center justify-center py-12">
                <RefreshCw className="h-8 w-8 animate-spin text-muted-foreground" />
              </div>
            ) : requests.length === 0 ? (
              <div className="flex flex-col items-center justify-center py-12 text-center">
                <div className="h-16 w-16 rounded-full bg-muted flex items-center justify-center mb-4">
                  <Check className="h-8 w-8 text-muted-foreground" />
                </div>
                <h3 className="text-lg font-medium mb-1">Nessuna richiesta in attesa</h3>
                <p className="text-muted-foreground">
                  Tutte le richieste di eliminazione sono state gestite
                </p>
              </div>
            ) : (
              <>
                <Table>
                  <TableHeader>
                    <TableRow>
                      <TableHead>Gioco</TableHead>
                      <TableHead>Motivo</TableHead>
                      <TableHead>Data Richiesta</TableHead>
                      <TableHead className="text-right">Azioni</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {requests.map(request => (
                      <TableRow key={request.id}>
                        <TableCell>
                          <div className="flex items-center gap-2">
                            <span className="font-medium">{request.gameTitle}</span>
                            <Link
                              href={`/admin/shared-games/${request.sharedGameId}`}
                              target="_blank"
                            >
                              <Button variant="ghost" size="icon" className="h-6 w-6">
                                <ExternalLink className="h-3 w-3" />
                              </Button>
                            </Link>
                          </div>
                        </TableCell>
                        <TableCell>
                          <div className="max-w-md">
                            <p className="text-sm text-muted-foreground line-clamp-2">
                              {request.reason}
                            </p>
                          </div>
                        </TableCell>
                        <TableCell>
                          <Badge variant="outline" className="font-normal">
                            <Clock className="h-3 w-3 mr-1" />
                            {formatDate(request.createdAt)}
                          </Badge>
                        </TableCell>
                        <TableCell className="text-right">
                          <div className="flex items-center justify-end gap-2">
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={() => openRejectDialog(request)}
                              className="text-destructive hover:text-destructive"
                            >
                              <X className="h-4 w-4 mr-1" />
                              Rifiuta
                            </Button>
                            <Button
                              variant="default"
                              size="sm"
                              onClick={() => openApproveDialog(request)}
                              className="bg-green-600 hover:bg-green-700"
                            >
                              <Check className="h-4 w-4 mr-1" />
                              Approva
                            </Button>
                          </div>
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>

                {/* Pagination */}
                {totalPages > 1 && (
                  <div className="flex items-center justify-between mt-4 pt-4 border-t">
                    <p className="text-sm text-muted-foreground">
                      Pagina {page} di {totalPages} ({total} totali)
                    </p>
                    <div className="flex items-center gap-2">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setPage(p => Math.max(1, p - 1))}
                        disabled={page === 1}
                      >
                        Precedente
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setPage(p => Math.min(totalPages, p + 1))}
                        disabled={page === totalPages}
                      >
                        Successiva
                      </Button>
                    </div>
                  </div>
                )}
              </>
            )}
          </CardContent>
        </Card>

        {/* Approve Dialog */}
        <AlertDialog open={approveDialogOpen} onOpenChange={setApproveDialogOpen}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle className="flex items-center gap-2">
                <Check className="h-5 w-5 text-green-600" />
                Approva Eliminazione
              </AlertDialogTitle>
              <AlertDialogDescription>
                Sei sicuro di voler approvare l&apos;eliminazione di{' '}
                <strong>{selectedRequest?.gameTitle}</strong>? Questa azione è irreversibile.
              </AlertDialogDescription>
            </AlertDialogHeader>

            <div className="py-4">
              <Label htmlFor="approve-comment" className="text-sm font-medium">
                Commento (opzionale)
              </Label>
              <Input
                id="approve-comment"
                placeholder="Aggiungi un commento..."
                value={approveComment}
                onChange={e => setApproveComment(e.target.value)}
                className="mt-2"
              />
            </div>

            <AlertDialogFooter>
              <AlertDialogCancel disabled={processing}>Annulla</AlertDialogCancel>
              <AlertDialogAction
                onClick={handleApprove}
                disabled={processing}
                className="bg-green-600 hover:bg-green-700"
              >
                {processing ? 'Elaborazione...' : 'Approva Eliminazione'}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>

        {/* Reject Dialog */}
        <AlertDialog open={rejectDialogOpen} onOpenChange={setRejectDialogOpen}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle className="flex items-center gap-2">
                <AlertTriangle className="h-5 w-5 text-destructive" />
                Rifiuta Eliminazione
              </AlertDialogTitle>
              <AlertDialogDescription>
                Rifiuta la richiesta di eliminazione per{' '}
                <strong>{selectedRequest?.gameTitle}</strong>. Il gioco rimarrà nel catalogo.
              </AlertDialogDescription>
            </AlertDialogHeader>

            <div className="py-4">
              <Label htmlFor="reject-reason" className="text-sm font-medium">
                Motivo del Rifiuto <span className="text-destructive">*</span>
              </Label>
              <Textarea
                id="reject-reason"
                placeholder="Spiega il motivo del rifiuto..."
                value={rejectReason}
                onChange={e => setRejectReason(e.target.value)}
                className="mt-2"
                rows={3}
              />
            </div>

            <AlertDialogFooter>
              <AlertDialogCancel disabled={processing}>Annulla</AlertDialogCancel>
              <AlertDialogAction
                onClick={handleReject}
                disabled={processing || !rejectReason.trim()}
                className="bg-destructive hover:bg-destructive/90"
              >
                {processing ? 'Elaborazione...' : 'Rifiuta Richiesta'}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </div>
    </AdminAuthGuard>
  );
}
