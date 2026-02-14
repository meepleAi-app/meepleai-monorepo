/**
 * Example 1: Basic Permission Check
 * Epic #4068 - Permission System
 *
 * Shows how to check permissions and conditionally render features
 */

import React from 'react';
import { usePermissions } from '@/contexts/PermissionContext';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Button } from '@/components/ui/button';
import { Lock, Sparkles } from 'lucide-react';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';

export function BasicPermissionExample() {
  const { tier, role, canAccess, hasTier, isAdmin, loading } = usePermissions();

  if (loading) {
    return <div>Loading permissions...</div>;
  }

  return (
    <div className="space-y-8 p-8">
      <section>
        <h1 className="text-2xl font-bold mb-4">Your Permissions</h1>

        <div className="grid grid-cols-3 gap-4">
          <div className="p-4 bg-card rounded-lg border">
            <p className="text-sm text-muted-foreground">Tier</p>
            <p className="text-2xl font-bold capitalize">{tier}</p>
          </div>

          <div className="p-4 bg-card rounded-lg border">
            <p className="text-sm text-muted-foreground">Role</p>
            <p className="text-2xl font-bold capitalize">{role}</p>
          </div>

          <div className="p-4 bg-card rounded-lg border">
            <p className="text-sm text-muted-foreground">Admin</p>
            <p className="text-2xl font-bold">{isAdmin() ? 'Yes' : 'No'}</p>
          </div>
        </div>
      </section>

      <section>
        <h2 className="text-xl font-semibold mb-4">Feature Access</h2>

        {/* Wishlist: Available to all */}
        <div className="mb-4">
          {canAccess('wishlist') ? (
            <Alert>
              <Sparkles className="h-4 w-4" />
              <AlertTitle>Wishlist Available</AlertTitle>
              <AlertDescription>
                You can save games to your wishlist.
              </AlertDescription>
            </Alert>
          ) : (
            <Alert variant="destructive">
              <Lock className="h-4 w-4" />
              <AlertTitle>Wishlist Locked</AlertTitle>
            </Alert>
          )}
        </div>

        {/* Bulk Select: Pro tier or Editor role */}
        <div className="mb-4">
          {canAccess('bulk-select') ? (
            <Alert>
              <Sparkles className="h-4 w-4" />
              <AlertTitle>Bulk Selection Available</AlertTitle>
              <AlertDescription>
                You can select multiple games for batch operations.
              </AlertDescription>
            </Alert>
          ) : (
            <Alert>
              <Lock className="h-4 w-4" />
              <AlertTitle>Bulk Selection Locked</AlertTitle>
              <AlertDescription>
                Upgrade to Pro or become an Editor to unlock bulk selection.
              </AlertDescription>
              <Button className="mt-2" size="sm" onClick={() => window.location.href = '/upgrade'}>
                Upgrade to Pro
              </Button>
            </Alert>
          )}
        </div>

        {/* Agent Creation: Pro tier or Creator role */}
        <div className="mb-4">
          {canAccess('agent.create') ? (
            <Alert>
              <Sparkles className="h-4 w-4" />
              <AlertTitle>Agent Creation Available</AlertTitle>
              <AlertDescription>
                You can create custom AI agents.
              </AlertDescription>
            </Alert>
          ) : (
            <Alert>
              <Lock className="h-4 w-4" />
              <AlertTitle>Agent Creation Locked</AlertTitle>
              <AlertDescription>
                Requires Pro tier or Creator role.
              </AlertDescription>
            </Alert>
          )}
        </div>

        {/* Admin Features: Admin role only */}
        <div className="mb-4">
          {canAccess('quick-action.delete') ? (
            <Alert variant="destructive">
              <AlertTitle>Admin Features Enabled</AlertTitle>
              <AlertDescription>
                You have access to destructive operations (delete, ban, etc.)
              </AlertDescription>
            </Alert>
          ) : (
            <Alert>
              <Lock className="h-4 w-4" />
              <AlertTitle>Admin Features Locked</AlertTitle>
              <AlertDescription>
                Admin role required for destructive operations.
              </AlertDescription>
            </Alert>
          )}
        </div>
      </section>

      <section>
        <h2 className="text-xl font-semibold mb-4">Tier Checks</h2>

        <div className="space-y-2">
          <p>Has Free tier: {hasTier('free') ? '✅' : '❌'}</p>
          <p>Has Normal tier: {hasTier('normal') ? '✅' : '❌'}</p>
          <p>Has Pro tier: {hasTier('pro') ? '✅' : '❌'}</p>
          <p>Has Enterprise tier: {hasTier('enterprise') ? '✅' : '❌'}</p>
        </div>

        <p className="mt-4 text-sm text-muted-foreground">
          Tier checks are hierarchical: Pro tier passes Normal tier check.
        </p>
      </section>

      <section>
        <h2 className="text-xl font-semibold mb-4">Example: Permission-Aware Card</h2>

        <MeepleCard
          entity="game"
          variant="grid"
          title="Wingspan"
          subtitle="Stonemaier Games"
          imageUrl="/games/wingspan.jpg"
          showWishlist={canAccess('wishlist')}
          selectable={canAccess('bulk-select')}
          draggable={canAccess('drag-drop')}
          quickActions={[
            { icon: Sparkles, label: 'View', onClick: () => alert('View game') },
            canAccess('quick-action.edit') && { icon: Sparkles, label: 'Edit', onClick: () => alert('Edit') },
            canAccess('quick-action.delete') && { icon: Sparkles, label: 'Delete', onClick: () => alert('Delete'), destructive: true }
          ].filter(Boolean)}
        />

        <p className="mt-4 text-sm text-muted-foreground">
          Card features automatically filtered based on your permissions.
        </p>
      </section>
    </div>
  );
}

export default BasicPermissionExample;
