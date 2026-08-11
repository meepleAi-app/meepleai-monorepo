/**
 * Chat Route Group Layout
 *
 * Renders the shared authenticated shell (UserShellClient) for the chat
 * experience. (chat) is a sibling of (authenticated), so it needs its own
 * shell wrapper.
 */

import { type ReactNode } from 'react';

import { UserShellClient } from '@/components/layout/UserShell';

export default function ChatLayout({ children }: { children: ReactNode }) {
  return <UserShellClient>{children}</UserShellClient>;
}
