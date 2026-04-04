'use client';

import { AlertTemplateGallery } from '@/components/admin/alert-rules/AlertTemplateGallery';
import { BudgetAlertBanner } from '@/components/admin/BudgetAlertBanner';

// AlertRuleForm requires react-hook-form + API client.
// We render AlertTemplateGallery and BudgetAlertBanner directly,
// and show a placeholder for the form.

function PlaceholderCard({ title, description }: { title: string; description: string }) {
  return (
    <div className="flex flex-col gap-2 rounded-xl border border-dashed border-border/50 bg-muted/20 p-5">
      <p className="font-quicksand text-sm font-semibold text-foreground">{title}</p>
      <p className="font-nunito text-xs text-muted-foreground">{description}</p>
      <span className="inline-flex w-fit items-center rounded-full border border-border/50 px-2.5 py-0.5 font-nunito text-[10px] text-muted-foreground">
        requires react-hook-form + API client
      </span>
    </div>
  );
}

const MOCK_TEMPLATES = [
  {
    alertType: 'HighRpm',
    name: 'High RPM Alert',
    description: 'Alert when requests per minute exceed threshold',
    category: 'Performance',
    thresholdValue: 50,
    thresholdUnit: 'RPM',
    durationMinutes: 5,
    severity: 'Warning' as const,
  },
  {
    alertType: 'BudgetThreshold',
    name: 'Budget Threshold',
    description: 'Alert when daily spend exceeds percentage of monthly budget',
    category: 'Cost',
    thresholdValue: 80,
    thresholdUnit: '%',
    durationMinutes: 1,
    severity: 'Critical' as const,
  },
  {
    alertType: 'CircuitBreaker',
    name: 'Circuit Breaker',
    description: 'Alert when circuit breaker trips due to repeated failures',
    category: 'Reliability',
    thresholdValue: 5,
    thresholdUnit: 'errors',
    durationMinutes: 2,
    severity: 'Error' as const,
  },
];

const MOCK_COST_DATA = {
  today: {
    totalCost: 4.2,
    totalRequests: 127,
    budgetLimit: 5.0,
    percentageUsed: 84,
  },
  thisMonth: {
    totalCost: 52.4,
    totalRequests: 3_840,
    budgetLimit: 100.0,
    percentageUsed: 52.4,
  },
  budgetStatus: 'warning' as const,
  lastUpdatedAt: new Date().toISOString(),
};

export default function AlertSystemScene() {
  return (
    <div className="space-y-8">
      {/* Budget alert banner */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">
          Budget Alert Banner
        </h3>
        <BudgetAlertBanner costData={MOCK_COST_DATA} />
      </div>

      {/* Alert template gallery */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">
          Alert Template Gallery
        </h3>
        <AlertTemplateGallery templates={MOCK_TEMPLATES} onSelect={() => undefined} />
      </div>

      {/* AlertRuleForm — requires API context */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">
          Additional Components
        </h3>
        <PlaceholderCard
          title="AlertRuleForm"
          description="Form for creating/editing alert rules with severity, threshold, duration, and notification channel configuration."
        />
      </div>
    </div>
  );
}
