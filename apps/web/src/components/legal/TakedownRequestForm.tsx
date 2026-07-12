/**
 * TakedownRequestForm — public "template takedown request" form (ADR-051, Issue #529).
 *
 * There is NO backend endpoint for takedown requests and no email-alias intake
 * pipeline. This form is a functional TEMPLATE: it validates the required fields
 * client-side, then either opens a `mailto:takedown@meepleai.app` link with a
 * structured, pre-filled subject + body, or copies that same request text to the
 * clipboard. No data leaves the browser.
 *
 * Rendered inside {@link LegalPageLayout}'s `footerSlot`, so it lives within the
 * `LegalLocaleProvider` and can use both `useLegalLocale()` (IT/EN toggle) and
 * `useTranslation()` (`t()`).
 */

'use client';

import { useId, useMemo, useState, type FormEvent } from 'react';

import { useLegalLocale } from '@/components/legal/LegalLocaleToggle';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { useTranslation } from '@/hooks/useTranslation';

export const TAKEDOWN_EMAIL = 'takedown@meepleai.app';

interface TakedownFields {
  name: string;
  email: string;
  work: string;
  cardUrl: string;
  description: string;
  confirmed: boolean;
}

type FieldKey = keyof Omit<TakedownFields, 'confirmed'>;

const EMPTY_FIELDS: TakedownFields = {
  name: '',
  email: '',
  work: '',
  cardUrl: '',
  description: '',
  confirmed: false,
};

// Simple, permissive email shape check — the browser + backend do the real work.
const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

interface ValidationErrors {
  name?: string;
  email?: string;
  work?: string;
  cardUrl?: string;
  description?: string;
  confirmed?: string;
}

/**
 * Build the structured request body text from the form fields. Shared by both
 * the mailto link and the "copy to clipboard" action so they stay identical.
 */
export function buildTakedownBody(
  fields: TakedownFields,
  labels: {
    name: string;
    email: string;
    work: string;
    cardUrl: string;
    description: string;
    confirm: string;
  }
): string {
  return [
    `${labels.name}: ${fields.name}`,
    `${labels.email}: ${fields.email}`,
    `${labels.work}: ${fields.work}`,
    `${labels.cardUrl}: ${fields.cardUrl}`,
    '',
    `${labels.description}:`,
    fields.description,
    '',
    labels.confirm,
  ].join('\n');
}

