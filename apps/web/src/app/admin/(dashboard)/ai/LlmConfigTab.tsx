'use client';

/**
 * #4169 (ADR-095 D8, fetta 6a): «Budget & quotas».
 *
 * The LLM gateway checks these values before every call (LlmBudgetGate, ADR-095 D6): daily and
 * monthly caps, the degrade threshold (from there answers use the reserve model of the agent profile
 * and background work pauses) and the quotas of background work and evaluations. The page saves those
 * and only those: circuit breaker thresholds and the fallback chain are gone, the runtime never read
 * them. Above the form, the spend the gateway counts today and this month, per budget class.
 */
import { useCallback, useEffect, useId, useState } from 'react';

import { AlertCircle, CheckCircle2, Loader2, RotateCcw, Save, Wallet } from 'lucide-react';

import { AdminHubEmptyState } from '@/components/admin/layout/AdminHubEmptyState';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type {
  LlmBudgetSpend,
  LlmBudgetUsage,
  LlmSystemConfigDto,
  UpdateLlmSystemConfigRequest,
} from '@/lib/api/schemas';

type FormState = {
  dailyBudgetUsd: string;
  monthlyBudgetUsd: string;
  degradeThresholdPercent: string;
  backgroundQuotaPercent: string;
  evaluationQuotaPercent: string;
};

const FIELDS = [
  'dailyBudgetUsd',
  'monthlyBudgetUsd',
  'degradeThresholdPercent',
  'backgroundQuotaPercent',
  'evaluationQuotaPercent',
] as const;

function configToForm(config: LlmSystemConfigDto): FormState {
  return {
    dailyBudgetUsd: String(config.dailyBudgetUsd),
    monthlyBudgetUsd: String(config.monthlyBudgetUsd),
    degradeThresholdPercent: String(config.degradeThresholdPercent),
    backgroundQuotaPercent: String(config.backgroundQuotaPercent),
    evaluationQuotaPercent: String(config.evaluationQuotaPercent),
  };
}

/** The request, or the reason it cannot be sent. The backend validates the same rules. */
function formToRequest(form: FormState): UpdateLlmSystemConfigRequest | string {
  const request: UpdateLlmSystemConfigRequest = {
    dailyBudgetUsd: Number(form.dailyBudgetUsd),
    monthlyBudgetUsd: Number(form.monthlyBudgetUsd),
    degradeThresholdPercent: Number(form.degradeThresholdPercent),
    backgroundQuotaPercent: Number(form.backgroundQuotaPercent),
    evaluationQuotaPercent: Number(form.evaluationQuotaPercent),
  };
  if (FIELDS.some(field => form[field].trim() === '' || !Number.isFinite(request[field]))) {
    return 'Every field needs a number.';
  }
  if (request.dailyBudgetUsd < 0 || request.monthlyBudgetUsd < 0) {
    return 'Caps cannot be negative.';
  }
  if (request.dailyBudgetUsd > request.monthlyBudgetUsd) {
    return 'The daily cap cannot exceed the monthly cap.';
  }
  const percents = [
    request.degradeThresholdPercent,
    request.backgroundQuotaPercent,
    request.evaluationQuotaPercent,
  ];
  if (percents.some(p => !Number.isInteger(p))) {
    return 'Threshold and quotas are whole percentages.';
  }
  if (request.degradeThresholdPercent < 1 || request.degradeThresholdPercent > 100) {
    return 'The degrade threshold must be between 1% and 100%.';
  }
  if (
    request.backgroundQuotaPercent < 0 ||
    request.evaluationQuotaPercent < 0 ||
    request.backgroundQuotaPercent + request.evaluationQuotaPercent > 100
  ) {
    return 'Background and evaluation quotas must be at least 0% and together no more than 100%.';
  }
  return request;
}

const usd = (value: number) => `$${value.toFixed(2)}`;

