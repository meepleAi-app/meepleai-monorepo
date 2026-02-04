/**
 * GameSideCard Component (Issue #3513)
 *
 * Side card with tabbed interface for:
 * - Knowledge Base: PDF documents (rulebooks, errata, homerules)
 * - Social Links: Useful links (BGG, official site, forums)
 *
 * Same dimensions as the game card for visual balance.
 * Follows MeepleAI design system.
 */

'use client';

import { useState } from 'react';

import {
  Book,
  ExternalLink,
  FileText,
  Globe,
  Link2,
  MessageSquare,
  Plus,
  Upload,
} from 'lucide-react';
import Link from 'next/link';

import { PdfUploadModal } from '@/components/library/PdfUploadModal';
import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

export interface GameSideCardProps {
  gameId: string;
  gameTitle: string;
  /** Optional BGG ID for external link */
  bggId?: number | null;
}

type TabType = 'kb' | 'social';

// Placeholder data for PDFs - in real implementation this would come from an API
interface PdfDocument {
  id: string;
  name: string;
  type: 'rulebook' | 'errata' | 'homerule';
  version: string;
  uploadedAt: string;
}

// Placeholder data for social links
interface SocialLink {
  id: string;
  name: string;
  url: string;
  type: 'bgg' | 'official' | 'forum' | 'video' | 'other';
}

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

const linkTypeIcons: Record<string, typeof Globe> = {
  bgg: Globe,
  official: Globe,
  forum: MessageSquare,
  video: ExternalLink,
  other: Link2,
};

export function GameSideCard({ gameId, gameTitle, bggId }: GameSideCardProps) {
  const [activeTab, setActiveTab] = useState<TabType>('kb');
  const [isPdfModalOpen, setIsPdfModalOpen] = useState(false);

  // Placeholder data - in real implementation, fetch from API
  const documents: PdfDocument[] = [];
  const socialLinks: SocialLink[] = bggId
    ? [
        {
          id: 'bgg',
          name: 'BoardGameGeek',
          url: `https://boardgamegeek.com/boardgame/${bggId}`,
          type: 'bgg',
        },
      ]
    : [];

  return (
    <>
      <div
        className="w-full max-w-[420px] flex-shrink-0 overflow-hidden rounded-3xl border border-[rgba(45,42,38,0.08)] bg-[#FFFDF9]"
        style={{
          aspectRatio: '3 / 4',
          boxShadow: '0 8px 32px rgba(45, 42, 38, 0.12)',
        }}
      >
        {/* Tabs */}
        <div className="flex border-b border-[rgba(45,42,38,0.08)]">
          <button
            onClick={() => setActiveTab('kb')}
            className={cn(
              'flex flex-1 items-center justify-center gap-2 px-4 py-4 font-quicksand text-sm font-semibold transition-all',
              activeTab === 'kb'
                ? 'border-b-2 border-[hsl(25,95%,38%)] bg-[hsla(25,95%,38%,0.05)] text-[hsl(25,95%,38%)]'
                : 'text-[#6B665C] hover:bg-[rgba(45,42,38,0.04)] hover:text-[#2D2A26]'
            )}
          >
            <Book className="h-4 w-4" />
            Knowledge Base
          </button>
          <button
            onClick={() => setActiveTab('social')}
            className={cn(
              'flex flex-1 items-center justify-center gap-2 px-4 py-4 font-quicksand text-sm font-semibold transition-all',
              activeTab === 'social'
                ? 'border-b-2 border-[hsl(262,83%,62%)] bg-[hsla(262,83%,62%,0.05)] text-[hsl(262,83%,62%)]'
                : 'text-[#6B665C] hover:bg-[rgba(45,42,38,0.04)] hover:text-[#2D2A26]'
            )}
          >
            <Link2 className="h-4 w-4" />
            Link Utili
          </button>
        </div>

        {/* Tab Content */}
        <div className="flex h-[calc(100%-57px)] flex-col overflow-y-auto p-6">
          {activeTab === 'kb' && (
            <div className="flex flex-1 flex-col">
              {/* Header */}
              <div className="mb-4 flex items-center justify-between">
                <h3 className="font-quicksand text-lg font-bold text-[#2D2A26]">
                  Documenti PDF
                </h3>
                <Button
                  size="sm"
                  onClick={() => setIsPdfModalOpen(true)}
                  className="bg-[hsl(25,95%,38%)] font-quicksand font-semibold text-white hover:bg-[hsl(25,95%,45%)]"
                >
                  <Upload className="mr-1.5 h-4 w-4" />
                  Carica
                </Button>
              </div>

              {/* Documents list */}
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
                              documentTypeColors[doc.type]
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
                /* Empty state */
                <div className="flex flex-1 flex-col items-center justify-center text-center">
                  <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-[hsla(25,95%,38%,0.1)]">
                    <FileText className="h-8 w-8 text-[hsl(25,95%,38%)]" />
                  </div>
                  <h4 className="mb-2 font-quicksand text-lg font-semibold text-[#2D2A26]">
                    Nessun documento
                  </h4>
                  <p className="mb-4 max-w-xs font-nunito text-sm text-[#6B665C]">
                    Carica regolamenti, errata o regole della casa per averli sempre a portata di mano.
                  </p>
                  <Button
                    onClick={() => setIsPdfModalOpen(true)}
                    className="bg-[hsl(25,95%,38%)] font-quicksand font-semibold text-white hover:bg-[hsl(25,95%,45%)]"
                  >
                    <Plus className="mr-1.5 h-4 w-4" />
                    Carica PDF
                  </Button>
                </div>
              )}
            </div>
          )}

          {activeTab === 'social' && (
            <div className="flex flex-1 flex-col">
              {/* Header */}
              <div className="mb-4 flex items-center justify-between">
                <h3 className="font-quicksand text-lg font-bold text-[#2D2A26]">
                  Link Utili
                </h3>
                <Button
                  size="sm"
                  variant="outline"
                  className="border-[rgba(45,42,38,0.12)] font-quicksand font-semibold text-[#6B665C] hover:bg-[rgba(45,42,38,0.04)] hover:text-[#2D2A26]"
                >
                  <Plus className="mr-1.5 h-4 w-4" />
                  Aggiungi
                </Button>
              </div>

              {/* Links list */}
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
                /* Empty state */
                <div className="flex flex-1 flex-col items-center justify-center text-center">
                  <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-[hsla(262,83%,62%,0.1)]">
                    <Link2 className="h-8 w-8 text-[hsl(262,83%,62%)]" />
                  </div>
                  <h4 className="mb-2 font-quicksand text-lg font-semibold text-[#2D2A26]">
                    Nessun link
                  </h4>
                  <p className="mb-4 max-w-xs font-nunito text-sm text-[#6B665C]">
                    Aggiungi link utili come BoardGameGeek, sito ufficiale o forum.
                  </p>
                  <Button
                    variant="outline"
                    className="border-[hsl(262,83%,62%)] font-quicksand font-semibold text-[hsl(262,83%,62%)] hover:bg-[hsla(262,83%,62%,0.1)]"
                  >
                    <Plus className="mr-1.5 h-4 w-4" />
                    Aggiungi Link
                  </Button>
                </div>
              )}
            </div>
          )}
        </div>
      </div>

      {/* PDF Upload Modal */}
      <PdfUploadModal
        isOpen={isPdfModalOpen}
        onClose={() => setIsPdfModalOpen(false)}
        gameId={gameId}
        gameTitle={gameTitle}
      />
    </>
  );
}
