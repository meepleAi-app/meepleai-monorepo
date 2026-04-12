'use client';

export const dynamic = 'force-dynamic';

/**
 * MeepleCard Showcase — dev page
 *
 * Demonstrates variants and entity types of the MeepleCard component.
 * Accessible without authentication: /dev/meeple-card
 */

import {
  MeepleCard,
  MeepleCardSkeleton,
  FlipCard,
  FlipBack,
  Carousel3D,
  EntityTable,
} from '@/components/ui/data-display/meeple-card';
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

        {/* Flip Card — 8 entity-specific back contents */}
        <EntityFlipShowcase />
        <NavClickBehaviorSection />
        <DisabledNavItemsSection />
        <FeatureMatrixSection />

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
 * EntityFlipShowcase — 8 flip cards one per entity type, each with
 * entity-specific back content matching the design brief:
 *
 * - game    → generic description text
 * - toolkit → action list for each tool bound to the parent game
 * - chat    → history of other chats with the same agent
 * - kb      → rapid rules summary (rows + text mix)
 * - agent   → most useful info rows
 * - session → management action list
 * - player  → info rows + social links
 * - tool    → history of past results
 */
function EntityFlipShowcase() {
  const FLIP_SIZE = 'w-[240px] h-[440px]';

  return (
    <Section
      title="Flip Cards — Back per Entity Type"
      description="8 flip cards, una per entity type. Il retro è diverso per ciascun tipo: game (descrizione), toolkit (azioni dei tool), chat (history con stesso agent), kb (regole riassuntive), agent (info utili), session (azioni gestione), player (info + social), tool (storico risultati)."
    >
      <p className="text-xs text-amber-600 dark:text-amber-400 mb-3">
        💡 Clicca sulle card per girarle. Il pulsante ✕ sul retro le rigira.
      </p>
      <div className="flex flex-wrap justify-center gap-5">
        <FlipCard
          className={FLIP_SIZE}
          front={
            <MeepleCard
              entity="game"
              variant="grid"
              title="I Coloni di Catan"
              subtitle="Klaus Teuber · 1995"
              imageUrl={GAME_IMAGE}
              rating={7.1}
              ratingMax={10}
              status="owned"
              badge="Bestseller"
              metadata={[{ label: '3-4' }, { label: '60-120m' }]}
              className="h-full"
            />
          }
          back={
            <FlipBack
              entity="game"
              title="I Coloni di Catan"
              subtitle="Eurogame · 1995"
              sections={[
                {
                  kind: 'text',
                  text: "Il classico gateway game di Klaus Teuber. I giocatori costruiscono insediamenti e strade sull'isola di Catan, raccogliendo risorse (legno, grano, pecore, argilla, minerali) in base ai tiri di dado. Vince chi raggiunge per primo 10 punti vittoria.",
                },
                {
                  kind: 'text',
                  text: 'Pubblicato da Kosmos. Vincitore dello Spiel des Jahres 1995 e del Deutscher Spiele Preis. Ha rivoluzionato il board gaming moderno introducendo meccaniche di scambio e gestione risorse.',
                },
              ]}
              footer="Descrizione generica"
            />
          }
        />
        <FlipCard
          className={FLIP_SIZE}
          front={
            <MeepleCard
              entity="toolkit"
              variant="grid"
              title="Catan Toolkit"
              subtitle="Strumenti per Catan · 5 tools"
              badge="Ufficiale"
              metadata={[{ label: '5 strumenti' }, { label: '124 uso' }]}
              className="h-full"
            />
          }
          back={
            <FlipBack
              entity="toolkit"
              title="Catan Toolkit"
              subtitle="5 strumenti disponibili"
              sections={[
                {
                  kind: 'actions',
                  title: 'Strumenti',
                  items: [
                    {
                      label: 'Dice Roller',
                      meta: '2d6',
                      icon: '🎲',
                      onClick: () => alert('Dice Roller'),
                    },
                    {
                      label: 'Score Tracker',
                      meta: '4p',
                      icon: '📊',
                      onClick: () => alert('Score Tracker'),
                    },
                    {
                      label: 'Turn Timer',
                      meta: '60s',
                      icon: '⏱',
                      onClick: () => alert('Turn Timer'),
                    },
                    {
                      label: 'Resource Calculator',
                      icon: '🧮',
                      onClick: () => alert('Calculator'),
                    },
                    { label: 'Longest Road', meta: 'beta', icon: '🛣', disabled: true },
                  ],
                },
              ]}
              footer="Azioni per-tool"
            />
          }
        />
        <FlipCard
          className={FLIP_SIZE}
          front={
            <MeepleCard
              entity="chat"
              variant="grid"
              title="Strategie Catan"
              subtitle="CatanHelper AI · 12 messaggi"
              status="active"
              badge="Oggi"
              metadata={[{ label: '12 msg' }]}
              className="h-full"
            />
          }
          back={
            <FlipBack
              entity="chat"
              title="Strategie Catan"
              subtitle="CatanHelper AI"
              sections={[
                {
                  kind: 'list',
                  title: 'Altre chat con CatanHelper AI',
                  items: [
                    {
                      title: 'Come costruire strade ottimali',
                      subtitle: '8 messaggi',
                      meta: '2g fa',
                    },
                    {
                      title: 'Setup iniziale per 4 giocatori',
                      subtitle: '15 messaggi',
                      meta: '1w fa',
                    },
                    {
                      title: 'Come bloccare chi porta alla vittoria',
                      subtitle: '6 messaggi',
                      meta: '2w fa',
                    },
                    { title: 'Regole cavaliere più grande', subtitle: '3 messaggi', meta: '3w fa' },
                    { title: 'Gestione carte sviluppo', subtitle: '10 messaggi', meta: '1m fa' },
                  ],
                },
              ]}
              footer="5 chat precedenti · 42 totali"
            />
          }
        />
        <FlipCard
          className={FLIP_SIZE}
          front={
            <MeepleCard
              entity="kb"
              variant="grid"
              title="Manuale Catan"
              subtitle="PDF · 24 pagine"
              status="indexed"
              tags={['Base Rules', 'PDF']}
              metadata={[{ label: '24 pg' }, { label: '2.4 MB' }]}
              className="h-full"
            />
          }
          back={
            <FlipBack
              entity="kb"
              title="Manuale Catan"
              subtitle="Regole riassuntive"
              sections={[
                {
                  kind: 'rows',
                  title: 'Setup',
                  rows: [
                    ['Giocatori', '3-4 (5-6 con espansione)'],
                    ['Durata', '60-120 minuti'],
                    ['Età', '10+'],
                    ['Carte iniziali', '2 settlement + 2 road'],
                  ],
                },
                {
                  kind: 'rows',
                  title: 'Turno',
                  rows: [
                    ['1. Tira i dadi', '2d6'],
                    ['2. Raccogli risorse', 'se 7 → ladro'],
                    ['3. Commercio', 'con banca 4:1'],
                    ['4. Costruisci', 'road/settlement/city'],
                  ],
                },
                {
                  kind: 'text',
                  text: 'Vittoria: primo a 10 punti. Fonti: 1 settlement = 1pt, 1 city = 2pt, carta "victory point" = 1pt, longest road/largest army = 2pt ciascuno.',
                },
              ]}
              footer="Estratto dalle prime 3 pagine"
            />
          }
        />
        <FlipCard
          className={FLIP_SIZE}
          front={
            <MeepleCard
              entity="agent"
              variant="grid"
              title="CatanHelper AI"
              subtitle="Strategico · GPT-4o"
              status="active"
              badge="v2"
              metadata={[{ label: '342 invoc.' }, { label: '12 fonti' }]}
              className="h-full"
            />
          }
          back={
            <FlipBack
              entity="agent"
              title="CatanHelper AI"
              subtitle="Info bot"
              sections={[
                {
                  kind: 'rows',
                  title: 'Configurazione',
                  rows: [
                    ['Gioco', 'I Coloni di Catan'],
                    ['Modello', 'GPT-4o-mini'],
                    ['Strategia RAG', 'Hybrid Search'],
                    ['Temperature', '0.3'],
                    ['Max tokens', '2048'],
                  ],
                },
                {
                  kind: 'rows',
                  title: 'Utilizzo',
                  rows: [
                    ['Documenti', '3 PDF (450 chunks)'],
                    ['Chat attive', '2 conversazioni'],
                    ['Invocazioni', '342 totali'],
                    ['Ultimo uso', 'Oggi, 14:30'],
                    ['Accuratezza', '94%'],
                  ],
                },
              ]}
              footer="Info più utili"
            />
          }
        />
        <FlipCard
          className={FLIP_SIZE}
          front={
            <MeepleCard
              entity="session"
              variant="grid"
              title="Serata Catan #5"
              subtitle="Casa di Marco · 4 players"
              status="inprogress"
              badge="Live"
              metadata={[{ label: '4p' }, { label: '45 min' }]}
              className="h-full"
            />
          }
          back={
            <FlipBack
              entity="session"
              title="Serata Catan #5"
              subtitle="Azioni di gestione"
              sections={[
                {
                  kind: 'actions',
                  title: 'Durante la partita',
                  items: [
                    { label: 'Registra punteggi', icon: '📝', onClick: () => alert('Scores') },
                    { label: 'Aggiungi foto', icon: '📷', onClick: () => alert('Photo') },
                    { label: 'Nota evento', icon: '✏️', onClick: () => alert('Note') },
                    { label: 'Timer turno', meta: 'on', icon: '⏱', onClick: () => alert('Timer') },
                  ],
                },
                {
                  kind: 'actions',
                  title: 'Chiusura',
                  items: [
                    { label: 'Termina partita', icon: '🏁', onClick: () => alert('End') },
                    { label: 'Condividi risultato', icon: '📤', onClick: () => alert('Share') },
                    { label: 'Archivia', icon: '📦', onClick: () => alert('Archive') },
                  ],
                },
              ]}
              footer="7 azioni disponibili"
            />
          }
        />
        <FlipCard
          className={FLIP_SIZE}
          front={
            <MeepleCard
              entity="player"
              variant="grid"
              title="Marco Rossi"
              subtitle="@marco_games"
              badge="Top 5%"
              status="active"
              metadata={[{ label: '142 plays' }, { label: '68% win' }]}
              className="h-full"
            />
          }
          back={
            <FlipBack
              entity="player"
              title="Marco Rossi"
              subtitle="@marco_games"
              sections={[
                {
                  kind: 'rows',
                  title: 'Stats',
                  rows: [
                    ['Livello', 'Pro (Top 5%)'],
                    ['Partite giocate', '142'],
                    ['Vittorie', '96 (68%)'],
                    ['Gioco preferito', 'Catan'],
                    ['Membro dal', 'Gen 2023'],
                  ],
                },
                {
                  kind: 'social',
                  title: 'Social',
                  links: [
                    {
                      platform: 'BGG',
                      handle: 'marcogames',
                      icon: '🎲',
                      href: 'https://boardgamegeek.com',
                    },
                    { platform: 'Twitter', handle: '@marco_games', icon: '🐦' },
                    { platform: 'Discord', handle: 'marco#4242', icon: '💬' },
                  ],
                },
              ]}
              footer="Profilo pubblico"
            />
          }
        />
        <FlipCard
          className={FLIP_SIZE}
          front={
            <MeepleCard
              entity="tool"
              variant="grid"
              title="Dice Roller"
              subtitle="2d6 per Catan"
              badge="18 usi"
              metadata={[{ label: '2d6' }, { label: '18 uso' }]}
              className="h-full"
            />
          }
          back={
            <FlipBack
              entity="tool"
              title="Dice Roller"
              subtitle="Storico risultati"
              sections={[
                {
                  kind: 'list',
                  title: 'Ultimi 8 tiri',
                  items: [
                    { title: '2d6 = 8', subtitle: '4 + 4', meta: 'Ora 14:32' },
                    { title: '2d6 = 11', subtitle: '5 + 6', meta: 'Ora 14:30' },
                    { title: '2d6 = 3', subtitle: '1 + 2', meta: 'Ora 14:28' },
                    { title: '2d6 = 7', subtitle: '3 + 4 (ladro)', meta: 'Ora 14:25' },
                    { title: '2d6 = 10', subtitle: '6 + 4', meta: 'Ora 14:22' },
                    { title: '2d6 = 5', subtitle: '2 + 3', meta: 'Ora 14:20' },
                    { title: '2d6 = 9', subtitle: '4 + 5', meta: 'Ora 14:17' },
                    { title: '2d6 = 6', subtitle: '2 + 4', meta: 'Ora 14:15' },
                  ],
                },
              ]}
              footer="Media: 7.4 · Totali: 18 tiri"
            />
          }
        />
      </div>
    </Section>
  );
}

