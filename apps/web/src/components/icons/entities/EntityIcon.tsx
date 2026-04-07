import type { ComponentType } from 'react';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

import { AgentIcon } from './icons/AgentIcon';
import { ChatIcon } from './icons/ChatIcon';
import { CustomIcon } from './icons/CustomIcon';
import { EventIcon } from './icons/EventIcon';
import { GameIcon } from './icons/GameIcon';
import { KnowledgeIcon } from './icons/KnowledgeIcon';
import { PlayerIcon } from './icons/PlayerIcon';
import { SessionIcon } from './icons/SessionIcon';
import { ToolIcon } from './icons/ToolIcon';
import { ToolkitIcon } from './icons/ToolkitIcon';

interface IconProps {
  size?: number;
  className?: string;
}

const ENTITY_ICONS: Record<MeepleEntityType, ComponentType<IconProps>> = {
  game: GameIcon,
  session: SessionIcon,
  player: PlayerIcon,
  event: EventIcon,
  agent: AgentIcon,
  kb: KnowledgeIcon,
  chat: ChatIcon,
  toolkit: ToolkitIcon,
  tool: ToolIcon,
};

interface EntityIconProps {
  entity: MeepleEntityType;
  size?: number;
  className?: string;
}

export function EntityIcon({ entity, size = 24, className }: EntityIconProps) {
  const IconComponent = ENTITY_ICONS[entity] ?? CustomIcon;
  return <IconComponent size={size} className={className} />;
}
