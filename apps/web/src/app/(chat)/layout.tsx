/**
 * Chat Route Group Layout
 * Carte in Mano — Uses UnifiedShell (card-hand navigation)
 *
 * (chat) is a sibling of (authenticated), so it needs its own UnifiedShell wrapper.
 */

import { type ReactNode } from 'react';

import { UnifiedShell } from '@/components/layout/UnifiedShell';

export default function ChatLayout({ children }: { children: ReactNode }) {
  return <UnifiedShell>{children}</UnifiedShell>;
}
