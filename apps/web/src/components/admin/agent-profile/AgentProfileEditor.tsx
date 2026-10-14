'use client';

/**
 * #4169 (ADR-095 D8, fetta 6c): la pagina «Agente».
 *
 * The system agent that answers users has one profile with immutable versions. The admin edits the
 * draft of the sections whose effect shows when reading an answer — identity, model, generation,
 * prompts — compares it with the published version, publishes it and can roll back to an archived
 * one. Retrieval and sources are shown read-only: their effect shows only when many questions are
 * measured, and nothing measures it before the answer evaluation (slice 7, #4170). The in-game
 * assistant is read-only too: its pilot is not enabled yet.
 */
import { useCallback, useEffect, useId, useMemo, useState, type ReactNode } from 'react';

import { Loader2 } from 'lucide-react';

import { priceStatus } from '@/components/admin/ai/model-price-status';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { api } from '@/lib/api';
import type {
  AgentProfileAdmin,
  AgentProfileContent,
  AiModelDto,
  SaveAgentProfileDraftRequest,
} from '@/lib/api/schemas';

const CITATION_STYLES: Record<string, string> = {
  PageReferencesInText: 'Page references in the text',
  StructuredEvents: 'Structured citation events',
};

const READ_ONLY_RETRIEVAL =
  'Read-only until the answer evaluation is in place: the effect of these settings shows only when many questions are measured, and nothing measures it yet. They unlock together with the evaluation.';

/** Il modulo: i numeri restano stringhe finché si scrive, e diventano numeri al salvataggio. */
interface EditableForm {
  name: string;
  description: string;
  persona: string;
  primaryModelId: string;
  fallbackModelId: string;
  temperature: string;
  maxResponseTokens: string;
  citationStyle: string;
  systemPrompts: Record<string, string>;
  fallbackLanguage: string;
}

function toForm(c: AgentProfileContent): EditableForm {
  return {
    name: c.name,
    description: c.description,
    persona: c.persona,
    primaryModelId: c.primaryModelId,
    fallbackModelId: c.fallbackModelId ?? '',
    temperature: String(c.temperature),
    maxResponseTokens: String(c.maxResponseTokens),
    citationStyle: c.citationStyle,
    systemPrompts: { ...c.systemPrompts },
    fallbackLanguage: c.fallbackLanguage,
  };
}

function toRequest(form: EditableForm): SaveAgentProfileDraftRequest {
  return {
    name: form.name,
    description: form.description,
    persona: form.persona,
    primaryModelId: form.primaryModelId,
    fallbackModelId: form.fallbackModelId === '' ? null : form.fallbackModelId,
    temperature: Number(form.temperature),
    maxResponseTokens: Number(form.maxResponseTokens),
    citationStyle: form.citationStyle,
    systemPrompts: form.systemPrompts,
    fallbackLanguage: form.fallbackLanguage,
  };
}

function sameForm(a: EditableForm, b: EditableForm): boolean {
  return JSON.stringify(toRequest(a)) === JSON.stringify(toRequest(b));
}

function errorMessage(err: unknown, fallback: string): string {
  return err instanceof Error && err.message ? err.message : fallback;
}

const dateTime = (iso: string | null) =>
  iso ? new Date(iso).toLocaleString('en-GB', { dateStyle: 'medium', timeStyle: 'short' }) : '—';

