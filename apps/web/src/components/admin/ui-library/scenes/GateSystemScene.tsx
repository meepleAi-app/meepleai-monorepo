'use client';

import { Shield, Star, Lock, Crown } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { TierBadge } from '@/components/ui/feedback/tier-badge';

// Gates rely on React context providers (FeatureFlagProvider, PermissionContext)
// which are not available in isolation. We show realistic gate scenarios
// using the TierBadge plus inline descriptive cards.

function GateCard({
  title,
  icon: Icon,
  color,
  description,
  component,
  fallback,
  granted,
}: {
  title: string;
  icon: React.ComponentType<{ className?: string }>;
  color: string;
  description: string;
  component: string;
  fallback: string;
  granted: boolean;
}) {
  return (
    <div className="overflow-hidden rounded-xl border border-border/60 bg-background">
      <div className={`border-b border-border/40 px-4 py-3 ${color}`}>
        <div className="flex items-center gap-2">
          <Icon className="h-4 w-4" />
          <span className="font-quicksand text-sm font-semibold">{title}</span>
          <Badge
            variant="outline"
            className={`ml-auto text-[10px] ${granted ? 'border-green-400 text-green-700 dark:text-green-400' : 'border-red-400 text-red-700 dark:text-red-400'}`}
          >
            {granted ? 'Granted' : 'Denied'}
          </Badge>
        </div>
      </div>
      <div className="space-y-2 p-4">
        <p className="font-nunito text-xs text-muted-foreground">{description}</p>
        <div className="rounded bg-muted/40 px-3 py-2">
          <code className="font-mono text-xs text-foreground">{component}</code>
        </div>
        {!granted && (
          <div className="rounded border border-amber-200 bg-amber-50/50 px-3 py-2 dark:bg-amber-950/20">
            <p className="font-nunito text-xs text-amber-800 dark:text-amber-300">
              Fallback: {fallback}
            </p>
          </div>
        )}
        <div
          className={`rounded border p-3 ${granted ? 'border-green-200 bg-green-50/50 dark:bg-green-950/20' : 'border-muted bg-muted/20'}`}
        >
          <p className="font-nunito text-xs font-medium">
            {granted ? '✓ Children rendered' : '— Children hidden, fallback shown'}
          </p>
        </div>
      </div>
    </div>
  );
}

export default function GateSystemScene() {
  return (
    <div className="space-y-6">
      <div>
        <h3 className="font-quicksand text-base font-semibold text-foreground">Gate Components</h3>
        <p className="mt-1 font-nunito text-sm text-muted-foreground">
          Conditional rendering gates. Each gate checks a specific access criterion and renders
          children or a fallback accordingly.
        </p>
      </div>

      {/* TierBadge — fully renderable */}
      <div className="space-y-3">
        <h4 className="font-quicksand text-sm font-semibold text-foreground">TierBadge</h4>
        <div className="flex flex-wrap gap-2">
          <TierBadge tier="free" />
          <TierBadge tier="normal" />
          <TierBadge tier="pro" />
          <TierBadge tier="enterprise" />
        </div>
      </div>

      {/* Gate scenario cards */}
      <div className="grid gap-4 sm:grid-cols-2">
        <GateCard
          title="FeatureGate"
          icon={Star}
          color="bg-amber-50/70 text-amber-900 dark:bg-amber-950/30 dark:text-amber-300"
          description="Renders children only when a feature flag is enabled for the current user."
          component={'<FeatureGate feature="advanced_rag">'}
          fallback="null (feature disabled)"
          granted={true}
        />

        <GateCard
          title="TierGate"
          icon={Crown}
          color="bg-purple-50/70 text-purple-900 dark:bg-purple-950/30 dark:text-purple-300"
          description="Renders children based on minimum tier level (free → normal → pro → enterprise)."
          component={'<TierGate tier="pro">'}
          fallback="<UpgradePrompt requiredTier='pro' />"
          granted={false}
        />

        <GateCard
          title="RoleGate"
          icon={Shield}
          color="bg-blue-50/70 text-blue-900 dark:bg-blue-950/30 dark:text-blue-300"
          description="Renders children only when user has the specified role."
          component={'<RoleGate role="Admin">'}
          fallback="null (role insufficient)"
          granted={true}
        />

        <GateCard
          title="PermissionGate"
          icon={Lock}
          color="bg-green-50/70 text-green-900 dark:bg-green-950/30 dark:text-green-300"
          description="Fine-grained permission checks beyond role-based access."
          component={'<PermissionGate permission="games.edit">'}
          fallback="<p>Access denied</p>"
          granted={false}
        />
      </div>
    </div>
  );
}
