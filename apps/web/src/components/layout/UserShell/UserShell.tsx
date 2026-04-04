import { type ReactNode } from 'react';

import { UserShellClient } from './UserShellClient';

interface UserShellProps {
  children: ReactNode;
}

export async function UserShell({ children }: UserShellProps) {
  return <UserShellClient>{children}</UserShellClient>;
}