/**
 * NavClickBehaviorSection — documentation of the 0/1/N/Chat click pattern
 * from admin-mockups/meeple-card-nav-buttons-mockup.html Section 4.
 */
function NavClickBehaviorSection() {
  return (
    <Section
      title="Nav Click Behavior"
      description="Pattern di click sui nav button (da meeple-card-nav-buttons-mockup.html Section 4). Il comportamento dipende dal numero di entità collegate."
    >
      <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
        <BehaviorCard
          count="0"
          title="Crea nuovo"
          description="Apre direttamente il flusso di creazione (upload PDF, nuova sessione, ecc.)"
          color="hsl(210,40%,55%)"
          icon="+"
        />
        <BehaviorCard
          count="1"
          title="Navigazione diretta"
          description="Click porta direttamente alla pagina dell'entità collegata"
          color="hsl(240,60%,55%)"
          icon="→"
        />
        <BehaviorCard
          count="N"
          title="Popover lista"
          description="Mostra lista compact MeepleCard con header + pulsante [+] per crearne nuove"
          color="hsl(38,92%,50%)"
          icon="≡"
        />
        <BehaviorCard
          count="Chat"
          title="History page"
          description="Naviga alla history per scegliere se riprendere una chat esistente o crearne una nuova"
          color="hsl(220,80%,55%)"
          icon="💬"
        />
      </div>
    </Section>
  );
}

