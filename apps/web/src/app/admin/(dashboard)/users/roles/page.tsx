import { Suspense } from 'react';

import { type Metadata } from 'next';

import { PermissionsMatrix } from '@/components/admin/users/permissions-matrix';
import { RoleCard } from '@/components/admin/users/role-card';

export const metadata: Metadata = {
  title: 'Roles & Permissions',
  description: 'Manage user roles and access control across the platform',
};

function CardSkeleton({ height = 'h-[180px]' }: { height?: string }) {
  return (
    <div
      className={`${height} bg-white/40 dark:bg-zinc-800/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 animate-pulse`}
    />
  );
}

export default function RolesPermissionsPage() {
  return (
    <div className="space-y-8">
      {/* Page Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Roles & Permissions
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Manage user roles and access control across the platform
        </p>
      </div>

      {/* Role Overview Cards */}
      <Suspense
        fallback={
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
            {Array.from({ length: 4 }).map((_, i) => (
              <CardSkeleton key={i} />
            ))}
          </div>
        }
      >
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
          <RoleCard
            role="Admin"
            userCount="5 users"
            description="Full system access with all permissions. Can manage users, roles, and system configuration."
            iconName="shield"
            colorClass="amber"
          />
          <RoleCard
            role="Editor"
            userCount="23 users"
            description="Content management access. Can approve games, upload documents, and configure AI agents."
            iconName="pencil"
            colorClass="blue"
          />
          <RoleCard
            role="User"
            userCount="1,247 users"
            description="Standard authenticated user. Can access RAG features, upload personal documents, and use AI chat."
            iconName="user"
            colorClass="green"
          />
          <RoleCard
            role="Anonymous"
            userCount="Public"
            description="Unauthenticated visitors. Limited to browsing public game catalog and viewing basic information."
            iconName="help-circle"
            colorClass="gray"
          />
        </div>
      </Suspense>

      {/* Permissions Matrix */}
      <Suspense fallback={<CardSkeleton height="h-[600px]" />}>
        <PermissionsMatrix />
      </Suspense>
    </div>
  );
}