export function LlmConfigTab() {
  const [config, setConfig] = useState<LlmSystemConfigDto | null>(null);
  const [usage, setUsage] = useState<LlmBudgetUsage | null>(null);
  // Review della 6a: una richiesta fallita non dice nulla sul gate, quindi non si confonde con
  // available=false (il gate che non legge la spesa).
  const [usageFailed, setUsageFailed] = useState(false);
  const [form, setForm] = useState<FormState | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const loadUsage = useCallback(async () => {
    try {
      setUsage(await api.admin.getLlmBudgetUsage());
      setUsageFailed(false);
    } catch {
      setUsage(null);
      setUsageFailed(true);
    }
  }, []);

  const load = useCallback(async () => {
    try {
      setError(null);
      const [data] = await Promise.all([api.admin.getLlmSystemConfig(), loadUsage()]);
      setConfig(data);
      setForm(configToForm(data));
    } catch {
      setError('Failed to load the LLM budget configuration');
    } finally {
      setLoading(false);
    }
  }, [loadUsage]);

  useEffect(() => {
    void load();
  }, [load]);

  const isDirty =
    config !== null &&
    form !== null &&
    FIELDS.some(field => form[field] !== configToForm(config)[field]);

  const handleSave = async () => {
    if (!form) return;
    const request = formToRequest(form);
    if (typeof request === 'string') {
      setError(request);
      setSuccess(null);
      return;
    }

    setSaving(true);
    setError(null);
    setSuccess(null);
    try {
      const updated = await api.admin.updateLlmSystemConfig(request);
      setConfig(updated);
      setForm(configToForm(updated));
      setSuccess('Saved. The gateway applies it within a minute.');
      void loadUsage();
    } catch {
      setError('Failed to save the budget configuration');
    } finally {
      setSaving(false);
    }
  };

  const handleReset = () => {
    if (config) {
      setForm(configToForm(config));
      setError(null);
      setSuccess(null);
    }
  };

  const updateField = (key: keyof FormState, value: string) => {
    setForm(prev => (prev ? { ...prev, [key]: value } : prev));
  };

  if (loading) {
    return (
      <div className="space-y-5" aria-busy="true">
        <Header />
        <div className="grid gap-3 sm:grid-cols-2">
          {[1, 2].map(i => (
            <div key={i} className="h-40 rounded-xl bg-card/40 animate-pulse" />
          ))}
        </div>
      </div>
    );
  }

  if (!config || !form) {
    return (
      <AdminHubEmptyState
        icon={<Wallet />}
        title="Budget configuration unavailable"
        description="Could not load the LLM budget configuration. Check that the API is running."
      />
    );
  }

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between gap-3">
        <Header />
        <div className="flex items-center gap-2">
          {isDirty && (
            <Button variant="outline" size="sm" onClick={handleReset} className="text-xs">
              <RotateCcw className="mr-1 h-3 w-3" aria-hidden="true" /> Discard
            </Button>
          )}
          <Button
            size="sm"
            onClick={() => void handleSave()}
            disabled={!isDirty || saving}
            className="text-xs"
          >
            {saving ? (
              <Loader2 className="mr-1 h-3 w-3 animate-spin" aria-hidden="true" />
            ) : (
              <Save className="mr-1 h-3 w-3" aria-hidden="true" />
            )}
            Save changes
          </Button>
        </div>
      </div>

      {error && (
        <div
          role="alert"
          className="flex items-center gap-2 rounded-lg border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive"
        >
          <AlertCircle className="h-4 w-4 shrink-0" aria-hidden="true" />
          {error}
        </div>
      )}
      {success && (
        <div
          role="status"
          className="flex items-center gap-2 rounded-lg border border-[hsl(var(--c-success)/0.4)] bg-[hsl(var(--c-success)/0.1)] p-3 text-sm text-[hsl(var(--c-success-ink))]"
        >
          <CheckCircle2 className="h-4 w-4 shrink-0" aria-hidden="true" />
          {success}
        </div>
      )}

      <UsagePanel
        usage={usage}
        failed={usageFailed}
        onRetry={() => void loadUsage()}
        config={config}
      />

      <section
        aria-labelledby="budget-settings-title"
        className="rounded-xl border border-border/60 bg-card/70 p-4 space-y-4"
      >
        <div className="flex items-baseline justify-between gap-2">
          <h3 id="budget-settings-title" className="text-sm font-semibold text-foreground">
            Caps, degrade and quotas
          </h3>
          <p className="text-xs text-muted-foreground">
            {config.source === 'database' ? 'Saved' : 'Defaults, nothing saved yet'}
            {config.lastUpdatedAt && (
              <> · last updated {new Date(config.lastUpdatedAt).toLocaleString()}</>
            )}
          </p>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <NumberField
            label="Daily cap (USD)"
            help="Spend of all AI in a UTC day. Over it no paid call starts. 0 turns paid AI off."
            value={form.dailyBudgetUsd}
            onChange={v => updateField('dailyBudgetUsd', v)}
            min={0}
            step={0.01}
          />
          <NumberField
            label="Monthly cap (USD)"
            help="Spend of all AI in the calendar month."
            value={form.monthlyBudgetUsd}
            onChange={v => updateField('monthlyBudgetUsd', v)}
            min={0}
            step={0.01}
          />
          <NumberField
            label="Degrade threshold (%)"
            help="Share of the daily cap. From here answers use the reserve model of the agent profile and background work pauses until the next day."
            value={form.degradeThresholdPercent}
            onChange={v => updateField('degradeThresholdPercent', v)}
            min={1}
            max={100}
          />
          <div className="hidden sm:block" aria-hidden="true" />
          <NumberField
            label="Background quota (%)"
            help="Share of the caps for background work: chunk translation, admin operations, events."
            value={form.backgroundQuotaPercent}
            onChange={v => updateField('backgroundQuotaPercent', v)}
            min={0}
            max={100}
          />
          <NumberField
            label="Evaluation quota (%)"
            help="Share of the caps for evaluations (KbQuality, dataset evaluation). With the background quota, at most 100%."
            value={form.evaluationQuotaPercent}
            onChange={v => updateField('evaluationQuotaPercent', v)}
            min={0}
            max={100}
          />
        </div>
      </section>
    </div>
  );
}

