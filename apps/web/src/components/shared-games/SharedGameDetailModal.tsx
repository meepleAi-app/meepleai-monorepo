/**
 * SharedGameDetailModal Component (Issue #2373: Phase 4)
 *
 * Enhanced game detail modal with tabs for Rules, FAQ, and Errata.
 * Displays community-curated content from SharedGameCatalog.
 *
 * Tabs:
 * - Overview: Game metadata, description, designers, publishers
 * - Rules: Rich text content display with language indicator
 * - FAQ: Accordion Q&A items sorted by order
 * - Errata: List sorted by published date
 *
 * @see claudedocs/shared-game-catalog-spec.md (Section: Game Detail Modal)
 */

'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';

import { format } from 'date-fns';
import { it } from 'date-fns/locale';
import DOMPurify from 'dompurify';
import {
  BookOpen,
  Calendar,
  ChevronDown,
  Clock,
  ExternalLink,
  FileText,
  HelpCircle,
  Loader2,
  Plus,
  Share2,
  Star,
  Users,
} from 'lucide-react';

import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/data-display/accordion';
import { Badge } from '@/components/ui/data-display/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { api } from '@/lib/api';
import { type SharedGameDetail } from '@/lib/api/schemas/shared-games.schemas';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface SharedGameDetailModalProps {
  /** Game ID to display */
  gameId: string | null;
  /** Whether the modal is open */
  open: boolean;
  /** Callback when modal is closed */
  onClose: () => void;
  /** Callback when user clicks "Add to Collection" */
  onAddToCollection?: (gameId: string) => void;
  /** Callback when user clicks "Share" */
  onShare?: (gameId: string) => void;
}

// ============================================================================
// Component
// ============================================================================

