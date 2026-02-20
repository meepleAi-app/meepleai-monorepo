'use client';

/**
 * ExtraMeepleCard - Expanded Session Card Component
 * Issue #4757 - ExtraMeepleCard Component Base + Session Tabs
 *
 * A ~600×900px expanded card with glassmorphism header, status badge,
 * session timer, and 4-tab system (Overview, Toolkit, Scoreboard, History).
 * Uses indigo entity color (HSL 240 60% 55%) with Quicksand/Nunito fonts.
 */

import React, { useState } from 'react';

import {
  Bot,
  Camera,
  Clock,
  Gamepad2,
  History,
  LayoutDashboard,
  Trophy,
  Users,
  Wrench,
  Loader2,
  AlertCircle,
} from 'lucide-react';

import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/navigation/tabs';
import { cn } from '@/lib/utils';

import type {
  ExtraMeepleCardProps,
  ExtraMeepleCardTab,
  TabConfig,
} from './types';
import { OverviewTab } from './tabs/OverviewTab';
import { ToolkitTab } from './tabs/ToolkitTab';
import { ScoreboardTab } from './tabs/ScoreboardTab';
import { HistoryTab } from './tabs/HistoryTab';
import { MediaTab } from './tabs/MediaTab';
import { AITab } from './tabs/AITab';

// ============================================================================
// Constants
// ============================================================================

/** Indigo entity color for sessions (HSL) */
const SESSION_COLOR = '240 60% 55%';

/** Status label + color mapping */
const STATUS_CONFIG: Record<string, { label: string; bg: string; text: string }> = {
  setup: { label: 'Setup', bg: 'bg-amber-100', text: 'text-amber-800' },
  inProgress: { label: 'In Progress', bg: 'bg-green-100', text: 'text-green-800' },
  paused: { label: 'Paused', bg: 'bg-orange-100', text: 'text-orange-800' },
  completed: { label: 'Completed', bg: 'bg-slate-100', text: 'text-slate-700' },
};

/** Tab definitions */
const TABS: TabConfig[] = [
  { id: 'overview', label: 'Overview', icon: LayoutDashboard },
  { id: 'toolkit', label: 'Toolkit', icon: Wrench },
  { id: 'scoreboard', label: 'Scoreboard', icon: Trophy },
  { id: 'history', label: 'History', icon: History },
  { id: 'media', label: 'Media', icon: Camera },
  { id: 'ai', label: 'AI', icon: Bot },
];

// ============================================================================
// Sub-components
// ============================================================================

/** Status badge pill */
function SessionStatusPill({ status }: { status: string }) {
  const config = STATUS_CONFIG[status] ?? STATUS_CONFIG.setup;
  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-semibold font-nunito',
        config.bg,
        config.text
      )}
    >
      <span
        className={cn(
          'h-1.5 w-1.5 rounded-full',
          status === 'inProgress' ? 'animate-pulse bg-green-500' : 'bg-current opacity-50'
        )}
      />
      {config.label}
    </span>
  );
}

/** Elapsed time display */
function ElapsedTimeDisplay({ time }: { time?: string }) {
  if (!time) return null;
  return (
    <div className="flex items-center gap-1.5 text-white/80 text-sm font-nunito">
      <Clock className="h-3.5 w-3.5" />
      <span className="tabular-nums">{time}</span>
    </div>
  );
}

// ============================================================================
// Main Component
// ============================================================================