export function AgentProfileEditor() {
  const [profile, setProfile] = useState<AgentProfileAdmin | null>(null);
  const [models, setModels] = useState<AiModelDto[] | null>(null);
  const [form, setForm] = useState<EditableForm | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [confirmRollback, setConfirmRollback] = useState<number | null>(null);

  const adopt = useCallback((next: AgentProfileAdmin) => {
    setProfile(next);
    setForm(toForm((next.draft ?? next.published).content));
  }, []);

  useEffect(() => {
    void (async () => {
      try {
        adopt(await api.admin.getAgentProfile());
      } catch (err: unknown) {
        setLoadError(errorMessage(err, 'unknown error'));
      }
    })();
    void (async () => {
      try {
        setModels((await api.admin.getAiModels({ pageSize: 100 })).models);
      } catch {
        // Senza catalogo la pagina funziona: mancano solo gli avvisi sul prezzo.
        setModels(null);
      }
    })();
  }, [adopt]);

  const base = useMemo(
    () => (profile ? toForm((profile.draft ?? profile.published).content) : null),
    [profile]
  );
  const published = useMemo(() => (profile ? toForm(profile.published.content) : null), [profile]);
  const dirty = form !== null && base !== null && !sameForm(form, base);

  const run = async (action: () => Promise<AgentProfileAdmin>, failure: string) => {
    setBusy(true);
    setActionError(null);
    try {
      adopt(await action());
      setConfirmRollback(null);
    } catch (err: unknown) {
      setActionError(`${failure}: ${errorMessage(err, 'unknown error')}`);
    } finally {
      setBusy(false);
    }
  };

  if (loadError) {
    return (
      <div role="alert" className="rounded-xl border border-destructive/30 bg-destructive/10 p-4">
        <p className="text-sm font-medium text-destructive">Could not load the agent profile.</p>
        <p className="mt-1 text-xs text-muted-foreground">{loadError}</p>
      </div>
    );
  }

  if (!profile || !form || !published) {
    return (
      <div className="space-y-3" aria-busy="true">
        {[1, 2, 3].map(i => (
          <div key={i} className="h-24 rounded-xl bg-card/40 animate-pulse" />
        ))}
      </div>
    );
  }

  const set = <K extends keyof EditableForm>(key: K, value: EditableForm[K]) =>
    setForm(current => (current ? { ...current, [key]: value } : current));

  const readOnly = (profile.draft ?? profile.published).content;

  // #4255: il guscio admin è scuro ma non dichiara il colore del testo, che senza questa classe
  // resterebbe quello del tema chiaro, scuro su scuro.
  return (
    <div className="space-y-6 text-foreground">
      <div>
        <h1 className="font-quicksand text-xl font-semibold tracking-tight text-foreground">
          Agent profile
        </h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          The system agent that answers users. Edit a draft, compare it with the published version,
          publish it. Every answer records the version it used.
        </p>
        <p className="mt-2 text-sm text-foreground">
          Published: version {profile.published.versionNumber} · since{' '}
          {dateTime(profile.published.publishedAt)}
          <span className="text-muted-foreground">
            {' · '}
            {profile.draft
              ? `Draft: version ${profile.draft.versionNumber}${dirty ? ', with unsaved changes' : ''}`
              : dirty
                ? 'No draft yet: saving creates one'
                : 'No draft'}
          </span>
        </p>
      </div>

      {actionError && (
        <div
          role="alert"
          className="rounded-lg border border-destructive/40 bg-destructive/10 p-3 text-sm text-destructive"
        >
          {actionError}
        </div>
      )}

      <Section title="Identity">
        <TextField
          label="Name"
          value={form.name}
          published={published.name}
          onChange={v => set('name', v)}
        />
        <TextField
          label="Description"
          value={form.description}
          published={published.description}
          onChange={v => set('description', v)}
          multiline
        />
        <TextField
          label="Persona"
          value={form.persona}
          published={published.persona}
          onChange={v => set('persona', v)}
          multiline
        />
      </Section>

      <Section title="Model">
        <ModelField
          label="Primary model"
          value={form.primaryModelId}
          published={published.primaryModelId}
          models={models}
          onChange={v => set('primaryModelId', v)}
        />
        <ModelField
          label="Reserve model"
          value={form.fallbackModelId}
          published={published.fallbackModelId}
          models={models}
          optional
          onChange={v => set('fallbackModelId', v)}
          hint="Answers use it between the degrade threshold and the daily budget cap."
        />
      </Section>

      <Section title="Generation">
        <NumberField
          label="Temperature"
          value={form.temperature}
          published={published.temperature}
          min={0}
          max={2}
          step={0.1}
          onChange={v => set('temperature', v)}
        />
        <NumberField
          label="Max response tokens"
          value={form.maxResponseTokens}
          published={published.maxResponseTokens}
          min={1}
          max={32000}
          step={1}
          onChange={v => set('maxResponseTokens', v)}
        />
        <SelectField
          label="Citation style"
          value={form.citationStyle}
          published={published.citationStyle}
          options={Object.entries(CITATION_STYLES).map(([value, label]) => ({ value, label }))}
          display={v => CITATION_STYLES[v] ?? v}
          onChange={v => set('citationStyle', v)}
        />
      </Section>

      <PromptsSection
        form={form}
        published={published}
        onChange={(systemPrompts, fallbackLanguage) =>
          setForm(current => (current ? { ...current, systemPrompts, fallbackLanguage } : current))
        }
      />

      <ReadOnlySection title="Retrieval" note={READ_ONLY_RETRIEVAL}>
        <ReadOnlyValue
          label="Passages before reranking"
          value={String(readOnly.candidatePoolSize)}
        />
        <ReadOnlyValue label="Passages after reranking" value={String(readOnly.finalTopK)} />
        <ReadOnlyValue label="Vector similarity threshold" value={String(readOnly.minScore)} />
        <ReadOnlyValue
          label="Fusion weights (vector / keyword)"
          value={`${readOnly.vectorWeight} / ${readOnly.keywordWeight}`}
        />
        <ReadOnlyValue label="Reranker" value={readOnly.rerankerEnabled ? 'On' : 'Off'} />
      </ReadOnlySection>

      <ReadOnlySection title="Sources" note={READ_ONLY_RETRIEVAL}>
        <ReadOnlyValue label="Document categories" value={readOnly.allowedCategories.join(', ')} />
      </ReadOnlySection>

      <ReadOnlySection
        title="In-game assistant"
        note="Read-only until the in-game assistant pilot is enabled."
      >
        <ReadOnlyValue label="Enabled" value={readOnly.sessionAssistant.enabled ? 'Yes' : 'No'} />
        <ReadOnlyValue
          label="Model"
          value={readOnly.sessionAssistant.useReserveModel ? 'Reserve model' : 'Primary model'}
        />
      </ReadOnlySection>

      <div className="flex flex-wrap items-center gap-2 border-t border-border/60 pt-4">
        <Button
          onClick={() =>
            void run(
              () => api.admin.saveAgentProfileDraft(toRequest(form)),
              'Could not save the draft'
            )
          }
          disabled={busy || !dirty}
        >
          {busy && <Loader2 className="mr-1 h-4 w-4 animate-spin" aria-hidden="true" />}
          Save draft
        </Button>
        <Button
          variant="outline"
          onClick={() =>
            void run(() => api.admin.publishAgentProfileDraft(), 'Could not publish the draft')
          }
          disabled={busy || !profile.draft || dirty}
        >
          Publish draft
        </Button>
        {dirty && base && (
          <Button variant="ghost" onClick={() => setForm(base)} disabled={busy}>
            Discard changes
          </Button>
        )}
        <p className="text-xs text-muted-foreground">
          {dirty
            ? 'Save the draft before publishing.'
            : profile.draft
              ? 'Publishing archives the current version: answers use the new one at once.'
              : 'Change a field to start a draft.'}
        </p>
      </div>

      <History
        profile={profile}
        busy={busy}
        confirming={confirmRollback}
        onAsk={setConfirmRollback}
        onRollback={n =>
          void run(() => api.admin.rollbackAgentProfile(n), `Could not roll back to version ${n}`)
        }
      />
    </div>
  );
}

