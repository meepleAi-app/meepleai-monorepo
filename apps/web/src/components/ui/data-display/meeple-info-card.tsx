/**
 * MeepleInfoCard - Companion Info Card for Game Detail Pages
 * Epic #3820 - MeepleCard System
 *
 * Tabbed card showing Knowledge Base (PDFs), Social Links, and optional Stats.
 * Used alongside MeepleCard (hero, flippable) on detail pages.
 *
 * Follows MeepleAI design system: rounded-3xl, bg-[#FFFDF9], font-quicksand/font-nunito
 */

'use client';

import { useState } from 'react';

import {
  BarChart3,
  Book,
  Calendar,
  Clock,
  ExternalLink,
  FileText,
  Globe,
  Link2,
  MessageSquare,
  Plus,
  Trophy,
  Upload,
} from 'lucide-react';
import Link from 'next/link';

import { PdfUploadModal } from '@/components/library/PdfUploadModal';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface MeepleInfoCardProps {
  gameId: string;
  gameTitle: string;
  bggId?: number | null;
  /** true for public pages (hides upload buttons, add link buttons) */
  readOnly?: boolean;
  showKnowledgeBase?: boolean;
  showSocialLinks?: boolean;
  showStats?: boolean;
  statsData?: {
    timesPlayed: number;
    lastPlayed: string | null;
    winRate: string | null;
    avgDuration: string | null;
  };
  recentSessions?: Array<{
    id: string;
    playedAt: string;
    durationFormatted: string;
    didWin: boolean | null;
  }>;
  className?: string;
  'data-testid'?: string;
}

type TabType = 'kb' | 'social' | 'stats';

// ============================================================================
// Tab configuration
// ============================================================================

const tabConfig: Record<TabType, { icon: typeof Book; label: string; color: string; bgColor: string }> = {
  kb: {
    icon: Book,
    label: 'Knowledge Base',
    color: 'hsl(25,95%,38%)',
    bgColor: 'hsla(25,95%,38%,0.05)',
  },
  social: {
    icon: Link2,
    label: 'Link Utili',
    color: 'hsl(262,83%,62%)',
    bgColor: 'hsla(262,83%,62%,0.05)',
  },
  stats: {
    icon: BarChart3,
    label: 'Statistiche',
    color: 'hsl(168,76%,42%)',
    bgColor: 'hsla(168,76%,42%,0.05)',
  },
};

// Social link type icons
const linkTypeIcons: Record<string, typeof Globe> = {
  bgg: Globe,
  official: Globe,
  forum: MessageSquare,
  video: ExternalLink,
  other: Link2,
};

// ============================================================================
// Component
// ============================================================================

