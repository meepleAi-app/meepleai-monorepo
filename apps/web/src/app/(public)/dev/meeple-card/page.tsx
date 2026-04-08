'use client';

export const dynamic = 'force-dynamic';

/**
 * MeepleCard Showcase — dev page
 *
 * Demonstrates variants and entity types of the MeepleCard component.
 * Accessible without authentication: /dev/meeple-card
 */

import { useState, useMemo } from 'react';

import {
  MeepleCard,
  MeepleCardSkeleton,
  MobileCardLayout,
  MobileDevicePreview,
  MobileCardDrawer,
  FlipCard,
  Carousel3D,
  EntityTable,
  entityHsl,
  entityLabel,
} from '@/components/ui/data-display/meeple-card';
import type { MeepleCardProps } from '@/components/ui/data-display/meeple-card';
import {
  buildAgentNavItems,
  buildChatNavItems,
  buildEventNavItems,
  buildGameNavItems,
  buildKbNavItems,
  buildPlayerNavItems,
  buildSessionNavItems,
  buildToolNavItems,
  buildToolkitNavItems,
} from '@/components/ui/data-display/meeple-card/nav-items';

const GAME_IMAGE = 'https://picsum.photos/seed/catan/400/300';

const MOBILE_DEMO_CARDS: MeepleCardProps[] = [
  {
    entity: 'game',
    id: 'mob-catan',
    title: 'Catan',
    subtitle: 'Klaus Teuber · 1995',
    imageUrl: 'https://picsum.photos/seed/mobile-catan/400/300',
    rating: 7.2,
    ratingMax: 10,
    badge: 'Owned',
    status: 'owned',
    metadata: [{ label: '3-4' }, { label: '60m' }],
  },
  {
    entity: 'game',
    id: 'mob-azul',
    title: 'Azul',
    subtitle: 'Michael Kiesling · 2017',
    imageUrl: 'https://picsum.photos/seed/mobile-azul/400/300',
    rating: 7.8,
    ratingMax: 10,
    badge: 'Top 10',
    status: 'owned',
    metadata: [{ label: '2-4' }, { label: '30m' }],
  },
  {
    entity: 'agent',
    id: 'mob-agent',
    title: 'Azul Rules Expert',
    subtitle: 'RAG · GPT-4o-mini',
    status: 'active',
    badge: 'v2',
    metadata: [{ label: '342 invoc.' }],
  },
  {
    entity: 'session',
    id: 'mob-session',
    title: 'Serata Azul',
    subtitle: '4 giocatori · Casa di Marco',
    status: 'inprogress',
    badge: 'Live',
    metadata: [{ label: '45 min' }],
  },
  {
    entity: 'kb',
    id: 'mob-kb',
    title: 'azul_rulebook.pdf',
    subtitle: 'Regolamento base · 12 pg',
    status: 'indexed',
    metadata: [{ label: '2.4 MB' }],
  },
  {
    entity: 'chat',
    id: 'mob-chat',
    title: 'Come si gioca ad Azul?',
    subtitle: 'Azul · 12 messaggi',
    badge: '12 nuovi',
    metadata: [{ label: '12 msg' }],
  },
];

function Section({
  title,
  description,
  children,
}: {
  title: string;
  description?: string;
  children: React.ReactNode;
}) {
  return (
    <section className="space-y-4">
      <div>
        <h2 className="text-xl font-bold text-foreground">{title}</h2>
        {description && <p className="text-sm text-muted-foreground mt-1">{description}</p>}
      </div>
      {children}
    </section>
  );
}

function Label({ children }: { children: React.ReactNode }) {
  return (
    <p className="text-xs font-mono uppercase tracking-widest text-muted-foreground mb-2">
      {children}
    </p>
  );
}

function CardRow({ children }: { children: React.ReactNode }) {
  return <div className="flex flex-wrap gap-5">{children}</div>;
}

