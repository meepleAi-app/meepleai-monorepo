'use client';

/**
 * StatusBannerAdmin — admin form to edit the global status banner (Issue #1089).
 *
 * Backed by `useAdminStatusBanner` (read) + `useUpdateStatusBanner` (write).
 * Includes two pre-canned templates ("investigating" / "resolved") that
 * pre-populate the form to speed up incident response.
 */

import { useEffect, useState } from 'react';

import { toast } from 'sonner';

import { useAdminStatusBanner, useUpdateStatusBanner } from '@/hooks/queries/useAdminStatusBanner';
import { useTranslation } from '@/hooks/useTranslation';
import type {
  StatusBannerSeverity,
  UpdateStatusBannerCommand,
} from '@/lib/api/schemas/status-banner.schemas';

interface FormState {
  message: string;
  severity: StatusBannerSeverity;
  isActive: boolean;
  startsAt: string; // datetime-local input value ("" when empty)
  endsAt: string;
}

const EMPTY_FORM: FormState = {
  message: '',
  severity: 'Info',
  isActive: false,
  startsAt: '',
  endsAt: '',
};

const TEMPLATES = {
  investigating: {
    message: "We're currently investigating an issue affecting [service]. Updates will follow.",
    severity: 'Warning' as StatusBannerSeverity,
    isActive: true,
  },
  resolved: {
    message: 'The incident has been resolved. Thank you for your patience.',
    severity: 'Info' as StatusBannerSeverity,
    isActive: true,
  },
};