export function MeepleInfoCard({
  gameId,
  gameTitle,
  bggId,
  readOnly = false,
  showKnowledgeBase = true,
  showSocialLinks = true,
  showStats = false,
  statsData,
  recentSessions,
  className,
  'data-testid': testId,
}: MeepleInfoCardProps) {
  // Determine available tabs
  const availableTabs: TabType[] = [];
  if (showKnowledgeBase) availableTabs.push('kb');
  if (showSocialLinks) availableTabs.push('social');
  if (showStats && statsData) availableTabs.push('stats');

  const [activeTab, setActiveTab] = useState<TabType>(availableTabs[0] || 'kb');
  const [isPdfModalOpen, setIsPdfModalOpen] = useState(false);

  // Build social links from bggId
  const socialLinks = bggId
    ? [
        {
          id: 'bgg',
          name: 'BoardGameGeek',
          url: `https://boardgamegeek.com/boardgame/${bggId}`,
          type: 'bgg' as const,
        },
      ]
    : [];

  // Placeholder for documents - will come from API in future
  const documents: Array<{
    id: string;
    name: string;
    type: 'rulebook' | 'errata' | 'homerule';
    version: string;
  }> = [];

  const documentTypeLabels: Record<string, string> = {
    rulebook: 'Regolamento',
    errata: 'Errata',
    homerule: 'Regole Casa',
  };

  const documentTypeColors: Record<string, string> = {
    rulebook: 'bg-[hsla(25,95%,38%,0.1)] text-[hsl(25,95%,38%)]',
    errata: 'bg-[hsla(262,83%,62%,0.1)] text-[hsl(262,83%,62%)]',
    homerule: 'bg-[hsla(210,90%,50%,0.1)] text-[hsl(210,90%,50%)]',
  };

  return (
    <>
      <div
        className={cn(
          'w-full max-w-[420px] flex-shrink-0 overflow-hidden rounded-3xl',
          'border border-[rgba(45,42,38,0.08)] bg-[#FFFDF9]',
          className,
        )}
        style={{
          aspectRatio: '3 / 4',
          boxShadow: '0 8px 32px rgba(45, 42, 38, 0.12)',
        }}
        data-testid={testId || 'meeple-info-card'}
      >
        {/* Tabs */}
        <div className="flex border-b border-[rgba(45,42,38,0.08)]">
          {availableTabs.map((tab) => {
            // eslint-disable-next-line security/detect-object-injection
            const config = tabConfig[tab];
            const Icon = config.icon;
            const isActive = activeTab === tab;

            return (
              <button
                key={tab}
                onClick={() => setActiveTab(tab)}
                className={cn(
                  'flex flex-1 items-center justify-center gap-2 px-3 py-4 font-quicksand text-sm font-semibold transition-all',
                  isActive
                    ? `border-b-2 text-[${config.color}]`
                    : 'text-[#6B665C] hover:bg-[rgba(45,42,38,0.04)] hover:text-[#2D2A26]',
                )}
                style={
                  isActive
                    ? { borderBottomColor: config.color, backgroundColor: config.bgColor, color: config.color }
                    : undefined
                }
                data-testid={`meeple-info-tab-${tab}`}
              >
                <Icon className="h-4 w-4" />
                <span className={availableTabs.length > 2 ? 'hidden sm:inline' : ''}>{config.label}</span>
              </button>
            );
          })}
        </div>

        {/* Tab Content */}
        <div className="flex h-[calc(100%-57px)] flex-col overflow-y-auto p-6">
          {/* Knowledge Base Tab */}
          {activeTab === 'kb' && (
            <div className="flex flex-1 flex-col">
              <div className="mb-4 flex items-center justify-between">
                <h3 className="font-quicksand text-lg font-bold text-[#2D2A26]">
                  Documenti PDF
                </h3>
                {!readOnly && (
                  <Button
                    size="sm"
                    onClick={() => setIsPdfModalOpen(true)}
                    className="bg-[hsl(25,95%,38%)] font-quicksand font-semibold text-white hover:bg-[hsl(25,95%,45%)]"
                  >
                    <Upload className="mr-1.5 h-4 w-4" />
                    Carica
                  </Button>
                )}
              </div>

              {documents.length > 0 ? (
                <div className="space-y-3">
                  {documents.map((doc) => (
                    <div
                      key={doc.id}
                      className="flex items-center gap-3 rounded-xl bg-[rgba(45,42,38,0.04)] p-4 transition-colors hover:bg-[rgba(45,42,38,0.06)]"
                    >
                      <div className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-lg bg-white shadow-sm">
                        <FileText className="h-5 w-5 text-[hsl(25,95%,38%)]" />
                      </div>
                      <div className="min-w-0 flex-1">
                        <p className="truncate font-nunito text-sm font-medium text-[#2D2A26]">
                          {doc.name}
                        </p>
                        <div className="flex items-center gap-2">
                          <span
                            className={cn(
                              'rounded px-1.5 py-0.5 text-xs font-medium',
                              documentTypeColors[doc.type],
                            )}
                          >
                            {documentTypeLabels[doc.type]}
                          </span>
                          <span className="text-xs text-[#9C958A]">v{doc.version}</span>
                        </div>
                      </div>
                      <Button
                        variant="ghost"
                        size="sm"
                        className="text-[#6B665C] hover:text-[hsl(25,95%,38%)]"
                      >
                        <ExternalLink className="h-4 w-4" />
                      </Button>
                    </div>
                  ))}
                </div>
              ) : (
                <div className="flex flex-1 flex-col items-center justify-center text-center">
                  <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-[hsla(25,95%,38%,0.1)]">
                    <FileText className="h-8 w-8 text-[hsl(25,95%,38%)]" />
                  </div>
                  <h4 className="mb-2 font-quicksand text-lg font-semibold text-[#2D2A26]">
                    Nessun documento
                  </h4>
                  <p className="mb-4 max-w-xs font-nunito text-sm text-[#6B665C]">
                    {readOnly
                      ? 'Nessun documento disponibile per questo gioco.'
                      : 'Carica regolamenti, errata o regole della casa per averli sempre a portata di mano.'}
                  </p>
                  {!readOnly && (
                    <Button
                      onClick={() => setIsPdfModalOpen(true)}
                      className="bg-[hsl(25,95%,38%)] font-quicksand font-semibold text-white hover:bg-[hsl(25,95%,45%)]"
                    >
                      <Plus className="mr-1.5 h-4 w-4" />
                      Carica PDF
                    </Button>
                  )}
                </div>
              )}
            </div>
          )}

          {/* Social Links Tab */}
          {activeTab === 'social' && (
            <div className="flex flex-1 flex-col">
              <div className="mb-4 flex items-center justify-between">
                <h3 className="font-quicksand text-lg font-bold text-[#2D2A26]">
                  Link Utili
                </h3>
                {!readOnly && (
                  <Button
                    size="sm"
                    variant="outline"
                    className="border-[rgba(45,42,38,0.12)] font-quicksand font-semibold text-[#6B665C] hover:bg-[rgba(45,42,38,0.04)] hover:text-[#2D2A26]"
                  >
                    <Plus className="mr-1.5 h-4 w-4" />
                    Aggiungi
                  </Button>
                )}
              </div>

              {socialLinks.length > 0 ? (
                <div className="space-y-3">
                  {socialLinks.map((link) => {
                    const IconComponent = linkTypeIcons[link.type] || Link2;
                    return (
                      <Link
                        key={link.id}
                        href={link.url}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="flex items-center gap-3 rounded-xl bg-[rgba(45,42,38,0.04)] p-4 transition-colors hover:bg-[hsla(262,83%,62%,0.08)]"
                      >
                        <div className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-lg bg-white shadow-sm">
                          <IconComponent className="h-5 w-5 text-[hsl(262,83%,62%)]" />
                        </div>
                        <div className="min-w-0 flex-1">
                          <p className="truncate font-nunito text-sm font-medium text-[#2D2A26]">
                            {link.name}
                          </p>
                          <p className="truncate text-xs text-[#9C958A]">{link.url}</p>
                        </div>
                        <ExternalLink className="h-4 w-4 flex-shrink-0 text-[#9C958A]" />
                      </Link>
                    );
                  })}
                </div>
              ) : (
                <div className="flex flex-1 flex-col items-center justify-center text-center">
                  <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-[hsla(262,83%,62%,0.1)]">
                    <Link2 className="h-8 w-8 text-[hsl(262,83%,62%)]" />
                  </div>
                  <h4 className="mb-2 font-quicksand text-lg font-semibold text-[#2D2A26]">
                    Nessun link
                  </h4>
                  <p className="mb-4 max-w-xs font-nunito text-sm text-[#6B665C]">
                    {readOnly
                      ? 'Nessun link disponibile per questo gioco.'
                      : 'Aggiungi link utili come BoardGameGeek, sito ufficiale o forum.'}
                  </p>
                  {!readOnly && (
                    <Button
                      variant="outline"
                      className="border-[hsl(262,83%,62%)] font-quicksand font-semibold text-[hsl(262,83%,62%)] hover:bg-[hsla(262,83%,62%,0.1)]"
                    >
                      <Plus className="mr-1.5 h-4 w-4" />
                      Aggiungi Link
                    </Button>
                  )}
                </div>
              )}
            </div>
          )}

          {/* Stats Tab */}
          {activeTab === 'stats' && statsData && (
            <div className="flex flex-1 flex-col">
              <h3 className="mb-4 font-quicksand text-lg font-bold text-[#2D2A26]">
                Statistiche di Gioco
              </h3>

              {/* Stats Grid */}
              <div className="mb-6 grid grid-cols-2 gap-3">
                {/* Times Played */}
                <div className="rounded-xl bg-[rgba(45,42,38,0.04)] p-4 text-center">
                  <div className="mb-1.5 flex items-center justify-center gap-1.5 text-[#6B665C]">
                    <BarChart3 className="h-4 w-4" />
                    <span className="font-nunito text-xs">Partite</span>
                  </div>
                  <p className="font-quicksand text-2xl font-bold text-[#2D2A26]">
                    {statsData.timesPlayed}
                  </p>
                </div>

                {/* Win Rate */}
                <div className="rounded-xl bg-[rgba(45,42,38,0.04)] p-4 text-center">
                  <div className="mb-1.5 flex items-center justify-center gap-1.5 text-[#6B665C]">
                    <Trophy className="h-4 w-4" />
                    <span className="font-nunito text-xs">Vittorie</span>
                  </div>
                  <p className="font-quicksand text-2xl font-bold text-[#2D2A26]">
                    {statsData.winRate || 'N/A'}
                  </p>
                </div>

                {/* Last Played */}
                <div className="rounded-xl bg-[rgba(45,42,38,0.04)] p-4 text-center">
                  <div className="mb-1.5 flex items-center justify-center gap-1.5 text-[#6B665C]">
                    <Calendar className="h-4 w-4" />
                    <span className="font-nunito text-xs">Ultima</span>
                  </div>
                  <p className="font-quicksand text-sm font-bold text-[#2D2A26]">
                    {statsData.lastPlayed
                      ? new Date(statsData.lastPlayed).toLocaleDateString('it-IT', {
                          day: 'numeric',
                          month: 'short',
                        })
                      : 'Mai'}
                  </p>
                </div>

                {/* Avg Duration */}
                <div className="rounded-xl bg-[rgba(45,42,38,0.04)] p-4 text-center">
                  <div className="mb-1.5 flex items-center justify-center gap-1.5 text-[#6B665C]">
                    <Clock className="h-4 w-4" />
                    <span className="font-nunito text-xs">Durata media</span>
                  </div>
                  <p className="font-quicksand text-sm font-bold text-[#2D2A26]">
                    {statsData.avgDuration || 'N/A'}
                  </p>
                </div>
              </div>

              {/* Recent Sessions */}
              {recentSessions && recentSessions.length > 0 && (
                <div>
                  <h4 className="mb-3 font-quicksand text-sm font-semibold uppercase tracking-wider text-[#9C958A]">
                    Sessioni Recenti
                  </h4>
                  <div className="space-y-2">
                    {recentSessions.slice(0, 5).map((session) => (
                      <div
                        key={session.id}
                        className="flex items-center justify-between rounded-lg bg-[rgba(45,42,38,0.04)] px-4 py-3"
                      >
                        <div className="flex items-center gap-3">
                          {session.didWin !== null && (
                            <span
                              className={cn(
                                'flex h-6 w-6 items-center justify-center rounded-full text-xs font-bold',
                                session.didWin
                                  ? 'bg-[hsla(168,76%,42%,0.15)] text-[hsl(168,76%,42%)]'
                                  : 'bg-[rgba(45,42,38,0.08)] text-[#9C958A]',
                              )}
                            >
                              {session.didWin ? 'W' : 'L'}
                            </span>
                          )}
                          <span className="font-nunito text-sm text-[#2D2A26]">
                            {new Date(session.playedAt).toLocaleDateString('it-IT', {
                              day: 'numeric',
                              month: 'short',
                              year: 'numeric',
                            })}
                          </span>
                        </div>
                        <span className="font-nunito text-xs text-[#9C958A]">
                          {session.durationFormatted}
                        </span>
                      </div>
                    ))}
                  </div>
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {/* PDF Upload Modal (only for non-readOnly) */}
      {!readOnly && (
        <PdfUploadModal
          isOpen={isPdfModalOpen}
          onClose={() => setIsPdfModalOpen(false)}
          gameId={gameId}
          gameTitle={gameTitle}
        />
      )}
    </>
  );
}
