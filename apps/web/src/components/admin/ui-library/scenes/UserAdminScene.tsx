'use client';

import { PermissionsMatrix } from '@/components/admin/users/permissions-matrix';
import { RoleCard } from '@/components/admin/users/role-card';

// ActivityTable and InviteUserDialog require API hooks (useApiClient, etc.)
// We render PermissionsMatrix and RoleCard directly; show placeholders for the rest.

function PlaceholderCard({ title, description }: { title: string; description: string }) {
  return (
    <div className="flex flex-col gap-2 rounded-xl border border-dashed border-border/50 bg-muted/20 p-5">
      <p className="font-quicksand text-sm font-semibold text-foreground">{title}</p>
      <p className="font-nunito text-xs text-muted-foreground">{description}</p>
      <span className="inline-flex w-fit items-center rounded-full border border-border/50 px-2.5 py-0.5 font-nunito text-[10px] text-muted-foreground">
        requires provider context
      </span>
    </div>
  );
}

export default function UserAdminScene() {
  return (
    <div className="space-y-8">
      {/* Role cards */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">Role Cards</h3>
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <RoleCard
            role="Admin"
            userCount="3 users"
            description="Full system access including configuration and user management."
            iconName="shield"
            colorClass="amber"
          />
          <RoleCard
            role="Editor"
            userCount="8 users"
            description="Can approve games, upload documents, and configure AI agents."
            iconName="pencil"
            colorClass="blue"
          />
          <RoleCard
            role="User"
            userCount="247 users"
            description="Standard access to library, chat, and personal collections."
            iconName="user"
            colorClass="green"
          />
          <RoleCard
            role="Anonymous"
            userCount="—"
            description="Read-only access to public catalog pages."
            iconName="help-circle"
            colorClass="gray"
          />
        </div>
      </div>

      {/* Permissions matrix */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">
          Permissions Matrix
        </h3>
        <PermissionsMatrix />
      </div>

      {/* Placeholder for context-dependent components */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">
          Additional Components
        </h3>
        <div className="grid gap-3 sm:grid-cols-2">
          <PlaceholderCard
            title="ActivityTable"
            description="Paginated table of user activity events with filters and export."
          />
          <PlaceholderCard
            title="InviteUserDialog"
            description="Modal dialog for inviting new users via email with role assignment."
          />
        </div>
      </div>
    </div>
  );
}