function toDatetimeLocal(iso: string | null): string {
  if (!iso) return '';
  // Trim seconds/ms + trailing Z for input compatibility — keeps the date
  // viewable in local format; backend round-trips as UTC ISO.
  const d = new Date(iso);
  if (isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function fromDatetimeLocal(value: string): string | null {
  if (!value) return null;
  const d = new Date(value);
  if (isNaN(d.getTime())) return null;
  return d.toISOString();
}

export function StatusBannerAdmin() {
  const { t } = useTranslation();
  const { data, isLoading, isError } = useAdminStatusBanner();
  const updateMutation = useUpdateStatusBanner();

  const [form, setForm] = useState<FormState>(EMPTY_FORM);

  // Hydrate form when the read query lands. Done in an effect so the user can
  // continue editing without being clobbered by subsequent refetches.
  const [hydrated, setHydrated] = useState(false);
  useEffect(() => {
    if (!hydrated && data) {
      setForm({
        message: data.message,
        severity: data.severity,
        isActive: data.isActive,
        startsAt: toDatetimeLocal(data.startsAt),
        endsAt: toDatetimeLocal(data.endsAt),
      });
      setHydrated(true);
    }
  }, [data, hydrated]);

  const applyTemplate = (key: keyof typeof TEMPLATES) => {
    const tpl = TEMPLATES[key];
    setForm(prev => ({
      ...prev,
      message: tpl.message,
      severity: tpl.severity,
      isActive: tpl.isActive,
    }));
  };

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const command: UpdateStatusBannerCommand = {
      message: form.message.trim(),
      severity: form.severity,
      isActive: form.isActive,
      startsAt: fromDatetimeLocal(form.startsAt),
      endsAt: fromDatetimeLocal(form.endsAt),
    };
    try {
      await updateMutation.mutateAsync(command);
      toast.success(t('common.statusBanner.admin.savedSuccess'));
    } catch {
      toast.error(t('common.statusBanner.admin.saveError'));
    }
  };

  if (isLoading) {
    return (
      <div className="text-sm text-muted-foreground" data-slot="status-banner-admin-loading">
        {t('common.statusBanner.admin.loading')}
      </div>
    );
  }

  if (isError) {
    return (
      <div className="text-sm text-destructive" data-slot="status-banner-admin-error">
        {t('common.statusBanner.admin.loadError')}
      </div>
    );
  }

  const messageInvalid = form.message.trim().length === 0 || form.message.length > 500;
  const submitting = updateMutation.isPending;

  return (
    <form
      onSubmit={handleSubmit}
      className="space-y-4"
      data-slot="status-banner-admin"
      aria-labelledby="status-banner-admin-title"
    >
      <div>
        <h2
          id="status-banner-admin-title"
          className="font-quicksand text-lg font-semibold text-foreground"
        >
          {t('common.statusBanner.admin.title')}
        </h2>
      </div>

      {/* Templates */}
      <fieldset className="space-y-2">
        <legend className="text-sm font-medium text-foreground">
          {t('common.statusBanner.admin.templates.title')}
        </legend>
        <div className="flex flex-wrap gap-2">
          <button
            type="button"
            onClick={() => applyTemplate('investigating')}
            className="rounded-md border border-border bg-card px-3 py-1.5 text-sm font-medium text-foreground hover:bg-muted focus:outline-none focus-visible:ring-2 focus-visible:ring-primary"
          >
            {t('common.statusBanner.admin.templates.investigating')}
          </button>
          <button
            type="button"
            onClick={() => applyTemplate('resolved')}
            className="rounded-md border border-border bg-card px-3 py-1.5 text-sm font-medium text-foreground hover:bg-muted focus:outline-none focus-visible:ring-2 focus-visible:ring-primary"
          >
            {t('common.statusBanner.admin.templates.resolved')}
          </button>
        </div>
      </fieldset>

      {/* Message */}
      <div className="space-y-1">
        <label
          htmlFor="status-banner-message"
          className="block text-sm font-medium text-foreground"
        >
          {t('common.statusBanner.admin.messageLabel')}
        </label>
        <textarea
          id="status-banner-message"
          value={form.message}
          onChange={e => setForm(prev => ({ ...prev, message: e.target.value }))}
          maxLength={500}
          rows={3}
          required
          placeholder={t('common.statusBanner.admin.messagePlaceholder')}
          aria-invalid={messageInvalid || undefined}
          className="w-full rounded-md border border-border bg-card px-3 py-2 text-sm text-foreground placeholder:text-muted-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-primary"
        />
        <p className="text-xs text-muted-foreground">{form.message.length} / 500</p>
      </div>

      {/* Severity */}
      <div className="space-y-1">
        <label
          htmlFor="status-banner-severity"
          className="block text-sm font-medium text-foreground"
        >
          {t('common.statusBanner.admin.severityLabel')}
        </label>
        <select
          id="status-banner-severity"
          value={form.severity}
          onChange={e =>
            setForm(prev => ({
              ...prev,
              severity: e.target.value as StatusBannerSeverity,
            }))
          }
          className="rounded-md border border-border bg-card px-3 py-2 text-sm text-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-primary"
        >
          <option value="Info">{t('common.statusBanner.severityLabel.info')}</option>
          <option value="Warning">{t('common.statusBanner.severityLabel.warning')}</option>
          <option value="Critical">{t('common.statusBanner.severityLabel.critical')}</option>
        </select>
      </div>

      {/* isActive */}
      <div className="flex items-center gap-2">
        <input
          type="checkbox"
          id="status-banner-isactive"
          checked={form.isActive}
          onChange={e => setForm(prev => ({ ...prev, isActive: e.target.checked }))}
          className="h-4 w-4 rounded border-border text-primary focus-visible:ring-2 focus-visible:ring-primary"
        />
        <label htmlFor="status-banner-isactive" className="text-sm text-foreground">
          {t('common.statusBanner.admin.isActiveLabel')}
        </label>
      </div>

      {/* startsAt / endsAt */}
      <div className="grid gap-3 sm:grid-cols-2">
        <div className="space-y-1">
          <label
            htmlFor="status-banner-startsat"
            className="block text-sm font-medium text-foreground"
          >
            {t('common.statusBanner.admin.startsAtLabel')}
          </label>
          <input
            type="datetime-local"
            id="status-banner-startsat"
            value={form.startsAt}
            onChange={e => setForm(prev => ({ ...prev, startsAt: e.target.value }))}
            className="w-full rounded-md border border-border bg-card px-3 py-2 text-sm text-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-primary"
          />
        </div>
        <div className="space-y-1">
          <label
            htmlFor="status-banner-endsat"
            className="block text-sm font-medium text-foreground"
          >
            {t('common.statusBanner.admin.endsAtLabel')}
          </label>
          <input
            type="datetime-local"
            id="status-banner-endsat"
            value={form.endsAt}
            onChange={e => setForm(prev => ({ ...prev, endsAt: e.target.value }))}
            className="w-full rounded-md border border-border bg-card px-3 py-2 text-sm text-foreground focus:outline-none focus-visible:ring-2 focus-visible:ring-primary"
          />
        </div>
      </div>

      <div className="pt-2">
        <button
          type="submit"
          disabled={submitting || messageInvalid}
          className="inline-flex items-center justify-center rounded-md bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground transition-colors hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-60 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2"
          data-slot="status-banner-admin-submit"
        >
          {submitting
            ? t('common.statusBanner.admin.saving')
            : t('common.statusBanner.admin.saveButton')}
        </button>
      </div>
    </form>
  );
}
