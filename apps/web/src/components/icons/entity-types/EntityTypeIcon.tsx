import {
  type LucideIcon,
  BookOpen,
  Swords,
  Users,
  HelpCircle,
  Map,
  MessageSquare,
  Wrench,
  FileText,
  Gamepad2,
} from 'lucide-react';

import { MechanicIcon } from '@/components/icons/mechanics';

const AGENT_TYPE_ICONS: Record<string, LucideIcon> = {
  'rules-expert': BookOpen,
  'strategy-advisor': Swords,
  'faq-helper': HelpCircle,
  'setup-guide': Map,
};

const SESSION_TYPE_ICONS: Record<string, LucideIcon> = {
  competitive: Swords,
  cooperative: Users,
  tutorial: BookOpen,
  solo: Gamepad2,
};

const CHAT_CONTEXT_ICONS: Record<string, LucideIcon> = {
  rules: BookOpen,
  strategy: Swords,
  setup: Map,
  general: MessageSquare,
};

const KB_TYPE_ICONS: Record<string, LucideIcon> = {
  rulebook: BookOpen,
  scenario: Map,
  faq: HelpCircle,
  reference: FileText,
};

const ENTITY_ICON_MAP: Record<string, Record<string, LucideIcon>> = {
  agent: AGENT_TYPE_ICONS,
  session: SESSION_TYPE_ICONS,
  chatSession: CHAT_CONTEXT_ICONS,
  kb: KB_TYPE_ICONS,
};

export interface EntityTypeIconProps {
  entity: string;
  subtype: string;
  size?: number;
  className?: string;
}

export function EntityTypeIcon({
  entity,
  subtype,
  size = 20,
  className = '',
}: EntityTypeIconProps) {
  if (entity === 'game') {
    return <MechanicIcon mechanic={subtype} size={size} className={className} />;
  }

  const Icon = ENTITY_ICON_MAP[entity]?.[subtype] ?? Wrench;
  return <Icon role="img" size={size} className={className} />;
}