function BehaviorCard({
  count,
  title,
  description,
  color,
  icon,
}: {
  count: string;
  title: string;
  description: string;
  color: string;
  icon: string;
}) {
  return (
    <div
      className="rounded-xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)] p-4"
      style={{ borderLeftWidth: 4, borderLeftColor: color }}
    >
      <div className="mb-2 flex items-center justify-between">
        <span className="text-[10px] font-mono uppercase tracking-widest text-[var(--mc-text-muted)]">
          {count} entità
        </span>
        <span className="text-xl font-bold" style={{ color }}>
          {icon}
        </span>
      </div>
      <h4 className="font-[var(--font-quicksand)] text-[0.88rem] font-bold text-[var(--mc-text-primary)]">
        {title}
      </h4>
      <p className="mt-1 text-[0.7rem] leading-relaxed text-[var(--mc-text-secondary)]">
        {description}
      </p>
    </div>
  );
}

/**
 * DisabledNavItemsSection — demonstrates NavFooter with disabled items and
 * tooltips explaining why (per-entity limits, license, etc).
 */
function DisabledNavItemsSection() {
  return (
    <Section
      title="NavItems Disabled + Tooltip"
      description="Nav items con stato disabled (45% opacity + cursor not-allowed). Hover mostra tooltip con la ragione. Da meeple-card-nav-buttons-mockup.html Section 5."
    >
      <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-3">
        <div>
          <Label>agent disabled — limite 0/1</Label>
          <MeepleCard
            entity="game"
            variant="grid"
            title="Gloomhaven"
            subtitle="Isaac Childres · 2017"
            imageUrl="https://picsum.photos/seed/gloom-disabled/400/300"
            rating={8.9}
            ratingMax={10}
            status="owned"
            metadata={[{ label: '1-4' }, { label: '60-120m' }]}
            navItems={buildGameNavItems(
              { kbCount: 2, agentCount: 0, chatCount: 0, sessionCount: 0 },
              {
                onKbClick: () => alert('KB'),
                onChatPlus: () => alert('Chat plus'),
                onSessionPlus: () => alert('Session plus'),
              }
            ).map((item, i) =>
              i === 1 ? { ...item, disabled: true, label: 'Agent (limite 0/1)' } : item
            )}
          />
        </div>
        <div>
          <Label>kb+agent disabled — upload pending</Label>
          <MeepleCard
            entity="game"
            variant="grid"
            title="Spirit Island"
            subtitle="R. Eric Reuss · 2017"
            imageUrl="https://picsum.photos/seed/spirit-disabled/400/300"
            rating={8.3}
            ratingMax={10}
            metadata={[{ label: '1-4' }, { label: '90-120m' }]}
            navItems={buildGameNavItems(
              { kbCount: 0, agentCount: 0, chatCount: 0, sessionCount: 0 },
              { onChatPlus: () => alert('Chat'), onSessionPlus: () => alert('Session') }
            ).map((item, i) => (i <= 1 ? { ...item, disabled: true } : item))}
          />
        </div>
        <div>
          <Label>archived agent — solo KB readable</Label>
          <MeepleCard
            entity="agent"
            variant="grid"
            title="Frozen Agent"
            subtitle="RAG · Deprecated"
            status="archived"
            badge="v1"
            metadata={[{ label: '0 invoc.' }]}
            navItems={buildAgentNavItems(
              { chatCount: 0, kbCount: 3 },
              { onKbClick: () => alert('KB') }
            ).map((item, i) => (i !== 1 ? { ...item, disabled: true } : item))}
          />
        </div>
      </div>
    </Section>
  );
}