// ─── Sezioni e campi ─────────────────────────────────────────────────────────────

function Section({ title, children }: { title: string; children: ReactNode }) {
  const id = useId();
  return (
    <section
      aria-labelledby={id}
      className="space-y-3 rounded-xl border border-border/60 bg-card/70 p-4"
    >
      <h2 id={id} className="text-sm font-semibold text-foreground">
        {title}
      </h2>
      {children}
    </section>
  );
}

function ReadOnlySection({
  title,
  note,
  children,
}: {
  title: string;
  note: string;
  children: ReactNode;
}) {
  const id = useId();
  return (
    <section
      aria-labelledby={id}
      className="space-y-2 rounded-xl border border-border/60 bg-muted/40 p-4"
    >
      <h2 id={id} className="text-sm font-semibold text-foreground">
        {title}
      </h2>
      <p className="text-xs text-muted-foreground">{note}</p>
      <dl className="grid gap-x-4 gap-y-1 text-sm sm:grid-cols-[max-content_1fr]">{children}</dl>
    </section>
  );
}

function ReadOnlyValue({ label, value }: { label: string; value: string }) {
  return (
    <>
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="font-mono text-foreground">{value}</dd>
    </>
  );
}

/** «Published: …» accanto a un campo che la bozza ha cambiato. */
function PublishedValue({ value, current }: { value: string; current: string }) {
  if (value === current) return null;
  return <p className="text-xs text-muted-foreground">Published: {value === '' ? '—' : value}</p>;
}

