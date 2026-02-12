import type { LucideIcon } from 'lucide-react';

export interface Tag {
  id: string;
  label: string;
  icon?: LucideIcon;
  color?: string; // HSL format
  bgColor?: string;
  tooltip?: string;
}

export type TagVariant = 'desktop' | 'tablet' | 'mobile';

export interface TagStripProps {
  tags: Tag[];
  maxVisible?: number;
  variant?: TagVariant;
  position?: 'left' | 'right';
}
