'use client';

/**
 * ExtraMeepleCardDrawer — Sheet wrapper for entity detail cards
 * Issue #5024 - ExtraMeepleCard Drawer System (Epic #5023)
 *
 * A Sheet (drawer) that renders the appropriate ExtraMeepleCard variant
 * based on entity type. Each entity content component handles its own
 * data fetching by entityId.
 *
 * Routing:
 *   game   → GameDrawerContent    (GameExtraMeepleCard)
 *   agent  → AgentDrawerContent   (AgentExtraMeepleCard — Issue #5026)
 *   chat   → ChatDrawerContent    (ChatExtraMeepleCard — Issue #5027)
 *   kb     → KbDrawerContent      (KbExtraMeepleCard — Issue #5028)
 */

import React from 'react';
import {
  AlertCircle,
  Bot,
  FileText,
  Gamepad2,
  MessageCircle,
  RefreshCw,
  Wrench,
  X,
} from 'lucide-react';

import { cn } from '@/lib/utils';
import {
  Sheet,
  SheetClose,
  SheetContent,
  SheetTitle,
} from '@/components/ui/navigation/sheet';
import { DRAWER_TEST_IDS } from './drawer-test-ids';
import { GameExtraMeepleCard, AgentExtraMeepleCard, ChatExtraMeepleCard, KbExtraMeepleCard } from './EntityExtraMeepleCard';
import type { GameDetailData, AgentDetailData, ChatDetailData, ChatDetailMessage, KbDetailData, KbDocumentPreview, GameAgentPreview } from './types';

// ============================================================================
// Types
// ============================================================================

export type DrawerEntityType = 'game' | 'agent' | 'chat' | 'kb';

export interface ExtraMeepleCardDrawerProps {
  /** Type of entity to display */
  entityType: DrawerEntityType;
  /** ID of the entity to load */
  entityId: string;
  /** Controls drawer visibility */
  open: boolean;
  /** Called when the drawer requests to close */
  onClose: () => void;
  className?: string;
  'data-testid'?: string;
}

// ============================================================================
// Entity Configuration
// ============================================================================

const ENTITY_CONFIG: Record<
  DrawerEntityType,
  {
    label: string;
    /** HSL color string (without hsl()) */
    color: string;
    Icon: React.ComponentType<{ className?: string }>;
  }
> = {
  game:  { label: 'Dettaglio Gioco',   color: '25 95% 45%',   Icon: Gamepad2      },
  agent: { label: 'Dettaglio Agente',  color: '220 70% 55%',  Icon: Bot           },
  chat:  { label: 'Dettaglio Chat',    color: '262 83% 58%',  Icon: MessageCircle },
  kb:    { label: 'Documento KB',      color: '174 60% 40%',  Icon: FileText      },
};

// ============================================================================
// ExtraMeepleCardDrawer
// ============================================================================

export const ExtraMeepleCardDrawer = React.memo(function ExtraMeepleCardDrawer({
  entityType,
  entityId,
  open,
  onClose,
  className,
  'data-testid': testId,
}: ExtraMeepleCardDrawerProps) {
  const config = ENTITY_CONFIG[entityType];
  const { Icon } = config;

  return (
    <Sheet open={open} onOpenChange={(isOpen) => { if (!isOpen) onClose(); }}>
      <SheetContent
        side="right"
        className={cn(
          // Layout overrides: remove default padding, set full-height flex column
          'flex flex-col p-0',
          // Width override for wide drawer
          'w-full sm:w-[600px] sm:max-w-[600px]',
          // Hide the built-in close button (we render our own in the colored header)
          '[&>button:first-child]:hidden',
          className,
        )}
        data-testid={testId}
      >
        {/* Screen-reader title (required by Radix Dialog for accessibility) */}
        <SheetTitle className="sr-only">
          {config.label}
        </SheetTitle>

        {/* Entity-colored header */}
        <div
          className="relative flex h-14 shrink-0 items-center gap-2.5 px-5"
          style={{ backgroundColor: `hsl(${config.color})` }}
        >
          <Icon className="h-4.5 w-4.5 text-white" aria-hidden="true" />
          <h2
            className="font-quicksand text-base font-bold text-white"
            data-testid={DRAWER_TEST_IDS.ENTITY_LABEL}
          >
            {config.label}
          </h2>

          {/* Close button */}
          <SheetClose
            className={cn(
              'absolute right-4 top-1/2 -translate-y-1/2',
              'flex h-7 w-7 items-center justify-center rounded-full',
              'bg-white/20 text-white transition-colors duration-150',
              'hover:bg-white/30 focus:outline-none focus:ring-2 focus:ring-white/50',
            )}
            aria-label="Chiudi pannello"
          >
            <X className="h-3.5 w-3.5" />
          </SheetClose>
        </div>

        {/* Scrollable entity content */}
        <div className="flex flex-1 flex-col overflow-y-auto">
          <DrawerEntityRouter entityType={entityType} entityId={entityId} />
        </div>
      </SheetContent>
    </Sheet>
  );
});

