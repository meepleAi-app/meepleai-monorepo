/**
 * Game Detail Client Component - Issue #3536
 *
 * Route: /admin/shared-games/[id]
 * Features:
 * - Tabbed interface (Details, Documents, Review History)
 * - PDF upload with drag-drop
 * - Document list with status badges
 * - Role-based approval workflow
 */

'use client';

import { use, useCallback, useState, useRef } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertCircle,
  ArrowLeft,
  Check,
  Clock,
  Download,
  ExternalLink,
  FileText,
  Loader2,
  MoreHorizontal,
  RefreshCw,
  Send,
  Trash2,
  Upload,
  X,
} from 'lucide-react';
import Image from 'next/image';
import Link from 'next/link';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { Button } from '@/components/ui/primitives/button';
import { api, type SharedGameDocument } from '@/lib/api';

interface GameDetailClientProps {
  params: Promise<{ id: string }>;
}

// Document type badge component
function DocumentTypeBadge({ documentType }: { documentType: number }) {
  const types: Record<number, { variant: 'default' | 'secondary' | 'outline'; label: string }> = {
    0: { variant: 'default', label: 'Rulebook' },
    1: { variant: 'secondary', label: 'Errata' },
    2: { variant: 'outline', label: 'Homerule' },
  };

  const config = types[documentType] || { variant: 'secondary' as const, label: 'Unknown' };

  return <Badge variant={config.variant}>{config.label}</Badge>;
}

// Game status badge
function GameStatusBadge({ status }: { status: string }) {
  const statusMap: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline'; label: string }> = {
    Draft: { variant: 'secondary', label: 'Draft' },
    Published: { variant: 'default', label: 'Published' },
    Archived: { variant: 'outline', label: 'Archived' },
    PendingApproval: { variant: 'destructive', label: 'Pending Approval' },
  };

  const config = statusMap[status] || { variant: 'secondary' as const, label: status };

  return <Badge variant={config.variant}>{config.label}</Badge>;
}

// Document list item
function DocumentListItem({
  document,
  onDelete,
  onDownload,
  isDeleting,
}: {
  document: SharedGameDocument;
  onDelete: (id: string) => void;
  onDownload: (id: string) => void;
  isDeleting: boolean;
}) {
  return (
    <div className="flex items-center justify-between rounded-lg border p-4">
      <div className="flex items-center gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted">
          <FileText className="h-5 w-5 text-muted-foreground" />
        </div>
        <div>
          <div className="flex items-center gap-2">
            <DocumentTypeBadge documentType={document.documentType} />
            <span className="text-sm text-muted-foreground">v{document.version}</span>
          </div>
          <div className="flex items-center gap-2 text-sm text-muted-foreground mt-1">
            <span>{new Date(document.createdAt).toLocaleDateString()}</span>
            {document.isActive && (
              <>
                <span>•</span>
                <Badge variant="outline" className="text-xs">Active</Badge>
              </>
            )}
          </div>
        </div>
      </div>
      <div className="flex items-center gap-2">
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon" disabled={isDeleting}>
              <MoreHorizontal className="h-4 w-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={() => onDownload(document.id)}>
              <Download className="mr-2 h-4 w-4" />
              Download
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem
              onClick={() => onDelete(document.id)}
              className="text-destructive focus:text-destructive"
            >
              <Trash2 className="mr-2 h-4 w-4" />
              Delete
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    </div>
  );
}

