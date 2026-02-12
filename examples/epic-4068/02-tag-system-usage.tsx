/**
 * Example 2: Tag System Usage
 * Epic #4068 - Issue #4181, #4182
 *
 * Demonstrates vertical tag system with presets and custom tags
 */

import React, { useState } from 'react';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { TagStrip } from '@/components/ui/tags/TagStrip';
import { createTagsFromKeys, sortTagsByPriority } from '@/lib/tags/utils';
import { GAME_TAG_PRESETS, AGENT_TAG_PRESETS } from '@/lib/tags/presets';
import { Sparkles, Tag as TagIcon, Check, Heart, Brain, Eye, Code2 } from 'lucide-react';
import type { Tag } from '@/types/tags';

export function TagSystemExample() {
  const [maxVisibleTags, setMaxVisibleTags] = useState(3);
  const [tagPosition, setTagPosition] = useState<'left' | 'right'>('left');

  // Game tags using presets
  const gameTags = createTagsFromKeys('game', ['new', 'sale', 'owned', 'wishlisted', 'exclusive']);

  // Agent tags using presets
  const agentTags = createTagsFromKeys('agent', ['rag', 'vision', 'code']);

  // Custom tags (manual creation)
  const customTags: Tag[] = [
    {
      id: 'limited',
      label: 'Limited Edition',
      icon: Sparkles,
      bgColor: 'hsl(280 70% 50%)',
      color: 'hsl(0 0% 100%)',
      tooltip: 'Limited quantity available'
    },
    {
      id: 'preorder',
      label: 'Pre-order',
      icon: Clock,
      bgColor: 'hsl(38 92% 50%)',
      color: 'hsl(0 0% 100%)',
      tooltip: 'Available for pre-order'
    }
  ];

  return (
    <div className="space-y-8 p-8">
      <section>
        <h1 className="text-2xl font-bold mb-4">Tag System Examples</h1>

        <div className="flex gap-4 mb-4">
          <div>
            <label className="text-sm">Max Visible Tags:</label>
            <input
              type="number"
              min={1}
              max={5}
              value={maxVisibleTags}
              onChange={(e) => setMaxVisibleTags(Number(e.target.value))}
              className="ml-2 px-2 py-1 border rounded"
            />
          </div>

          <div>
            <label className="text-sm">Position:</label>
            <select
              value={tagPosition}
              onChange={(e) => setTagPosition(e.target.value as 'left' | 'right')}
              className="ml-2 px-2 py-1 border rounded"
            >
              <option value="left">Left</option>
              <option value="right">Right</option>
            </select>
          </div>
        </div>
      </section>

      <section>
        <h2 className="text-xl font-semibold mb-4">Game Tags (Using Presets)</h2>

        <div className="grid grid-cols-3 gap-4">
          <MeepleCard
            entity="game"
            variant="grid"
            title="Wingspan"
            subtitle="Stonemaier Games"
            imageUrl="/games/wingspan.jpg"
            tags={gameTags}
            maxVisibleTags={maxVisibleTags}
            tagPosition={tagPosition}
          />

          <MeepleCard
            entity="game"
            variant="grid"
            title="Azul"
            subtitle="Plan B Games"
            imageUrl="/games/azul.jpg"
            tags={gameTags.slice(0, 2)} // Only 2 tags
            maxVisibleTags={maxVisibleTags}
          />

          <MeepleCard
            entity="game"
            variant="grid"
            title="Catan"
            subtitle="Catan Studio"
            imageUrl="/games/catan.jpg"
            tags={sortTagsByPriority(gameTags, 'game')} // Sorted by priority
            maxVisibleTags={maxVisibleTags}
          />
        </div>

        <p className="mt-4 text-sm text-muted-foreground">
          Tags: New (green), Sale (red), Owned (blue), Wishlist (rose), Exclusive (purple)
        </p>
      </section>

      <section>
        <h2 className="text-xl font-semibold mb-4">Agent Tags (Capabilities)</h2>

        <div className="grid grid-cols-3 gap-4">
          <MeepleCard
            entity="agent"
            variant="grid"
            title="Rules Expert"
            subtitle="RAG Strategy"
            tags={agentTags}
            maxVisibleTags={3}
            agentMetadata={{
              status: 'active',
              model: { name: 'GPT-4o-mini', temperature: 0.7 },
              invocationCount: 342,
              capabilities: ['RAG', 'Vision', 'Code']
            }}
          />

          <MeepleCard
            entity="agent"
            variant="grid"
            title="Strategy Analyzer"
            subtitle="Analysis Agent"
            tags={createTagsFromKeys('agent', ['rag', 'code'])}
            maxVisibleTags={3}
          />
        </div>

        <p className="mt-4 text-sm text-muted-foreground">
          Agent capability tags: RAG (amber), Vision (purple), Code (slate)
        </p>
      </section>

      <section>
        <h2 className="text-xl font-semibold mb-4">Custom Tags</h2>

        <MeepleCard
          entity="game"
          variant="featured"
          title="Limited Edition Board Game"
          subtitle="Exclusive Release"
          imageUrl="/games/limited.jpg"
          tags={customTags}
          maxVisibleTags={2}
        />

        <p className="mt-4 text-sm text-muted-foreground">
          Custom tags with manual color/icon configuration
        </p>
      </section>

      <section>
        <h2 className="text-xl font-semibold mb-4">Tag Overflow Behavior</h2>

        <div className="grid grid-cols-2 gap-4">
          <div>
            <h3 className="text-lg font-medium mb-2">Max 3 Visible</h3>
            <MeepleCard
              entity="game"
              variant="grid"
              title="Game with Many Tags"
              tags={gameTags}
              maxVisibleTags={3}
            />
            <p className="mt-2 text-sm text-muted-foreground">
              Shows 3 tags + "+2" overflow badge. Hover "+2" to see hidden tags.
            </p>
          </div>

          <div>
            <h3 className="text-lg font-medium mb-2">Max 2 Visible</h3>
            <MeepleCard
              entity="game"
              variant="grid"
              title="Game with Many Tags"
              tags={gameTags}
              maxVisibleTags={2}
            />
            <p className="mt-2 text-sm text-muted-foreground">
              Shows 2 tags + "+3" overflow badge.
            </p>
          </div>
        </div>
      </section>

      <section>
        <h2 className="text-xl font-semibold mb-4">Responsive Tag Variants</h2>

        <div className="space-y-4">
          <div>
            <h3 className="text-lg font-medium mb-2">Desktop (32px strip, full labels)</h3>
            <div className="w-[300px]">
              <MeepleCard
                entity="game"
                variant="grid"
                title="Desktop View"
                tags={gameTags.slice(0, 3)}
                maxVisibleTags={3}
              />
            </div>
          </div>

          <div>
            <h3 className="text-lg font-medium mb-2">Tablet (28px strip, abbreviated)</h3>
            <div className="w-[250px]">
              <MeepleCard
                entity="game"
                variant="grid"
                title="Tablet View"
                tags={gameTags.slice(0, 3)}
                maxVisibleTags={3}
              />
            </div>
          </div>

          <div>
            <h3 className="text-lg font-medium mb-2">Mobile (24px strip, icon-only)</h3>
            <div className="w-[200px]">
              <MeepleCard
                entity="game"
                variant="grid"
                title="Mobile View"
                tags={gameTags.slice(0, 3)}
                maxVisibleTags={2}
              />
            </div>
          </div>
        </div>
      </section>

      <section>
        <h2 className="text-xl font-semibold mb-4">Tag Presets Reference</h2>

        <div>
          <h3 className="text-lg font-medium mb-2">Game Tag Presets</h3>
          <div className="flex flex-wrap gap-2">
            {Object.entries(GAME_TAG_PRESETS).map(([key, preset]) => {
              const Icon = preset.icon;
              return (
                <div
                  key={key}
                  className="flex items-center gap-1 px-2 py-1 rounded text-xs font-medium"
                  style={{ backgroundColor: preset.bgColor, color: preset.color }}
                >
                  {Icon && <Icon className="w-3 h-3" />}
                  <span>{preset.label}</span>
                </div>
              );
            })}
          </div>
        </div>

        <div className="mt-4">
          <h3 className="text-lg font-medium mb-2">Agent Tag Presets</h3>
          <div className="flex flex-wrap gap-2">
            {Object.entries(AGENT_TAG_PRESETS).map(([key, preset]) => {
              const Icon = preset.icon;
              return (
                <div
                  key={key}
                  className="flex items-center gap-1 px-2 py-1 rounded text-xs font-medium"
                  style={{ backgroundColor: preset.bgColor, color: preset.color }}
                >
                  {Icon && <Icon className="w-3 h-3" />}
                  <span>{preset.label}</span>
                </div>
              );
            })}
          </div>
        </div>
      </section>

      <section>
        <h2 className="text-xl font-semibold mb-4">Code Examples</h2>

        <div className="space-y-4">
          <div>
            <h3 className="text-lg font-medium mb-2">Using createTagsFromKeys</h3>
            <pre className="bg-muted p-4 rounded text-sm overflow-auto">
{`import { createTagsFromKeys } from '@/lib/tags/utils';

const tags = createTagsFromKeys('game', ['new', 'sale', 'owned']);

<MeepleCard entity="game" title="Wingspan" tags={tags} />`}
            </pre>
          </div>

          <div>
            <h3 className="text-lg font-medium mb-2">Custom Tags</h3>
            <pre className="bg-muted p-4 rounded text-sm overflow-auto">
{`import { Sparkles } from 'lucide-react';

const customTag: Tag = {
  id: 'limited',
  label: 'Limited Edition',
  icon: Sparkles,
  bgColor: 'hsl(280 70% 50%)',
  color: 'hsl(0 0% 100%)',
  tooltip: 'Limited quantity'
};

<MeepleCard tags={[customTag]} />`}
            </pre>
          </div>

          <div>
            <h3 className="text-lg font-medium mb-2">Sorting by Priority</h3>
            <pre className="bg-muted p-4 rounded text-sm overflow-auto">
{`import { sortTagsByPriority } from '@/lib/tags/utils';

const unsorted = [...tags];
const sorted = sortTagsByPriority(unsorted, 'game');

// Order: exclusive, new, preorder, sale, owned, wishlisted
<MeepleCard tags={sorted} />`}
            </pre>
          </div>
        </div>
      </section>
    </div>
  );
}

export default TagSystemExample;
