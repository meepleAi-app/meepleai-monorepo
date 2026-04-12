'use client';

import React, { useState } from 'react';

import {
  Clock3,
  CreditCard,
  Dices,
  ExternalLink,
  Layers,
  Pencil,
  Play,
  Wrench,
} from 'lucide-react';
import { useRouter } from 'next/navigation';

import { Tabs, TabsList, TabsContent } from '@/components/ui/navigation/tabs';

import { DrawerLoadingSkeleton, DrawerErrorState } from '../drawer-states';
import { DrawerActionFooter } from '../DrawerActionFooter';
import { useToolkitDetail } from '../hooks';
import { ENTITY_COLORS, EntityHeader, EntityTabTrigger, StatCard } from '../shared';

import type { DrawerAction } from '../DrawerActionFooter';

// ============================================================================
// ToolkitDrawerContent — drawer-specific toolkit detail view
// ============================================================================

interface ToolkitDrawerContentProps {
  entityId: string;
}

type ToolkitTab = 'overview' | 'template' | 'storico';

export function ToolkitDrawerContent({ entityId }: ToolkitDrawerContentProps) {
  const { data, loading, error, retry } = useToolkitDetail(entityId);
  const [activeTab, setActiveTab] = useState<ToolkitTab>('overview');
  const router = useRouter();
  const colors = ENTITY_COLORS.toolkit;

  if (loading) return <DrawerLoadingSkeleton />;
  if (error) return <DrawerErrorState error={error} onRetry={retry} />;
  if (!data) return <DrawerLoadingSkeleton />;

  const footerActions: DrawerAction[] = [
    ...(data.isPublished
      ? [
          {
            icon: Play,
            label: 'Usa in sessione',
            onClick: () => router.push(`/sessions/new?toolkitId=${entityId}`),
            variant: 'secondary' as const,
            enabled: true,
          },
        ]
      : []),
    ...(data.isOwner
      ? [
          {
            icon: Pencil,
            label: 'Modifica',
            onClick: () => router.push(`/toolkits/${entityId}/edit`),
            variant: 'secondary' as const,
            enabled: true,
          },
        ]
      : []),
    {
      icon: ExternalLink,
      label: 'Apri',
      onClick: () => router.push(`/toolkits/${entityId}`),
      variant: 'primary' as const,
      enabled: true,
    },
  ];

  const allTools = [
    ...data.diceTools.map(t => ({
      name: t.name,
      config: `Dado ${t.diceType} ×${t.quantity}`,
      category: 'Dadi',
    })),
    ...data.cardTools.map(t => ({
      name: t.name,
      config: `${t.deckType} (${t.cardCount} carte)`,
      category: 'Carte',
    })),
    ...data.timerTools.map(t => ({
      name: t.name,
      config: `${t.timerType} – ${t.durationSeconds}s`,
      category: 'Timer',
    })),
    ...data.counterTools.map(t => ({
      name: t.name,
      config: `${t.defaultValue} [${t.minValue}–${t.maxValue}]`,
      category: 'Contatori',
    })),
  ];

  return (
    <div className="flex flex-1 flex-col">
      <EntityHeader
        title={data.name}
        imageUrl={undefined}
        color={colors.hsl}
        badge={data.isPublished ? `v${data.version}` : 'Bozza'}
        badgeIcon={<Layers className="h-3 w-3" />}
      />

      <Tabs
        value={activeTab}
        onValueChange={v => setActiveTab(v as ToolkitTab)}
        className="flex flex-1 flex-col"
      >
        <TabsList className="mx-4 mt-3 h-10 w-auto justify-start gap-1 bg-slate-100/80 rounded-lg p-1">
          <EntityTabTrigger
            value="overview"
            icon={Wrench}
            label="Overview"
            activeAccent={colors.activeAccent}
          />
          <EntityTabTrigger
            value="template"
            icon={Layers}
            label="Template"
            activeAccent={colors.activeAccent}
          />
          <EntityTabTrigger
            value="storico"
            icon={Clock3}
            label="Storico"
            activeAccent={colors.activeAccent}
          />
        </TabsList>

        <div className="flex-1 overflow-y-auto px-4 py-3">
          {/* Overview tab */}
          <TabsContent value="overview" className="mt-0">
            <div className="space-y-3">
              {/* Game link row */}
              {data.gameName && (
                <div className="rounded-lg bg-rose-50/50 border border-rose-200/40 p-3">
                  <p className="font-nunito text-[10px] text-rose-500 uppercase tracking-wider">
                    Gioco
                  </p>
                  <p className="font-quicksand text-sm font-bold text-rose-700">{data.gameName}</p>
                </div>
              )}

              {/* Tool summary — 2×2 grid */}
              <div className="grid grid-cols-2 gap-2">
                <StatCard
                  label="Dadi"
                  value={data.diceTools.length.toString()}
                  icon={Dices}
                  variant="game"
                />
                <StatCard
                  label="Carte"
                  value={data.cardTools.length.toString()}
                  icon={CreditCard}
                  variant="game"
                />
                <StatCard
                  label="Timer"
                  value={data.timerTools.length.toString()}
                  icon={Clock3}
                  variant="game"
                />
                <StatCard
                  label="Contatori"
                  value={data.counterTools.length.toString()}
                  icon={Wrench}
                  variant="game"
                />
              </div>

              {/* Published badge */}
              <div className="flex items-center gap-2">
                <span
                  className={
                    data.isPublished
                      ? 'inline-flex items-center rounded-full bg-green-100 px-2.5 py-1 font-nunito text-xs font-bold text-green-700'
                      : 'inline-flex items-center rounded-full bg-slate-100 px-2.5 py-1 font-nunito text-xs font-bold text-slate-600'
                  }
                >
                  {data.isPublished ? 'Pubblicato' : 'Bozza'}
                </span>
              </div>

              {/* Description */}
              {data.description && (
                <p className="font-nunito text-xs text-slate-600 leading-relaxed">
                  {data.description}
                </p>
              )}
            </div>
          </TabsContent>

          {/* Template tab — list of all tools */}
          <TabsContent value="template" className="mt-0">
            <div className="space-y-2">
              {allTools.length === 0 ? (
                <p className="font-nunito text-xs text-slate-400 text-center py-8">
                  Nessuno strumento configurato
                </p>
              ) : (
                allTools.map((tool, idx) => (
                  <div
                    key={idx}
                    className="flex items-center justify-between rounded-lg bg-white/50 border border-slate-200/40 p-2.5"
                  >
                    <div className="min-w-0">
                      <p className="font-nunito text-xs font-medium text-slate-700 truncate">
                        {tool.name}
                      </p>
                      <p className="font-nunito text-[10px] text-slate-400">{tool.config}</p>
                    </div>
                    <span className="ml-2 shrink-0 rounded-full bg-slate-100 px-2 py-0.5 font-nunito text-[10px] font-medium text-slate-600">
                      {tool.category}
                    </span>
                  </div>
                ))
              )}
            </div>
          </TabsContent>

          {/* Storico tab — version history */}
          <TabsContent value="storico" className="mt-0">
            <div className="space-y-2">
              {data.history.length === 0 ? (
                <p className="font-nunito text-xs text-slate-400 text-center py-8">
                  Nessuna versione precedente
                </p>
              ) : (
                data.history.map((entry, idx) => (
                  <div
                    key={idx}
                    className="flex items-start gap-2.5 rounded-lg bg-white/50 border border-slate-200/40 p-2.5"
                  >
                    <span className="shrink-0 rounded-full bg-slate-100 px-2 py-0.5 font-quicksand text-xs font-bold text-slate-600">
                      v{entry.version}
                    </span>
                    <div className="flex-1 min-w-0">
                      {entry.note && (
                        <p className="font-nunito text-xs font-medium text-slate-700">
                          {entry.note}
                        </p>
                      )}
                      <p className="font-nunito text-[10px] text-slate-400">
                        {new Date(entry.updatedAt).toLocaleDateString('it-IT')}
                      </p>
                    </div>
                  </div>
                ))
              )}
            </div>
          </TabsContent>
        </div>
      </Tabs>

      <DrawerActionFooter actions={footerActions} />
    </div>
  );
}