export function SharedGameDetailModal({
  gameId,
  open,
  onClose,
  onAddToCollection,
  onShare,
}: SharedGameDetailModalProps) {
  const [game, setGame] = useState<SharedGameDetail | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState('overview');

  // ============================================================================
  // Fetch Game Details
  // ============================================================================

  useEffect(() => {
    if (!gameId || !open) {
      setGame(null);
      setActiveTab('overview');
      return;
    }

    const fetchGame = async () => {
      setLoading(true);
      setError(null);

      try {
        const data = await api.sharedGames.getById(gameId);
        setGame(data);
      } catch (_err) {
        setError('Impossibile caricare i dettagli del gioco.');
      } finally {
        setLoading(false);
      }
    };

    void fetchGame();
  }, [gameId, open]);

  // ============================================================================
  // Handlers
  // ============================================================================

  const handleAddToCollection = useCallback(() => {
    if (gameId) {
      onAddToCollection?.(gameId);
    }
  }, [gameId, onAddToCollection]);

  const handleShare = useCallback(() => {
    if (gameId) {
      // Copy link to clipboard
      const url = `${window.location.origin}/games/catalog/${gameId}`;
      navigator.clipboard.writeText(url).then(() => {
        onShare?.(gameId);
      });
    }
  }, [gameId, onShare]);

  // ============================================================================
  // Tab Visibility
  // ============================================================================

  const hasRules = game?.rules?.content;
  const hasFaqs = game?.faqs && game.faqs.length > 0;
  const hasErratas = game?.erratas && game.erratas.length > 0;

  // ============================================================================
  // Render
  // ============================================================================

  return (
    <Dialog open={open} onOpenChange={open => !open && onClose()}>
      <DialogContent className="max-w-3xl max-h-[90vh] p-0">
        {/* Loading State */}
        {loading && (
          <div className="flex items-center justify-center h-64">
            <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
          </div>
        )}

        {/* Error State */}
        {error && !loading && (
          <div className="flex flex-col items-center justify-center h-64 p-6 text-center">
            <p className="text-destructive mb-4">{error}</p>
            <Button onClick={onClose} variant="outline">
              Chiudi
            </Button>
          </div>
        )}

        {/* Game Content */}
        {game && !loading && !error && (
          <>
            {/* Header */}
            <DialogHeader className="p-6 pb-0">
              <div className="flex gap-4">
                {/* Thumbnail */}
                <div className="w-24 h-32 rounded-lg overflow-hidden bg-muted flex-shrink-0">
                  {game.imageUrl ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img
                      src={game.imageUrl}
                      alt={game.title}
                      className="w-full h-full object-cover"
                    />
                  ) : (
                    <div className="w-full h-full flex items-center justify-center text-4xl">
                      🎲
                    </div>
                  )}
                </div>

                {/* Title & Metadata */}
                <div className="flex-1 min-w-0">
                  <DialogTitle className="text-xl font-semibold mb-2">{game.title}</DialogTitle>

                  <div className="flex flex-wrap gap-2 mb-2">
                    {game.categories.map(cat => (
                      <Badge key={cat.id} variant="secondary" className="text-xs">
                        {cat.name}
                      </Badge>
                    ))}
                  </div>

                  <div className="flex flex-wrap gap-4 text-sm text-muted-foreground">
                    <span className="flex items-center gap-1">
                      <Users className="h-4 w-4" />
                      {game.minPlayers === game.maxPlayers
                        ? `${game.minPlayers} giocatori`
                        : `${game.minPlayers}-${game.maxPlayers} giocatori`}
                    </span>
                    <span className="flex items-center gap-1">
                      <Clock className="h-4 w-4" />
                      {game.playingTimeMinutes} min
                    </span>
                    <span className="flex items-center gap-1">
                      <Calendar className="h-4 w-4" />
                      {game.yearPublished}
                    </span>
                    {game.averageRating && (
                      <span className="flex items-center gap-1">
                        <Star className="h-4 w-4 fill-yellow-400 text-yellow-400" />
                        {game.averageRating.toFixed(1)}/10
                      </span>
                    )}
                  </div>
                </div>
              </div>
            </DialogHeader>

            {/* Tabs */}
            <Tabs value={activeTab} onValueChange={setActiveTab} className="flex-1">
              <div className="px-6 pt-4 border-b">
                <TabsList>
                  <TabsTrigger value="overview" className="gap-2">
                    <FileText className="h-4 w-4" />
                    Panoramica
                  </TabsTrigger>
                  {hasRules && (
                    <TabsTrigger value="rules" className="gap-2">
                      <BookOpen className="h-4 w-4" />
                      Regole
                    </TabsTrigger>
                  )}
                  {hasFaqs && (
                    <TabsTrigger value="faq" className="gap-2">
                      <HelpCircle className="h-4 w-4" />
                      FAQ ({game.faqs.length})
                    </TabsTrigger>
                  )}
                  {hasErratas && (
                    <TabsTrigger value="errata" className="gap-2">
                      <FileText className="h-4 w-4" />
                      Errata ({game.erratas.length})
                    </TabsTrigger>
                  )}
                </TabsList>
              </div>

              <ScrollArea className="h-[400px]">
                {/* Overview Tab */}
                <TabsContent value="overview" className="p-6 pt-4 m-0">
                  <OverviewTab game={game} />
                </TabsContent>

                {/* Rules Tab */}
                {hasRules && (
                  <TabsContent value="rules" className="p-6 pt-4 m-0">
                    {/* eslint-disable-next-line @typescript-eslint/no-non-null-assertion */}
                    <RulesTab rules={game.rules!} />
                  </TabsContent>
                )}

                {/* FAQ Tab */}
                {hasFaqs && (
                  <TabsContent value="faq" className="p-6 pt-4 m-0">
                    <FaqTab faqs={game.faqs} />
                  </TabsContent>
                )}

                {/* Errata Tab */}
                {hasErratas && (
                  <TabsContent value="errata" className="p-6 pt-4 m-0">
                    <ErrataTab erratas={game.erratas} />
                  </TabsContent>
                )}
              </ScrollArea>
            </Tabs>

            {/* Footer Actions */}
            <div className="p-4 border-t flex items-center justify-between">
              <div className="flex gap-2">
                <Button onClick={handleAddToCollection} className="gap-2">
                  <Plus className="h-4 w-4" />
                  Aggiungi alla Collezione
                </Button>
                <Button onClick={handleShare} variant="outline" size="icon">
                  <Share2 className="h-4 w-4" />
                </Button>
              </div>

              {game.bggId && (
                <Button variant="ghost" size="sm" asChild>
                  <a
                    href={`https://boardgamegeek.com/boardgame/${game.bggId}`}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="gap-2"
                  >
                    <ExternalLink className="h-4 w-4" />
                    Vedi su BGG
                  </a>
                </Button>
              )}
            </div>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}

// ============================================================================
// Tab Content Components
// ============================================================================

interface OverviewTabProps {
  game: SharedGameDetail;
}

function OverviewTab({ game }: OverviewTabProps) {
  return (
    <div className="space-y-6">
      {/* Description */}
      <div>
        <h3 className="font-semibold mb-2">Descrizione</h3>
        <p className="text-sm text-muted-foreground whitespace-pre-wrap">{game.description}</p>
      </div>

      {/* Mechanics */}
      {game.mechanics.length > 0 && (
        <div>
          <h3 className="font-semibold mb-2">Meccaniche</h3>
          <div className="flex flex-wrap gap-2">
            {game.mechanics.map(mech => (
              <Badge key={mech.id} variant="outline" className="text-xs">
                {mech.name}
              </Badge>
            ))}
          </div>
        </div>
      )}

      {/* Info Grid */}
      <div className="grid grid-cols-2 gap-4">
        {/* Designers */}
        {game.designers.length > 0 && (
          <div>
            <h4 className="text-sm font-medium text-muted-foreground mb-1">Autori</h4>
            <p className="text-sm">{game.designers.map(d => d.name).join(', ')}</p>
          </div>
        )}

        {/* Publishers */}
        {game.publishers.length > 0 && (
          <div>
            <h4 className="text-sm font-medium text-muted-foreground mb-1">Editori</h4>
            <p className="text-sm">{game.publishers.map(p => p.name).join(', ')}</p>
          </div>
        )}

        {/* Complexity */}
        {game.complexityRating && (
          <div>
            <h4 className="text-sm font-medium text-muted-foreground mb-1">Complessità</h4>
            <p className="text-sm">{game.complexityRating.toFixed(2)} / 5.00</p>
          </div>
        )}

        {/* Min Age */}
        <div>
          <h4 className="text-sm font-medium text-muted-foreground mb-1">Età minima</h4>
          <p className="text-sm">{game.minAge}+</p>
        </div>
      </div>
    </div>
  );
}

interface RulesTabProps {
  rules: { content: string; language: string };
}

function RulesTab({ rules }: RulesTabProps) {
  // Sanitize HTML content with DOMPurify to prevent XSS
  const sanitizedContent = useMemo(() => {
    return DOMPurify.sanitize(rules.content, {
      ALLOWED_TAGS: [
        'p',
        'br',
        'strong',
        'em',
        'b',
        'i',
        'u',
        'h1',
        'h2',
        'h3',
        'h4',
        'h5',
        'h6',
        'ul',
        'ol',
        'li',
        'blockquote',
        'pre',
        'code',
        'a',
        'img',
        'table',
        'thead',
        'tbody',
        'tr',
        'th',
        'td',
        'div',
        'span',
      ],
      ALLOWED_ATTR: ['href', 'src', 'alt', 'title', 'class', 'target', 'rel'],
      ADD_ATTR: ['target'],
      FORBID_TAGS: ['script', 'style', 'iframe', 'form', 'input'],
      FORBID_ATTR: ['onerror', 'onload', 'onclick', 'onmouseover'],
    });
  }, [rules.content]);

  return (
    <div>
      <div className="flex items-center justify-between mb-4">
        <Badge variant="secondary" className="text-xs">
          Lingua: {rules.language.toUpperCase()}
        </Badge>
        <Button variant="outline" size="sm" onClick={() => window.print()} className="gap-2">
          <FileText className="h-4 w-4" />
          Stampa
        </Button>
      </div>

      <div
        className="prose prose-sm max-w-none dark:prose-invert"
        dangerouslySetInnerHTML={{ __html: sanitizedContent }}
      />
    </div>
  );
}

interface FaqTabProps {
  faqs: Array<{
    id: string;
    question: string;
    answer: string;
    order: number;
  }>;
}

function FaqTab({ faqs }: FaqTabProps) {
  const [expandAll, setExpandAll] = useState(false);
  const sortedFaqs = [...faqs].sort((a, b) => a.order - b.order);

  return (
    <div>
      <div className="flex justify-end mb-4">
        <Button
          variant="outline"
          size="sm"
          onClick={() => setExpandAll(!expandAll)}
          className="gap-2"
        >
          <ChevronDown className={cn('h-4 w-4 transition-transform', expandAll && 'rotate-180')} />
          {expandAll ? 'Chiudi tutte' : 'Espandi tutte'}
        </Button>
      </div>

      <Accordion type="multiple" defaultValue={expandAll ? sortedFaqs.map(f => f.id) : []}>
        {sortedFaqs.map(faq => (
          <AccordionItem key={faq.id} value={faq.id}>
            <AccordionTrigger className="text-left">
              <span className="font-medium">{faq.question}</span>
            </AccordionTrigger>
            <AccordionContent>
              <p className="text-sm text-muted-foreground whitespace-pre-wrap">{faq.answer}</p>
            </AccordionContent>
          </AccordionItem>
        ))}
      </Accordion>

      {faqs.length === 0 && (
        <div className="text-center py-8 text-muted-foreground">
          <HelpCircle className="h-8 w-8 mx-auto mb-2 opacity-50" />
          <p>Nessuna FAQ disponibile per questo gioco.</p>
        </div>
      )}
    </div>
  );
}

interface ErrataTabProps {
  erratas: Array<{
    id: string;
    description: string;
    pageReference: string;
    publishedDate: string;
  }>;
}

function ErrataTab({ erratas }: ErrataTabProps) {
  const sortedErratas = [...erratas].sort(
    (a, b) => new Date(b.publishedDate).getTime() - new Date(a.publishedDate).getTime()
  );

  return (
    <div className="space-y-4">
      {sortedErratas.map(errata => (
        <div key={errata.id} className="border rounded-lg p-4">
          <div className="flex items-center gap-2 mb-2">
            <Badge variant="destructive" className="text-xs">
              Pagina {errata.pageReference}
            </Badge>
            <span className="text-xs text-muted-foreground">
              {format(new Date(errata.publishedDate), 'dd MMM yyyy', { locale: it })}
            </span>
          </div>
          <p className="text-sm">{errata.description}</p>
        </div>
      ))}

      {erratas.length === 0 && (
        <div className="text-center py-8 text-muted-foreground">
          <FileText className="h-8 w-8 mx-auto mb-2 opacity-50" />
          <p>Nessun errata disponibile per questo gioco.</p>
        </div>
      )}
    </div>
  );
}