export const ExtraMeepleCard = React.memo(function ExtraMeepleCard({
  sessionId,
  title,
  status,
  imageUrl,
  elapsedTime,
  playerCount,
  activeTab: controlledTab,
  onTabChange,
  actions,
  tabBadges,
  overviewData,
  toolkitData,
  scoreboardData,
  historyData,
  mediaData,
  aiData,
  onAISendMessage,
  loading,
  error,
  className,
  'data-testid': testId,
}: ExtraMeepleCardProps) {
  const [internalTab, setInternalTab] = useState<ExtraMeepleCardTab>('overview');
  const currentTab = controlledTab ?? internalTab;

  const handleTabChange = (value: string) => {
    const tab = value as ExtraMeepleCardTab;
    if (controlledTab === undefined) {
      setInternalTab(tab);
    }
    onTabChange?.(tab);
  };

  // Loading state
  if (loading) {
    return (
      <div
        className={cn(
          'flex h-[900px] w-[600px] items-center justify-center rounded-2xl',
          'bg-white/70 backdrop-blur-md shadow-lg border border-white/20',
          className
        )}
        data-testid={testId}
      >
        <div className="flex flex-col items-center gap-3 text-slate-400">
          <Loader2 className="h-8 w-8 animate-spin" />
          <span className="font-nunito text-sm">Loading session...</span>
        </div>
      </div>
    );
  }

  // Error state
  if (error) {
    return (
      <div
        className={cn(
          'flex h-[900px] w-[600px] items-center justify-center rounded-2xl',
          'bg-white/70 backdrop-blur-md shadow-lg border border-white/20',
          className
        )}
        data-testid={testId}
      >
        <div className="flex flex-col items-center gap-3 text-red-400">
          <AlertCircle className="h-8 w-8" />
          <span className="font-nunito text-sm">{error}</span>
        </div>
      </div>
    );
  }

  return (
    <div
      className={cn(
        'flex w-[600px] flex-col rounded-2xl overflow-hidden',
        'bg-white/70 backdrop-blur-md',
        'shadow-[0_8px_32px_rgba(0,0,0,0.12)]',
        'border border-white/20',
        // Responsive: full width on mobile
        'max-md:w-full max-md:max-w-full',
        className
      )}
      style={{
        '--session-color': SESSION_COLOR,
      } as React.CSSProperties}
      data-testid={testId}
    >
      {/* ================================================================
          Header - Glassmorphism with game cover background
          ================================================================ */}
      <div className="relative h-[180px] overflow-hidden">
        {/* Background image */}
        {imageUrl ? (
          <div
            className="absolute inset-0 bg-cover bg-center"
            style={{ backgroundImage: `url(${imageUrl})` }}
          />
        ) : (
          <div
            className="absolute inset-0"
            style={{ background: `hsl(${SESSION_COLOR})` }}
          />
        )}

        {/* Gradient overlay */}
        <div className="absolute inset-0 bg-gradient-to-b from-black/40 via-black/50 to-black/70" />

        {/* Indigo accent bar */}
        <div
          className="absolute bottom-0 left-0 right-0 h-1"
          style={{ background: `hsl(${SESSION_COLOR})` }}
        />

        {/* Header content */}
        <div className="relative flex h-full flex-col justify-between p-5">
          {/* Top row: status + timer */}
          <div className="flex items-start justify-between">
            <SessionStatusPill status={status} />
            <ElapsedTimeDisplay time={elapsedTime} />
          </div>

          {/* Bottom row: title + player count */}
          <div className="space-y-1.5">
            <h2 className="font-quicksand text-xl font-bold text-white leading-tight line-clamp-2">
              {title}
            </h2>
            <div className="flex items-center gap-4">
              <div className="flex items-center gap-1.5 text-white/70 text-sm font-nunito">
                <Users className="h-3.5 w-3.5" />
                <span>{playerCount} player{playerCount !== 1 ? 's' : ''}</span>
              </div>
              <div className="flex items-center gap-1.5 text-white/70 text-sm font-nunito">
                <Gamepad2 className="h-3.5 w-3.5" />
                <span className="font-mono text-xs opacity-60">
                  {sessionId ? sessionId.slice(0, 8) : ''}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* ================================================================
          Tab System
          ================================================================ */}
      <Tabs
        value={currentTab}
        onValueChange={handleTabChange}
        className="flex flex-1 flex-col"
      >
        {/* Tab bar */}
        <TabsList
          className={cn(
            'mx-4 mt-3 h-10 w-auto justify-start gap-1',
            'bg-slate-100/80 backdrop-blur-sm',
            'rounded-lg p-1',
            // Mobile: horizontal scroll
            'max-md:overflow-x-auto max-md:scrollbar-none'
          )}
        >
          {TABS.map((tab) => {
            const Icon = tab.icon;
            const badge = tabBadges?.[tab.id];
            return (
              <TabsTrigger
                key={tab.id}
                value={tab.id}
                className={cn(
                  'flex items-center gap-1.5 px-3 py-1.5 font-nunito text-xs font-medium',
                  'data-[state=active]:bg-white data-[state=active]:shadow-sm',
                  'data-[state=active]:text-indigo-700',
                  'transition-all duration-200',
                  // Mobile: prevent shrinking
                  'max-md:flex-shrink-0'
                )}
              >
                <Icon className="h-3.5 w-3.5" />
                <span className="max-md:hidden sm:inline">{tab.label}</span>
                {badge != null && badge > 0 && (
                  <span
                    className={cn(
                      'ml-0.5 flex h-4 min-w-4 items-center justify-center rounded-full px-1',
                      'bg-indigo-100 text-indigo-700 text-[10px] font-bold'
                    )}
                  >
                    {badge > 99 ? '99+' : badge}
                  </span>
                )}
              </TabsTrigger>
            );
          })}
        </TabsList>

        {/* Tab content area */}
        <div className="flex-1 overflow-y-auto px-4 py-3">
          <TabsContent value="overview" className="mt-0">
            <OverviewTab
              data={overviewData}
              status={status}
              actions={actions}
            />
          </TabsContent>

          <TabsContent value="toolkit" className="mt-0">
            <ToolkitTab data={toolkitData} />
          </TabsContent>

          <TabsContent value="scoreboard" className="mt-0">
            <ScoreboardTab data={scoreboardData} />
          </TabsContent>

          <TabsContent value="history" className="mt-0">
            <HistoryTab data={historyData} />
          </TabsContent>

          <TabsContent value="media" className="mt-0">
            <MediaTab data={mediaData} />
          </TabsContent>

          <TabsContent value="ai" className="mt-0">
            <AITab data={aiData} onSendMessage={onAISendMessage} />
          </TabsContent>
        </div>
      </Tabs>
    </div>
  );
});