// PDF upload section with drag-drop
function PdfUploadSection({
  gameId: _gameId,
  onUploadSuccess,
}: {
  gameId: string;
  onUploadSuccess: () => void;
}) {
  const [isDragging, setIsDragging] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const uploadMutation = useMutation({
    mutationFn: async (_file: File) => {
      // PDF upload workflow placeholder
      // In production, this would:
      // 1. Upload PDF to document service to get pdfDocumentId
      // 2. Call api.sharedGames.addDocument(gameId, { pdfDocumentId, documentType, version, ... })
      throw new Error('PDF upload not yet implemented. Coming soon!');
    },
    onSuccess: () => {
      setUploadError(null);
      onUploadSuccess();
    },
    onError: (err) => {
      setUploadError(err instanceof Error ? err.message : 'Upload failed');
    },
  });

  const validateFile = useCallback((file: File): string | null => {
    if (file.type !== 'application/pdf') {
      return 'Only PDF files are allowed';
    }
    if (file.size > 50 * 1024 * 1024) {
      return 'File size must be less than 50MB';
    }
    return null;
  }, []);

  const handleFile = useCallback(
    (file: File) => {
      const error = validateFile(file);
      if (error) {
        setUploadError(error);
        return;
      }
      uploadMutation.mutate(file);
    },
    [validateFile, uploadMutation]
  );

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setIsDragging(false);

      const file = e.dataTransfer.files[0];
      if (file) {
        handleFile(file);
      }
    },
    [handleFile]
  );

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
  }, []);

  const handleFileSelect = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (file) {
        handleFile(file);
      }
    },
    [handleFile]
  );

  return (
    <div className="space-y-4">
      <div
        className={`
          relative rounded-lg border-2 border-dashed p-8
          transition-colors duration-200
          ${isDragging ? 'border-primary bg-primary/5' : 'border-muted-foreground/25'}
          ${uploadMutation.isPending ? 'pointer-events-none opacity-50' : 'cursor-pointer hover:border-primary/50'}
        `}
        onDrop={handleDrop}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onClick={() => fileInputRef.current?.click()}
      >
        <input
          ref={fileInputRef}
          type="file"
          accept=".pdf,application/pdf"
          onChange={handleFileSelect}
          className="hidden"
        />
        <div className="flex flex-col items-center gap-2 text-center">
          {uploadMutation.isPending ? (
            <>
              <Loader2 className="h-10 w-10 animate-spin text-primary" />
              <p className="text-sm font-medium">Uploading...</p>
            </>
          ) : (
            <>
              <Upload className="h-10 w-10 text-muted-foreground" />
              <p className="text-sm font-medium">Drop PDF here or click to browse</p>
              <p className="text-xs text-muted-foreground">Max file size: 50MB</p>
            </>
          )}
        </div>
      </div>

      {uploadError && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>{uploadError}</AlertDescription>
        </Alert>
      )}
    </div>
  );
}

// Loading skeleton for game details
function GameDetailSkeleton() {
  return (
    <div className="space-y-6">
      <div className="flex items-center gap-4">
        <Skeleton className="h-10 w-10" />
        <div className="space-y-2">
          <Skeleton className="h-8 w-64" />
          <Skeleton className="h-4 w-32" />
        </div>
      </div>
      <div className="grid gap-6 md:grid-cols-3">
        <div className="md:col-span-2 space-y-4">
          <Skeleton className="h-40 w-full" />
          <Skeleton className="h-20 w-full" />
        </div>
        <Skeleton className="h-64 w-full" />
      </div>
    </div>
  );
}

// Review history item
function ReviewHistoryItem({
  action,
  user,
  timestamp,
  notes,
}: {
  action: string;
  user: string;
  timestamp: string;
  notes?: string;
}) {
  const actionIcons: Record<string, React.ReactNode> = {
    submitted: <Send className="h-4 w-4" />,
    approved: <Check className="h-4 w-4 text-green-500" />,
    rejected: <X className="h-4 w-4 text-red-500" />,
    updated: <RefreshCw className="h-4 w-4" />,
  };

  return (
    <div className="flex gap-3 border-l-2 border-muted pl-4 pb-4">
      <div className="flex h-8 w-8 items-center justify-center rounded-full bg-muted">
        {actionIcons[action] || <Clock className="h-4 w-4" />}
      </div>
      <div className="flex-1">
        <p className="text-sm font-medium capitalize">{action}</p>
        <p className="text-xs text-muted-foreground">
          {user} • {new Date(timestamp).toLocaleString()}
        </p>
        {notes && <p className="mt-1 text-sm text-muted-foreground">{notes}</p>}
      </div>
    </div>
  );
}