export function TakedownRequestForm() {
  const { t } = useTranslation();
  const { locale } = useLegalLocale();
  const baseId = useId();

  const [fields, setFields] = useState<TakedownFields>(EMPTY_FIELDS);
  const [errors, setErrors] = useState<ValidationErrors>({});
  const [submitted, setSubmitted] = useState(false);
  const [copied, setCopied] = useState(false);

  // Field label strings, resolved once per render (also embedded in the email body).
  const labels = useMemo(
    () => ({
      name: t('legal.takedown.form.nameLabel'),
      email: t('legal.takedown.form.emailLabel'),
      work: t('legal.takedown.form.workLabel'),
      cardUrl: t('legal.takedown.form.cardUrlLabel'),
      description: t('legal.takedown.form.descriptionLabel'),
      confirm: t('legal.takedown.form.confirmLabel'),
    }),
    [t]
  );

  function validate(current: TakedownFields): ValidationErrors {
    const next: ValidationErrors = {};
    const requiredMsg = t('legal.takedown.form.requiredError');

    if (!current.name.trim()) next.name = requiredMsg;

    if (!current.email.trim()) {
      next.email = requiredMsg;
    } else if (!EMAIL_RE.test(current.email.trim())) {
      next.email = t('legal.takedown.form.emailInvalidError');
    }

    if (!current.work.trim()) next.work = requiredMsg;
    if (!current.cardUrl.trim()) next.cardUrl = requiredMsg;
    if (!current.description.trim()) next.description = requiredMsg;
    if (!current.confirmed) next.confirmed = t('legal.takedown.form.confirmError');

    return next;
  }

  function setField(key: keyof TakedownFields, value: string | boolean) {
    setCopied(false);
    setFields(prev => {
      const next = { ...prev, [key]: value };
      // Re-validate incrementally only after the first submit attempt so we
      // don't shout at the user before they've tried anything.
      if (submitted) setErrors(validate(next));
      return next;
    });
  }

  function buildRequest(): { subject: string; body: string } {
    const subject = t('legal.takedown.form.mailSubject', {
      work: fields.work.trim() || '—',
    });
    const body = buildTakedownBody(fields, labels);
    return { subject, body };
  }

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSubmitted(true);
    const nextErrors = validate(fields);
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) return;

    const { subject, body } = buildRequest();
    const mailto = `mailto:${TAKEDOWN_EMAIL}?subject=${encodeURIComponent(
      subject
    )}&body=${encodeURIComponent(body)}`;
    // Trigger the user's mail client.
    window.location.href = mailto;
  }

  async function handleCopy() {
    setSubmitted(true);
    const nextErrors = validate(fields);
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) return;

    const { subject, body } = buildRequest();
    const text = `${subject}\n\n${body}`;
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
    } catch {
      // Clipboard unavailable (e.g. insecure context) — silently no-op; the
      // mailto path still works. We deliberately avoid a disruptive alert.
      setCopied(false);
    }
  }

  const hasErrors = submitted && Object.keys(errors).length > 0;

  const textInputFields: Array<{
    key: FieldKey;
    type: 'text' | 'email' | 'url';
    autoComplete?: string;
  }> = [
    { key: 'name', type: 'text', autoComplete: 'name' },
    { key: 'email', type: 'email', autoComplete: 'email' },
    { key: 'work', type: 'text' },
    { key: 'cardUrl', type: 'url' },
  ];

  const labelFor: Record<FieldKey, string> = {
    name: t('legal.takedown.form.nameLabel'),
    email: t('legal.takedown.form.emailLabel'),
    work: t('legal.takedown.form.workLabel'),
    cardUrl: t('legal.takedown.form.cardUrlLabel'),
    description: t('legal.takedown.form.descriptionLabel'),
  };
  const placeholderFor: Record<FieldKey, string> = {
    name: t('legal.takedown.form.namePlaceholder'),
    email: t('legal.takedown.form.emailPlaceholder'),
    work: t('legal.takedown.form.workPlaceholder'),
    cardUrl: t('legal.takedown.form.cardUrlPlaceholder'),
    description: t('legal.takedown.form.descriptionPlaceholder'),
  };

  return (
    <form
      noValidate
      onSubmit={handleSubmit}
      data-slot="takedown-request-form"
      aria-label={t('legal.takedown.form.heading')}
      className="mx-auto mt-6 w-full max-w-2xl rounded-2xl border border-border bg-card p-6 text-left"
    >
      <h2 className="text-lg font-semibold text-foreground">{t('legal.takedown.form.heading')}</h2>
      <p className="mt-1 text-sm text-muted-foreground">{t('legal.takedown.form.intro')}</p>

      {hasErrors && (
        <p
          role="alert"
          data-testid="takedown-error-summary"
          className="mt-4 rounded-md border border-destructive/40 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {t('legal.takedown.form.errorSummary')}
        </p>
      )}

      <div className="mt-4 flex flex-col gap-4">
        {textInputFields.map(({ key, type, autoComplete }) => {
          const fieldId = `${baseId}-${key}`;
          const errorId = `${fieldId}-error`;
          const error = errors[key];
          return (
            <div key={key} className="flex flex-col gap-1.5">
              <Label htmlFor={fieldId}>
                {labelFor[key]}
                <span aria-hidden="true" className="ml-0.5 text-destructive">
                  *
                </span>
              </Label>
              <Input
                id={fieldId}
                type={type}
                value={fields[key]}
                onChange={e => setField(key, e.target.value)}
                placeholder={placeholderFor[key]}
                autoComplete={autoComplete}
                required
                aria-required="true"
                aria-invalid={error ? 'true' : undefined}
                aria-describedby={error ? errorId : undefined}
              />
              {error && (
                <p id={errorId} role="alert" className="text-sm text-destructive">
                  {error}
                </p>
              )}
            </div>
          );
        })}

        {/* Description (textarea) */}
        {(() => {
          const fieldId = `${baseId}-description`;
          const errorId = `${fieldId}-error`;
          const error = errors.description;
          return (
            <div className="flex flex-col gap-1.5">
              <Label htmlFor={fieldId}>
                {labelFor.description}
                <span aria-hidden="true" className="ml-0.5 text-destructive">
                  *
                </span>
              </Label>
              <Textarea
                id={fieldId}
                rows={4}
                value={fields.description}
                onChange={e => setField('description', e.target.value)}
                placeholder={placeholderFor.description}
                required
                aria-required="true"
                aria-invalid={error ? 'true' : undefined}
                aria-describedby={error ? errorId : undefined}
              />
              {error && (
                <p id={errorId} role="alert" className="text-sm text-destructive">
                  {error}
                </p>
              )}
            </div>
          );
        })()}

        {/* Good-faith + accuracy confirmation */}
        {(() => {
          const fieldId = `${baseId}-confirm`;
          const errorId = `${fieldId}-error`;
          const error = errors.confirmed;
          return (
            <div className="flex flex-col gap-1.5">
              <div className="flex items-start gap-2">
                <Checkbox
                  id={fieldId}
                  checked={fields.confirmed}
                  onCheckedChange={checked => setField('confirmed', checked === true)}
                  aria-required="true"
                  aria-invalid={error ? 'true' : undefined}
                  aria-describedby={error ? errorId : undefined}
                  className="mt-0.5"
                />
                <Label htmlFor={fieldId} className="text-sm font-normal text-muted-foreground">
                  {t('legal.takedown.form.confirmLabel')}
                </Label>
              </div>
              {error && (
                <p id={errorId} role="alert" className="text-sm text-destructive">
                  {error}
                </p>
              )}
            </div>
          );
        })()}
      </div>

      <div className="mt-6 flex flex-col gap-3 sm:flex-row">
        <Button type="submit" data-testid="takedown-submit">
          {t('legal.takedown.form.submit')}
        </Button>
        <Button
          type="button"
          variant="outline"
          onClick={handleCopy}
          data-testid="takedown-copy"
          aria-live="polite"
        >
          {copied ? t('legal.takedown.form.copied') : t('legal.takedown.form.copy')}
        </Button>
      </div>

      <p className="mt-4 text-xs text-muted-foreground">
        {locale === 'it' ? 'Invia a: ' : 'Send to: '}
        <a
          href={`mailto:${TAKEDOWN_EMAIL}`}
          className="underline hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        >
          {TAKEDOWN_EMAIL}
        </a>
      </p>
    </form>
  );
}