function Header() {
  return (
    <div>
      <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
        Budget &amp; quotas
      </h2>
      <p className="text-sm text-muted-foreground mt-0.5">
        The LLM gateway checks these caps before every call. Answers can use the whole cap.
      </p>
    </div>
  );
}

function UsagePanel({
  usage,
  failed,
  onRetry,
  config,
}: {
  usage: LlmBudgetUsage | null;
  failed: boolean;
  onRetry: () => void;
  config: LlmSystemConfigDto;
}) {
  if (failed || !usage) {
    return (
      <div
        role="status"
        className="flex items-center justify-between gap-2 rounded-lg border border-border bg-muted/40 p-3 text-sm text-foreground"
      >
        <span className="flex items-center gap-2">
          <AlertCircle className="h-4 w-4 shrink-0" aria-hidden="true" />
          Could not load the spend: the request failed. This says nothing about the gateway.
        </span>
        <Button variant="outline" size="sm" onClick={onRetry} className="text-xs">
          Retry
        </Button>
      </div>
    );
  }

  if (!usage.available || !usage.today || !usage.month) {
    return (
      <div
        role="status"
        className="flex items-center gap-2 rounded-lg border border-[hsl(var(--c-warning)/0.4)] bg-[hsl(var(--c-warning)/0.1)] p-3 text-sm text-[hsl(var(--c-warning-ink))]"
      >
        <AlertCircle className="h-4 w-4 shrink-0" aria-hidden="true" />
        The spend cannot be read right now: the gateway refuses paid calls until it can.
      </div>
    );
  }

  return (
    <div className="space-y-3">
      {usage.isDegraded ? (
        <p
          role="status"
          className="rounded-lg border border-[hsl(var(--c-warning)/0.4)] bg-[hsl(var(--c-warning)/0.1)] p-3 text-sm text-[hsl(var(--c-warning-ink))]"
        >
          <strong>Degrade active.</strong> Today&apos;s spend reached the degrade threshold: answers
          use the reserve model of the agent profile, background work and evaluations are paused
          until tomorrow.
        </p>
      ) : (
        <p className="text-xs text-muted-foreground">
          Under the degrade threshold: answers use the main model.
        </p>
      )}
      <div className="grid gap-4 lg:grid-cols-2">
        <SpendCard
          title="Today"
          subtitle={usage.day ? `${usage.day} (UTC)` : undefined}
          spend={usage.today}
          capUsd={config.dailyBudgetUsd}
          thresholdPercent={config.degradeThresholdPercent}
          backgroundQuotaPercent={config.backgroundQuotaPercent}
          evaluationQuotaPercent={config.evaluationQuotaPercent}
        />
        <SpendCard
          title="This month"
          spend={usage.month}
          capUsd={config.monthlyBudgetUsd}
          backgroundQuotaPercent={config.backgroundQuotaPercent}
          evaluationQuotaPercent={config.evaluationQuotaPercent}
        />
      </div>
      <p className="text-xs text-muted-foreground">
        The spend the gateway counts: the cost log plus the calls still open.
      </p>
    </div>
  );
}

