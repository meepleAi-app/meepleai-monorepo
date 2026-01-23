'use client';

import { AdminAuthGuard } from '@/components/admin/AdminAuthGuard';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { TierConfigSection } from './_components/TierConfigSection/TierConfigSection';
import { UserOverridesSection } from './_components/UserOverridesSection/UserOverridesSection';

export function RateLimitConfigClient() {
  return (
    <AdminAuthGuard>
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