// ============================================================================
// Entity Router
// ============================================================================

function DrawerEntityRouter({
  entityType,
  entityId,
}: {
  entityType: DrawerEntityType;
  entityId: string;
}) {
  switch (entityType) {
    case 'game':
      return <GameDrawerContent entityId={entityId} />;
    case 'agent':
      return <AgentDrawerContent entityId={entityId} />;
    case 'chat':
      return <ChatDrawerContent entityId={entityId} />;
    case 'kb':
      return <KbDrawerContent entityId={entityId} />;
    default:
      return <DrawerErrorState error="Tipo entità non supportato" />;
  }
}

// ============================================================================
// Game Drawer Content
// Fetches game data by ID and renders GameExtraMeepleCard.
// ============================================================================

function GameDrawerContent({ entityId }: { entityId: string }) {
  const { data, loading, error, retry } = useGameDetail(entityId);

  if (loading) return <DrawerLoadingSkeleton />;
  if (error)   return <DrawerErrorState error={error} onRetry={retry} />;
  if (!data)   return <DrawerLoadingSkeleton />;

  return (
    <GameExtraMeepleCard
      data={data}
      className="w-full rounded-none border-0 shadow-none bg-transparent"
    />
  );
}

// ============================================================================
// Agent Drawer Content
// Fetches agent data by ID and renders AgentExtraMeepleCard.
// ============================================================================

function AgentDrawerContent({ entityId }: { entityId: string }) {
  const { data, loading, error, retry } = useAgentDetail(entityId);

  if (loading) return <DrawerLoadingSkeleton />;
  if (error)   return <DrawerErrorState error={error} onRetry={retry} />;
  if (!data)   return <DrawerLoadingSkeleton />;

  return (
    <AgentExtraMeepleCard
      data={data}
      className="w-full rounded-none border-0 shadow-none bg-transparent"
    />
  );
}

function ChatDrawerContent({ entityId }: { entityId: string }) {
  const { data, loading, error, retry } = useChatDetail(entityId);

  if (loading) return <DrawerLoadingSkeleton />;
  if (error)   return <DrawerErrorState error={error} onRetry={retry} />;
  if (!data)   return <DrawerLoadingSkeleton />;

  return (
    <ChatExtraMeepleCard
      data={data}
      className="w-full rounded-none border-0 shadow-none bg-transparent"
    />
  );
}

function KbDrawerContent({ entityId }: { entityId: string }) {
  const { data, loading, error, retry } = useKbDetail(entityId);

  if (loading) return <DrawerLoadingSkeleton />;
  if (error)   return <DrawerErrorState error={error} onRetry={retry} />;
  if (!data)   return <DrawerLoadingSkeleton />;

  // v1: delete and retry are omitted in the drawer (read-only preview);
  // consumers needing these actions should use KbExtraMeepleCard standalone.
  return (
    <KbExtraMeepleCard
      data={data}
      className="w-full rounded-none border-0 shadow-none bg-transparent"
    />
  );
}

// ============================================================================
// Shared UI States
// ============================================================================

