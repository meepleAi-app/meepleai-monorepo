'use client';

import { AdminAuthGuard } from '@/components/admin/AdminAuthGuard';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { TierConfigSection } from './_components/TierConfigSection/TierConfigSection';
import { UserOverridesSection } from './_components/UserOverridesSection/UserOverridesSection';

export function RateLimitConfigClient() {
  const { user, loading: authLoading } = useAuthUser();

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto py-8 space-y-8">
        <Card>
          <CardHeader>
            <CardTitle>Rate Limit Configuration</CardTitle>
            <CardDescription>
              Configure share request limits for each user tier and manage individual user overrides
            </CardDescription>
          </CardHeader>
          <CardContent className="space-y-8">
            <TierConfigSection />
            <UserOverridesSection />
          </CardContent>
        </Card>
      </div>
    </AdminAuthGuard>
  );
}