export function GameDetailClient({ params }: GameDetailClientProps) {
  const resolvedParams = use(params);
  const gameId = resolvedParams.id;
  const queryClient = useQueryClient();

  // Fetch game details
  const {
    data: game,
    isLoading,
    error,
  } = useQuery({
    queryKey: ['admin', 'shared-games', gameId],
    queryFn: () => api.sharedGames.getById(gameId),
  });

  // Fetch documents
  const { data: documents, refetch: refetchDocuments } = useQuery({
    queryKey: ['admin', 'shared-games', gameId, 'documents'],
    queryFn: () => api.sharedGames.getDocuments(gameId),
    enabled: !!game,
  });

  // Delete document mutation
  const deleteDocumentMutation = useMutation({
    mutationFn: (documentId: string) => api.sharedGames.removeDocument(gameId, documentId),
    onSuccess: () => {
      refetchDocuments();
    },
  });

  // Submit for approval mutation
  const submitForApprovalMutation = useMutation({
    mutationFn: () => api.sharedGames.submitForApproval(gameId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'shared-games', gameId] });
    },
  });

  // Approve mutation
  const approveMutation = useMutation({
    mutationFn: () => api.sharedGames.approvePublication(gameId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'shared-games', gameId] });
    },
  });

  // Reject mutation
  const rejectMutation = useMutation({
    mutationFn: (reason: string) => api.sharedGames.rejectPublication(gameId, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'shared-games', gameId] });
    },
  });

  const handleDownload = useCallback((_documentId: string) => {
    // Download functionality - will be wired up when PDF API is complete
    // For now, this is a placeholder
  }, []);

  if (isLoading) {
    return (
      <div className="container py-6">
        <GameDetailSkeleton />
      </div>
    );
  }

  if (error || !game) {
    return (
      <div className="container py-6">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            {error instanceof Error ? error.message : 'Failed to load game details'}
          </AlertDescription>
        </Alert>
        <div className="mt-4">
          <Link href="/admin/shared-games">
            <Button variant="outline">
              <ArrowLeft className="mr-2 h-4 w-4" />
              Back to Games
            </Button>
          </Link>
        </div>
      </div>
    );
  }

  const canSubmitForApproval = game.status === 'Draft';
  const canApprove = game.status === 'PendingApproval';
  const hasImage = game.imageUrl && game.imageUrl.length > 0;

  return (
    <div className="container py-6 space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div className="flex items-center gap-4">
          <Link href="/admin/shared-games">
            <Button variant="ghost" size="icon">
              <ArrowLeft className="h-5 w-5" />
            </Button>
          </Link>
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-2xl font-bold">{game.title}</h1>
              <GameStatusBadge status={game.status} />
            </div>
            {game.bggId && (
              <a
                href={`https://boardgamegeek.com/boardgame/${game.bggId}`}
                target="_blank"
                rel="noopener noreferrer"
                className="text-sm text-muted-foreground hover:text-foreground flex items-center gap-1"
              >
                BGG #{game.bggId}
                <ExternalLink className="h-3 w-3" />
              </a>
            )}
          </div>
        </div>

        {/* Action buttons */}
        <div className="flex gap-2">
          {canSubmitForApproval && (
            <Button
              onClick={() => submitForApprovalMutation.mutate()}
              disabled={submitForApprovalMutation.isPending}
            >
              {submitForApprovalMutation.isPending ? (
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              ) : (
                <Send className="mr-2 h-4 w-4" />
              )}
              Submit for Approval
            </Button>
          )}
          {canApprove && (
            <>
              <Button
                variant="outline"
                onClick={() => rejectMutation.mutate('Rejected by admin')}
                disabled={rejectMutation.isPending}
              >
                {rejectMutation.isPending ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <X className="mr-2 h-4 w-4" />
                )}
                Reject
              </Button>
              <Button
                onClick={() => approveMutation.mutate()}
                disabled={approveMutation.isPending}
              >
                {approveMutation.isPending ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <Check className="mr-2 h-4 w-4" />
                )}
                Approve
              </Button>
            </>
          )}
        </div>
      </div>

      {/* Tabs */}
      <Tabs defaultValue="details">
        <TabsList>
          <TabsTrigger value="details">Details</TabsTrigger>
          <TabsTrigger value="documents">
            Documents {documents?.length ? `(${documents.length})` : ''}
          </TabsTrigger>
          <TabsTrigger value="history">Review History</TabsTrigger>
        </TabsList>

        {/* Details Tab */}
        <TabsContent value="details" className="space-y-6">
          <div className="grid gap-6 md:grid-cols-3">
            <div className="md:col-span-2 space-y-6">
              {/* Description */}
              <Card>
                <CardHeader>
                  <CardTitle>Description</CardTitle>
                </CardHeader>
                <CardContent>
                  <p className="text-muted-foreground whitespace-pre-wrap">
                    {game.description || 'No description available.'}
                  </p>
                </CardContent>
              </Card>

              {/* Game Info */}
              <Card>
                <CardHeader>
                  <CardTitle>Game Information</CardTitle>
                </CardHeader>
                <CardContent>
                  <dl className="grid grid-cols-2 gap-4 text-sm">
                    {game.yearPublished && (
                      <>
                        <dt className="text-muted-foreground">Year Published</dt>
                        <dd>{game.yearPublished}</dd>
                      </>
                    )}
                    {game.minPlayers && game.maxPlayers && (
                      <>
                        <dt className="text-muted-foreground">Players</dt>
                        <dd>
                          {game.minPlayers === game.maxPlayers
                            ? game.minPlayers
                            : `${game.minPlayers}-${game.maxPlayers}`}
                        </dd>
                      </>
                    )}
                    {game.playingTimeMinutes && (
                      <>
                        <dt className="text-muted-foreground">Playing Time</dt>
                        <dd>{game.playingTimeMinutes} minutes</dd>
                      </>
                    )}
                    {game.averageRating && (
                      <>
                        <dt className="text-muted-foreground">BGG Rating</dt>
                        <dd className="flex items-center gap-1">
                          <span className="text-amber-500">★</span>
                          {game.averageRating.toFixed(1)}
                        </dd>
                      </>
                    )}
                  </dl>
                </CardContent>
              </Card>
            </div>

            {/* Sidebar with image */}
            <div className="space-y-4">
              <Card>
                <CardContent className="p-4">
                  <div className="relative aspect-square w-full overflow-hidden rounded-lg bg-muted">
                    {hasImage && game.imageUrl ? (
                      <Image
                        src={game.imageUrl}
                        alt={game.title}
                        fill
                        className="object-cover"
                        sizes="(max-width: 768px) 100vw, 33vw"
                      />
                    ) : (
                      <div className="flex h-full items-center justify-center">
                        <FileText className="h-16 w-16 text-muted-foreground/50" />
                      </div>
                    )}
                  </div>
                </CardContent>
              </Card>

              {/* Metadata */}
              <Card>
                <CardHeader>
                  <CardTitle className="text-sm">Metadata</CardTitle>
                </CardHeader>
                <CardContent className="text-sm space-y-2">
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Created</span>
                    <span>{new Date(game.createdAt).toLocaleDateString()}</span>
                  </div>
                  {game.modifiedAt && (
                    <div className="flex justify-between">
                      <span className="text-muted-foreground">Modified</span>
                      <span>{new Date(game.modifiedAt).toLocaleDateString()}</span>
                    </div>
                  )}
                  {game.createdBy && (
                    <div className="flex justify-between">
                      <span className="text-muted-foreground">Created By</span>
                      <span className="truncate max-w-[120px]" title={game.createdBy}>{game.createdBy}</span>
                    </div>
                  )}
                </CardContent>
              </Card>
            </div>
          </div>
        </TabsContent>

        {/* Documents Tab */}
        <TabsContent value="documents" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>Upload Document</CardTitle>
              <CardDescription>
                Upload PDF rulebooks and reference materials for this game.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <PdfUploadSection gameId={gameId} onUploadSuccess={refetchDocuments} />
            </CardContent>
          </Card>

          <Card>
            <CardHeader>
              <CardTitle>Documents</CardTitle>
              <CardDescription>
                {documents?.length
                  ? `${documents.length} document${documents.length === 1 ? '' : 's'} uploaded`
                  : 'No documents uploaded yet'}
              </CardDescription>
            </CardHeader>
            <CardContent>
              {documents && documents.length > 0 ? (
                <div className="space-y-3">
                  {documents.map((doc) => (
                    <DocumentListItem
                      key={doc.id}
                      document={doc}
                      onDelete={(id) => deleteDocumentMutation.mutate(id)}
                      onDownload={handleDownload}
                      isDeleting={deleteDocumentMutation.isPending}
                    />
                  ))}
                </div>
              ) : (
                <div className="text-center py-8 text-muted-foreground">
                  <FileText className="h-12 w-12 mx-auto mb-2 opacity-50" />
                  <p>No documents uploaded yet.</p>
                  <p className="text-sm">Upload a PDF to get started.</p>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* Review History Tab */}
        <TabsContent value="history" className="space-y-6">
          <Card>
            <CardHeader>
              <CardTitle>Review History</CardTitle>
              <CardDescription>
                Track all status changes and review actions for this game.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {/* Placeholder - would need actual history data from API */}
              <div className="space-y-0">
                <ReviewHistoryItem
                  action="submitted"
                  user="System"
                  timestamp={game.createdAt}
                  notes="Game imported from BoardGameGeek"
                />
                {game.status === 'Published' && game.modifiedAt && (
                  <ReviewHistoryItem
                    action="approved"
                    user="Admin"
                    timestamp={game.modifiedAt}
                  />
                )}
              </div>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