function TextField({
  label,
  value,
  published,
  onChange,
  multiline = false,
}: {
  label: string;
  value: string;
  published: string;
  onChange: (value: string) => void;
  multiline?: boolean;
}) {
  const id = useId();
  return (
    <div className="space-y-1">
      <Label htmlFor={id}>{label}</Label>
      {multiline ? (
        <Textarea id={id} value={value} rows={2} onChange={e => onChange(e.target.value)} />
      ) : (
        <Input id={id} value={value} onChange={e => onChange(e.target.value)} />
      )}
      <PublishedValue value={published} current={value} />
    </div>
  );
}

function NumberField({
  label,
  value,
  published,
  min,
  max,
  step,
  onChange,
}: {
  label: string;
  value: string;
  published: string;
  min: number;
  max: number;
  step: number;
  onChange: (value: string) => void;
}) {
  const id = useId();
  return (
    <div className="space-y-1">
      <Label htmlFor={id}>{label}</Label>
      <Input
        id={id}
        type="number"
        inputMode="decimal"
        min={min}
        max={max}
        step={step}
        value={value}
        onChange={e => onChange(e.target.value)}
        className="max-w-[12rem]"
      />
      <PublishedValue value={published} current={value} />
    </div>
  );
}

function SelectField({
  label,
  value,
  published,
  options,
  display,
  onChange,
}: {
  label: string;
  value: string;
  published: string;
  options: { value: string; label: string }[];
  display: (value: string) => string;
  onChange: (value: string) => void;
}) {
  const id = useId();
  return (
    <div className="space-y-1">
      <Label htmlFor={id}>{label}</Label>
      <NativeSelect id={id} value={value} onChange={onChange}>
        {options.map(o => (
          <option key={o.value} value={o.value}>
            {o.label}
          </option>
        ))}
      </NativeSelect>
      <PublishedValue value={display(published)} current={display(value)} />
    </div>
  );
}

function NativeSelect({
  id,
  value,
  onChange,
  children,
}: {
  id: string;
  value: string;
  onChange: (value: string) => void;
  children: ReactNode;
}) {
  return (
    <select
      id={id}
      value={value}
      onChange={e => onChange(e.target.value)}
      className="h-9 w-full max-w-md rounded-md border border-border bg-background px-2 text-sm text-foreground"
    >
      {children}
    </select>
  );
}

const usd = (value: number) =>
  `$${value.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 4 })}`;

