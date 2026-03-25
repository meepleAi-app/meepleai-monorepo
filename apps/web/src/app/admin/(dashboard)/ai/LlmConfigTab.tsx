'use client';

import { useState, useEffect, useCallback } from 'react';

import {
  Settings,
  Database,
  FileText,
  Shield,
  Save,
  RotateCcw,
  AlertCircle,
  CheckCircle2,
  Loader2,
} from 'lucide-react';

import { AdminHubEmptyState } from '@/components/admin/layout/AdminHubEmptyState';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { LlmSystemConfigDto, UpdateLlmSystemConfigRequest } from '@/lib/api/schemas';

type FormState = {
  circuitBreakerFailureThreshold: number;
  circuitBreakerOpenDurationSeconds: number;
  circuitBreakerSuccessThreshold: number;
  dailyBudgetUsd: number;
  monthlyBudgetUsd: number;
  fallbackChainJson: string;
};

function configToForm(config: LlmSystemConfigDto): FormState {
  return {
    circuitBreakerFailureThreshold: config.circuitBreakerFailureThreshold,
    circuitBreakerOpenDurationSeconds: config.circuitBreakerOpenDurationSeconds,
    circuitBreakerSuccessThreshold: config.circuitBreakerSuccessThreshold,
    dailyBudgetUsd: config.dailyBudgetUsd,
    monthlyBudgetUsd: config.monthlyBudgetUsd,
    fallbackChainJson: config.fallbackChainJson,
  };
}

