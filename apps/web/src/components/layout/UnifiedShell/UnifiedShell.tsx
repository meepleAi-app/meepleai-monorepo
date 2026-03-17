import { type ReactNode } from 'react';

import { cookies } from 'next/headers';

import { UnifiedShellClient } from './UnifiedShellClient';

interface UnifiedShellProps {
  children: ReactNode;
  isAdmin?: boolean;
  impersonationBanner?: ReactNode;
  onboardingBanner?: ReactNode;
  userMenu?: ReactNode;
  notificationBell?: ReactNode;
  searchTrigger?: ReactNode;
}

export async function UnifiedShell({
  children,
  isAdmin = false,
  ...clientProps
}: UnifiedShellProps) {
  // Read server-side cookie for initial stack expansion state (prevents flash)
  const cookieStore = await cookies();
  const _initialStackExpanded = cookieStore.get('card-stack-expanded')?.value === 'true';

  return (
    <UnifiedShellClient isAdmin={isAdmin} {...clientProps}>
      {children}
    </UnifiedShellClient>
  );
}
