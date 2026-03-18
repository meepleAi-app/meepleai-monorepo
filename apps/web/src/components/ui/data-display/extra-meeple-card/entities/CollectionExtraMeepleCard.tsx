'use client';

/**
 * CollectionExtraMeepleCard — expanded card for Collection entities
 * Issue #4762 - ExtraMeepleCard: Media Tab + AI Tab + Other Entity Types
 */

import React, { useState } from 'react';

import { Gamepad2, Library, Share2 } from 'lucide-react';

import { Tabs, TabsList, TabsContent } from '@/components/ui/navigation/tabs';
import { cn } from '@/lib/utils';

import {
  ENTITY_COLORS,
  EntityHeader,
  EntityTabTrigger,
  StatCard,
  EntityLoadingState,
  EntityErrorState,
} from '../shared';

import type { CollectionDetailData } from '../types';

// ============================================================================
// Types
// ============================================================================

export interface CollectionExtraMeepleCardProps {
  data: CollectionDetailData;
  loading?: boolean;
  error?: string;
  className?: string;
  'data-testid'?: string;
}

type CollectionTab = 'overview' | 'games';

// ============================================================================
// CollectionExtraMeepleCard
// ============================================================================

export const CollectionExtraMeepleCard = React.memo(function CollectionExtraMeepleCard({
  data,
  loading,
  error,
  className,
  'data-testid': testId,
}: CollectionExtraMeepleCardProps) {
  const [activeTab, setActiveTab] = useState<CollectionTab>('overview');
  const colors = ENTITY_COLORS.collection;

  if (loading) return <EntityLoadingState className={className} testId={testId} />;
  if (error) return <EntityErrorState error={error} className={className} testId={testId} />;

  return (
    <div
      className={cn(
        'flex w-[600px] flex-col rounded-2xl overflow-hidden',
        'bg-white/70 backdrop-blur-md shadow-[0_8px_32px_rgba(0,0,0,0.12)] border border-white/20',
        'max-md:w-full',
        className
      )}
      data-testid={testId}
    >
      {/* Header */}
      <EntityHeader
        title={data.name}
        subtitle={`by ${data.ownerName}`}
        imageUrl={data.imageUrl}
        color={colors.hsl}
        badge={data.gameCount.toString()}
        badgeIcon={<Gamepad2 className="h-3 w-3" />}
      />

      {/* Tabs */}
      <Tabs
        value={activeTab}
        onValueChange={v => setActiveTab(v as CollectionTab)}
        className="flex flex-1 flex-col"
      >
        <TabsList className="mx-4 mt-3 h-10 w-auto justify-start gap-1 bg-slate-100/80 rounded-lg p-1">
          <EntityTabTrigger
            value="overview"
            icon={Library}
            label="Overview"
            activeAccent={colors.activeAccent}
          />
          <EntityTabTrigger
            value="games"
            icon={Gamepad2}
            label={`Games (${data.gameCount})`}
            activeAccent={colors.activeAccent}
          />
        </TabsList>

        <div className="flex-1 overflow-y-auto px-4 py-3">
          <TabsContent value="overview" className="mt-0">
            <div className="space-y-3">
              <div className="grid grid-cols-2 gap-2">
                <StatCard
                  label="Games"
                  value={data.gameCount.toString()}
                  icon={Gamepad2}
                  variant="collection"
                />
                <StatCard
                  label="Shared"
                  value={data.isShared ? 'Yes' : 'No'}
                  icon={Share2}
                  variant="collection"
                />
              </div>
              {data.description && (
                <p className="font-nunito text-xs text-slate-600 leading-relaxed">
                  {data.description}
                </p>
              )}
            </div>
          </TabsContent>

          <TabsContent value="games" className="mt-0">
            <div className="space-y-2">
              {data.games.length === 0 ? (
                <p className="font-nunito text-xs text-slate-400 text-center py-8">
                  No games in collection
                </p>
              ) : (
                data.games.map(g => (
                  <div
                    key={g.id}
                    className="flex items-center gap-3 rounded-lg bg-white/50 border border-slate-200/40 p-2.5"
                  >
                    {g.imageUrl ? (
                      <img
                        src={g.imageUrl}
                        alt={g.title}
                        className="h-10 w-10 rounded object-cover"
                      />
                    ) : (
                      <div className="flex h-10 w-10 items-center justify-center rounded bg-teal-50">
                        <Gamepad2 className="h-5 w-5 text-teal-400" />
                      </div>
                    )}
                    <p className="font-nunito text-xs font-medium text-slate-700">{g.title}</p>
                  </div>
                ))
              )}
            </div>
          </TabsContent>
        </div>
      </Tabs>
    </div>
  );
});
