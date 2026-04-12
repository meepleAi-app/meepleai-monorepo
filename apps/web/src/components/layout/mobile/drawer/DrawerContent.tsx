import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

import { AgentDrawerContent } from './AgentDrawerContent';
import { ChatDrawerContent } from './ChatDrawerContent';
import { GameDrawerContent } from './GameDrawerContent';
import { KbDrawerContent } from './KbDrawerContent';
import { PlayerDrawerContent } from './PlayerDrawerContent';
import { SessionDrawerContent } from './SessionDrawerContent';

interface DrawerContentProps {
  entityType: MeepleEntityType;
  entityId: string;
  activeTab?: string;
  onNavigate: () => void;
}

export function DrawerContent({ entityType, entityId, activeTab, onNavigate }: DrawerContentProps) {
  const props = { entityId, activeTab, onNavigate };

  switch (entityType) {
    case 'game':
      return <GameDrawerContent {...props} />;
    case 'player':
      return <PlayerDrawerContent {...props} />;
    case 'session':
      return <SessionDrawerContent {...props} />;
    case 'agent':
      return <AgentDrawerContent {...props} />;
    case 'kb':
      return <KbDrawerContent {...props} />;
    case 'chat':
      return <ChatDrawerContent {...props} />;
    default:
      return null;
  }
}
