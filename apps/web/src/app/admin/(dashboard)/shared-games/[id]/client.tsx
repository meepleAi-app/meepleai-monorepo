/**
 * Game Detail Client Component - New Admin Dashboard
 *
 * Route: /admin/shared-games/[id]
 * Three tabs: Details (game info + image), Documents (PDF upload + list), Agent (KB linking).
 */

'use client';

import { use, useCallback, useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertCircle,
  ArrowLeft,
  Bot,
  CheckCircle2,
  Download,
  ExternalLink,
  FileText,
  Link2,
  MoreHorizontal,
  Settings2,
  Trash2,
  Unlink,
} from 'lucide-react';
import Image from 'next/image';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { PdfIndexingStatus } from '@/components/admin/shared-games/PdfIndexingStatus';
import { PdfUploadSection } from '@/components/admin/shared-games/PdfUploadSection';
import { Badge } from '@/components/ui/data-display/badge';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { api, type SharedGameDocument } from '@/lib/api';
import { getAgentDefinitions } from '@/lib/api/admin-agent-client';

// ─── Sub-components ──────────────────────────────────────────────────────────

function StatusBadge({ status }: { status: string }) {
  const map: Record<
    string,
    { variant: 'default' | 'secondary' | 'destructive' | 'outline'; label: string }
  > = {
    Draft: { variant: 'secondary', label: 'Draft' },
    Published: { variant: 'default', label: 'Published' },
    Archived: { variant: 'outline', label: 'Archived' },
    PendingApproval: { variant: 'destructive', label: 'Pending Approval' },
  };
  const config = map[status] ?? { variant: 'secondary' as const, label: status };
  return <Badge variant={config.variant}>{config.label}</Badge>;
}

function DocTypeBadge({ documentType }: { documentType: number }) {
  const types: Record<number, { variant: 'default' | 'secondary' | 'outline'; label: string }> = {
    0: { variant: 'default', label: 'Rulebook' },
    1: { variant: 'secondary', label: 'Errata' },
    2: { variant: 'outline', label: 'Homerule' },
  };
  const config = types[documentType] ?? { variant: 'secondary' as const, label: 'Unknown' };
  return <Badge variant={config.variant}>{config.label}</Badge>;
}

function DocumentItem({
  document,
  onDelete,
  isDeleting,
}: {
  document: SharedGameDocument;
  onDelete: (id: string) => void;
  isDeleting: boolean;
}) {
  const handleDownload = useCallback(() => {
    // Download is not yet wired to a signed URL - placeholder
  }, []);

  return (
    <div className="rounded-lg border bg-white/60 dark:bg-zinc-800/60 p-4 space-y-3">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-muted">
            <FileText className="h-5 w-5 text-muted-foreground" />
          </div>
          <div>
            <div className="flex items-center gap-2">
              <DocTypeBadge documentType={document.documentType} />
              <span className="text-sm text-muted-foreground">v{document.version}</span>
              {document.isActive && (
                <Badge variant="outline" className="text-xs">
                  Active
                </Badge>
              )}
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              {new Date(document.createdAt).toLocaleDateString()}
            </p>
          </div>
        </div>

        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon" disabled={isDeleting}>
              <MoreHorizontal className="h-4 w-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={handleDownload}>
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

      {/* Inline RAG indexing status */}
      <PdfIndexingStatus pdfId={document.id} compact={true} />
    </div>
  );
}

// ─── Main Component ───────────────────────────────────────────────────────────

interface GameDetailClientProps {
  params: Promise<{ id: string }>;
}

