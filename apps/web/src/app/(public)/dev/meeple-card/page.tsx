'use client';

/**
 * MeepleCard Showcase — pagina di sviluppo
 *
 * Dimostra tutte le varianti e le funzionalità del componente MeepleCard:
 *   Varianti:  grid · list · compact · featured · hero · expanded
 *   Entità:    game · player · session · agent · kb · chatSession · event · toolkit
 *   Features:  flip · wishlist · status · selectable · quick-actions · tags · badge
 *
 * Accessibile senza autenticazione: /dev/meeple-card
 */

import { useState } from 'react';

import { BookOpen, Heart, Play, Star, Trash2, Users } from 'lucide-react';

import { MeepleCard, MeepleCardSkeleton } from '@/components/ui/data-display/meeple-card';

// ── Helpers ────────────────────────────────────────────────────────────────────

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

// ── Mock data ──────────────────────────────────────────────────────────────────

const GAME_IMAGE = 'https://picsum.photos/seed/catan/400/300';
const PLAYER_IMAGE = 'https://picsum.photos/seed/alice/200/200';

const QUICK_ACTIONS = [
  { icon: Play, label: 'Avvia partita', onClick: () => alert('Avvia partita') },
  { icon: BookOpen, label: 'Regolamento', onClick: () => alert('Apri regolamento') },
  { icon: Heart, label: 'Aggiungi ai preferiti', onClick: () => alert('Preferiti') },
  {
    separator: true,
    icon: Trash2,
    label: 'Rimuovi',
    onClick: () => alert('Rimuovi'),
    destructive: true,
  },
];

// ── Page ──────────────────────────────────────────────────────────────────────