export default function MeepleCardDevPage() {
  return (
    <div className="min-h-screen bg-background">
      <div className="border-b bg-card px-6 py-8">
        <p className="text-xs font-mono text-muted-foreground uppercase tracking-widest mb-1">
          /dev/meeple-card
        </p>
        <h1 className="text-3xl font-bold">MeepleCard Showcase</h1>
        <p className="text-muted-foreground mt-2 max-w-2xl">
          Universal card component with 5 variants and 9 entity types. Tutte le feature dei mockup
          in <code className="text-xs">admin-mockups/</code> sono dimostrate qui.
        </p>
      </div>

      <div className="px-6 py-10 space-y-16 max-w-7xl mx-auto">
        {/* Entity Types */}
        <Section title="Entity Types" description="9 entity types with distinct color tokens.">
          <CardRow>
            {(
              [
                'game',
                'player',
                'session',
                'agent',
                'kb',
                'chat',
                'event',
                'toolkit',
                'tool',
              ] as const
            ).map(entity => (
              <div key={entity}>
                <Label>{entity}</Label>
                <MeepleCard
                  entity={entity}
                  variant="grid"
                  title={entity.charAt(0).toUpperCase() + entity.slice(1)}
                  subtitle="Entity type demo"
                  metadata={[{ label: 'Demo' }]}
                />
              </div>
            ))}
          </CardRow>
        </Section>

        {/* Variants */}
        <Section title="Variants" description="5 layout variants.">
          <Label>grid</Label>
          <CardRow>
            <MeepleCard
              entity="game"
              variant="grid"
              title="I Coloni di Catan"
              subtitle="Klaus Teuber · 1995"
              imageUrl={GAME_IMAGE}
              rating={4.2}
              ratingMax={5}
              badge="Bestseller"
              metadata={[{ label: '3-4 giocatori' }, { label: '60-120 min' }]}
            />
            <MeepleCard
              entity="game"
              variant="grid"
              title="Pandemic"
              subtitle="Matt Leacock · 2008"
              imageUrl="https://picsum.photos/seed/pandemic/400/300"
              rating={4.4}
              ratingMax={5}
              metadata={[{ label: '2-4 giocatori' }, { label: '45 min' }]}
            />
          </CardRow>

          <Label>list</Label>
          <div className="max-w-2xl space-y-2">
            <MeepleCard
              entity="game"
              variant="list"
              title="Ticket to Ride"
              subtitle="Alan R. Moon · 2004"
              imageUrl="https://picsum.photos/seed/ticket/400/300"
              rating={4.3}
              ratingMax={5}
              badge="Classic"
              metadata={[{ label: '2-5' }, { label: '60-90 min' }]}
            />
            <MeepleCard
              entity="session"
              variant="list"
              title="Game Night #42"
              subtitle="Catan · 4 giocatori"
              badge="Completata"
              metadata={[{ label: '15 Apr 2026' }, { label: '2h 15min' }]}
            />
          </div>

          <Label>compact</Label>
          <div className="max-w-lg space-y-1">
            <MeepleCard
              entity="agent"
              variant="compact"
              title="RulesBot Pro"
              subtitle="RAG Agent"
              badge="Attivo"
            />
            <MeepleCard
              entity="kb"
              variant="compact"
              title="Manuale Catan"
              subtitle="PDF · 48 pagine"
              badge="Indicizzato"
            />
            <MeepleCard entity="chat" variant="compact" title="Chat Catan" subtitle="12 messaggi" />
          </div>

          <Label>featured</Label>
          <MeepleCard
            entity="game"
            variant="featured"
            title="Twilight Imperium"
            subtitle="Fantasy Flight Games · 1997"
            imageUrl="https://picsum.photos/seed/twilight/600/400"
            rating={4.7}
            ratingMax={5}
            badge="Epic"
            metadata={[{ label: '3-6 giocatori' }, { label: '4-8 ore' }]}
          />

          <Label>hero</Label>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <MeepleCard
              entity="game"
              variant="hero"
              title="Game of the Month: Catan"
              subtitle="The classic gateway game that started it all"
              imageUrl="https://picsum.photos/seed/catan-hero/800/600"
              rating={7.2}
              ratingMax={10}
              badge="Editor's Pick"
              metadata={[{ label: 'Editor Pick' }, { label: '10K+ plays' }]}
            />
            <MeepleCard
              entity="player"
              variant="hero"
              title="Alice Rossi"
              subtitle="Giocatrice del mese"
              metadata={[{ label: '42 partite' }, { label: '68% win rate' }]}
            />
          </div>
        </Section>

        {/* Quick Actions — hover-reveal (fix showQuickActions prop) */}
        <Section
          title="Quick Actions (Hover)"
          description="Le azioni rapide si rivelano al passaggio del mouse. Richiedono showQuickActions={true} AND actions.length > 0 (entrambe obbligatorie)."
        >
          <p className="text-xs text-amber-600 dark:text-amber-400 mb-3">
            💡 Passa il mouse sopra ogni card per vedere i pulsanti delle azioni in alto a destra.
          </p>
          <CardRow>
            <div>
              <Label>game · 3 azioni</Label>
              <MeepleCard
                entity="game"
                variant="grid"
                title="Wingspan"
                subtitle="Elizabeth Hargrave · 2019"
                imageUrl="https://picsum.photos/seed/wingspan/400/300"
                rating={4.8}
                ratingMax={5}
                metadata={[{ label: '1-5 giocatori' }]}
                showQuickActions
                actions={[
                  { icon: '▶', label: 'Avvia partita', onClick: () => alert('Avvia!') },
                  { icon: '📖', label: 'Regolamento', onClick: () => alert('Regolamento') },
                  {
                    icon: '🗑️',
                    label: 'Rimuovi',
                    onClick: () => alert('Rimuovi'),
                    variant: 'danger',
                  },
                ]}
              />
            </div>
            <div>
              <Label>agent · 2 azioni</Label>
              <MeepleCard
                entity="agent"
                variant="grid"
                title="RulesBot Pro"
                subtitle="RAG · GPT-4o-mini"
                status="active"
                metadata={[{ label: '342 invocazioni' }]}
                showQuickActions
                actions={[
                  { icon: '💬', label: 'Chat', onClick: () => alert('Chat!') },
                  { icon: '⚙️', label: 'Configura', onClick: () => alert('Config!') },
                ]}
              />
            </div>
            <div>
              <Label>kb · disabled + danger</Label>
              <MeepleCard
                entity="kb"
                variant="grid"
                title="Manuale Catan"
                subtitle="PDF · 48 pagine"
                status="indexed"
                metadata={[{ label: '124 chunks' }, { label: '2.4 MB' }]}
                showQuickActions
                actions={[
                  { icon: '👁', label: 'Anteprima', onClick: () => alert('Preview') },
                  {
                    icon: '🔄',
                    label: 'Reindex in corso',
                    onClick: () => alert('Reindex'),
                    disabled: true,
                  },
                  {
                    icon: '🗑️',
                    label: 'Elimina',
                    onClick: () => alert('Elimina'),
                    variant: 'danger',
                  },
                ]}
              />
            </div>
          </CardRow>
        </Section>

        {/* Showcase Completo — tutte le feature combinate (match mockup summary Section 2) */}
        <Section
          title="Showcase Completo — Tutte le Feature"
          description="Una card per ogni entity con image + badge + rating + metadata + status + tags + navItems + quickActions combinati. Mirrors admin-mockups/meeple-card-summary-render.html Section 2."
        >
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
            <MeepleCard
              entity="game"
              variant="grid"
              title="Catan"
              subtitle="Klaus Teuber · 1995"
              imageUrl="https://picsum.photos/seed/catan-full/400/300"
              rating={7.2}
              ratingMax={10}
              badge="Bestseller"
              status="owned"
              tags={['Classic', 'Family', 'Strategy']}
              metadata={[{ label: '3-4 giocatori' }, { label: '60-120 min' }]}
              showQuickActions
              actions={[
                { icon: 'ℹ', label: 'Info', onClick: () => alert('Info') },
                { icon: '📚', label: 'Library', onClick: () => alert('Library') },
                { icon: '🤖', label: 'Crea agent', onClick: () => alert('Agent') },
              ]}
              navItems={buildGameNavItems(
                { kbCount: 3, agentCount: 1, chatCount: 5, sessionCount: 12 },
                {
                  onKbClick: () => alert('KB'),
                  onAgentClick: () => alert('Agent'),
                  onChatClick: () => alert('Chat'),
                  onSessionClick: () => alert('Session'),
                }
              )}
            />
            <MeepleCard
              entity="player"
              variant="grid"
              title="Marco Rossi"
              subtitle="@marco_games"
              badge="Top 5%"
              status="active"
              metadata={[{ label: '142 plays' }, { label: '68% win' }]}
              navItems={buildPlayerNavItems(
                { totalWins: 96, totalSessions: 142 },
                {
                  onWinsClick: () => alert('Wins'),
                  onSessionsClick: () => alert('Sessions'),
                }
              )}
            />
            <MeepleCard
              entity="session"
              variant="grid"
              title="Serata Azul"
              subtitle="Azul · Casa di Marco"
              status="inprogress"
              badge="Live"
              metadata={[{ label: '4 giocatori' }, { label: '45 min' }]}
              navItems={buildSessionNavItems(
                { playerCount: 4, hasNotes: true, toolCount: 2, photoCount: 6 },
                {
                  onPlayersClick: () => alert('Players'),
                  onNotesClick: () => alert('Notes'),
                  onToolsClick: () => alert('Tools'),
                  onPhotosClick: () => alert('Photos'),
                }
              )}
            />
            <MeepleCard
              entity="agent"
              variant="grid"
              title="Azul Rules Expert"
              subtitle="RAG Strategy · GPT-4o-mini"
              status="active"
              badge="v2"
              metadata={[{ label: '342 invocazioni' }, { label: '12 fonti' }]}
              showQuickActions
              actions={[
                { icon: '💬', label: 'Chat', onClick: () => alert('Chat') },
                { icon: '⚙️', label: 'Config', onClick: () => alert('Config') },
              ]}
              navItems={buildAgentNavItems(
                { chatCount: 18, kbCount: 12 },
                {
                  onChatClick: () => alert('Chat'),
                  onKbClick: () => alert('KB'),
                  onConfigClick: () => alert('Config'),
                }
              )}
            />
            <MeepleCard
              entity="kb"
              variant="grid"
              title="azul_rulebook.pdf"
              subtitle="Azul · Regolamento base"
              status="indexed"
              tags={['PDF', 'Base Rules']}
              metadata={[{ label: '12 pagine' }, { label: '2.4 MB' }]}
              showQuickActions
              actions={[
                { icon: '👁', label: 'Anteprima', onClick: () => alert('Preview') },
                { icon: '⬇', label: 'Download', onClick: () => alert('Download') },
              ]}
              navItems={buildKbNavItems(
                { chunkCount: 124 },
                {
                  onChunksClick: () => alert('Chunks'),
                  onReindexClick: () => alert('Reindex'),
                  onPreviewClick: () => alert('Preview'),
                  onDownloadClick: () => alert('Download'),
                }
              )}
            />
            <MeepleCard
              entity="chat"
              variant="grid"
              title="Come si gioca ad Azul?"
              subtitle="Azul · Azul Rules Expert"
              status="active"
              badge="12 nuovi"
              metadata={[{ label: '12 messaggi' }]}
              navItems={buildChatNavItems(
                { messageCount: 12 },
                {
                  onMessagesClick: () => alert('Messages'),
                  onAgentLinkClick: () => alert('Agent'),
                }
              )}
            />
            <MeepleCard
              entity="event"
              variant="grid"
              title="MeepleAI Tournament"
              subtitle="Grand Finals · 15 Mar"
              badge="Finals"
              status="setup"
              metadata={[{ label: '32 team' }, { label: '15 Mar' }]}
              navItems={buildEventNavItems(
                { participantCount: 32, gameCount: 4 },
                {
                  onParticipantsClick: () => alert('Participants'),
                  onLocationClick: () => alert('Location'),
                  onGamesClick: () => alert('Games'),
                  onDateClick: () => alert('Date'),
                }
              )}
            />
            <MeepleCard
              entity="toolkit"
              variant="grid"
              title="Catan Companion"
              subtitle="Toolkit ufficiale · 6 strumenti"
              badge="Ufficiale"
              status="active"
              tags={['Official', 'Stats', 'Dice']}
              metadata={[{ label: '6 strumenti' }, { label: '124 uso' }]}
              navItems={buildToolkitNavItems(
                { toolCount: 6, deckCount: 2, phaseCount: 4, useCount: 124 },
                {
                  onToolsClick: () => alert('Tools'),
                  onDecksClick: () => alert('Decks'),
                  onPhasesClick: () => alert('Phases'),
                  onHistoryClick: () => alert('History'),
                }
              )}
            />
            <MeepleCard
              entity="tool"
              variant="grid"
              title="Dice Roller"
              subtitle="Lancio dadi · 6d6"
              badge="Beta"
              metadata={[{ label: '18 uso' }]}
              showQuickActions
              actions={[
                { icon: '🎲', label: 'Lancia', onClick: () => alert('Roll') },
                { icon: '📋', label: 'Duplica', onClick: () => alert('Duplicate') },
              ]}
              navItems={buildToolNavItems({
                onUseClick: () => alert('Use'),
                onEditClick: () => alert('Edit'),
                onDuplicateClick: () => alert('Duplicate'),
                onHistoryClick: () => alert('History'),
              })}
            />
          </div>
        </Section>

        {/* Tags showcase */}
        <Section
          title="Tags (TagStrip)"
          description="Il prop tags produce una striscia verticale di tag sul bordo sinistro della cover. Supporta overflow con +N."
        >
          <CardRow>
            <div>
              <Label>3 tags</Label>
              <MeepleCard
                entity="game"
                variant="grid"
                title="Scythe"
                subtitle="Jamey Stegmaier · 2016"
                imageUrl="https://picsum.photos/seed/scythe/400/300"
                rating={8.2}
                ratingMax={10}
                tags={['Heavy', 'Engine', 'Area Control']}
                metadata={[{ label: '1-5' }, { label: '90-115m' }]}
              />
            </div>
            <div>
              <Label>overflow +2</Label>
              <MeepleCard
                entity="game"
                variant="grid"
                title="Gloomhaven"
                subtitle="Isaac Childres · 2017"
                imageUrl="https://picsum.photos/seed/gloomhaven/400/300"
                rating={8.9}
                ratingMax={10}
                tags={['RPG', 'Campaign', 'Coop', 'Deckbuild', 'Legacy']}
                metadata={[{ label: '1-4' }, { label: '60-120m' }]}
              />
            </div>
            <div>
              <Label>kb tags</Label>
              <MeepleCard
                entity="kb"
                variant="grid"
                title="rules_addendum.pdf"
                subtitle="Scythe · FAQ ufficiali"
                status="indexed"
                tags={['FAQ', 'Errata']}
                metadata={[{ label: '8 pagine' }]}
              />
            </div>
          </CardRow>
        </Section>

        {/* Grid with Status Badges */}
        <Section
          title="Grid con Status Badge"
          description="Lo status badge è mostrato sulla cover nei variant grid (oltre al rendering compact)."
        >
          <CardRow>
            {(
              [
                { s: 'owned', t: 'Posseduto', e: 'game' },
                { s: 'wishlist', t: 'Wishlist', e: 'game' },
                { s: 'active', t: 'Attivo', e: 'agent' },
                { s: 'indexed', t: 'Indicizzato', e: 'kb' },
                { s: 'processing', t: 'Processing', e: 'kb' },
                { s: 'failed', t: 'Fallito', e: 'kb' },
                { s: 'inprogress', t: 'In corso', e: 'session' },
                { s: 'completed', t: 'Completata', e: 'session' },
              ] as const
            ).map(({ s, t, e }) => (
              <div key={s}>
                <Label>{s}</Label>
                <MeepleCard
                  entity={e}
                  variant="grid"
                  title={t}
                  subtitle={`status=${s}`}
                  status={s}
                />
              </div>
            ))}
          </CardRow>
        </Section>

        {/* Status badges (compact) */}
        <Section
          title="Status Badges (compact)"
          description="Tutti i 12 valori CardStatus nel variant compact."
        >
          <CardRow>
            {(
              [
                'owned',
                'wishlist',
                'active',
                'idle',
                'archived',
                'processing',
                'indexed',
                'failed',
                'inprogress',
                'setup',
                'completed',
                'paused',
              ] as const
            ).map(s => (
              <MeepleCard key={s} entity="game" variant="compact" title={s} status={s} />
            ))}
          </CardRow>
        </Section>

        {/* NavFooter showcase */}
        <Section
          title="NavFooter — tutti gli stati"
          description="Esempi di navItems per ogni entity con count, plus indicator e disabled."
        >
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
            <div>
              <Label>game · counts pieni</Label>
              <MeepleCard
                entity="game"
                variant="grid"
                title="Catan"
                subtitle="Klaus Teuber · 1995"
                imageUrl={GAME_IMAGE}
                rating={4.2}
                ratingMax={5}
                metadata={[{ label: '3-4' }, { label: '60min' }]}
                navItems={buildGameNavItems(
                  { kbCount: 3, agentCount: 1, chatCount: 5, sessionCount: 12 },
                  {
                    onKbClick: () => alert('KB!'),
                    onAgentClick: () => alert('Agent!'),
                    onChatClick: () => alert('Chat!'),
                    onSessionClick: () => alert('Sessions!'),
                  }
                )}
              />
            </div>

            <div>
              <Label>game · vuoto (plus)</Label>
              <MeepleCard
                entity="game"
                variant="grid"
                title="Azul"
                subtitle="2017"
                imageUrl="https://picsum.photos/seed/azul/400/300"
                rating={4.0}
                ratingMax={5}
                navItems={buildGameNavItems(
                  { kbCount: 0, agentCount: 0, chatCount: 0, sessionCount: 0 },
                  {
                    onKbPlus: () => alert('Add KB!'),
                    onAgentPlus: () => alert('Create agent!'),
                    onChatPlus: () => alert('Start chat!'),
                    onSessionPlus: () => alert('New session!'),
                  }
                )}
              />
            </div>

            <div>
              <Label>player</Label>
              <MeepleCard
                entity="player"
                variant="grid"
                title="Alice Rossi"
                subtitle="Giocatrice attiva"
                navItems={buildPlayerNavItems(
                  { totalWins: 18, totalSessions: 42 },
                  {
                    onWinsClick: () => alert('Wins!'),
                    onSessionsClick: () => alert('Sessions!'),
                  }
                )}
              />
            </div>

            <div>
              <Label>session</Label>
              <MeepleCard
                entity="session"
                variant="grid"
                title="Game Night #42"
                subtitle="Catan · 2h 15min"
                navItems={buildSessionNavItems(
                  { playerCount: 4, hasNotes: true, toolCount: 6, photoCount: 12 },
                  {
                    onPlayersClick: () => alert('Players!'),
                    onNotesClick: () => alert('Notes!'),
                    onToolsClick: () => alert('Tools!'),
                    onPhotosClick: () => alert('Photos!'),
                  }
                )}
              />
            </div>

            <div>
              <Label>agent</Label>
              <MeepleCard
                entity="agent"
                variant="grid"
                title="RulesBot Pro"
                subtitle="RAG · 12 fonti"
                navItems={buildAgentNavItems(
                  { chatCount: 3, kbCount: 12 },
                  {
                    onChatClick: () => alert('Chat!'),
                    onKbClick: () => alert('KB!'),
                    onConfigClick: () => alert('Config!'),
                  }
                )}
              />
            </div>

            <div>
              <Label>kb</Label>
              <MeepleCard
                entity="kb"
                variant="grid"
                title="Manuale Catan"
                subtitle="48 pagine"
                navItems={buildKbNavItems(
                  { chunkCount: 124 },
                  {
                    onChunksClick: () => alert('Chunks!'),
                    onReindexClick: () => alert('Reindex!'),
                    onPreviewClick: () => alert('Preview!'),
                    onDownloadClick: () => alert('Download!'),
                  }
                )}
              />
            </div>

            <div>
              <Label>chat</Label>
              <MeepleCard
                entity="chat"
                variant="grid"
                title="Chat Catan"
                subtitle="ieri"
                navItems={buildChatNavItems(
                  { messageCount: 18 },
                  {
                    onMessagesClick: () => alert('Messages!'),
                    onAgentLinkClick: () => alert('Agent!'),
                  }
                )}
              />
            </div>

            <div>
              <Label>event</Label>
              <MeepleCard
                entity="event"
                variant="grid"
                title="Serata Lupus"
                subtitle="Sabato 12 Apr"
                navItems={buildEventNavItems(
                  { participantCount: 8, gameCount: 3 },
                  {
                    onParticipantsClick: () => alert('Participants!'),
                    onLocationClick: () => alert('Location!'),
                    onGamesClick: () => alert('Games!'),
                    onDateClick: () => alert('Date!'),
                  }
                )}
              />
            </div>

            <div>
              <Label>toolkit</Label>
              <MeepleCard
                entity="toolkit"
                variant="grid"
                title="Catan Tools"
                subtitle="6 strumenti"
                navItems={buildToolkitNavItems(
                  { toolCount: 6, deckCount: 2, phaseCount: 4, useCount: 18 },
                  {
                    onToolsClick: () => alert('Tools!'),
                    onDecksClick: () => alert('Decks!'),
                    onPhasesClick: () => alert('Phases!'),
                    onHistoryClick: () => alert('History!'),
                  }
                )}
              />
            </div>

            <div>
              <Label>tool</Label>
              <MeepleCard
                entity="tool"
                variant="grid"
                title="Dice Roller"
                subtitle="6d6"
                navItems={buildToolNavItems({
                  onUseClick: () => alert('Use!'),
                  onEditClick: () => alert('Edit!'),
                  onDuplicateClick: () => alert('Duplicate!'),
                  onHistoryClick: () => alert('History!'),
                })}
              />
            </div>
          </div>
        </Section>

        {/* Flip Card — match visual-test Section 4 */}
        <Section
          title="Flip Card"
          description="Click sulla card per girare. Front = card standard, back = entity-colored header + dettagli. Componente FlipCard con trigger='card'|'button'."
        >
          <p className="text-xs text-amber-600 dark:text-amber-400 mb-3">
            💡 Clicca sulle card per girarle. Il pulsante ✕ sul retro le rigira.
          </p>
          <CardRow>
            <FlipCard
              className="w-[220px] h-[420px]"
              front={
                <MeepleCard
                  entity="game"
                  variant="grid"
                  title="I Coloni di Catan"
                  subtitle="Klaus Teuber · Kosmos · 1995"
                  imageUrl={GAME_IMAGE}
                  rating={7.1}
                  ratingMax={10}
                  status="owned"
                  tags={['Gioco', 'Posseduto']}
                  metadata={[{ label: '3-4' }, { label: '60-120m' }, { label: '2.3' }]}
                  className="h-full"
                />
              }
              back={
                <FlipBackContent
                  entity="game"
                  title="I Coloni di Catan"
                  subtitle="Gioco da Tavolo · 1995"
                  rows={[
                    ['Giocatori', '3-4'],
                    ['Durata', '60-120 min'],
                    ['Complessità', '2.32 / 5'],
                    ['Rating', '7.1 / 10'],
                    ['Documenti KB', '3 PDF'],
                    ['Sessioni', '5 giocate'],
                    ['Agente AI', 'CatanHelper'],
                    ['Chat', '2 conversazioni'],
                  ]}
                />
              }
            />
            <FlipCard
              className="w-[220px] h-[420px]"
              front={
                <MeepleCard
                  entity="agent"
                  variant="grid"
                  title="CatanHelper AI"
                  subtitle="Agente Strategico · GPT-4o"
                  status="active"
                  metadata={[{ label: '450 chunks' }, { label: '2 chat' }]}
                  navItems={buildAgentNavItems(
                    { chatCount: 2, kbCount: 3 },
                    {
                      onChatClick: () => alert('Chat'),
                      onKbClick: () => alert('KB'),
                      onConfigClick: () => alert('Config'),
                    }
                  )}
                  className="h-full"
                />
              }
              back={
                <FlipBackContent
                  entity="agent"
                  title="CatanHelper AI"
                  subtitle="Agente AI · 10 Feb 2026"
                  rows={[
                    ['Gioco', 'I Coloni di Catan'],
                    ['Modello', 'GPT-4o'],
                    ['Strategia RAG', 'Hybrid Search'],
                    ['Documenti', '3 PDF (450 chunks)'],
                    ['Chat', '2 conversazioni'],
                    ['Ultimo uso', 'Oggi, 14:30'],
                  ]}
                />
              }
            />
          </CardRow>
        </Section>

        {/* 3D Carousel — match visual-test Section 5 */}
        <Section
          title="3D Carousel"
          description="Prospettiva 1200px. Centro: scale(1.1), opacity 1. Laterali: scale(0.85), opacity 0.6, blur 2px, rotateY(-5deg). Componente Carousel3D con nav buttons."
        >
          <Carousel3D
            cards={[
              {
                entity: 'game',
                id: 'car-catan',
                title: 'I Coloni di Catan',
                subtitle: 'Klaus Teuber',
                imageUrl: GAME_IMAGE,
                rating: 7.1,
                ratingMax: 10,
                tags: ['Gioco'],
                metadata: [{ label: '3-4' }, { label: '60-120m' }],
              },
              {
                entity: 'game',
                id: 'car-wingspan',
                title: 'Wingspan',
                subtitle: 'Elizabeth Hargrave',
                imageUrl: 'https://picsum.photos/seed/wingspan-car/400/300',
                rating: 8.1,
                ratingMax: 10,
                metadata: [{ label: '1-5' }, { label: '40-70m' }],
              },
              {
                entity: 'game',
                id: 'car-azul',
                title: 'Azul',
                subtitle: 'Michael Kiesling',
                rating: 7.8,
                ratingMax: 10,
                metadata: [{ label: '2-4' }, { label: '30-45m' }],
              },
              {
                entity: 'game',
                id: 'car-tfm',
                title: 'Terraforming Mars',
                subtitle: 'Jacob Fryxelius',
                imageUrl: 'https://picsum.photos/seed/tfm-car/400/300',
                rating: 8.4,
                ratingMax: 10,
                metadata: [{ label: '1-5' }, { label: '120m' }],
              },
              {
                entity: 'game',
                id: 'car-spirit',
                title: 'Spirit Island',
                subtitle: 'R. Eric Reuss',
                rating: 8.3,
                ratingMax: 10,
                metadata: [{ label: '1-4' }, { label: '90-120m' }],
              },
              {
                entity: 'game',
                id: 'car-gloom',
                title: 'Gloomhaven',
                subtitle: 'Isaac Childres',
                imageUrl: 'https://picsum.photos/seed/gloom-car/400/300',
                rating: 8.7,
                ratingMax: 10,
                metadata: [{ label: '1-4' }, { label: '60-120m' }],
              },
            ]}
          />
          <p className="text-xs text-center text-muted-foreground mt-2">
            Nota: Carousel3D usa <code className="text-xs">hidden md:flex</code> — visibile solo su
            desktop (md+).
          </p>
        </Section>

        {/* Multi-Entity Grid — match visual-test Section 6 */}
        <Section
          title="Multi-Entity Grid"
          description="Card di entity diversi insieme (Chat, Player, Document) in un grid misto — come nella homepage o search results."
        >
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
            <MeepleCard
              entity="chat"
              variant="grid"
              title="Strategie Catan"
              subtitle="12 messaggi · Oggi"
              badge="12"
              metadata={[{ label: '12 msg' }]}
              navItems={buildChatNavItems(
                { messageCount: 12 },
                {
                  onMessagesClick: () => alert('Messages'),
                  onAgentLinkClick: () => alert('Agent'),
                }
              )}
            />
            <MeepleCard
              entity="player"
              variant="grid"
              title="Marco R."
              subtitle="12 sessioni · 5 vittorie"
              metadata={[{ label: '12 sessioni' }, { label: '42% win' }]}
              navItems={buildPlayerNavItems(
                { totalWins: 5, totalSessions: 12 },
                {
                  onWinsClick: () => alert('Wins'),
                  onSessionsClick: () => alert('Sessions'),
                }
              )}
            />
            <MeepleCard
              entity="kb"
              variant="grid"
              title="Regolamento TFM"
              subtitle="PDF · 32 pagine · 200 chunks"
              status="indexed"
              metadata={[{ label: '32 pg' }, { label: '200 chunks' }]}
              navItems={buildKbNavItems(
                { chunkCount: 200 },
                {
                  onChunksClick: () => alert('Chunks'),
                  onReindexClick: () => alert('Reindex'),
                  onPreviewClick: () => alert('Preview'),
                  onDownloadClick: () => alert('Download'),
                }
              )}
            />
          </div>
        </Section>

        {/* Table View — EntityTable component */}
        <Section
          title="Table View — EntityTable"
          description="Tabella con righe entity-colored (border-left 4px), header sortable (Titolo/Tipo/Rating), entity badge, status label semantico, nav icons con count. Match visual-test Section 3."
        >
          <p className="text-xs text-amber-600 dark:text-amber-400 mb-3">
            💡 Clicca sugli header <strong>Titolo</strong>, <strong>Tipo</strong> o{' '}
            <strong>Rating</strong> per cambiare ordinamento (▲/▼).
          </p>
          <EntityTable
            cards={[
              {
                entity: 'game',
                id: 'tbl-catan',
                title: 'I Coloni di Catan',
                subtitle: 'Klaus Teuber',
                imageUrl: GAME_IMAGE,
                rating: 7.1,
                ratingMax: 10,
                status: 'owned',
                metadata: [{ label: '3-4' }, { label: '60-120m' }],
                navItems: buildGameNavItems(
                  { kbCount: 3, agentCount: 1, chatCount: 2, sessionCount: 5 },
                  {
                    onKbClick: () => alert('KB'),
                    onAgentClick: () => alert('Agent'),
                    onChatClick: () => alert('Chat'),
                    onSessionClick: () => alert('Session'),
                  }
                ),
              },
              {
                entity: 'session',
                id: 'tbl-session',
                title: 'Serata Catan #5',
                subtitle: '15 Feb 2026',
                status: 'completed',
                metadata: [{ label: 'Catan' }, { label: '4 giocatori' }],
                navItems: buildSessionNavItems(
                  { playerCount: 4, hasNotes: false, toolCount: 0, photoCount: 0 },
                  { onPlayersClick: () => alert('Players') }
                ),
              },
              {
                entity: 'agent',
                id: 'tbl-agent',
                title: 'CatanHelper AI',
                subtitle: 'GPT-4o · Hybrid RAG',
                status: 'active',
                metadata: [{ label: 'Catan' }, { label: '450 chunks' }],
                navItems: buildAgentNavItems(
                  { chatCount: 2, kbCount: 3 },
                  {
                    onChatClick: () => alert('Chat'),
                    onKbClick: () => alert('KB'),
                    onConfigClick: () => alert('Config'),
                  }
                ),
              },
              {
                entity: 'kb',
                id: 'tbl-kb',
                title: 'Regolamento TFM',
                subtitle: 'PDF · 32 pg',
                rating: 9.1,
                ratingMax: 10,
                status: 'indexed',
                metadata: [{ label: '200 chunks' }, { label: '2.4 MB' }],
                navItems: buildKbNavItems(
                  { chunkCount: 200 },
                  {
                    onChunksClick: () => alert('Chunks'),
                    onReindexClick: () => alert('Reindex'),
                    onPreviewClick: () => alert('Preview'),
                    onDownloadClick: () => alert('Download'),
                  }
                ),
              },
              {
                entity: 'chat',
                id: 'tbl-chat',
                title: 'Strategie Catan',
                subtitle: 'Oggi · 12:45',
                status: 'active',
                metadata: [{ label: '12 messaggi' }],
                navItems: buildChatNavItems(
                  { messageCount: 12 },
                  {
                    onMessagesClick: () => alert('Messages'),
                    onAgentLinkClick: () => alert('Agent'),
                  }
                ),
              },
              {
                entity: 'player',
                id: 'tbl-player',
                title: 'Marco Rossi',
                subtitle: '@marco_games',
                status: 'active',
                metadata: [{ label: '142 plays' }, { label: '68% win' }],
                navItems: buildPlayerNavItems(
                  { totalWins: 96, totalSessions: 142 },
                  {
                    onWinsClick: () => alert('Wins'),
                    onSessionsClick: () => alert('Sessions'),
                  }
                ),
              },
              {
                entity: 'game',
                id: 'tbl-azul',
                title: 'Azul',
                subtitle: 'Michael Kiesling · 2017',
                rating: 7.8,
                ratingMax: 10,
                status: 'wishlist',
                metadata: [{ label: '2-4' }, { label: '30-45m' }],
                navItems: buildGameNavItems(
                  { kbCount: 0, agentCount: 0, chatCount: 0, sessionCount: 0 },
                  {
                    onKbPlus: () => alert('Add KB'),
                    onAgentPlus: () => alert('Create agent'),
                    onChatPlus: () => alert('Start chat'),
                    onSessionPlus: () => alert('New session'),
                  }
                ),
              },
            ]}
            onRowClick={card => alert(`Row clicked: ${card.title}`)}
            caption="Esempio di EntityTable con 7 entità miste"
          />
        </Section>

        {/* Mobile Card Layout — match admin-mockups/mobile-card-layout-mockup.html */}
        <MobilePreviewSection />

        {/* Skeleton */}
        <Section title="Skeleton" description="Stato di caricamento.">
          <CardRow>
            <MeepleCardSkeleton variant="grid" />
            <MeepleCardSkeleton variant="grid" />
            <MeepleCardSkeleton variant="grid" />
          </CardRow>
        </Section>
      </div>
    </div>
  );
}