/** Animated skeleton shown while entity content loads */
export function DrawerLoadingSkeleton({
  'data-testid': testId,
}: {
  'data-testid'?: string;
}) {
  return (
    <div
      className="flex flex-col gap-3 p-5"
      data-testid={testId ?? DRAWER_TEST_IDS.LOADING_SKELETON}
      aria-busy="true"
      aria-label="Caricamento in corso"
    >
      {/* Fake hero image */}
      <div className="h-[140px] w-full animate-pulse rounded-xl bg-slate-200" />
      {/* Fake tab strip */}
      <div className="flex gap-2">
        {[80, 110, 70].map((w) => (
          <div
            key={w}
            className="h-8 animate-pulse rounded-lg bg-slate-200"
            style={{ width: w }}
          />
        ))}
      </div>
      {/* Fake stat cards */}
      <div className="grid grid-cols-3 gap-2">
        {[1, 2, 3].map((i) => (
          <div key={i} className="h-16 animate-pulse rounded-lg bg-slate-200" />
        ))}
      </div>
      {/* Fake text block */}
      <div className="h-20 w-full animate-pulse rounded-lg bg-slate-200" />
    </div>
  );
}

/** Error state with optional retry button */
export function DrawerErrorState({
  error,
  onRetry,
  'data-testid': testId,
}: {
  error: string;
  onRetry?: () => void;
  'data-testid'?: string;
}) {
  return (
    <div
      className="flex flex-col items-center justify-center gap-4 p-8 text-center"
      data-testid={testId ?? DRAWER_TEST_IDS.ERROR_STATE}
      role="alert"
    >
      <AlertCircle className="h-10 w-10 text-red-400" aria-hidden="true" />
      <div className="space-y-1">
        <p className="font-quicksand text-sm font-semibold text-slate-700">
          Si è verificato un errore
        </p>
        <p className="font-nunito text-xs text-slate-500">{error}</p>
      </div>
      {onRetry && (
        <button
          onClick={onRetry}
          className={cn(
            'flex items-center gap-1.5 rounded-lg',
            'bg-slate-100 px-3 py-1.5',
            'font-nunito text-xs font-medium text-slate-700',
            'transition-colors duration-150 hover:bg-slate-200',
            'focus:outline-none focus:ring-2 focus:ring-slate-400 focus:ring-offset-1',
          )}
        >
          <RefreshCw className="h-3.5 w-3.5" aria-hidden="true" />
          Riprova
        </button>
      )}
    </div>
  );
}

/** Placeholder shown for entity types not yet implemented */
function DrawerComingSoon({
  label,
  issueNumber,
}: {
  label: string;
  issueNumber: number;
}) {
  return (
    <div
      className="flex flex-col items-center justify-center gap-3 p-8 text-center"
      data-testid={DRAWER_TEST_IDS.COMING_SOON(issueNumber)}
    >
      <div className="flex h-12 w-12 items-center justify-center rounded-full bg-slate-100">
        <Wrench className="h-5 w-5 text-slate-400" aria-hidden="true" />
      </div>
      <div className="space-y-1">
        <p className="font-quicksand text-sm font-semibold text-slate-600">
          {label}
        </p>
        <p className="font-nunito text-xs text-slate-400">
          In arrivo — Issue #{issueNumber}
        </p>
      </div>
    </div>
  );
}

// ============================================================================
// Data Fetching Hook — Game
// ============================================================================

interface UseGameDetailResult {
  data: GameDetailData | null;
  loading: boolean;
  error: string | null;
  retry: () => void;
}