/** Che cosa sa il catalogo del prezzo di un modello: la regola D5 rifiuta chi non ce l'ha. */
function priceHint(modelId: string, models: AiModelDto[] | null): { text: string; warn: boolean } {
  if (models === null) return { text: 'The model catalog could not be read.', warn: true };
  const model = models.find(m => m.modelId === modelId);
  if (!model) {
    return { text: 'Not in the model catalog: publishing is refused.', warn: true };
  }
  const { pricing } = model.settings;
  switch (priceStatus(model)) {
    case 'foreign-currency':
      return { text: 'Not priced in USD: publishing is refused.', warn: true };
    case 'unpriced':
      return {
        text: 'Price 0 on a paid provider: counted as free, so it escapes the budget cap.',
        warn: true,
      };
    case 'free':
      return { text: 'Free: served in house.', warn: false };
    default:
      return {
        text: `${usd(pricing.inputPricePerMillion)} / ${usd(pricing.outputPricePerMillion)} per 1M tokens (in / out).`,
        warn: false,
      };
  }
}

function ModelField({
  label,
  value,
  published,
  models,
  onChange,
  optional = false,
  hint,
}: {
  label: string;
  value: string;
  published: string;
  models: AiModelDto[] | null;
  onChange: (value: string) => void;
  optional?: boolean;
  hint?: string;
}) {
  const id = useId();
  const ids = (models ?? []).map(m => m.modelId).sort((a, b) => a.localeCompare(b));
  if (value !== '' && !ids.includes(value)) ids.unshift(value);
  const price = value === '' ? null : priceHint(value, models);

  return (
    <div className="space-y-1">
      <Label htmlFor={id}>{label}</Label>
      <NativeSelect id={id} value={value} onChange={onChange}>
        {optional && <option value="">None</option>}
        {ids.map(modelId => (
          <option key={modelId} value={modelId}>
            {modelId}
          </option>
        ))}
      </NativeSelect>
      {hint && <p className="text-xs text-muted-foreground">{hint}</p>}
      {price && (
        <p
          className={
            price.warn ? 'text-xs font-medium text-destructive' : 'text-xs text-muted-foreground'
          }
        >
          {price.text}
        </p>
      )}
      <PublishedValue value={published} current={value} />
    </div>
  );
}

function PromptsSection({
  form,
  published,
  onChange,
}: {
  form: EditableForm;
  published: EditableForm;
  onChange: (prompts: Record<string, string>, fallbackLanguage: string) => void;
}) {
  const newLanguageId = useId();
  const fallbackId = useId();
  const [newLanguage, setNewLanguage] = useState('');
  const languages = Object.keys(form.systemPrompts).sort((a, b) => a.localeCompare(b));
  const candidate = newLanguage.trim().toLowerCase();
  const canAdd = /^[a-z]{2}$/.test(candidate) && !(candidate in form.systemPrompts);

  return (
    <Section title="Prompts">
      <div className="space-y-1">
        <Label htmlFor={fallbackId}>Fallback language</Label>
        <NativeSelect
          id={fallbackId}
          value={form.fallbackLanguage}
          onChange={v => onChange(form.systemPrompts, v)}
        >
          {languages.map(lang => (
            <option key={lang} value={lang}>
              {lang}
            </option>
          ))}
        </NativeSelect>
        <p className="text-xs text-muted-foreground">
          The language of the prompt used when the question is in a language without its own prompt.
        </p>
        <PublishedValue value={published.fallbackLanguage} current={form.fallbackLanguage} />
      </div>

      {languages.map(lang => (
        <PromptField
          key={lang}
          language={lang}
          value={form.systemPrompts[lang] ?? ''}
          published={published.systemPrompts[lang]}
          removable={lang !== form.fallbackLanguage}
          onChange={value =>
            onChange({ ...form.systemPrompts, [lang]: value }, form.fallbackLanguage)
          }
          onRemove={() => {
            const { [lang]: _removed, ...rest } = form.systemPrompts;
            onChange(rest, form.fallbackLanguage);
          }}
        />
      ))}

      <div className="flex flex-wrap items-end gap-2">
        <div className="space-y-1">
          <Label htmlFor={newLanguageId}>New language (ISO 639-1)</Label>
          <Input
            id={newLanguageId}
            value={newLanguage}
            maxLength={2}
            onChange={e => setNewLanguage(e.target.value)}
            className="w-24"
          />
        </div>
        <Button
          variant="outline"
          disabled={!canAdd}
          onClick={() => {
            onChange({ ...form.systemPrompts, [candidate]: '' }, form.fallbackLanguage);
            setNewLanguage('');
          }}
        >
          Add language
        </Button>
      </div>
    </Section>
  );
}

