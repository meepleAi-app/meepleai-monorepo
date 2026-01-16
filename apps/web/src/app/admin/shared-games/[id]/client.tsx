'use client';
/* eslint-disable security/detect-object-injection */

/**
 * Edit Game Client Component - Issue #2372
 *
 * Client component for editing an existing shared game.
 * Features:
 * - Tabbed interface: Details, Categories, Rules, History
 * - Status management (Publish/Archive/Delete)
 * - Role-based actions (Admin vs Editor)
 * - Delete request workflow for Editors
 */

import { useState, useEffect, useCallback } from 'react';

import {
  ArrowLeft,
  Edit3,
  History,
  Tag,
  FileText,
  Trash2,
  Archive,
  CheckCircle,
  Clock,
  RefreshCw,
  ExternalLink,
  AlertTriangle,
  Files,
} from 'lucide-react';
import { useRouter, useParams } from 'next/navigation';
import { toast } from 'sonner';

import {
  AdminAuthGuard,
  GameForm,
  GameStatusBadge,
  PlayersBadge,
  PlayTimeBadge,
  ComplexityBadge,
  PdfDocumentList,
} from '@/components/admin';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmationDialog } from '@/components/ui/overlays/confirmation-dialog';
import { Skeleton } from '@/components/ui/skeleton';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Textarea } from '@/components/ui/textarea';
import { api, type SharedGameDetail, type SharedGameDocument } from '@/lib/api';

// Game status numeric values (matches C# GameStatus enum)
const GAME_STATUS = {
  Draft: 0,
  Published: 1,
  Archived: 2,
} as const;

type TabValue = 'details' | 'categories' | 'rules' | 'documents' | 'history';

type ConfirmDialogState = {
  isOpen: boolean;
  title: string;
  message: string;
  variant: 'default' | 'destructive' | 'warning';
  onConfirm: () => void;
};

