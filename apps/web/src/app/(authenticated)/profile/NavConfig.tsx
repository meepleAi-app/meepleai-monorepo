'use client';

/**
 * ProfileNavConfig — Registers MiniNav tabs + ActionBar actions for /profile
 * Issue #5047 — Profile + Settings MiniNav
 *
 * Tabs: Profile · Achievements · Badges · Settings
 * ActionBar: Edit Profile (primary) · Export Data
 *
 * Include in profile/page.tsx:
 *   <ProfileNavConfig />
 */

import { useEffect } from 'react';

import { Download, Edit } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useSetNavConfig } from '@/hooks/useSetNavConfig';

export function ProfileNavConfig() {
  const setNavConfig = useSetNavConfig();
  const router = useRouter();

  useEffect(() => {
    setNavConfig({
      miniNav: [
        { id: 'profile',      label: 'Profile',      href: '/profile' },
        { id: 'achievements', label: 'Achievements', href: '/profile?tab=achievements' },
        { id: 'badges',       label: 'Badges',       href: '/profile?tab=badges' },
        { id: 'settings',     label: 'Settings',     href: '/profile?tab=settings' },
      ],
      actionBar: [
        {
          id: 'edit-profile',
          label: 'Edit Profile',
          icon: Edit,
          variant: 'primary',
          onClick: () => router.push('/profile?action=edit'),
        },
        {
          id: 'export-data',
          label: 'Export Data',
          icon: Download,
          onClick: () => router.push('/profile?action=export'),
        },
      ],
    });
  }, [setNavConfig, router]);

  return null;
}