export function LlmConfigTab() {
  const [config, setConfig] = useState<LlmSystemConfigDto | null>(null);
  const [form, setForm] = useState<FormState | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const fetchConfig = useCallback(async () => {
    try {
      setError(null);
      const data = await api.admin.getLlmSystemConfig();
      setConfig(data);
      setForm(configToForm(data));
    } catch {
      setError('Failed to load LLM configuration');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchConfig();
  }, [fetchConfig]);

  const isDirty =
    config &&
    form &&
    (form.circuitBreakerFailureThreshold !== config.circuitBreakerFailureThreshold ||
      form.circuitBreakerOpenDurationSeconds !== config.circuitBreakerOpenDurationSeconds ||
      form.circuitBreakerSuccessThreshold !== config.circuitBreakerSuccessThreshold ||
      form.dailyBudgetUsd !== config.dailyBudgetUsd ||
      form.monthlyBudgetUsd !== config.monthlyBudgetUsd ||
      form.fallbackChainJson !== config.fallbackChainJson);

  const handleSave = async () => {
    if (!form) return;

    if (form.dailyBudgetUsd > form.monthlyBudgetUsd) {
      setError('Daily budget cannot exceed monthly budget');
      return;
    }

    try {
      const parsed = JSON.parse(form.fallbackChainJson);
      if (!Array.isArray(parsed)) {
        setError('Fallback chain must be a valid JSON array');
        return;
      }
    } catch {
      setError('Fallback chain must be valid JSON');
      return;
    }

    setSaving(true);
    setError(null);
    setSuccess(null);

    try {
      const request: UpdateLlmSystemConfigRequest = { ...form };
      const updated = await api.admin.updateLlmSystemConfig(request);
      setConfig(updated);
      setForm(configToForm(updated));
      setSuccess('Configuration saved successfully');
      setTimeout(() => setSuccess(null), 3000);
    } catch {
      setError('Failed to save configuration');
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

  const updateField = <K extends keyof FormState>(key: K, value: FormState[K]) => {
    setForm(prev => (prev ? { ...prev, [key]: value } : prev));
  };

  if (loading) {
    return (
      <div className="space-y-5">
        <div>
          <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
            LLM Configuration
          </h2>
          <p className="text-sm text-muted-foreground mt-0.5">
            Unified view of all LLM configuration layers.
          </p>
        </div>
        <div className="grid gap-3 sm:grid-cols-2">
          {[1, 2, 3, 4].map(i => (
            <div
              key={i}
              className="h-40 rounded-xl bg-white/40 dark:bg-zinc-800/40 animate-pulse"
            />
          ))}
        </div>
      </div>
    );
  }

  if (!config || !form) {
    return (
      <AdminHubEmptyState
        icon={<Settings />}
        title="Configuration unavailable"
        description="Could not load LLM system configuration. Check that the API is running."
      />
    );
  }

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
            LLM Configuration
          </h2>
          <p className="text-sm text-muted-foreground mt-0.5">
            Unified view of all LLM configuration layers.
          </p>
        </div>
        <div className="flex items-center gap-2">
          {isDirty && (
            <Button variant="outline" size="sm" onClick={handleReset} className="text-xs">
              <RotateCcw className="mr-1 h-3 w-3" /> Discard
            </Button>
          )}
          <Button size="sm" onClick={handleSave} disabled={!isDirty || saving} className="text-xs">
            {saving ? (
              <Loader2 className="mr-1 h-3 w-3 animate-spin" />
            ) : (
              <Save className="mr-1 h-3 w-3" />
            )}
            Save Changes
          </Button>
        </div>
      </div>

      {/* Status messages */}
      {error && (
        <div className="flex items-center gap-2 rounded-lg border border-red-200 dark:border-red-800/40 bg-red-50 dark:bg-red-900/20 p-3 text-sm text-red-700 dark:text-red-400">
          <AlertCircle className="h-4 w-4 shrink-0" />
          {error}
        </div>
      )}
      {success && (
        <div className="flex items-center gap-2 rounded-lg border border-emerald-200 dark:border-emerald-800/40 bg-emerald-50 dark:bg-emerald-900/20 p-3 text-sm text-emerald-700 dark:text-emerald-400">
          <CheckCircle2 className="h-4 w-4 shrink-0" />
          {success}
        </div>
      )}

      {/* Source indicator */}
      <div className="flex items-center gap-2 text-xs text-muted-foreground">
        <span
          className={`inline-block h-2 w-2 rounded-full ${
            config.source === 'database' ? 'bg-emerald-500' : 'bg-amber-500'
          }`}
        />
        Source: <span className="font-medium">{config.source}</span>
        {config.lastUpdatedAt && (
          <>
            {' '}
            · Last updated:{' '}
            <span className="font-medium">{new Date(config.lastUpdatedAt).toLocaleString()}</span>
          </>
        )}
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        {/* Layer 1: Database Config (editable) */}
        <ConfigSection
          icon={<Database className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />}
          title="Database Configuration"
          badge="Editable"
          badgeColor="emerald"
        >
          <div className="space-y-3">
            <h4 className="text-xs font-medium text-muted-foreground uppercase tracking-wider">
              Circuit Breaker
            </h4>
            <div className="grid grid-cols-3 gap-2">
              <NumberField
                label="Failure Threshold"
                value={form.circuitBreakerFailureThreshold}
                onChange={v => updateField('circuitBreakerFailureThreshold', v)}
                min={1}
                max={100}
                integer
              />
              <NumberField
                label="Open Duration (s)"
                value={form.circuitBreakerOpenDurationSeconds}
                onChange={v => updateField('circuitBreakerOpenDurationSeconds', v)}
                min={1}
                max={3600}
                integer
              />
              <NumberField
                label="Success Threshold"
                value={form.circuitBreakerSuccessThreshold}
                onChange={v => updateField('circuitBreakerSuccessThreshold', v)}
                min={1}
                max={100}
                integer
              />
            </div>

            <h4 className="text-xs font-medium text-muted-foreground uppercase tracking-wider pt-2">
              Budget Limits
            </h4>
            <div className="grid grid-cols-2 gap-2">
              <NumberField
                label="Daily Budget (USD)"
                value={form.dailyBudgetUsd}
                onChange={v => updateField('dailyBudgetUsd', v)}
                min={0}
                max={10000}
                step={0.01}
              />
              <NumberField
                label="Monthly Budget (USD)"
                value={form.monthlyBudgetUsd}
                onChange={v => updateField('monthlyBudgetUsd', v)}
                min={0}
                max={100000}
                step={0.01}
              />
            </div>

            <h4 className="text-xs font-medium text-muted-foreground uppercase tracking-wider pt-2">
              Fallback Chain
            </h4>
            <textarea
              value={form.fallbackChainJson}
              onChange={e => updateField('fallbackChainJson', e.target.value)}
              rows={2}
              className="w-full rounded-md border border-slate-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-3 py-2 text-xs font-mono text-foreground focus:outline-none focus:ring-2 focus:ring-primary/50"
              placeholder='["OpenRouter", "Ollama"]'
            />
          </div>
        </ConfigSection>

        {/* Layer 2: appsettings (read-only) */}
        <ConfigSection
          icon={<FileText className="h-4 w-4 text-amber-600 dark:text-amber-400" />}
          title="appsettings.json"
          badge="Requires redeploy"
          badgeColor="amber"
        >
          <p className="text-xs text-muted-foreground mb-3">
            Default values from application configuration. Overridden when database config exists.
          </p>
          <div className="space-y-2 text-xs">
            <ReadOnlyRow
              label="Default Failure Threshold"
              value={
                config.circuitBreakerFailureThreshold != null
                  ? String(config.circuitBreakerFailureThreshold)
                  : '\u2014'
              }
            />
            <ReadOnlyRow
              label="Default Open Duration"
              value={
                config.circuitBreakerOpenDurationSeconds != null
                  ? `${config.circuitBreakerOpenDurationSeconds}s`
                  : '\u2014'
              }
            />
            <ReadOnlyRow
              label="Default Success Threshold"
              value={
                config.circuitBreakerSuccessThreshold != null
                  ? String(config.circuitBreakerSuccessThreshold)
                  : '\u2014'
              }
            />
            <ReadOnlyRow
              label="Default Daily Budget"
              value={
                config.dailyBudgetUsd != null ? `$${config.dailyBudgetUsd.toFixed(2)}` : '\u2014'
              }
            />
            <ReadOnlyRow
              label="Default Monthly Budget"
              value={
                config.monthlyBudgetUsd != null
                  ? `$${config.monthlyBudgetUsd.toFixed(2)}`
                  : '\u2014'
              }
            />
          </div>
        </ConfigSection>

        {/* Layer 3: Redis (operational state) */}
        <ConfigSection
          icon={<RotateCcw className="h-4 w-4 text-blue-600 dark:text-blue-400" />}
          title="Redis Cache"
          badge="Runtime state"
          badgeColor="blue"
        >
          <p className="text-xs text-muted-foreground mb-3">
            In-memory cache of configuration (60s TTL). Automatically invalidated on save.
          </p>
          <div className="space-y-2 text-xs">
            <ReadOnlyRow label="Cache TTL" value="60 seconds" />
            <ReadOnlyRow label="Provider" value="LlmSystemConfigProvider (Singleton)" />
            <ReadOnlyRow
              label="Last Invalidated"
              value={config.lastUpdatedAt ? new Date(config.lastUpdatedAt).toLocaleString() : 'N/A'}
            />
          </div>
        </ConfigSection>

        {/* Layer 4: Secrets (read-only) */}
        <ConfigSection
          icon={<Shield className="h-4 w-4 text-rose-600 dark:text-rose-400" />}
          title="Secrets"
          badge="Read-only"
          badgeColor="rose"
        >
          <p className="text-xs text-muted-foreground mb-3">
            API keys and sensitive credentials managed via .secret files. Never exposed in UI.
          </p>
          <div className="space-y-2 text-xs">
            <ReadOnlyRow label="OpenRouter API Key" value="••••••••" />
            <ReadOnlyRow label="Embedding Service Key" value="••••••••" />
            <ReadOnlyRow label="Management" value="infra/secrets/*.secret" />
          </div>
        </ConfigSection>
      </div>
    </div>
  );
}

function ConfigSection({
  icon,
  title,
  badge,
  badgeColor,
  children,
}: {
  icon: React.ReactNode;
  title: string;
  badge: string;
  badgeColor: 'emerald' | 'amber' | 'blue' | 'rose';
  children: React.ReactNode;
}) {
  const badgeClasses: Record<string, string> = {
    emerald: 'bg-emerald-100 dark:bg-emerald-900/30 text-emerald-700 dark:text-emerald-400',
    amber: 'bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400',
    blue: 'bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-400',
    rose: 'bg-rose-100 dark:bg-rose-900/30 text-rose-700 dark:text-rose-400',
  };

  return (
    <div className="rounded-xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-md p-4">
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-2">
          {icon}
          <h3 className="text-sm font-semibold text-foreground">{title}</h3>
        </div>
        <span
          className={`inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-medium ${badgeClasses[badgeColor]}`}
        >
          {badge}
        </span>
      </div>
      {children}
    </div>
  );
}

function NumberField({
  label,
  value,
  onChange,
  min,
  max,
  step = 1,
  integer,
}: {
  label: string;
  value: number;
  onChange: (v: number) => void;
  min: number;
  max: number;
  step?: number;
  integer?: boolean;
}) {
  return (
    <div>
      <label className="block text-[10px] text-muted-foreground mb-1">{label}</label>
      <input
        type="number"
        value={value}
        onChange={e => {
          const raw = Number(e.target.value);
          onChange(integer ? Math.round(raw) : raw);
        }}
        min={min}
        max={max}
        step={step}
        className="w-full rounded-md border border-slate-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-2 py-1.5 text-xs text-foreground focus:outline-none focus:ring-2 focus:ring-primary/50"
      />
    </div>
  );
}

function ReadOnlyRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex items-center justify-between py-1">
      <span className="text-muted-foreground">{label}</span>
      <span className="font-medium text-foreground font-mono">{value}</span>
    </div>
  );
}