function SpendCard({
  title,
  subtitle,
  spend,
  capUsd,
  thresholdPercent,
  backgroundQuotaPercent,
  evaluationQuotaPercent,
}: {
  title: string;
  subtitle?: string;
  spend: LlmBudgetSpend;
  capUsd: number;
  thresholdPercent?: number;
  backgroundQuotaPercent: number;
  evaluationQuotaPercent: number;
}) {
  const titleId = useId();
  const usedPercent = capUsd > 0 ? Math.min(100, (spend.totalUsd / capUsd) * 100) : 100;
  const quotaCap = (percent: number) => (capUsd * percent) / 100;

  return (
    <section
      aria-labelledby={titleId}
      className="rounded-xl border border-border/60 bg-card/70 p-4 space-y-3"
    >
      <div className="flex items-baseline justify-between gap-2">
        <h3 id={titleId} className="text-sm font-semibold text-foreground">
          {title}
        </h3>
        {subtitle && <span className="text-xs text-muted-foreground">{subtitle}</span>}
      </div>
      <p className="text-sm text-foreground">
        <span className="font-mono font-semibold">{usd(spend.totalUsd)}</span>
        <span className="text-muted-foreground"> of {usd(capUsd)}</span>
      </p>
      <div
        role="meter"
        aria-label={`${title}: share of the cap spent`}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={Math.round(usedPercent)}
        aria-valuetext={
          capUsd > 0 ? `${Math.round(usedPercent)}% of the cap` : 'Cap is 0: paid AI is off'
        }
        className="relative h-2 rounded-full bg-muted"
      >
        <div className="h-2 rounded-full bg-primary" style={{ width: `${usedPercent}%` }} />
        {thresholdPercent !== undefined && (
          <div
            className="absolute top-[-2px] h-3 w-0.5 bg-foreground/60"
            style={{ left: `${Math.min(100, thresholdPercent)}%` }}
            title={`Degrade threshold ${thresholdPercent}%`}
          />
        )}
      </div>
      <dl className="grid grid-cols-3 gap-2 text-xs">
        <SpendRow label="Answers" value={usd(spend.answersUsd)} />
        <SpendRow
          label="Background"
          value={usd(spend.backgroundUsd)}
          limit={`quota ${usd(quotaCap(backgroundQuotaPercent))}`}
        />
        <SpendRow
          label="Evaluations"
          value={usd(spend.evaluationsUsd)}
          limit={`quota ${usd(quotaCap(evaluationQuotaPercent))}`}
        />
      </dl>
    </section>
  );
}

function SpendRow({ label, value, limit }: { label: string; value: string; limit?: string }) {
  return (
    <div>
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="font-mono text-foreground">{value}</dd>
      {limit && <dd className="text-muted-foreground">{limit}</dd>}
    </div>
  );
}

function NumberField({
  label,
  help,
  value,
  onChange,
  min,
  max,
  step = 1,
}: {
  label: string;
  help: string;
  value: string;
  onChange: (v: string) => void;
  min: number;
  max?: number;
  step?: number;
}) {
  const id = useId();
  return (
    <div>
      <label htmlFor={id} className="block text-xs font-medium text-foreground mb-1">
        {label}
      </label>
      <input
        id={id}
        type="number"
        inputMode="decimal"
        value={value}
        onChange={e => onChange(e.target.value)}
        min={min}
        max={max}
        step={step}
        aria-describedby={`${id}-help`}
        className="w-full rounded-md border border-border bg-background px-2 py-1.5 text-sm text-foreground focus:outline-none focus:ring-2 focus:ring-primary/50"
      />
      <p id={`${id}-help`} className="mt-1 text-xs text-muted-foreground">
        {help}
      </p>
    </div>
  );
}
