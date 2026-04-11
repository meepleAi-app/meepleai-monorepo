'use client';

import React, { useState } from 'react';

import {
  CreditCard,
  Dices,
  ExternalLink,
  Eye,
  Hash,
  Pencil,
  Play,
  Timer,
  Wrench,
} from 'lucide-react';
import { useRouter } from 'next/navigation';

import { Tabs, TabsList, TabsContent } from '@/components/ui/navigation/tabs';

import { DrawerLoadingSkeleton, DrawerErrorState } from '../drawer-states';
import { DrawerActionFooter } from '../DrawerActionFooter';
import { useToolDetail } from '../hooks';
import { ENTITY_COLORS, EntityHeader, EntityTabTrigger } from '../shared';

import type { DrawerAction } from '../DrawerActionFooter';
import type { ToolDetailData } from '../types';

// ============================================================================
// ToolDrawerContent — drawer-specific tool detail view
// ============================================================================

interface ToolDrawerContentProps {
  entityId: string;
}

type ToolTab = 'dettaglio' | 'preview';

// ============================================================================
// Helpers
// ============================================================================

const TOOL_TYPE_CONFIG: Record<
  ToolDetailData['toolType'],
  { label: string; Icon: React.ComponentType<{ className?: string }> }
> = {
  dice: { label: 'Dado', Icon: Dices },
  card: { label: 'Carte', Icon: CreditCard },
  timer: { label: 'Timer', Icon: Timer },
  counter: { label: 'Contatore', Icon: Hash },
};

// ============================================================================
// Component
// ============================================================================

export function ToolDrawerContent({ entityId }: ToolDrawerContentProps) {
  const { data, loading, error, retry } = useToolDetail(entityId);
  const [activeTab, setActiveTab] = useState<ToolTab>('dettaglio');
  const router = useRouter();
  const colors = ENTITY_COLORS.tool;

  if (loading) return <DrawerLoadingSkeleton />;
  if (error) return <DrawerErrorState error={error} onRetry={retry} />;
  if (!data) return <DrawerLoadingSkeleton />;

  const typeConfig = TOOL_TYPE_CONFIG[data.toolType];

  const footerActions: DrawerAction[] = [
    ...(data.hasActiveSession
      ? [
          {
            icon: Play,
            label: 'Usa',
            onClick: () => router.push(`/sessions/active?toolId=${entityId}`),
            variant: 'primary' as const,
            enabled: true,
          },
        ]
      : []),
    ...(data.isOwner
      ? [
          {
            icon: Pencil,
            label: 'Modifica',
            onClick: () => router.push(`/tools/${entityId}/edit`),
            variant: 'secondary' as const,
            enabled: true,
          },
        ]
      : []),
    {
      icon: ExternalLink,
      label: 'Apri',
      onClick: () => router.push(`/tools/${entityId}`),
      variant: 'primary' as const,
      enabled: true,
    },
  ];

  return (
    <div className="flex flex-1 flex-col">
      <EntityHeader
        title={data.name}
        color={colors.hsl}
        badge={typeConfig.label}
        badgeIcon={<typeConfig.Icon className="h-3 w-3" />}
      />

      <Tabs
        value={activeTab}
        onValueChange={v => setActiveTab(v as ToolTab)}
        className="flex flex-1 flex-col"
      >
        <TabsList className="mx-4 mt-3 h-10 w-auto justify-start gap-1 bg-slate-100/80 rounded-lg p-1">
          <EntityTabTrigger
            value="dettaglio"
            icon={Wrench}
            label="Dettaglio"
            activeAccent={colors.activeAccent}
          />
          <EntityTabTrigger
            value="preview"
            icon={Eye}
            label="Preview"
            activeAccent={colors.activeAccent}
          />
        </TabsList>

        <div className="flex-1 overflow-y-auto px-4 py-3">
          {/* Dettaglio tab */}
          <TabsContent value="dettaglio" className="mt-0">
            <div className="space-y-3">
              {/* Tool type badge */}
              <div className="flex items-center gap-2">
                <span className="inline-flex items-center gap-1.5 rounded-full bg-cyan-100 px-2.5 py-1 font-nunito text-xs font-bold text-cyan-700">
                  <typeConfig.Icon className="h-3.5 w-3.5" />
                  {typeConfig.label}
                </span>
              </div>

              {/* Toolkit link row */}
              {data.toolkitName && (
                <div className="rounded-lg bg-cyan-50/50 border border-cyan-200/40 p-3">
                  <p className="font-nunito text-[10px] text-cyan-600 uppercase tracking-wider">
                    Toolkit
                  </p>
                  <p className="font-quicksand text-sm font-bold text-cyan-800">
                    {data.toolkitName}
                  </p>
                </div>
              )}

              {/* Config entries */}
              {Object.entries(data.config).length > 0 && (
                <div className="space-y-1.5">
                  <p className="font-nunito text-[10px] text-slate-500 uppercase tracking-wider">
                    Configurazione
                  </p>
                  {Object.entries(data.config).map(([key, value]) => (
                    <div
                      key={key}
                      className="flex items-center justify-between rounded-lg bg-white/50 border border-slate-200/40 px-3 py-2"
                    >
                      <span className="font-nunito text-xs text-slate-500">{key}</span>
                      <span className="font-quicksand text-xs font-semibold text-slate-700">
                        {String(value)}
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </TabsContent>

          {/* Preview tab */}
          <TabsContent value="preview" className="mt-0">
            <div className="flex flex-col items-center gap-4 py-6">
              <typeConfig.Icon className="h-8 w-8 text-cyan-500" aria-hidden="true" />
              {data.previewDescription ? (
                <div className="w-full rounded-lg bg-cyan-50/50 border border-cyan-200/40 p-4">
                  <p className="font-nunito text-sm text-slate-700 leading-relaxed text-center">
                    {data.previewDescription}
                  </p>
                </div>
              ) : (
                <p className="font-nunito text-xs text-slate-400 text-center">
                  Anteprima non disponibile
                </p>
              )}
            </div>
          </TabsContent>
        </div>
      </Tabs>

      <DrawerActionFooter actions={footerActions} />
    </div>
  );
}
