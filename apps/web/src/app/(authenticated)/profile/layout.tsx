/**
 * Profile Section Layout
 * Issue #5039 — User Route Consolidation
 * Issue #5047 — Profile + Settings Hub + ActionBar
 *
 * /profile is the consolidated hub for profile/settings/badges/achievements.
 * Registers MiniNav tabs and FloatingActionBar via NavigationContext.
 */

'use client';

import { type ReactNode, useEffect } from 'react';

import { Award, Edit, Settings, Shield, User } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/context/NavigationContext';

export default function ProfileLayout({ children }: { children: ReactNode }) {
  const router = useRouter();
  const setNavConfig = useSetNavConfig();

  useEffect(() => {
    setNavConfig({
      miniNav: [
        { id: 'profile', label: 'Profilo', href: '/profile', icon: User },
        { id: 'achievements', label: 'Achievement', href: '/profile?tab=achievements', icon: Award },
        { id: 'badges', label: 'Badge', href: '/profile?tab=badges', icon: Shield },
        { id: 'settings', label: 'Impostazioni', href: '/profile?tab=settings', icon: Settings },
      ],
      actionBar: [
        {
          id: 'edit-profile',
          label: 'Modifica Profilo',
          icon: Edit,
          variant: 'primary',
          onClick: () => {
            router.push('/profile?tab=settings&section=profile');
          },
        },
      ],
    });
  }, [setNavConfig, router]);

  return <>{children}</>;
}