/**
 * FeatureMatrixSection — high-level capability table from summary mockup
 * Section 5. Documents what MeepleCard can do across all variants/entities.
 */
function FeatureMatrixSection() {
  const rows: [string, string][] = [
    ['Entity Types', 'game · player · session · agent · kb · chat · event · toolkit · tool (9)'],
    [
      'Layout Variants',
      'grid (7:10 cover) · list (56px thumb) · compact (dot only) · featured (16:9) · hero (full-bleed)',
    ],
    [
      'Visual Effects',
      'Glassmorphism · Warm shadows · Entity glow rings · Shimmer on hover · Cover scale(1.06) · Badge pulse',
    ],
    [
      'Flip Card',
      'perspective 1000px · transition 600ms · Entity-colored back header · 5 section kinds (text/rows/actions/list/social)',
    ],
    [
      'Quick Actions',
      'Hover-reveal glass buttons · showQuickActions={true} + actions[] · Entity-colored glow · WCAG 44px touch',
    ],
    ['Tags', 'Vertical left-edge TagStrip · entity color tint · overflow +N indicator'],
    [
      'Status Badges',
      '12 CardStatus values (owned/wishlist/active/idle/archived/processing/indexed/failed/inprogress/setup/completed/paused)',
    ],
    [
      'Navigation Footer',
      'Entity-aware nav items · count badges · plus indicators · disabled + tooltip',
    ],
    [
      'Click Behavior',
      '0 entities → create new · 1 entity → direct nav · N entities → popover list · Chat → history page',
    ],
    ['Table View', 'EntityTable with sortable headers (title/type/rating) + entity row borders'],
    ['3D Carousel', 'perspective 1200px · translateX(35%) · rotateY(-5deg) · blur 2px laterally'],
    [
      'Accessibility',
      'WCAG AA · keyboard nav (Tab/Enter/Space) · aria-label auto-gen · aria-sort · focus rings · screen reader support',
    ],
  ];

  return (
    <Section
      title="Feature Matrix"
      description="Capacità di MeepleCard a alto livello (da meeple-card-summary-render.html Section 5)."
    >
      <div className="overflow-x-auto rounded-2xl border border-[var(--mc-border)] bg-[var(--mc-bg-card)] shadow-[var(--mc-shadow-sm)]">
        <table className="w-full border-separate border-spacing-0 text-[0.82rem]">
          <tbody>
            {rows.map(([key, value], i) => (
              <tr key={key}>
                <td
                  className={`w-[160px] px-4 py-2.5 align-top font-[var(--font-quicksand)] text-[0.78rem] font-bold text-[var(--mc-text-secondary)] ${
                    i < rows.length - 1 ? 'border-b border-[var(--mc-border)]' : ''
                  }`}
                >
                  {key}
                </td>
                <td
                  className={`px-4 py-2.5 text-[var(--mc-text-primary)] ${
                    i < rows.length - 1 ? 'border-b border-[var(--mc-border)]' : ''
                  }`}
                >
                  {value}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </Section>
  );
}