function useGameDetail(gameId: string): UseGameDetailResult {
  const [data, setData] = React.useState<GameDetailData | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  const fetchData = React.useCallback(async (signal: AbortSignal) => {
    setLoading(true);
    setError(null);
    try {
      // Fetch game details, PDF list, and agent config in parallel (Issue #5029).
      // KB: /pdfs returns List<GamePdfDto> (bare array). Agent: /agent-config returns
      // AgentConfigDto? (null when not configured). Both handled gracefully on non-200.
      const [gameRes, kbRes, agentRes] = await Promise.all([
        fetch(`/api/v1/library/games/${gameId}`, { signal }),
        fetch(`/api/v1/library/games/${gameId}/pdfs`, { signal }),
        fetch(`/api/v1/library/games/${gameId}/agent-config`, { signal }),
      ]);

      if (!gameRes.ok) {
        throw new Error(`Errore ${gameRes.status}: gioco non trovato`);
      }

      const json = (await gameRes.json()) as Record<string, unknown>;
      const kbRawJson = kbRes.ok ? (await kbRes.json()) as unknown : [];
      const agentJson = agentRes.ok ? (await agentRes.json()) as Record<string, unknown> : null;

      setData(mapToGameDetailData(
        json,
        Array.isArray(kbRawJson) ? kbRawJson : [],
        agentJson,
      ));
    } catch (err) {
      if (err instanceof Error && err.name === 'AbortError') return;
      setError(
        err instanceof Error
          ? err.message
          : 'Impossibile caricare i dati del gioco',
      );
    } finally {
      setLoading(false);
    }
  }, [gameId]);

  React.useEffect(() => {
    if (!gameId) return;
    const controller = new AbortController();
    void fetchData(controller.signal);
    return () => { controller.abort(); };
  }, [gameId, fetchData]);

  // retry creates a fresh controller each time
  const retry = React.useCallback(() => {
    const controller = new AbortController();
    void fetchData(controller.signal);
  }, [fetchData]);

  return { data, loading, error, retry };
}

function mapToGameDetailData(
  json: Record<string, unknown>,
  kbRaw: unknown[] = [],
  agentRaw: Record<string, unknown> | null = null,
): GameDetailData {
  // GamePdfDto shape: { id, name, pageCount, fileSizeBytes, uploadedAt, source, language }
  // PDFs returned by /pdfs are already indexed/available — no processingStatus field.
  const kbDocuments: KbDocumentPreview[] = kbRaw.map((item) => {
    const d = item as Record<string, unknown>;
    return {
      id:         String(d.id ?? ''),
      fileName:   String(d.name ?? 'Documento'),
      uploadedAt: String(d.uploadedAt ?? new Date().toISOString()),
      status:     'indexed' as const,
    };
  });

  // AgentConfigDto shape: { llmModel, temperature, maxTokens, personality, detailLevel, personalNotes }
  // No identity fields (id/name/isActive) — use placeholder values when config exists.
  const agent: GameAgentPreview | undefined = agentRaw != null ? {
    id:       '',
    name:     'Agente AI',
    model:    agentRaw.llmModel != null ? String(agentRaw.llmModel) : undefined,
    isActive: true,
  } : undefined;

  return {
    id:                 String(json.id ?? ''),
    title:              String(json.title ?? json.name ?? ''),
    imageUrl:           json.imageUrl          != null ? String(json.imageUrl)          : undefined,
    publisher:          json.publisher         != null ? String(json.publisher)         : undefined,
    yearPublished:      json.yearPublished      != null ? Number(json.yearPublished)     : undefined,
    minPlayers:         json.minPlayers         != null ? Number(json.minPlayers)        : undefined,
    maxPlayers:         json.maxPlayers         != null ? Number(json.maxPlayers)        : undefined,
    playTimeMinutes:    json.playTimeMinutes    != null ? Number(json.playTimeMinutes)   : undefined,
    description:        json.description        != null ? String(json.description)       : undefined,
    averageRating:      json.averageRating      != null ? Number(json.averageRating)     : undefined,
    totalPlays:         json.totalPlays         != null ? Number(json.totalPlays)        : undefined,
    faqCount:           json.faqCount           != null ? Number(json.faqCount)          : undefined,
    rulesDocumentCount: json.rulesDocumentCount != null ? Number(json.rulesDocumentCount): undefined,
    kbDocuments:        kbDocuments.length > 0 ? kbDocuments : undefined,
    agent,
  };
}

// ============================================================================
// Data Fetching Hook — Agent
// ============================================================================

interface UseAgentDetailResult {
  data: AgentDetailData | null;
  loading: boolean;
  error: string | null;
  retry: () => void;
}

