'use client';

export const dynamic = 'force-dynamic';

/**
 * MeepleCard Showcase — dev page
 *
 * Demonstrates variants and entity types of the MeepleCard component.
 * Accessible without authentication: /dev/meeple-card
 */

import { MeepleCard, MeepleCardSkeleton } from '@/components/ui/data-display/meeple-card';
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
          Universal card component with 5 variants and 9 entity types.
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
          <MeepleCard
            entity="player"
            variant="hero"
            title="Alice Rossi"
            subtitle="Giocatrice del mese"
            metadata={[{ label: '42 partite' }, { label: '68% win rate' }]}
          />
        </Section>

        {/* With Actions */}
        <Section title="Con Azioni" description="MeepleCard con actions array.">
          <CardRow>
            <MeepleCard
              entity="game"
              variant="grid"
              title="Wingspan"
              subtitle="Elizabeth Hargrave · 2019"
              imageUrl="https://picsum.photos/seed/wingspan/400/300"
              rating={4.8}
              ratingMax={5}
              metadata={[{ label: '1-5 giocatori' }]}
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
          </CardRow>
        </Section>

        {/* Status badges */}
        <Section title="Status Badges" description="Stato delle entità tramite CardStatus.">
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