/**
 * Interactive mobile preview section.
 *
 * Separated as a component so it can use useState without making the
 * entire page re-render on every state change.
 */
function MobilePreviewSection() {
  const [drawerCard, setDrawerCard] = useState<MeepleCardProps | null>(null);

  const interactiveCards = useMemo(
    () =>
      MOBILE_DEMO_CARDS.map(card => ({
        ...card,
        onClick: () => setDrawerCard(card),
      })),
    []
  );

  return (
    <Section
      title="Mobile Card Layout — Focus Mode"
      description="Il componente MobileCardLayout con phone-frame da mobile-card-layout-mockup.html. Hand-stack a sinistra, FocusedCard centrale con swipe. Click sulla card apre il drawer con tab specifici per entity type."
    >
      <p className="text-xs text-amber-600 dark:text-amber-400 mb-3">
        💡 Clicca sulla focused card (centro) per aprire il drawer con tab entity-specifici. Clicca
        sulle card nella hand-stack (sinistra) per cambiare focus.
      </p>
      <div className="flex flex-col items-center gap-8 lg:flex-row lg:items-start lg:justify-center">
        {/* Phone frame */}
        <MobileDevicePreview>
          {/* MobileCardLayout uses md:hidden — override with md:!flex for desktop */}
          <MobileCardLayout className="md:!flex" cards={interactiveCards} />
          {/* Drawer overlay — renders inside the phone frame */}
          <MobileCardDrawer card={drawerCard} onClose={() => setDrawerCard(null)} />
        </MobileDevicePreview>

        {/* Sidebar notes */}
        <div className="max-w-sm space-y-4 text-sm text-[var(--mc-text-secondary)]">
          <h3 className="text-base font-bold text-[var(--mc-text-primary)]">
            Componenti del Mockup
          </h3>
          <ul className="list-disc space-y-2 pl-5">
            <li>
              <strong>MobileDevicePreview</strong> — phone frame 390x720 con status bar, navbar
              (logo MeepleAI + notifiche + avatar), search bar, action bar
            </li>
            <li>
              <strong>HandSidebar</strong> — stack verticale a sinistra (44px) con le card come in
              mano a un giocatore
            </li>
            <li>
              <strong>FocusedCard</strong> — card centrale in focus con cover, entity badge, rating,
              metadata e actions
            </li>
            <li>
              <strong>SwipeGesture</strong> — swipe L/R per navigare tra card (o click su hand card)
            </li>
            <li>
              <strong>MobileCardDrawer</strong> — drawer overlay con tabs entity-specifici. Si apre
              cliccando sulla focused card. Tab diversi per ogni entity (game: Overview/AI/Sessioni/
              Media/Scoreboard, agent: Overview/Config/Stats/Chat/Fonti, ecc.)
            </li>
          </ul>
          <div className="rounded-lg border border-[var(--mc-border)] bg-[var(--mc-bg-muted)] p-3 text-xs">
            <strong>Stato implementazione:</strong> MobileCardLayout e subcomponenti sono
            implementati ed esportati da <code>meeple-card/index.ts</code>. Attualmente{' '}
            <em>nessun consumer</em> li usa in produzione — il componente è <code>md:hidden</code>{' '}
            (mobile-only). Override <code>md:!flex</code> usato qui per il rendering desktop.
          </div>
        </div>
      </div>
    </Section>
  );
}