function PromptField({
  language,
  value,
  published,
  removable,
  onChange,
  onRemove,
}: {
  language: string;
  value: string;
  published: string | undefined;
  removable: boolean;
  onChange: (value: string) => void;
  onRemove: () => void;
}) {
  const id = useId();
  return (
    <div className="space-y-1">
      <div className="flex items-center justify-between gap-2">
        <Label htmlFor={id}>System prompt ({language})</Label>
        {removable && (
          <Button variant="ghost" size="sm" onClick={onRemove}>
            Remove {language}
          </Button>
        )}
      </div>
      <Textarea id={id} value={value} rows={5} onChange={e => onChange(e.target.value)} />
      {published === undefined ? (
        <p className="text-xs text-muted-foreground">Not in the published version.</p>
      ) : (
        published !== value && (
          <p className="text-xs text-muted-foreground">Changed from the published version.</p>
        )
      )}
    </div>
  );
}

function History({
  profile,
  busy,
  confirming,
  onAsk,
  onRollback,
}: {
  profile: AgentProfileAdmin;
  busy: boolean;
  confirming: number | null;
  onAsk: (versionNumber: number | null) => void;
  onRollback: (versionNumber: number) => void;
}) {
  const id = useId();
  return (
    <section aria-labelledby={id} className="space-y-2">
      <h2 id={id} className="text-sm font-semibold text-foreground">
        History
      </h2>
      <p className="text-xs text-muted-foreground">
        Rolling back publishes the content of an archived version as a new version: the history
        stays as it is.
      </p>
      <div className="overflow-x-auto rounded-xl border border-border/60 bg-card/70">
        <table className="w-full text-sm">
          <caption className="sr-only">Agent profile versions</caption>
          <thead>
            <tr className="border-b border-border/60 text-left text-xs text-muted-foreground">
              <th scope="col" className="px-3 py-2 font-medium">
                Version
              </th>
              <th scope="col" className="px-3 py-2 font-medium">
                Status
              </th>
              <th scope="col" className="px-3 py-2 font-medium">
                Created
              </th>
              <th scope="col" className="px-3 py-2 font-medium">
                Published
              </th>
              <th scope="col" className="px-3 py-2 font-medium">
                Archived
              </th>
              <th scope="col" className="px-3 py-2 font-medium">
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {profile.versions.map(v => (
              <tr
                key={v.id}
                aria-label={`Version ${v.versionNumber} (${v.status})`}
                className="border-b border-border/40"
              >
                <td className="px-3 py-2 font-mono text-foreground">{v.versionNumber}</td>
                <td className="px-3 py-2 text-foreground">{v.status}</td>
                <td className="px-3 py-2 text-muted-foreground">{dateTime(v.createdAt)}</td>
                <td className="px-3 py-2 text-muted-foreground">{dateTime(v.publishedAt)}</td>
                <td className="px-3 py-2 text-muted-foreground">{dateTime(v.archivedAt)}</td>
                <td className="px-3 py-2 text-right">
                  {v.status === 'Archived' &&
                    (confirming === v.versionNumber ? (
                      <span className="inline-flex gap-1">
                        <Button
                          size="sm"
                          onClick={() => onRollback(v.versionNumber)}
                          disabled={busy}
                        >
                          Confirm roll back
                        </Button>
                        <Button
                          size="sm"
                          variant="ghost"
                          onClick={() => onAsk(null)}
                          disabled={busy}
                        >
                          Cancel
                        </Button>
                      </span>
                    ) : (
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => onAsk(v.versionNumber)}
                        disabled={busy}
                      >
                        Roll back
                      </Button>
                    ))}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
