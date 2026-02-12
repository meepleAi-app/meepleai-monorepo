/**
 * Example 5: Collection Limit UI
 * Epic #4068 - Issue #4183
 *
 * Shows progress indicators and tier-based limits
 */

import React from 'react';
import { usePermissions } from '@/contexts/PermissionContext';
import { CollectionLimitIndicator } from '@/components/dashboard/CollectionLimitIndicator';
import { Button } from '@/components/ui/button';
import { Progress } from '@/components/ui/progress';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { AlertTriangle, Lock } from 'lucide-react';

export function CollectionLimitsExample() {
  const { tier, limits } = usePermissions();

  // Simulate different collection states
  const collections = [
    { name: 'Low usage (25%)', gameCount: Math.floor(limits.maxGames * 0.25), storageMB: Math.floor(limits.storageQuotaMB * 0.2) },
    { name: 'Medium usage (60%)', gameCount: Math.floor(limits.maxGames * 0.6), storageMB: Math.floor(limits.storageQuotaMB * 0.55) },
    { name: 'High usage (80%)', gameCount: Math.floor(limits.maxGames * 0.8), storageMB: Math.floor(limits.storageQuotaMB * 0.75) },
    { name: 'Approaching limit (95%)', gameCount: Math.floor(limits.maxGames * 0.95), storageMB: Math.floor(limits.storageQuotaMB * 0.92) },
    { name: 'At limit (100%)', gameCount: limits.maxGames, storageMB: limits.storageQuotaMB }
  ];

  const getColor = (percent: number) => {
    if (percent >= 90) return { bg: 'bg-red-500', text: 'text-red-600' };
    if (percent >= 75) return { bg: 'bg-yellow-500', text: 'text-yellow-600' };
    return { bg: 'bg-green-500', text: 'text-green-600' };
  };

  return (
    <div className="space-y-8 p-8">
      <section>
        <h1 className="text-2xl font-bold mb-4">Collection Limit UI Examples</h1>
        <p className="text-muted-foreground">
          Progress bars, warnings, and tier-based limits (Epic #4068 - Issue #4183)
        </p>

        <div className="mt-4 p-4 bg-card border rounded">
          <h3 className="font-semibold mb-2">Your Tier: {tier.toUpperCase()}</h3>
          <dl className="grid grid-cols-2 gap-4 text-sm">
            <div>
              <dt className="text-muted-foreground">Max Games</dt>
              <dd className="font-semibold">{limits.maxGames === Number.MAX_SAFE_INTEGER ? '∞ Unlimited' : limits.maxGames}</dd>
            </div>
            <div>
              <dt className="text-muted-foreground">Storage Quota</dt>
              <dd className="font-semibold">{limits.storageQuotaMB === Number.MAX_SAFE_INTEGER ? '∞ Unlimited' : `${limits.storageQuotaMB}MB`}</dd>
            </div>
          </dl>
        </div>
      </section>

      <section>
        <h2 className="text-xl font-semibold mb-4">Progress Indicators at Different Usage Levels</h2>

        <div className="space-y-6">
          {collections.map(col => {
            const gamePercent = (col.gameCount / limits.maxGames) * 100;
            const storagePercent = (col.storageMB / limits.storageQuotaMB) * 100;
            const gameColor = getColor(gamePercent);
            const storageColor = getColor(storagePercent);

            return (
              <div key={col.name} className="p-6 border rounded">
                <h3 className="font-semibold mb-4">{col.name}</h3>

                <div className="space-y-4">
                  {/* Game count progress */}
                  <div>
                    <div className="flex justify-between text-sm mb-2">
                      <span>Games</span>
                      <span className={gameColor.text}>
                        {col.gameCount} / {limits.maxGames} ({gamePercent.toFixed(0)}%)
                      </span>
                    </div>
                    <Progress value={gamePercent} className={gameColor.bg} />

                    {gamePercent > 75 && (
                      <p className={`text-xs mt-2 flex items-center gap-1 ${gameColor.text}`}>
                        <AlertTriangle className="w-3 h-3" />
                        {gamePercent >= 90 ? 'Critical: Approaching limit' : 'Warning: High usage'}
                      </p>
                    )}
                  </div>

                  {/* Storage quota progress */}
                  <div>
                    <div className="flex justify-between text-sm mb-2">
                      <span>Storage</span>
                      <span className={storageColor.text}>
                        {col.storageMB}MB / {limits.storageQuotaMB}MB ({storagePercent.toFixed(0)}%)
                      </span>
                    </div>
                    <Progress value={storagePercent} className={storageColor.bg} />
                  </div>

                  {/* Upgrade CTA if > 90% */}
                  {(gamePercent > 90 || storagePercent > 90) && tier !== 'enterprise' && (
                    <Alert variant="destructive">
                      <Lock className="h-4 w-4" />
                      <AlertTitle>Limit Almost Reached</AlertTitle>
                      <AlertDescription>
                        Upgrade to {tier === 'free' ? 'Normal' : tier === 'normal' ? 'Pro' : 'Enterprise'} for higher limits
                        <Button className="mt-2" size="sm" onClick={() => alert('Navigate to upgrade')}>
                          Upgrade Now
                        </Button>
                      </AlertDescription>
                    </Alert>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </section>

      <section>
        <h2 className="text-xl font-semibold mb-4">Tier Comparison Table</h2>

        <table className="w-full border-collapse border">
          <thead>
            <tr className="bg-muted">
              <th className="border p-2 text-left">Tier</th>
              <th className="border p-2 text-right">Max Games</th>
              <th className="border p-2 text-right">Storage Quota</th>
              <th className="border p-2 text-center">Price</th>
            </tr>
          </thead>
          <tbody>
            <tr className={tier === 'free' ? 'bg-primary/10' : ''}>
              <td className="border p-2 font-medium">Free</td>
              <td className="border p-2 text-right">50</td>
              <td className="border p-2 text-right">100 MB</td>
              <td className="border p-2 text-center">$0/month</td>
            </tr>
            <tr className={tier === 'normal' ? 'bg-primary/10' : ''}>
              <td className="border p-2 font-medium">Normal</td>
              <td className="border p-2 text-right">100</td>
              <td className="border p-2 text-right">500 MB</td>
              <td className="border p-2 text-center">$4.99/month</td>
            </tr>
            <tr className={tier === 'pro' || tier === 'premium' ? 'bg-primary/10' : ''}>
              <td className="border p-2 font-medium">Pro</td>
              <td className="border p-2 text-right">500</td>
              <td className="border p-2 text-right">5 GB</td>
              <td className="border p-2 text-center">$9.99/month</td>
            </tr>
            <tr className={tier === 'enterprise' ? 'bg-primary/10' : ''}>
              <td className="border p-2 font-medium">Enterprise</td>
              <td className="border p-2 text-right">∞ Unlimited</td>
              <td className="border p-2 text-right">∞ Unlimited</td>
              <td className="border p-2 text-center">Contact Sales</td>
            </tr>
          </tbody>
        </table>

        <p className="mt-2 text-xs text-muted-foreground">
          Your current tier ({tier}) is highlighted
        </p>
      </section>

      <section>
        <h2 className="text-xl font-semibold mb-4">Implementation Code</h2>

        <pre className="bg-muted p-4 rounded text-sm overflow-auto">
{`import { CollectionLimitIndicator } from '@/components/dashboard/CollectionLimitIndicator';
import { usePermissions } from '@/contexts/PermissionContext';

function MyCollectionPage() {
  const { limits } = usePermissions();
  const collection = useCollectionQuery();

  return (
    <div>
      <h1>My Collection</h1>

      <CollectionLimitIndicator
        gameCount={collection.games.length}
        storageMB={collection.totalStorageMB}
      />

      {/* Collection grid */}
      <div className="grid grid-cols-4 gap-4 mt-8">
        {collection.games.map(game => <GameCard key={game.id} {...game} />)}
      </div>
    </div>
  );
}`}
        </pre>
      </section>
    </div>
  );
}

export default CollectionLimitsExample;