function useAgentDetail(agentId: string): UseAgentDetailResult {
  const [data, setData] = React.useState<AgentDetailData | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  const fetchData = React.useCallback(async (signal: AbortSignal) => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch(`/api/v1/agents/${agentId}`, { signal });
      if (!res.ok) {
        throw new Error(`Errore ${res.status}: agente non trovato`);
      }
      const json = (await res.json()) as Record<string, unknown>;
      setData(mapToAgentDetailData(json));
    } catch (err) {
      if (err instanceof Error && err.name === 'AbortError') return;
      setError(
        err instanceof Error
          ? err.message
          : 'Impossibile caricare i dati dell\'agente',
      );
    } finally {
      setLoading(false);
    }
  }, [agentId]);

  React.useEffect(() => {
    if (!agentId) return;
    const controller = new AbortController();
    void fetchData(controller.signal);
    return () => { controller.abort(); };
  }, [agentId, fetchData]);

  const retry = React.useCallback(() => {
    const controller = new AbortController();
    void fetchData(controller.signal);
  }, [fetchData]);

  return { data, loading, error, retry };
}

function mapToAgentDetailData(json: Record<string, unknown>): AgentDetailData {
  const params = (json.strategyParameters != null && typeof json.strategyParameters === 'object')
    ? (json.strategyParameters as Record<string, unknown>)
    : {};
  return {
    id:                 String(json.id ?? ''),
    name:               String(json.name ?? ''),
    type:               String(json.type ?? ''),
    strategyName:       String(json.strategyName ?? ''),
    strategyParameters: params,
    isActive:           Boolean(json.isActive ?? false),
    isIdle:             Boolean(json.isIdle ?? true),
    invocationCount:    json.invocationCount != null ? Number(json.invocationCount) : 0,
    lastInvokedAt:      json.lastInvokedAt   != null ? String(json.lastInvokedAt)  : null,
    createdAt:          String(json.createdAt ?? new Date().toISOString()),
    gameId:             json.gameId   != null ? String(json.gameId)   : undefined,
    gameName:           json.gameName != null ? String(json.gameName) : undefined,
  };
}

// ============================================================================
// Data Fetching Hook — Chat
// ============================================================================

interface UseChatDetailResult {
  data: ChatDetailData | null;
  loading: boolean;
  error: string | null;
  retry: () => void;
}

function useChatDetail(threadId: string): UseChatDetailResult {
  const [data, setData] = React.useState<ChatDetailData | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  const fetchData = React.useCallback(async (signal: AbortSignal) => {
    setLoading(true);
    setError(null);
    try {
      const [threadRes, msgsRes] = await Promise.all([
        fetch(`/api/v1/chat/threads/${threadId}`, { signal }),
        fetch(`/api/v1/chat/threads/${threadId}/messages?limit=10`, { signal }),
      ]);
      if (!threadRes.ok) {
        throw new Error(`Errore ${threadRes.status}: thread non trovato`);
      }
      const thread = (await threadRes.json()) as Record<string, unknown>;
      const msgs = msgsRes.ok ? ((await msgsRes.json()) as unknown[]) : [];
      setData(mapToChatDetailData(thread, msgs));
    } catch (err) {
      if (err instanceof Error && err.name === 'AbortError') return;
      setError(
        err instanceof Error
          ? err.message
          : 'Impossibile caricare i dati della chat',
      );
    } finally {
      setLoading(false);
    }
  }, [threadId]);

  React.useEffect(() => {
    if (!threadId) return;
    const controller = new AbortController();
    void fetchData(controller.signal);
    return () => { controller.abort(); };
  }, [threadId, fetchData]);

  const retry = React.useCallback(() => {
    const controller = new AbortController();
    void fetchData(controller.signal);
  }, [fetchData]);

  return { data, loading, error, retry };
}