export default function MeepleCardShowcasePage() {
  const [wishlisted, setWishlisted] = useState(false);
  const [selected, setSelected] = useState(false);
  const [flipped, setFlipped] = useState(false);

  return (
    <div className="min-h-screen bg-background">
      {/* Header */}
      <div className="border-b bg-card px-6 py-8">
        <p className="text-xs font-mono text-muted-foreground uppercase tracking-widest mb-1">
          /dev/meeple-card
        </p>
        <h1 className="text-3xl font-bold">MeepleCard Showcase</h1>
        <p className="text-muted-foreground mt-2 max-w-2xl">
          Tutte le varianti e le funzionalità del componente universale di MeepleAI per la
          visualizzazione di entità: giochi, giocatori, sessioni, agenti e altro.
        </p>
      </div>

      <div className="px-6 py-10 space-y-16 max-w-7xl mx-auto">
        {/* ── 1. VARIANTI ─────────────────────────────────────────────────── */}
        <Section
          title="Varianti di layout"
          description="Lo stesso componente supporta 6 layout diversi tramite il prop variant."
        >
          {/* GRID */}
          <Label>grid — griglia 2-4 colonne, uso principale in libreria e catalogo</Label>
          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-4">
            <MeepleCard
              entity="game"
              variant="grid"
              title="I Coloni di Catan"
              subtitle="Klaus Teuber · 1995"
              imageUrl={GAME_IMAGE}
              rating={4.2}
              ratingMax={5}
              badge="Bestseller"
              metadata={[{ icon: Users, label: '3-4' }, { label: '60-120 min' }]}
              playerCountDisplay="3-4"
              playTimeDisplay="90min"
              identityChip1="Strategia"
              identityChip2="Euro"
            />
            <MeepleCard
              entity="game"
              variant="grid"
              title="Pandemic"
              subtitle="Matt Leacock · 2008"
              imageUrl="https://picsum.photos/seed/pandemic/400/300"
              rating={4.5}
              ratingMax={5}
              playerCountDisplay="2-4"
              playTimeDisplay="45min"
              identityChip1="Cooperativo"
              status="owned"
              showStatusIcon
            />
            <MeepleCard
              entity="game"
              variant="grid"
              title="Wingspan"
              subtitle="Elizabeth Hargrave · 2019"
              imageUrl="https://picsum.photos/seed/wingspan/400/300"
              rating={4.8}
              ratingMax={5}
              playerCountDisplay="1-5"
              playTimeDisplay="70min"
              identityChip1="Engine Building"
              status={['owned', 'played']}
              showStatusIcon
            />
            <MeepleCard
              entity="game"
              variant="grid"
              title="Senza immagine"
              subtitle="Entità senza cover"
              rating={3.0}
              ratingMax={5}
            />
          </div>

          {/* LIST */}
          <Label>list — riga orizzontale, uso in liste dense e ricerche</Label>
          <div className="flex flex-col gap-2">
            <MeepleCard
              entity="game"
              variant="list"
              title="I Coloni di Catan"
              subtitle="Klaus Teuber · Strategia · 3-4 giocatori"
              imageUrl={GAME_IMAGE}
              rating={4.2}
              ratingMax={5}
              metadata={[{ icon: Users, label: '3-4' }, { label: '90 min' }]}
              status="owned"
              showStatusIcon
            />
            <MeepleCard
              entity="player"
              variant="list"
              title="Alice Rossi"
              subtitle="42 partite · 68% vittorie"
              avatarUrl={PLAYER_IMAGE}
              rating={4.0}
              ratingMax={5}
              gamesPlayed={42}
              winRate={68}
            />
            <MeepleCard
              entity="agent"
              variant="list"
              title="RulesBot Pro"
              subtitle="GPT-4o · Regolamenti e FAQ"
              agentStatus="active"
              agentModel={{ modelName: 'gpt-4o' }}
              conversationCount={1240}
            />
          </div>

          {/* COMPACT */}
          <Label>compact — chip/pill minimale, per spazi molto ridotti</Label>
          <div className="flex flex-wrap gap-2">
            <MeepleCard entity="game" variant="compact" title="Catan" imageUrl={GAME_IMAGE} />
            <MeepleCard entity="player" variant="compact" title="Alice" avatarUrl={PLAYER_IMAGE} />
            <MeepleCard entity="agent" variant="compact" title="RulesBot" />
            <MeepleCard entity="kb" variant="compact" title="Manuale PDF" />
            <MeepleCard entity="session" variant="compact" title="Sessione #12" />
            <MeepleCard entity="event" variant="compact" title="Game Night" />
          </div>

          {/* FEATURED */}
          <Label>featured — card grande con CTA, per highlight e selezioni curate</Label>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <MeepleCard
              entity="game"
              variant="featured"
              title="I Coloni di Catan"
              subtitle="Il classico dei giochi da tavolo moderni"
              imageUrl={GAME_IMAGE}
              rating={4.2}
              ratingMax={5}
              badge="Consigliato"
              playerCountDisplay="3-4"
              playTimeDisplay="90min"
              identityChip1="Strategia"
              identityChip2="Euro"
              actions={[
                { label: 'Aggiungi', primary: true, onClick: () => alert('Aggiunto') },
                { label: 'Dettagli', onClick: () => alert('Dettagli') },
              ]}
              showHolo
            />
            <MeepleCard
              entity="event"
              variant="featured"
              title="Game Night — Aprile"
              subtitle="Venerdì 18 Aprile · ore 20:00 · Milano"
              imageUrl="https://picsum.photos/seed/gamenight/400/300"
              badge="Posti limitati"
              metadata={[{ icon: Users, label: '8 partecipanti' }, { label: '4 tavoli' }]}
              actions={[{ label: 'Partecipa', primary: true, onClick: () => alert('Partecipa') }]}
            />
          </div>

          {/* HERO */}
          <Label>hero — banner full-width, per pagine di dettaglio e landing</Label>
          <MeepleCard
            entity="game"
            variant="hero"
            title="I Coloni di Catan"
            subtitle="Klaus Teuber · 1995 · Kosmos / Asmodee"
            imageUrl={GAME_IMAGE}
            rating={4.2}
            ratingMax={5}
            badge="Hall of Fame"
            playerCountDisplay="3-4"
            playTimeDisplay="60-120min"
            identityChip1="Strategia"
            identityChip2="Euro"
            metadata={[
              { icon: Users, label: '3-4 giocatori' },
              { icon: Star, label: '4.2 / 5' },
              { label: 'Complessità media' },
            ]}
            actions={[
              { label: 'Aggiungi alla libreria', primary: true, onClick: () => alert('Aggiunto') },
              { label: 'Sfoglia regolamento', onClick: () => alert('Regolamento') },
            ]}
            showHolo
          />
        </Section>

        {/* ── 2. ENTITÀ ──────────────────────────────────────────────────────── */}
        <Section
          title="Tipi di entità"
          description="Ogni entità ha il proprio schema di colori e feature specifiche."
        >
          <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-4">
            <div>
              <Label>game — arancione</Label>
              <MeepleCard
                entity="game"
                variant="grid"
                title="Catan"
                subtitle="Strategia · 3-4p"
                imageUrl={GAME_IMAGE}
                rating={4.2}
                ratingMax={5}
                playerCountDisplay="3-4"
                playTimeDisplay="90min"
              />
            </div>
            <div>
              <Label>player — viola</Label>
              <MeepleCard
                entity="player"
                variant="grid"
                title="Alice Rossi"
                subtitle="42 partite"
                avatarUrl={PLAYER_IMAGE}
                gamesPlayed={42}
                winRate={68}
              />
            </div>
            <div>
              <Label>session — indaco</Label>
              <MeepleCard
                entity="session"
                variant="grid"
                title="Sessione #12"
                subtitle="Catan · 3 giocatori"
                imageUrl={GAME_IMAGE}
                sessionStatus="completed"
                winnerScore="10 punti"
                sessionDate="15 Apr"
              />
            </div>
            <div>
              <Label>agent — ambra</Label>
              <MeepleCard
                entity="agent"
                variant="grid"
                title="RulesBot"
                subtitle="GPT-4o · FAQ"
                agentStatus="active"
                agentModel={{ modelName: 'gpt-4o' }}
                conversationCount={1240}
                agentAccuracy={94}
              />
            </div>
            <div>
              <Label>kb — teal</Label>
              <MeepleCard
                entity="kb"
                variant="grid"
                title="Manuale Catan"
                subtitle="PDF · 48 pagine"
                documentStatus="indexed"
                pageCount={48}
                chunkCount={320}
              />
            </div>
            <div>
              <Label>chatSession — blu</Label>
              <MeepleCard
                entity="chatSession"
                variant="grid"
                title="Regole espansione"
                subtitle="Oggi · 5 messaggi"
                chatStatus="active"
                chatAgent={{ name: 'RulesBot', modelName: 'GPT-4o' }}
                chatStats={{ messageCount: 5, durationMinutes: 3 }}
                unreadCount={2}
              />
            </div>
            <div>
              <Label>event — rose</Label>
              <MeepleCard
                entity="event"
                variant="grid"
                title="Game Night"
                subtitle="Ven 18 Apr · 20:00"
                metadata={[{ icon: Users, label: '8 iscritti' }]}
              />
            </div>
            <div>
              <Label>toolkit — verde</Label>
              <MeepleCard
                entity="toolkit"
                variant="grid"
                title="Kit Strategia"
                subtitle="12 strumenti"
                metadata={[{ label: '12 tools' }]}
              />
            </div>
          </div>
        </Section>

        {/* ── 3. FEATURES INTERATTIVE ────────────────────────────────────── */}
        <Section
          title="Features interattive"
          description="Ogni feature è opt-in tramite prop dedicato."
        >
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-8">
            {/* Wishlist */}
            <div>
              <Label>wishlist — cuore toggle</Label>
              <MeepleCard
                entity="game"
                variant="grid"
                title="Pandemic"
                subtitle="Clicca il cuore →"
                imageUrl="https://picsum.photos/seed/pandemic/400/300"
                showWishlist
                isWishlisted={wishlisted}
                onWishlistToggle={(_, v) => setWishlisted(v)}
              />
              <p className="text-xs text-muted-foreground mt-2">
                Stato: {wishlisted ? '❤️ Nella wishlist' : '🤍 Non in wishlist'}
              </p>
            </div>

            {/* Status badges */}
            <div>
              <Label>status — badge di stato multipli</Label>
              <div className="flex flex-col gap-2">
                <MeepleCard
                  entity="game"
                  variant="list"
                  title="Owned + Played"
                  status={['owned', 'played']}
                  showStatusIcon
                  imageUrl={GAME_IMAGE}
                />
                <MeepleCard
                  entity="game"
                  variant="list"
                  title="For Trade"
                  status="for-trade"
                  showStatusIcon
                  imageUrl="https://picsum.photos/seed/wingspan/400/300"
                />
                <MeepleCard
                  entity="game"
                  variant="list"
                  title="Wishlisted"
                  status="wishlisted"
                  showStatusIcon
                  imageUrl="https://picsum.photos/seed/pandemic/400/300"
                />
              </div>
            </div>

            {/* Selectable */}
            <div>
              <Label>selectable — selezione bulk</Label>
              <MeepleCard
                entity="game"
                variant="grid"
                title="Clicca per selezionare"
                subtitle="Modalità selezione multipla"
                imageUrl={GAME_IMAGE}
                selectable
                selected={selected}
                onSelect={(_, v) => setSelected(v)}
              />
              <p className="text-xs text-muted-foreground mt-2">
                Stato: {selected ? '✅ Selezionata' : '⬜ Non selezionata'}
              </p>
            </div>

            {/* Quick actions */}
            <div>
              <Label>quickActions — menu contestuale hover</Label>
              <MeepleCard
                entity="game"
                variant="grid"
                title="Hover per azioni"
                subtitle="Menu contestuale ···"
                imageUrl={GAME_IMAGE}
                rating={4.0}
                ratingMax={5}
                quickActions={QUICK_ACTIONS}
                userRole="admin"
              />
            </div>

            {/* Tags */}
            <div>
              <Label>tags — strip verticale sinistra</Label>
              <MeepleCard
                entity="game"
                variant="grid"
                title="Con tag strip"
                subtitle="Etichette visive"
                imageUrl={GAME_IMAGE}
                tags={['new', 'sale', 'rag']}
                showTagStrip
              />
            </div>

            {/* Flip */}
            <div>
              <Label>flippable — flip 3D retro carta</Label>
              <MeepleCard
                entity="game"
                variant="grid"
                title="Clicca per girare"
                subtitle="Flip 3D attivo"
                imageUrl={GAME_IMAGE}
                rating={4.2}
                ratingMax={5}
                flippable
                flipTrigger="card"
                isFlipped={flipped}
                onFlip={setFlipped}
                flipData={{
                  description:
                    'I giocatori costruiscono insediamenti, città e strade raccogliendo risorse.',
                  complexityRating: 2.3,
                  designers: [{ id: '1', name: 'Klaus Teuber' }],
                  mechanics: [
                    { id: '1', name: 'Trading' },
                    { id: '2', name: 'Dice Rolling' },
                  ],
                  categories: [
                    { id: '1', name: 'Strategia' },
                    { id: '2', name: 'Famiglia' },
                  ],
                }}
              />
              <p className="text-xs text-muted-foreground mt-2">
                {flipped ? '🔄 Retro visibile' : '▶️ Clicca la carta'}
              </p>
            </div>
          </div>
        </Section>

        {/* ── 4. STATI DI CARICAMENTO ────────────────────────────────────── */}
        <Section
          title="Stati di caricamento"
          description="Skeleton per ogni variante durante il fetch dei dati."
        >
          <Label>skeleton — grid</Label>
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4">
            {Array.from({ length: 4 }).map((_, i) => (
              <MeepleCardSkeleton key={i} variant="grid" />
            ))}
          </div>
          <Label>skeleton — list</Label>
          <div className="flex flex-col gap-2">
            {Array.from({ length: 3 }).map((_, i) => (
              <MeepleCardSkeleton key={i} variant="list" />
            ))}
          </div>
        </Section>

        {/* ── 5. COVER OVERLAY ──────────────────────────────────────────── */}
        <Section
          title="Cover overlay — sistema a 4 angoli"
          description="Label top-left, icone bottom-left, badge bottom-right sovrapposti all'immagine."
        >
          <div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
            <MeepleCard
              entity="game"
              variant="grid"
              title="Con cover overlay"
              imageUrl={GAME_IMAGE}
              coverLabels={[{ text: 'NEW', primary: true }, { text: 'Edizione 2024' }]}
              subtypeIcons={[
                { icon: '🏆', tooltip: 'Premio Spiel des Jahres' },
                { icon: '⚔️', tooltip: 'Conflitto' },
              ]}
              stateLabel={{ text: 'In stock', variant: 'success' }}
            />
            <MeepleCard
              entity="game"
              variant="grid"
              title="Warning state"
              imageUrl="https://picsum.photos/seed/7wonders/400/300"
              stateLabel={{ text: 'Ultimi pezzi', variant: 'warning' }}
              coverLabels={[{ text: 'LIMITED', primary: true }]}
            />
            <MeepleCard
              entity="kb"
              variant="grid"
              title="Documento in errore"
              imageUrl="https://picsum.photos/seed/doc/400/300"
              stateLabel={{ text: 'Errore OCR', variant: 'error' }}
              documentStatus="failed"
            />
          </div>
        </Section>

        {/* ── 6. ENTITÀ AGENTE ──────────────────────────────────────────── */}
        <Section
          title="Entità: Agente AI"
          description="Card specializzate con status, modello, statistiche e capabilities."
        >
          <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-4">
            <MeepleCard
              entity="agent"
              variant="grid"
              title="RulesBot Pro"
              subtitle="Regolamenti e FAQ"
              agentStatus="active"
              agentModel={{ modelName: 'claude-sonnet-4-6', parameters: { temperature: 0.3 } }}
              agentStats={{ invocationCount: 1240, avgResponseTimeMs: 1200 }}
              conversationCount={1240}
              agentAccuracy={97}
              capabilities={['rag', 'vision']}
            />
            <MeepleCard
              entity="agent"
              variant="grid"
              title="SessionMaster"
              subtitle="Tracciamento partite"
              agentStatus="idle"
              agentModel={{ modelName: 'gpt-4o-mini' }}
              conversationCount={340}
              agentAccuracy={88}
            />
            <MeepleCard
              entity="agent"
              variant="grid"
              title="TrainingBot"
              subtitle="In addestramento…"
              agentStatus="training"
              agentModel={{ modelName: 'llama3:8b' }}
              conversationCount={12}
            />
          </div>
        </Section>

        {/* ── 7. ENTITY LINK & MANA ─────────────────────────────────────── */}
        <Section
          title="Entity Links & Mana footer"
          description="Badge con conteggio link verso altre entità + footer Mana con pip colorati."
        >
          <div className="grid grid-cols-2 sm:grid-cols-3 gap-4">
            <MeepleCard
              entity="game"
              variant="grid"
              title="Catan con links"
              imageUrl={GAME_IMAGE}
              linkCount={3}
              firstLinkPreview={{ linkType: 'ExpansionOf', targetName: 'Catan: Seafarers' }}
              onLinksClick={() => alert('Apri pannello links')}
              rating={4.2}
              ratingMax={5}
            />
            <MeepleCard
              entity="game"
              variant="grid"
              title="Con azione primaria"
              imageUrl="https://picsum.photos/seed/pandemic/400/300"
              rating={4.5}
              ratingMax={5}
              primaryActions={[
                { label: 'Avvia chat', icon: '💬', onClick: () => alert('Chat aperta') },
              ]}
            />
            <MeepleCard
              entity="game"
              variant="grid"
              title="Con agent footer"
              imageUrl="https://picsum.photos/seed/wingspan/400/300"
              rating={4.8}
              ratingMax={5}
              hasAgent={false}
              hasKb={false}
              onCreateAgent={() => alert('Crea agente')}
              onAddToCollection={() => alert('Aggiungi a libreria')}
            />
          </div>
        </Section>
      </div>

      {/* Footer */}
      <div className="border-t bg-card px-6 py-6 text-center text-xs text-muted-foreground">
        <code>/dev/meeple-card</code> · Solo in development · Non incluso nella build di produzione
      </div>
    </div>
  );
}