/**
 * Flip card back content — entity-colored header + detail rows.
 * Matches the visual-test mockup Section 4.
 */
function FlipBackContent({
  entity,
  title,
  subtitle,
  rows,
}: {
  entity: MeepleCardProps['entity'];
  title: string;
  subtitle: string;
  rows: [string, string][];
}) {
  return (
    <div className="flex h-full flex-col overflow-hidden rounded-2xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)]">
      {/* Entity-colored header with diagonal stripe pattern */}
      <div
        className="relative px-4 py-4"
        style={{
          background: entityHsl(entity),
          backgroundImage:
            'repeating-linear-gradient(45deg, transparent, transparent 10px, rgba(255,255,255,0.05) 10px, rgba(255,255,255,0.05) 20px)',
        }}
      >
        <h3 className="font-[var(--font-quicksand)] text-base font-bold text-white">{title}</h3>
        <p className="text-xs text-white/80">{subtitle}</p>
      </div>
      {/* Detail rows */}
      <div className="flex-1 overflow-y-auto px-4 py-2">
        {rows.map(([label, value]) => (
          <div
            key={label}
            className="flex items-center justify-between border-b border-[var(--mc-border)] py-2 text-xs last:border-0"
          >
            <span className="font-semibold text-[var(--mc-text-secondary)]">{label}</span>
            <span className="text-[var(--mc-text-primary)]">{value}</span>
          </div>
        ))}
      </div>
      <div className="px-4 py-2 text-center text-[10px] font-semibold uppercase tracking-wide text-[var(--mc-text-muted)]">
        {entityLabel[entity]}
      </div>
    </div>
  );
}