function mapToChatDetailData(
  thread: Record<string, unknown>,
  msgs: unknown[],
): ChatDetailData {
  const statusMap: Record<string, ChatDetailData['status']> = {
    active: 'active', waiting: 'waiting', archived: 'archived', closed: 'closed',
  };
  const rawStatus = String(thread.status ?? 'closed').toLowerCase();
  return {
    id:               String(thread.id ?? ''),
    status:           statusMap[rawStatus] ?? 'closed',
    agentId:          thread.agentId          != null ? String(thread.agentId)          : undefined,
    agentName:        thread.agentName        != null ? String(thread.agentName)        : undefined,
    agentModel:       thread.agentModel       != null ? String(thread.agentModel)       : undefined,
    gameId:           thread.gameId           != null ? String(thread.gameId)           : undefined,
    gameName:         thread.gameName         != null ? String(thread.gameName)         : undefined,
    gameThumbnailUrl: thread.gameThumbnailUrl != null ? String(thread.gameThumbnailUrl) : undefined,
    startedAt:        String(thread.startedAt ?? thread.createdAt ?? new Date().toISOString()),
    durationMinutes:  thread.durationMinutes  != null ? Number(thread.durationMinutes)  : undefined,
    messageCount:     thread.messageCount     != null ? Number(thread.messageCount)     : msgs.length,
    messages:         mapChatMessages(msgs),
    temperature:      thread.temperature      != null ? Number(thread.temperature)      : undefined,
    maxTokens:        thread.maxTokens        != null ? Number(thread.maxTokens)        : undefined,
    systemPrompt:     thread.systemPrompt     != null ? String(thread.systemPrompt)     : undefined,
  };
}

function mapChatMessages(json: unknown[]): ChatDetailMessage[] {
  return json.map((raw) => {
    const m = raw as Record<string, unknown>;
    return {
      id:        String(m.id ?? ''),
      role:      m.role === 'user' ? 'user' : 'assistant',
      content:   String(m.content ?? ''),
      createdAt: String(m.createdAt ?? m.timestamp ?? new Date().toISOString()),
    };
  });
}

// ============================================================================
// KB Detail Hook (Issue #5028)
// ============================================================================

interface UseKbDetailResult {
  data: KbDetailData | null;
  loading: boolean;
  error: string | null;
  retry: () => void;
}

function useKbDetail(pdfId: string): UseKbDetailResult {
  const [data, setData] = React.useState<KbDetailData | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<string | null>(null);

  const loadKbDetail = React.useCallback(() => {
    const controller = new AbortController();
    setLoading(true);
    setError(null);

    const load = async () => {
      try {
        const res = await fetch(`/api/v1/pdfs/${pdfId}/text`, {
          signal: controller.signal,
        });
        if (!res.ok) throw new Error(`HTTP ${res.status}`);
        const doc = (await res.json()) as Record<string, unknown>;
        setData(mapToKbDetailData(doc));
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return;
        setError(err instanceof Error ? err.message : 'Errore caricamento documento');
      } finally {
        setLoading(false);
      }
    };

    void load();
    return () => controller.abort();
  }, [pdfId]);

  React.useEffect(() => {
    const cleanup = loadKbDetail();
    return cleanup;
  }, [loadKbDetail]);

  return { data, loading, error, retry: loadKbDetail };
}

function mapToKbDetailData(doc: Record<string, unknown>): KbDetailData {
  const rawStatus = String(doc.processingStatus ?? 'none').toLowerCase();
  const statusMap: Record<string, KbDetailData['status']> = {
    uploaded:   'processing',
    extracting: 'processing',
    indexing:   'processing',
    indexed:    'indexed',
    failed:     'failed',
    none:       'none',
  };
  const status = statusMap[rawStatus] ?? 'none';

  const extractedText = doc.extractedText != null ? String(doc.extractedText) : undefined;
  const MAX_CHARS = 2000; // ~500 words
  const extractedContent = extractedText
    ? extractedText.slice(0, MAX_CHARS)
    : undefined;
  const hasMoreContent = extractedText ? extractedText.length > MAX_CHARS : false;

  return {
    id:               String(doc.id ?? ''),
    fileName:         String(doc.fileName ?? 'Documento'),
    fileSize:         doc.fileSize    != null ? Number(doc.fileSize)    : undefined,
    pageCount:        doc.pageCount   != null ? Number(doc.pageCount)   : undefined,
    characterCount:   doc.characterCount != null ? Number(doc.characterCount) : undefined,
    uploadedAt:       doc.uploadedAt  != null ? String(doc.uploadedAt)  : undefined,
    processedAt:      doc.processedAt != null ? String(doc.processedAt) : undefined,
    status,
    errorMessage:     doc.processingError != null ? String(doc.processingError) : undefined,
    extractedContent,
    hasMoreContent,
  };
}
