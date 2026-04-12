'use client';

import React, { useState } from 'react';

import {
  CalendarDays,
  CheckCircle,
  ExternalLink,
  ListChecks,
  MapPin,
  Users,
  UserPlus,
} from 'lucide-react';
import { useRouter } from 'next/navigation';

import { Tabs, TabsList, TabsContent } from '@/components/ui/navigation/tabs';

import { DrawerLoadingSkeleton, DrawerErrorState } from '../drawer-states';
import { DrawerActionFooter } from '../DrawerActionFooter';
import { useEventDetail } from '../hooks';
import { ENTITY_COLORS, EntityHeader, EntityTabTrigger, StatCard } from '../shared';

import type { DrawerAction } from '../DrawerActionFooter';

// ============================================================================
// EventDrawerContent — drawer-specific event detail view
// ============================================================================

interface EventDrawerContentProps {
  entityId: string;
}

type EventTab = 'overview' | 'programma';

export function EventDrawerContent({ entityId }: EventDrawerContentProps) {
  const { data, loading, error, retry } = useEventDetail(entityId);
  const [activeTab, setActiveTab] = useState<EventTab>('overview');
  const router = useRouter();
  const colors = ENTITY_COLORS.event;

  if (loading) return <DrawerLoadingSkeleton />;
  if (error) return <DrawerErrorState error={error} onRetry={retry} />;
  if (!data) return <DrawerLoadingSkeleton />;

  const canConfirm = data.rsvpStatus !== 'confirmed';

  const footerActions: DrawerAction[] = [
    ...(canConfirm
      ? [
          {
            icon: CheckCircle,
            label: 'Conferma',
            onClick: () => router.push(`/events/${entityId}?action=confirm`),
            variant: 'secondary' as const,
            enabled: true,
          },
        ]
      : []),
    ...(data.isOrganizer
      ? [
          {
            icon: UserPlus,
            label: 'Invita',
            onClick: () => router.push(`/events/${entityId}?action=invite`),
            variant: 'secondary' as const,
            enabled: true,
          },
        ]
      : []),
    {
      icon: ExternalLink,
      label: 'Apri',
      onClick: () => router.push(`/events/${entityId}`),
      variant: 'primary' as const,
      enabled: true,
    },
  ];

  const attendeeLabel =
    data.maxAttendees != null
      ? `${data.attendeeCount}/${data.maxAttendees}`
      : data.attendeeCount.toString();

  const locationDisplay = data.isOnline ? 'Online' : (data.location ?? '—');

  return (
    <div className="flex flex-1 flex-col">
      <EntityHeader
        title={data.title}
        imageUrl={data.imageUrl}
        color={colors.hsl}
        badge={data.attendeeCount.toString()}
        badgeIcon={<Users className="h-3 w-3" />}
      />

      <Tabs
        value={activeTab}
        onValueChange={v => setActiveTab(v as EventTab)}
        className="flex flex-1 flex-col"
      >
        <TabsList className="mx-4 mt-3 h-10 w-auto justify-start gap-1 bg-slate-100/80 rounded-lg p-1">
          <EntityTabTrigger
            value="overview"
            icon={CalendarDays}
            label="Overview"
            activeAccent={colors.activeAccent}
          />
          <EntityTabTrigger
            value="programma"
            icon={ListChecks}
            label="Programma"
            activeAccent={colors.activeAccent}
          />
        </TabsList>

        <div className="flex-1 overflow-y-auto px-4 py-3">
          {/* Overview tab */}
          <TabsContent value="overview" className="mt-0">
            <div className="space-y-3">
              {/* Date row */}
              {data.startDate && (
                <div className="rounded-lg bg-rose-50/50 border border-rose-200/40 p-3">
                  <p className="font-nunito text-[10px] text-rose-500 uppercase tracking-wider">
                    Data
                  </p>
                  <p className="font-quicksand text-sm font-bold text-rose-700">
                    {new Date(data.startDate).toLocaleDateString('it-IT', {
                      day: '2-digit',
                      month: 'long',
                      year: 'numeric',
                    })}
                    {data.endDate && data.endDate !== data.startDate && (
                      <>
                        {' — '}
                        {new Date(data.endDate).toLocaleDateString('it-IT', {
                          day: '2-digit',
                          month: 'long',
                          year: 'numeric',
                        })}
                      </>
                    )}
                  </p>
                </div>
              )}

              {/* Location row */}
              <div className="flex items-center gap-2 rounded-lg bg-white/50 border border-slate-200/40 p-3">
                <MapPin className="h-4 w-4 shrink-0 text-rose-500" aria-hidden="true" />
                <span className="font-nunito text-sm text-slate-700">{locationDisplay}</span>
              </div>

              {/* Attendees stat */}
              <StatCard label="Partecipanti" value={attendeeLabel} icon={Users} variant="event" />

              {/* Description */}
              {data.description && (
                <div className="rounded-lg bg-white/50 border border-slate-200/40 p-3">
                  <p className="font-nunito text-[10px] text-slate-500 uppercase tracking-wider mb-1">
                    Descrizione
                  </p>
                  <p className="font-nunito text-sm text-slate-700 leading-relaxed">
                    {data.description}
                  </p>
                </div>
              )}
            </div>
          </TabsContent>

          {/* Programma tab */}
          <TabsContent value="programma" className="mt-0">
            <div className="space-y-2">
              {data.schedule.length === 0 ? (
                <p className="font-nunito text-xs text-slate-400 text-center py-8">
                  Nessuna attività programmata
                </p>
              ) : (
                data.schedule.map(item => (
                  <div
                    key={item.id}
                    className="rounded-lg bg-white/50 border border-slate-200/40 p-2.5"
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="min-w-0 flex-1">
                        <p className="font-nunito text-xs font-medium text-slate-700 truncate">
                          {item.title}
                        </p>
                        {item.gameName && (
                          <p className="font-nunito text-[10px] text-rose-500 mt-0.5">
                            {item.gameName}
                          </p>
                        )}
                      </div>
                      <p className="font-nunito text-[10px] text-slate-400 shrink-0">
                        {new Date(item.scheduledAt).toLocaleTimeString('it-IT', {
                          hour: '2-digit',
                          minute: '2-digit',
                        })}
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