export function GameDetailClient({ params }: GameDetailClientProps) {
  const { id: gameId } = use(params);
  const router = useRouter();
  const queryClient = useQueryClient();

  const {
    data: game,
    isLoading,
    error,
  } = useQuery({
    queryKey: ['admin', 'shared-games', gameId],
    queryFn: () => api.sharedGames.getById(gameId),
  });

  const { data: documents, refetch: refetchDocuments } = useQuery({
    queryKey: ['admin', 'shared-games', gameId, 'documents'],
    queryFn: () => api.sharedGames.getDocuments(gameId),
    enabled: !!game,
  });

  const deleteDocMutation = useMutation({
    mutationFn: (documentId: string) => api.sharedGames.removeDocument(gameId, documentId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'shared-games', gameId, 'documents'] });
    },
  });

  // ── Agent linking state ──────────────────────────────────────────────────
  const [selectedAgentId, setSelectedAgentId] = useState('');

  const { data: linkedAgent, isLoading: linkedAgentLoading } = useQuery({
    queryKey: ['admin', 'shared-games', gameId, 'linked-agent'],
    queryFn: () => api.sharedGames.getLinkedAgent(gameId),
    enabled: !!game,
  });

  const { data: kbCards } = useQuery({
    queryKey: ['admin', 'shared-games', gameId, 'kb-cards'],
    queryFn: () => api.sharedGames.getKbCards(gameId),
    enabled: !!game,
  });

  const { data: agentDefinitions, isLoading: agentsLoading } = useQuery({
    queryKey: ['admin', 'agent-definitions', 'list'],
    queryFn: () => getAgentDefinitions({ activeOnly: true }),
    enabled: !!game,
  });

  const linkAgentMutation = useMutation({
    mutationFn: (agentId: string) => api.sharedGames.linkAgent(gameId, agentId),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ['admin', 'shared-games', gameId, 'linked-agent'],
      });
      setSelectedAgentId('');
    },
  });

  const unlinkAgentMutation = useMutation({
    mutationFn: () => api.sharedGames.unlinkAgent(gameId),
    onSuccess: () => {
      queryClient.invalidateQueries({
        queryKey: ['admin', 'shared-games', gameId, 'linked-agent'],
      });
    },
  });

  const kbCompletedCount = kbCards?.filter(k => k.indexingStatus === 'Completed').length ?? 0;
  const kbTotalCount = kbCards?.length ?? 0;

  if (isLoading) {
    return (
      <div className="space-y-6">
        <div className="flex items-center gap-4">
          <Skeleton className="h-9 w-9 rounded-md" />
          <div className="space-y-2">
            <Skeleton className="h-8 w-64" />
            <Skeleton className="h-4 w-32" />
          </div>
        </div>
        <Skeleton className="h-10 w-80" />
        <Skeleton className="h-48 w-full rounded-xl" />
      </div>
    );
  }

  if (error || !game) {
    return (
      <div className="space-y-4">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            {error instanceof Error ? error.message : 'Failed to load game details.'}
          </AlertDescription>
        </Alert>
        <Button variant="outline" onClick={() => router.push('/admin/shared-games/all')}>
          <ArrowLeft className="mr-2 h-4 w-4" />
          Back to All Games
        </Button>
      </div>
    );
  }

  const hasImage = !!game.imageUrl;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start gap-4">
        <Button
          variant="ghost"
          size="icon"
          onClick={() => router.push('/admin/shared-games/all')}
          className="mt-1 shrink-0"
        >
          <ArrowLeft className="h-5 w-5" />
        </Button>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-3 flex-wrap">
            <h1 className="font-quicksand text-2xl font-bold tracking-tight truncate">
              {game.title}
            </h1>
            <StatusBadge status={game.status} />
          </div>
          <div className="flex items-center gap-4 mt-1 text-sm text-muted-foreground">
            <span>{game.yearPublished}</span>
            {game.bggId && (
              <a
                href={`https://boardgamegeek.com/boardgame/${game.bggId}`}
                target="_blank"
                rel="noopener noreferrer"
                className="flex items-center gap-1 hover:text-foreground transition-colors"
              >
                BGG #{game.bggId}
                <ExternalLink className="h-3 w-3" />
              </a>
            )}
          </div>
        </div>

        <div className="flex items-center gap-2">
          <Link href={`/admin/shared-games/${gameId}/rag-setup`}>
            <Button variant="outline" size="sm" className="gap-2">
              <Settings2 className="h-4 w-4" />
              RAG Setup
            </Button>
          </Link>
          <Button variant="outline" size="sm" disabled title="Edit functionality coming soon">
            Edit Game
          </Button>
        </div>
      </div>

      {/* Tabs */}
      <Tabs defaultValue="details">
        <TabsList className="bg-white/60 dark:bg-zinc-800/60">
          <TabsTrigger value="details">Details</TabsTrigger>
          <TabsTrigger value="documents">
            Documents{documents && documents.length > 0 ? ` (${documents.length})` : ''}
          </TabsTrigger>
          <TabsTrigger value="agent">Agent{linkedAgent ? ' ✓' : ''}</TabsTrigger>
        </TabsList>

        {/* ── Details Tab ─────────────────────────────────────────────────── */}
        <TabsContent value="details" className="space-y-6 mt-6">
          <div className="grid gap-6 md:grid-cols-3">
            {/* Left: description + info */}
            <div className="md:col-span-2 space-y-4">
              {/* Description */}
              <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/50 dark:border-zinc-700/50">
                <CardHeader>
                  <CardTitle className="font-quicksand">Description</CardTitle>
                </CardHeader>
                <CardContent>
                  <p className="text-muted-foreground whitespace-pre-wrap">
                    {game.description || 'No description available.'}
                  </p>
                </CardContent>
              </Card>

              {/* Game Info */}
              <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/50 dark:border-zinc-700/50">
                <CardHeader>
                  <CardTitle className="font-quicksand">Game Information</CardTitle>
                </CardHeader>
                <CardContent>
                  <dl className="grid grid-cols-2 gap-x-6 gap-y-3 text-sm">
                    <dt className="text-muted-foreground">Players</dt>
                    <dd className="font-medium">
                      {game.minPlayers === game.maxPlayers
                        ? game.minPlayers
                        : `${game.minPlayers}–${game.maxPlayers}`}
                    </dd>

                    <dt className="text-muted-foreground">Playing Time</dt>
                    <dd className="font-medium">{game.playingTimeMinutes} min</dd>

                    <dt className="text-muted-foreground">Min Age</dt>
                    <dd className="font-medium">{game.minAge}+</dd>

                    {game.averageRating && (
                      <>
                        <dt className="text-muted-foreground">BGG Rating</dt>
                        <dd className="font-medium flex items-center gap-1">
                          <span className="text-amber-500">★</span>
                          {game.averageRating.toFixed(1)}
                        </dd>
                      </>
                    )}

                    {game.complexityRating && (
                      <>
                        <dt className="text-muted-foreground">Complexity</dt>
                        <dd className="font-medium">{game.complexityRating.toFixed(1)} / 5</dd>
                      </>
                    )}

                    {game.publishers && game.publishers.length > 0 && (
                      <>
                        <dt className="text-muted-foreground">Publisher</dt>
                        <dd className="font-medium">
                          {game.publishers.map(p => p.name).join(', ')}
                        </dd>
                      </>
                    )}

                    {game.designers && game.designers.length > 0 && (
                      <>
                        <dt className="text-muted-foreground">Designer</dt>
                        <dd className="font-medium">
                          {game.designers.map(d => d.name).join(', ')}
                        </dd>
                      </>
                    )}
                  </dl>
                </CardContent>
              </Card>
            </div>

            {/* Right: image + metadata */}
            <div className="space-y-4">
              <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/50 dark:border-zinc-700/50">
                <CardContent className="p-4">
                  <div className="relative aspect-square w-full overflow-hidden rounded-lg bg-muted">
                    {hasImage ? (
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

              <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/50 dark:border-zinc-700/50">
                <CardHeader>
                  <CardTitle className="text-sm font-quicksand">Metadata</CardTitle>
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
                </CardContent>
              </Card>
            </div>
          </div>
        </TabsContent>

        {/* ── Agent Tab ───────────────────────────────────────────────────── */}
        <TabsContent value="agent" className="space-y-6 mt-6">
          {/* Current linked agent */}
          <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/50 dark:border-zinc-700/50">
            <CardHeader>
              <CardTitle className="font-quicksand flex items-center gap-2">
                <Bot className="h-5 w-5" />
                Linked Agent
              </CardTitle>
              <CardDescription>
                One AI agent definition can be linked to this game to power its chat experience.
              </CardDescription>
            </CardHeader>
            <CardContent>
              {linkedAgentLoading ? (
                <div className="space-y-2">
                  <Skeleton className="h-5 w-48" />
                  <Skeleton className="h-4 w-32" />
                </div>
              ) : linkedAgent ? (
                <div className="flex items-start justify-between gap-4">
                  <div className="flex items-start gap-3">
                    <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary/10 shrink-0">
                      <Bot className="h-5 w-5 text-primary" />
                    </div>
                    <div>
                      <p className="font-medium text-sm">{linkedAgent.name}</p>
                      <p className="text-xs text-muted-foreground mt-0.5">{linkedAgent.type}</p>
                      {linkedAgent.strategyName && (
                        <Badge variant="outline" className="mt-1.5 text-xs">
                          {linkedAgent.strategyName}
                        </Badge>
                      )}
                    </div>
                  </div>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => unlinkAgentMutation.mutate()}
                    disabled={unlinkAgentMutation.isPending}
                    className="text-destructive hover:text-destructive shrink-0"
                  >
                    <Unlink className="mr-1.5 h-3.5 w-3.5" />
                    {unlinkAgentMutation.isPending ? 'Unlinking…' : 'Unlink'}
                  </Button>
                </div>
              ) : (
                <div className="text-center py-8 text-muted-foreground">
                  <Bot className="h-10 w-10 mx-auto mb-2 opacity-40" />
                  <p className="text-sm font-medium">No agent linked</p>
                  <p className="text-xs mt-1">
                    Select an agent below to enable AI chat for this game.
                  </p>
                  <Link
                    href="/admin/agents/definitions/create"
                    className="text-xs text-primary underline underline-offset-2 mt-2 inline-block hover:text-primary/80"
                  >
                    Create a new agent →
                  </Link>
                </div>
              )}

              {unlinkAgentMutation.isError && (
                <Alert variant="destructive" className="mt-3">
                  <AlertCircle className="h-4 w-4" />
                  <AlertDescription>
                    {unlinkAgentMutation.error instanceof Error
                      ? unlinkAgentMutation.error.message
                      : 'Failed to unlink agent.'}
                  </AlertDescription>
                </Alert>
              )}
            </CardContent>
          </Card>

          {/* Link new agent — only shown when no agent is linked */}
          {!linkedAgent && !linkedAgentLoading && (
            <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/50 dark:border-zinc-700/50">
              <CardHeader>
                <CardTitle className="font-quicksand text-base">Link an Agent</CardTitle>
                <CardDescription>
                  Choose an active agent definition to connect to this game.
                </CardDescription>
              </CardHeader>
              <CardContent>
                <div className="flex items-center gap-3">
                  <Select
                    value={selectedAgentId}
                    onValueChange={setSelectedAgentId}
                    disabled={agentsLoading || linkAgentMutation.isPending}
                  >
                    <SelectTrigger className="flex-1 h-9 text-sm">
                      <SelectValue
                        placeholder={agentsLoading ? 'Loading agents…' : 'Select agent definition'}
                      />
                    </SelectTrigger>
                    <SelectContent>
                      {agentDefinitions?.map(agent => (
                        <SelectItem key={agent.id} value={agent.id} className="text-sm">
                          <span className="font-medium">{agent.name}</span>
                          <span className="text-muted-foreground ml-2 text-xs">{agent.type}</span>
                        </SelectItem>
                      ))}
                      {agentDefinitions?.length === 0 && (
                        <div className="px-3 py-2 text-sm text-muted-foreground">
                          No active agents found.
                        </div>
                      )}
                    </SelectContent>
                  </Select>
                  <Button
                    size="sm"
                    onClick={() => linkAgentMutation.mutate(selectedAgentId)}
                    disabled={!selectedAgentId || linkAgentMutation.isPending}
                  >
                    <Link2 className="mr-1.5 h-3.5 w-3.5" />
                    {linkAgentMutation.isPending ? 'Linking…' : 'Link'}
                  </Button>
                </div>

                {linkAgentMutation.isError && (
                  <Alert variant="destructive" className="mt-3">
                    <AlertCircle className="h-4 w-4" />
                    <AlertDescription>
                      {linkAgentMutation.error instanceof Error
                        ? linkAgentMutation.error.message
                        : 'Failed to link agent.'}
                    </AlertDescription>
                  </Alert>
                )}
              </CardContent>
            </Card>
          )}

          {/* KB Cards status */}
          <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/50 dark:border-zinc-700/50">
            <CardHeader>
              <CardTitle className="font-quicksand text-base flex items-center gap-2">
                Knowledge Base Status
                {kbTotalCount > 0 && (
                  <Badge variant={kbCompletedCount === kbTotalCount ? 'default' : 'secondary'}>
                    {kbCompletedCount}/{kbTotalCount} indexed
                  </Badge>
                )}
              </CardTitle>
              <CardDescription>Documents indexed and available for RAG retrieval.</CardDescription>
            </CardHeader>
            <CardContent>
              {kbTotalCount === 0 ? (
                <div className="text-center py-6 text-muted-foreground">
                  <FileText className="h-8 w-8 mx-auto mb-2 opacity-40" />
                  <p className="text-sm">
                    No KB cards yet. Upload and process documents in the Documents tab.
                  </p>
                </div>
              ) : (
                <div className="space-y-2">
                  {kbCards?.map(card => (
                    <div
                      key={card.id}
                      className="flex items-center justify-between rounded-lg border px-3 py-2 text-sm"
                    >
                      <div className="flex items-center gap-2 min-w-0">
                        <FileText className="h-4 w-4 text-muted-foreground shrink-0" />
                        <span className="truncate text-xs">{card.fileName}</span>
                      </div>
                      <div className="flex items-center gap-2 shrink-0 ml-2">
                        {card.indexingStatus === 'Completed' && (
                          <CheckCircle2 className="h-3.5 w-3.5 text-green-500" />
                        )}
                        <Badge
                          variant={card.indexingStatus === 'Completed' ? 'default' : 'secondary'}
                          className="text-xs"
                        >
                          {card.indexingStatus}
                        </Badge>
                        {card.chunkCount > 0 && (
                          <span className="text-xs text-muted-foreground">
                            {card.chunkCount} chunks
                          </span>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>

        {/* ── Documents Tab ───────────────────────────────────────────────── */}
        <TabsContent value="documents" className="space-y-6 mt-6">
          {/* Link to RAG Setup dashboard (#256: deprecate inline wizard) */}
          <div className="flex justify-end">
            <Link href={`/admin/shared-games/${gameId}/rag-setup`}>
              <Button size="sm">
                <Settings2 className="mr-1.5 h-3.5 w-3.5" />
                RAG Setup Dashboard
              </Button>
            </Link>
          </div>

          {/* Upload */}
          <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/50 dark:border-zinc-700/50">
            <CardHeader>
              <CardTitle className="font-quicksand">Upload Document</CardTitle>
              <CardDescription>
                Upload PDF rulebooks and reference materials. They will be indexed for RAG search.
              </CardDescription>
            </CardHeader>
            <CardContent>
              <PdfUploadSection gameId={gameId} onPdfUploaded={() => refetchDocuments()} />
            </CardContent>
          </Card>

          {/* Document list */}
          <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/50 dark:border-zinc-700/50">
            <CardHeader>
              <CardTitle className="font-quicksand">Documents</CardTitle>
              <CardDescription>
                {documents?.length
                  ? `${documents.length} document${documents.length === 1 ? '' : 's'} linked to this game`
                  : 'No documents uploaded yet'}
              </CardDescription>
            </CardHeader>
            <CardContent>
              {documents && documents.length > 0 ? (
                <div className="space-y-3">
                  {documents.map(doc => (
                    <DocumentItem
                      key={doc.id}
                      document={doc}
                      onDelete={id => deleteDocMutation.mutate(id)}
                      isDeleting={
                        deleteDocMutation.isPending && deleteDocMutation.variables === doc.id
                      }
                    />
                  ))}
                  <div className="pt-2">
                    <Link href={`/admin/knowledge-base/queue?gameId=${gameId}`}>
                      <Button variant="outline" size="sm">
                        View in Processing Queue →
                      </Button>
                    </Link>
                  </div>
                </div>
              ) : (
                <div className="text-center py-10 text-muted-foreground">
                  <FileText className="h-12 w-12 mx-auto mb-3 opacity-40" />
                  <p className="font-medium">No documents yet.</p>
                  <p className="text-sm mt-1">Upload a PDF above to enable RAG search.</p>
                </div>
              )}
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>

    </div>
  );
}
