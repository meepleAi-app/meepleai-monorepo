'use client';

import { FileDown, MessageSquare, Pin } from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';

// Reusable quick actions for chatSession (mirrors use-entity-actions.ts)
const chatQuickActions = [
  { icon: Pin, label: 'Fissa', onClick: () => {} },
  { icon: MessageSquare, label: 'Continua Chat', onClick: () => {} },
  { icon: FileDown, label: 'Esporta', onClick: () => {} },
];

// Flip data adapted for chat context (no description — keeps card compact)
const chatFlipData = {
  categories: [
    { id: '1', name: 'Regole' },
    { id: '2', name: 'Strategia' },
    { id: '3', name: 'FAQ' },
  ],
  mechanics: [
    { id: '1', name: 'AI Chat' },
    { id: '2', name: 'RAG' },
    { id: '3', name: 'Multi-turn' },
  ],
};

export default function ChatCardPreview() {
  return (
    <div className="min-h-screen bg-background p-8">
      <h1 className="text-2xl font-bold mb-6">ChatSession MeepleCard Preview</h1>

      {/* Grid variant demos */}
      <section className="mb-10">
        <h2 className="text-lg font-semibold mb-4">Grid Variant</h2>
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-6">
          {/* Active chat */}
          <MeepleCard
            entity="chatSession"
            variant="grid"
            title="Catan Rules Help"
            subtitle="How to trade resources"
            imageUrl="https://placehold.co/400x560/3b82f6/white?text=Chat"
            chatStatus="active"
            chatAgent={{ name: 'Rules Bot', modelName: 'GPT-4o-mini' }}
            chatGame={{ name: 'Catan', id: 'game-123' }}
            chatStats={{ messageCount: 42, lastMessageAt: new Date(Date.now() - 5 * 60 * 1000), durationMinutes: 23 }}
            chatPreview={{ lastMessage: 'You can trade during your turn with any player.', sender: 'agent' }}
            unreadCount={3}
            entityQuickActions={chatQuickActions}
            flippable
            flipData={chatFlipData}
            detailHref="/chat/chat-001"
          />

          {/* Waiting chat */}
          <MeepleCard
            entity="chatSession"
            variant="grid"
            title="Twilight Imperium Strategy"
            subtitle="Fleet composition advice"
            imageUrl="https://placehold.co/400x560/eab308/white?text=Chat"
            chatStatus="waiting"
            chatAgent={{ name: 'Strategy AI', modelName: 'Claude 3.5 Sonnet' }}
            chatGame={{ name: 'Twilight Imperium' }}
            chatStats={{ messageCount: 128, durationMinutes: 83 }}
            chatPreview={{ lastMessage: 'What fleet should I build for round 3?', sender: 'user' }}
            unreadCount={0}
            entityQuickActions={chatQuickActions}
            flippable
            flipData={{
              categories: [
                { id: '1', name: 'Strategia' },
                { id: '2', name: 'Flotta' },
              ],
              mechanics: [
                { id: '1', name: 'AI Chat' },
                { id: '2', name: 'Deep Analysis' },
              ],
            }}
            detailHref="/chat/chat-002"
          />

          {/* Archived chat */}
          <MeepleCard
            entity="chatSession"
            variant="grid"
            title="Gloomhaven Scenario 5"
            subtitle="Completed walkthrough"
            imageUrl="https://placehold.co/400x560/6b7280/white?text=Chat"
            chatStatus="archived"
            chatAgent={{ name: 'Dungeon Guide', modelName: 'Gemini Pro' }}
            chatStats={{ messageCount: 256, durationMinutes: 120 }}
            entityQuickActions={chatQuickActions}
            flippable
            flipData={chatFlipData}
            detailHref="/chat/chat-001"
          />

          {/* Closed, no image */}
          <MeepleCard
            entity="chatSession"
            variant="grid"
            title="Quick Rules Check"
            subtitle="Wingspan egg rules"
            chatStatus="closed"
            chatStats={{ messageCount: 8, durationMinutes: 3 }}
            entityQuickActions={chatQuickActions}
            flippable
            flipData={{
              categories: [{ id: '1', name: 'Regole' }],
              mechanics: [{ id: '1', name: 'AI Chat' }],
            }}
            detailHref="/chat/chat-004"
          />
        </div>
      </section>

      {/* List variant demos */}
      <section className="mb-10">
        <h2 className="text-lg font-semibold mb-4">List Variant</h2>
        <div className="flex flex-col gap-3 max-w-2xl">
          <MeepleCard
            entity="chatSession"
            variant="list"
            title="Catan Rules Help"
            subtitle="Active conversation"
            chatStatus="active"
            chatAgent={{ name: 'Rules Bot', modelName: 'GPT-4o-mini' }}
            chatGame={{ name: 'Catan', id: 'game-123' }}
            chatStats={{ messageCount: 42 }}
            unreadCount={7}
            entityQuickActions={chatQuickActions}
          />
          <MeepleCard
            entity="chatSession"
            variant="list"
            title="Spirit Island Combo Guide"
            subtitle="Waiting for response"
            chatStatus="waiting"
            chatAgent={{ name: 'Combo AI', modelName: 'Claude 3.5 Sonnet' }}
            chatStats={{ messageCount: 15 }}
            entityQuickActions={chatQuickActions}
          />
          <MeepleCard
            entity="chatSession"
            variant="list"
            title="Old Chess Analysis"
            subtitle="Archived session"
            chatStatus="archived"
            chatStats={{ messageCount: 200 }}
            entityQuickActions={chatQuickActions}
          />
        </div>
      </section>

      {/* Compact variant */}
      <section className="mb-10">
        <h2 className="text-lg font-semibold mb-4">Compact Variant (no chat info section)</h2>
        <div className="flex flex-col gap-2 max-w-md">
          <MeepleCard
            entity="chatSession"
            variant="compact"
            title="Catan Rules Help"
            subtitle="42 messages"
            chatStatus="active"
            unreadCount={3}
            entityQuickActions={chatQuickActions}
          />
          <MeepleCard
            entity="chatSession"
            variant="compact"
            title="TI4 Strategy"
            subtitle="128 messages"
            chatStatus="waiting"
            entityQuickActions={chatQuickActions}
          />
        </div>
      </section>

      {/* Featured variant */}
      <section className="mb-10">
        <h2 className="text-lg font-semibold mb-4">Featured Variant</h2>
        <div className="max-w-lg">
          <MeepleCard
            entity="chatSession"
            variant="featured"
            title="Epic Gloomhaven Campaign"
            subtitle="Complete walkthrough with AI dungeon master"
            imageUrl="https://placehold.co/800x450/3b82f6/white?text=Featured+Chat"
            chatStatus="active"
            chatAgent={{ name: 'Dungeon Master AI', modelName: 'GPT-4o' }}
            chatGame={{ name: 'Gloomhaven', id: 'game-456' }}
            chatStats={{ messageCount: 1500, lastMessageAt: new Date(Date.now() - 30 * 60 * 1000), durationMinutes: 480 }}
            chatPreview={{ lastMessage: 'The Brute takes 3 damage from the trap!', sender: 'agent' }}
            unreadCount={12}
            entityQuickActions={chatQuickActions}
            flippable
            flipData={{
              categories: [
                { id: '1', name: 'Campagna' },
                { id: '2', name: 'Walkthrough' },
                { id: '3', name: 'Tattica' },
              ],
              mechanics: [
                { id: '1', name: 'AI DM' },
                { id: '2', name: 'Multi-turn' },
                { id: '3', name: 'Context Memory' },
              ],
            }}
            detailHref="/chat/chat-005"
          />
        </div>
      </section>

      {/* Unread badge edge cases */}
      <section className="mb-10">
        <h2 className="text-lg font-semibold mb-4">Unread Badge Edge Cases</h2>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-6 max-w-3xl">
          <MeepleCard
            entity="chatSession"
            variant="grid"
            title="1 unread"
            chatStatus="active"
            chatStats={{ messageCount: 5 }}
            unreadCount={1}
            entityQuickActions={chatQuickActions}
          />
          <MeepleCard
            entity="chatSession"
            variant="grid"
            title="99 unread"
            chatStatus="active"
            chatStats={{ messageCount: 200 }}
            unreadCount={99}
            entityQuickActions={chatQuickActions}
          />
          <MeepleCard
            entity="chatSession"
            variant="grid"
            title="99+ unread"
            chatStatus="active"
            chatStats={{ messageCount: 500 }}
            unreadCount={150}
            entityQuickActions={chatQuickActions}
          />
        </div>
      </section>
    </div>
  );
}
