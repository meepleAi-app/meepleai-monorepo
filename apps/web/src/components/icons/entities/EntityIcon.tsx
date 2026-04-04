import type { ComponentType } from 'react';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card-styles';

import { AchievementIcon } from './icons/AchievementIcon';
import { AgentIcon } from './icons/AgentIcon';
import { ChatIcon } from './icons/ChatIcon';
import { CollectionIcon } from './icons/CollectionIcon';
import { CustomIcon } from './icons/CustomIcon';
import { EventIcon } from './icons/EventIcon';
import { GroupIcon } from './icons/GroupIcon';
import { LocationIcon } from './icons/LocationIcon';
import { ExpansionIcon } from './icons/ExpansionIcon';
import { GameIcon } from './icons/GameIcon';
import { KnowledgeIcon } from './icons/KnowledgeIcon';
import { NoteIcon } from './icons/NoteIcon';
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
  collection: CollectionIcon,
  group: GroupIcon,
  location: LocationIcon,
  expansion: ExpansionIcon,
  agent: AgentIcon,
  kb: KnowledgeIcon,
  chatSession: ChatIcon,
  note: NoteIcon,
  toolkit: ToolkitIcon,
  tool: ToolIcon,
  achievement: AchievementIcon,
  custom: CustomIcon,
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
