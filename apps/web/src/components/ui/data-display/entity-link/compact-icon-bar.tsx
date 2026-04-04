'use client';

import {
  Bot,
  Calendar,
  FileText,
  Gamepad2,
  MessageCircle,
  PlayCircle,
  User,
  Wrench,
} from 'lucide-react';

import { CompactIconButton } from './compact-icon-button';

import type { LinkEntityType } from './entity-link-types';
import type { EntityLinkGroup } from './use-entity-link-groups';
import type { LucideIcon } from 'lucide-react';

const ENTITY_ICON_CONFIG: Record<
  string,
  { icon: LucideIcon; label: string; color: string; group: number }
> = {
  Session: { icon: PlayCircle, label: 'Sessioni', color: '240 60% 55%', group: 0 },
  KbCard: { icon: FileText, label: 'Documenti KB', color: '174 60% 40%', group: 0 },
  Document: { icon: FileText, label: 'Documenti', color: '210 40% 55%', group: 0 },
  Agent: { icon: Bot, label: 'Agent', color: '38 92% 50%', group: 1 },
  ChatSession: { icon: MessageCircle, label: 'Chat AI', color: '220 80% 55%', group: 1 },
  Game: { icon: Gamepad2, label: 'Espansioni', color: '25 95% 45%', group: 2 },
  Toolkit: { icon: Wrench, label: 'Toolkit', color: '142 70% 45%', group: 2 },
  Player: { icon: User, label: 'Giocatori', color: '262 83% 58%', group: 2 },
  Event: { icon: Calendar, label: 'Eventi', color: '350 89% 60%', group: 2 },
};

interface CompactIconBarProps {
  groups: EntityLinkGroup[];
  onIconClick: (entityType: LinkEntityType) => void;
  className?: string;
}

export function CompactIconBar({ groups, onIconClick, className }: CompactIconBarProps) {
  if (groups.length === 0) return null;

  const sorted = [...groups]
    .filter(g => ENTITY_ICON_CONFIG[g.entityType])
    .sort((a, b) => {
      const ga = ENTITY_ICON_CONFIG[a.entityType]?.group ?? 99;
      const gb = ENTITY_ICON_CONFIG[b.entityType]?.group ?? 99;
      return ga - gb;
    });

  if (sorted.length === 0) return null;

  const elements: React.ReactNode[] = [];
  let lastGroup = -1;

  for (const item of sorted) {
    const config = ENTITY_ICON_CONFIG[item.entityType];
    if (!config) continue;

    if (lastGroup !== -1 && config.group !== lastGroup) {
      elements.push(
        <div
          key={`sep-${config.group}`}
          data-separator
          className="mx-0.5 h-[3px] w-[3px] shrink-0 rounded-full"
          style={{ background: 'rgba(180,130,80,0.15)' }}
        />
      );
    }
    lastGroup = config.group;

    elements.push(
      <CompactIconButton
        key={item.entityType}
        icon={config.icon}
        count={item.count}
        label={config.label}
        entityColor={config.color}
        onClick={() => onIconClick(item.entityType as LinkEntityType)}
      />
    );
  }

  return (
    <div
      role="toolbar"
      aria-label="Entità collegate"
      className={`flex items-center justify-center gap-1 rounded-b-[20px] border-t border-[rgba(180,130,80,0.08)] bg-[hsl(var(--muted)/0.5)] px-3 py-2 ${className ?? ''}`}
    >
      {elements}
    </div>
  );
}
