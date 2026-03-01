'use client';

import { useQuery } from '@tanstack/react-query';

import { RoleCard } from '@/components/admin/users/role-card';
import { adminClient } from '@/lib/api/admin-client';

const ROLE_CONFIG = [
  {
    role: 'Admin' as const,
    apiRole: 'admin',
    description: 'Full system access with all permissions. Can manage users, roles, and system configuration.',
    iconName: 'shield' as const,
    colorClass: 'amber' as const,
  },
  {
    role: 'Editor' as const,
    apiRole: 'editor',
    description: 'Content management access. Can approve games, upload documents, and configure AI agents.',
    iconName: 'pencil' as const,
    colorClass: 'blue' as const,
  },
  {
    role: 'User' as const,
    apiRole: 'user',
    description: 'Standard authenticated user. Can access RAG features, upload personal documents, and use AI chat.',
    iconName: 'user' as const,
    colorClass: 'green' as const,
  },
] as const;

export function RoleCardsGrid() {
  const { data: adminCount, isLoading: loadingAdmin } = useQuery({
    queryKey: ['admin-role-count', 'admin'],
    queryFn: () => adminClient.getUsers({ role: 'admin', page: 1, pageSize: 1 }),
    staleTime: 5 * 60 * 1000,
  });

  const { data: editorCount, isLoading: loadingEditor } = useQuery({
    queryKey: ['admin-role-count', 'editor'],
    queryFn: () => adminClient.getUsers({ role: 'editor', page: 1, pageSize: 1 }),
    staleTime: 5 * 60 * 1000,
  });

  const { data: userCount, isLoading: loadingUser } = useQuery({
    queryKey: ['admin-role-count', 'user'],
    queryFn: () => adminClient.getUsers({ role: 'user', page: 1, pageSize: 1 }),
    staleTime: 5 * 60 * 1000,
  });

  const counts: Record<string, { count: number | undefined; loading: boolean }> = {
    admin: { count: adminCount?.totalCount, loading: loadingAdmin },
    editor: { count: editorCount?.totalCount, loading: loadingEditor },
    user: { count: userCount?.totalCount, loading: loadingUser },
  };

  const formatCount = (count: number | undefined) => {
    if (count === undefined) return '—';
    return `${count.toLocaleString()} user${count !== 1 ? 's' : ''}`;
  };

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
      {ROLE_CONFIG.map((config) => {
        const roleData = counts[config.apiRole];
        return (
          <RoleCard
            key={config.role}
            role={config.role}
            userCount={formatCount(roleData?.count)}
            description={config.description}
            iconName={config.iconName}
            colorClass={config.colorClass}
            href={`/admin/users/management?role=${config.apiRole}`}
            isLoading={roleData?.loading}
          />
        );
      })}
      <RoleCard
        role="Anonymous"
        userCount="Public"
        description="Unauthenticated visitors. Limited to browsing public game catalog and viewing basic information."
        iconName="help-circle"
        colorClass="gray"
      />
    </div>
  );
}