export function EditGameClient() {
  const router = useRouter();
  const params = useParams();
  const gameId = params?.id as string | undefined;
  const { user, loading: authLoading } = useAuthUser();

  // State
  const [activeTab, setActiveTab] = useState<TabValue>('details');
  const [game, setGame] = useState<SharedGameDetail | null>(null);
  const [documents, setDocuments] = useState<SharedGameDocument[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isActionLoading, setIsActionLoading] = useState(false);
  const [deleteReason, setDeleteReason] = useState('');
  const [confirmDialog, setConfirmDialog] = useState<ConfirmDialogState>({
    isOpen: false,
    title: '',
    message: '',
    variant: 'default',
    onConfirm: () => {},
  });

  const isAdmin = user?.role?.toLowerCase() === 'admin';

  // Fetch game data
  const fetchGame = useCallback(async () => {
    if (!gameId) return;

    setIsLoading(true);
    try {
      const result = await api.sharedGames.getById(gameId);
      setGame(result);
    } catch (error) {
      console.error('Failed to fetch game:', error);
      toast.error('Errore nel caricamento del gioco');
    } finally {
      setIsLoading(false);
    }
  }, [gameId]);

  // Fetch documents (Issue #2391 Sprint 1)
  const fetchDocuments = useCallback(async () => {
    if (!gameId) return;

    try {
      const result = await api.sharedGames.getDocuments(gameId);
      setDocuments(result);
    } catch (error) {
      console.error('Failed to fetch documents:', error);
      toast.error('Errore nel caricamento dei documenti');
    }
  }, [gameId]);

  useEffect(() => {
    fetchGame();
    fetchDocuments();
  }, [fetchGame, fetchDocuments]);

  // Handle missing ID
  if (!gameId) {
    return (
      <AdminAuthGuard loading={authLoading} user={user}>
        <div className="container mx-auto py-8 px-4">
          <Alert variant="destructive">
            <AlertTriangle className="h-4 w-4" />
            <AlertTitle>ID Gioco non valido</AlertTitle>
            <AlertDescription>
              L&apos;ID del gioco non è stato fornito.{' '}
              <Button
                variant="link"
                className="p-0 h-auto"
                onClick={() => router.push('/admin/shared-games')}
              >
                Torna al catalogo
              </Button>
            </AlertDescription>
          </Alert>
        </div>
      </AdminAuthGuard>
    );
  }

  // Navigation handlers
  const handleBack = () => router.push('/admin/shared-games');
  const handleSubmit = () => {
    fetchGame();
    toast.success('Gioco aggiornato con successo');
  };
  const handleCancel = () => router.push('/admin/shared-games');

  // Status change handlers
  const handlePublish = () => {
    setConfirmDialog({
      isOpen: true,
      title: 'Pubblica Gioco',
      message: 'Sei sicuro di voler pubblicare questo gioco? Sarà visibile a tutti gli utenti.',
      variant: 'default',
      onConfirm: async () => {
        setIsActionLoading(true);
        try {
          await api.sharedGames.publish(gameId);
          toast.success('Gioco pubblicato con successo');
          fetchGame();
        } catch (error) {
          console.error('Failed to publish game:', error);
          toast.error('Errore nella pubblicazione del gioco');
        } finally {
          setIsActionLoading(false);
        }
      },
    });
  };

  const handleArchive = () => {
    setConfirmDialog({
      isOpen: true,
      title: 'Archivia Gioco',
      message:
        'Sei sicuro di voler archiviare questo gioco? Non sarà più visibile agli utenti ma potrà essere ripristinato.',
      variant: 'warning',
      onConfirm: async () => {
        setIsActionLoading(true);
        try {
          await api.sharedGames.archive(gameId);
          toast.success('Gioco archiviato con successo');
          fetchGame();
        } catch (error) {
          console.error('Failed to archive game:', error);
          toast.error("Errore nell'archiviazione del gioco");
        } finally {
          setIsActionLoading(false);
        }
      },
    });
  };

  const handleRequestDelete = () => {
    if (!deleteReason.trim()) {
      toast.error('Inserisci una motivazione per la richiesta di eliminazione');
      return;
    }

    setConfirmDialog({
      isOpen: true,
      title: 'Richiedi Eliminazione',
      message:
        'La richiesta verrà inviata agli amministratori per approvazione. Sei sicuro di voler procedere?',
      variant: 'destructive',
      onConfirm: async () => {
        setIsActionLoading(true);
        try {
          // Editor: use requestDelete which creates a delete request for admin approval
          await api.sharedGames.requestDelete(gameId, { reason: deleteReason });
          toast.success('Richiesta di eliminazione inviata');
          setDeleteReason('');
          fetchGame();
        } catch (error) {
          console.error('Failed to request delete:', error);
          toast.error("Errore nell'invio della richiesta");
        } finally {
          setIsActionLoading(false);
        }
      },
    });
  };

  const handleDelete = () => {
    setConfirmDialog({
      isOpen: true,
      title: 'Elimina Gioco',
      message:
        'ATTENZIONE: Questa azione è irreversibile. Il gioco verrà eliminato definitivamente dal database. Sei sicuro?',
      variant: 'destructive',
      onConfirm: async () => {
        setIsActionLoading(true);
        try {
          // Admin: delete without reason performs immediate deletion
          await api.sharedGames.delete(gameId);
          toast.success('Gioco eliminato');
          router.push('/admin/shared-games');
        } catch (error) {
          console.error('Failed to delete game:', error);
          toast.error("Errore nell'eliminazione del gioco");
        } finally {
          setIsActionLoading(false);
        }
      },
    });
  };

  // Document handlers (Issue #2391 Sprint 1)
  const handleSetActiveDocument = async (documentId: string) => {
    if (!gameId) return;

    setIsActionLoading(true);
    try {
      await api.sharedGames.setActiveDocument(gameId, documentId);
      toast.success('Versione impostata come attiva');
      await fetchDocuments();
    } catch (error) {
      console.error('Failed to set active document:', error);
      toast.error("Errore nell'impostazione della versione attiva");
    } finally {
      setIsActionLoading(false);
    }
  };

  const handleRemoveDocument = (documentId: string) => {
    setConfirmDialog({
      isOpen: true,
      title: 'Rimuovi Documento',
      message: 'Sei sicuro di voler rimuovere questo documento dal gioco?',
      variant: 'destructive',
      onConfirm: async () => {
        if (!gameId) return;

        setIsActionLoading(true);
        try {
          await api.sharedGames.removeDocument(gameId, documentId);
          toast.success('Documento rimosso');
          await fetchDocuments();
        } catch (error: unknown) {
          console.error('Failed to remove document:', error);
          toast.error('Errore nella rimozione del documento');
        } finally {
          setIsActionLoading(false);
        }
      },
    });
  };

  // Loading state
  if (isLoading) {
    return (
      <AdminAuthGuard loading={authLoading} user={user}>
        <div className="container mx-auto py-8 px-4">
          <div className="mb-8">
            <Skeleton className="h-8 w-32 mb-4" />
            <Skeleton className="h-10 w-96 mb-2" />
            <Skeleton className="h-5 w-64" />
          </div>
          <Skeleton className="h-[600px] w-full" />
        </div>
      </AdminAuthGuard>
    );
  }

  // Game not found
  if (!game) {
    return (
      <AdminAuthGuard loading={authLoading} user={user}>
        <div className="container mx-auto py-8 px-4">
          <Alert variant="destructive">
            <AlertTriangle className="h-4 w-4" />
            <AlertTitle>Gioco non trovato</AlertTitle>
            <AlertDescription>
              Il gioco richiesto non esiste o è stato eliminato.{' '}
              <Button variant="link" className="p-0 h-auto" onClick={handleBack}>
                Torna al catalogo
              </Button>
            </AlertDescription>
          </Alert>
        </div>
      </AdminAuthGuard>
    );
  }

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto py-8 px-4">
        {/* Header */}
        <div className="mb-8">
          <Button variant="ghost" size="sm" onClick={handleBack} className="mb-4">
            <ArrowLeft className="h-4 w-4 mr-2" />
            Torna al Catalogo
          </Button>

          <div className="flex items-start justify-between">
            <div className="flex items-center gap-4">
              {game.thumbnailUrl && (
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  src={game.thumbnailUrl}
                  alt={game.title}
                  className="h-20 w-20 rounded-lg object-cover border"
                />
              )}
              <div>
                <div className="flex items-center gap-3 mb-1">
                  <h1 className="text-3xl font-bold tracking-tight">{game.title}</h1>
                  <GameStatusBadge status={game.status} />
                </div>
                <p className="text-muted-foreground">
                  {game.yearPublished} &bull;{' '}
                  {game.bggId && (
                    <Button
                      variant="link"
                      size="sm"
                      className="p-0 h-auto"
                      onClick={() =>
                        window.open(`https://boardgamegeek.com/boardgame/${game.bggId}`, '_blank')
                      }
                    >
                      BGG #{game.bggId}
                      <ExternalLink className="h-3 w-3 ml-1" />
                    </Button>
                  )}
                </p>
                <div className="flex gap-2 mt-2">
                  <PlayersBadge min={game.minPlayers} max={game.maxPlayers} />
                  <PlayTimeBadge minutes={game.playingTimeMinutes} />
                  {game.complexityRating && <ComplexityBadge rating={game.complexityRating} />}
                </div>
              </div>
            </div>

            {/* Action Buttons */}
            <div className="flex gap-2">
              <Button variant="outline" size="sm" onClick={fetchGame} disabled={isActionLoading}>
                <RefreshCw className="h-4 w-4 mr-1" />
                Aggiorna
              </Button>

              {game.status === GAME_STATUS.Draft && (
                <Button size="sm" onClick={handlePublish} disabled={isActionLoading}>
                  <CheckCircle className="h-4 w-4 mr-1" />
                  Pubblica
                </Button>
              )}

              {game.status === GAME_STATUS.Published && (
                <Button
                  variant="outline"
                  size="sm"
                  onClick={handleArchive}
                  disabled={isActionLoading}
                >
                  <Archive className="h-4 w-4 mr-1" />
                  Archivia
                </Button>
              )}

              {isAdmin && (
                <Button
                  variant="destructive"
                  size="sm"
                  onClick={handleDelete}
                  disabled={isActionLoading}
                >
                  <Trash2 className="h-4 w-4 mr-1" />
                  Elimina
                </Button>
              )}
            </div>
          </div>
        </div>

        {/* Note: Delete requests are managed through the Pending Deletes admin page */}

        {/* Tabs */}
        <Tabs value={activeTab} onValueChange={v => setActiveTab(v as TabValue)}>
          <TabsList className="mb-6">
            <TabsTrigger value="details" className="gap-2">
              <Edit3 className="h-4 w-4" />
              Dettagli
            </TabsTrigger>
            <TabsTrigger value="categories" className="gap-2">
              <Tag className="h-4 w-4" />
              Categorie
            </TabsTrigger>
            <TabsTrigger value="rules" className="gap-2">
              <FileText className="h-4 w-4" />
              Regole
            </TabsTrigger>
            <TabsTrigger value="documents" className="gap-2">
              <Files className="h-4 w-4" />
              Documenti
            </TabsTrigger>
            <TabsTrigger value="history" className="gap-2">
              <History className="h-4 w-4" />
              Cronologia
            </TabsTrigger>
          </TabsList>

          {/* Details Tab */}
          <TabsContent value="details" className="max-w-4xl">
            <GameForm
              game={game}
              onSubmit={handleSubmit}
              onCancel={handleCancel}
              isLoading={isActionLoading}
            />
          </TabsContent>

          {/* Categories Tab */}
          <TabsContent value="categories">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <Card>
                <CardHeader>
                  <CardTitle>Categorie</CardTitle>
                  <CardDescription>Le categorie associate a questo gioco</CardDescription>
                </CardHeader>
                <CardContent>
                  <p className="text-sm text-muted-foreground">
                    La gestione delle categorie sarà disponibile in una prossima versione.
                  </p>
                </CardContent>
              </Card>

              <Card>
                <CardHeader>
                  <CardTitle>Meccaniche</CardTitle>
                  <CardDescription>Le meccaniche di gioco associate</CardDescription>
                </CardHeader>
                <CardContent>
                  <p className="text-sm text-muted-foreground">
                    La gestione delle meccaniche sarà disponibile in una prossima versione.
                  </p>
                </CardContent>
              </Card>
            </div>
          </TabsContent>

          {/* Rules Tab */}
          <TabsContent value="rules">
            <Card>
              <CardHeader>
                <CardTitle>Contenuto Regole</CardTitle>
                <CardDescription>Le regole del gioco salvate nel sistema</CardDescription>
              </CardHeader>
              <CardContent>
                {game.rules ? (
                  <div className="space-y-4">
                    <div className="flex items-center gap-2">
                      <Badge variant="outline">
                        Lingua: {game.rules.language?.toUpperCase() || 'IT'}
                      </Badge>
                    </div>
                    <div className="p-4 bg-muted rounded-lg">
                      <pre className="whitespace-pre-wrap text-sm font-mono">
                        {game.rules.content || 'Nessun contenuto'}
                      </pre>
                    </div>
                  </div>
                ) : (
                  <p className="text-sm text-muted-foreground">
                    Nessuna regola salvata. Modifica il gioco nella tab Dettagli per aggiungere le
                    regole.
                  </p>
                )}
              </CardContent>
            </Card>
          </TabsContent>

          {/* Documents Tab (Issue #2391 Sprint 1) */}
          <TabsContent value="documents">
            <Card>
              <CardHeader>
                <CardTitle>Documenti Associati</CardTitle>
                <CardDescription>
                  PDF associati a questo gioco: Rulebook, Errata, Homerule con gestione versioni
                </CardDescription>
              </CardHeader>
              <CardContent>
                <PdfDocumentList
                  documents={documents}
                  onSetActive={handleSetActiveDocument}
                  onRemove={handleRemoveDocument}
                  isLoading={isActionLoading}
                />
              </CardContent>
            </Card>
          </TabsContent>

          {/* History Tab */}
          <TabsContent value="history">
            <div className="space-y-6">
              {/* Audit Info Card */}
              <Card>
                <CardHeader>
                  <CardTitle>Informazioni Audit</CardTitle>
                  <CardDescription>Tracciamento delle modifiche al gioco</CardDescription>
                </CardHeader>
                <CardContent className="space-y-4">
                  <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                    <div>
                      <p className="text-sm text-muted-foreground">Creato il</p>
                      <p className="font-medium">
                        {new Date(game.createdAt).toLocaleDateString('it-IT', {
                          day: '2-digit',
                          month: 'short',
                          year: 'numeric',
                          hour: '2-digit',
                          minute: '2-digit',
                        })}
                      </p>
                    </div>
                    <div>
                      <p className="text-sm text-muted-foreground">Ultima modifica</p>
                      <p className="font-medium">
                        {game.modifiedAt
                          ? new Date(game.modifiedAt).toLocaleDateString('it-IT', {
                              day: '2-digit',
                              month: 'short',
                              year: 'numeric',
                              hour: '2-digit',
                              minute: '2-digit',
                            })
                          : '-'}
                      </p>
                    </div>
                    <div>
                      <p className="text-sm text-muted-foreground">Stato</p>
                      <GameStatusBadge status={game.status} />
                    </div>
                    <div>
                      <p className="text-sm text-muted-foreground">ID</p>
                      <p className="font-mono text-sm">{game.id}</p>
                    </div>
                  </div>
                </CardContent>
              </Card>

              {/* Delete Request Card (for Editors) */}
              {!isAdmin && (
                <Card>
                  <CardHeader>
                    <CardTitle className="flex items-center gap-2">
                      <Trash2 className="h-5 w-5 text-destructive" />
                      Richiedi Eliminazione
                    </CardTitle>
                    <CardDescription>
                      Gli editor possono richiedere l&apos;eliminazione di un gioco. La richiesta
                      verrà esaminata da un amministratore.
                    </CardDescription>
                  </CardHeader>
                  <CardContent className="space-y-4">
                    <div>
                      <label className="text-sm font-medium mb-2 block">
                        Motivazione della richiesta *
                      </label>
                      <Textarea
                        value={deleteReason}
                        onChange={e => setDeleteReason(e.target.value)}
                        placeholder="Spiega perché questo gioco dovrebbe essere eliminato..."
                        rows={3}
                      />
                    </div>
                    <Button
                      variant="destructive"
                      onClick={handleRequestDelete}
                      disabled={isActionLoading || !deleteReason.trim()}
                    >
                      <Clock className="h-4 w-4 mr-2" />
                      Invia Richiesta di Eliminazione
                    </Button>
                  </CardContent>
                </Card>
              )}
            </div>
          </TabsContent>
        </Tabs>

        {/* Confirmation Dialog */}
        <ConfirmationDialog
          isOpen={confirmDialog.isOpen}
          onClose={() => setConfirmDialog(prev => ({ ...prev, isOpen: false }))}
          onConfirm={() => {
            confirmDialog.onConfirm();
            setConfirmDialog(prev => ({ ...prev, isOpen: false }));
          }}
          title={confirmDialog.title}
          message={confirmDialog.message}
          variant={confirmDialog.variant}
        />
      </div>
    </AdminAuthGuard>
  );
}
