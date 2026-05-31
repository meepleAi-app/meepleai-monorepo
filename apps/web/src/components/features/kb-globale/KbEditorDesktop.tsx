/**
 * KbEditorDesktop — Phase 3 #1737 / Issue #1482 component #8.
 *
 * Metadata-edit form for owner-curated KB docs. Calls PATCH /api/v1/kb-docs/{id}
 * (BE PR #1732 #1687) with partial update semantics (D-4): null = no-op, ""/[] = clear.
 *
 * Error handling:
 *   - 404 → generic "Documento non trovato" (D-2 anti-info-leak)
 *   - 422 → inline field errors via aria-describedby (FluentValidation envelope)
 *   - other → generic error toast
 *
 * Owner-only affordance enforcement is upstream (DEC-3 — only mounted from
 * KbHomeDesktop recent cards which are BE-filtered to owned docs).
 *
 * Mounted lazy via dynamic({ ssr: false }) by KbGlobaleView (DEC-5 bundle).
 */
'use client';

import { type JSX, useState, useId } from 'react';

import { useUpdateKbDocMeta } from '@/hooks/mutations/useUpdateKbDocMeta';
import type { PatchKbDocMetadataRequest } from '@/lib/api/schemas/kb-docs-patch.schemas';
import type { UserKbDocDto } from '@/lib/api/schemas/kb-docs.schemas';

export interface KbEditorDesktopLabels {
  heading: string;
  titleLabel: string;
  documentTypeLabel: string;
  languageLabel: string;
  tagsLabel: string;
  saveLabel: string;
  cancelLabel: string;
  notFoundError: string;
  genericError: string;
  documentTypeOptions: Readonly<Record<string, string>>;
  languageOptions: Readonly<Record<string, string>>;
}

export interface KbEditorDesktopProps {
  doc: UserKbDocDto;
  onClose: () => void;
  labels: KbEditorDesktopLabels;
}

interface FieldError {
  title?: string;
  documentType?: string;
  language?: string;
  tags?: string;
}

interface HttpFieldError extends Error {
  status?: number;
  fieldErrors?: Record<string, string>;
}

function isHttpFieldError(err: unknown): err is HttpFieldError {
  return err instanceof Error && 'status' in err;
}

function is404Error(err: unknown): boolean {
  if (isHttpFieldError(err) && err.status === 404) return true;
  if (err instanceof Error && err.message.includes('404')) return true;
  return false;
}

function buildPatchBody(
  initial: UserKbDocDto,
  draft: { title: string; documentType: string; language: string; tags: string[] }
): PatchKbDocMetadataRequest {
  const body: PatchKbDocMetadataRequest = {};
  if (draft.title !== (initial.title ?? '')) {
    body.title = draft.title; // empty string ⇒ clear (D-4)
  }
  if (draft.documentType !== ((initial as { documentType?: string }).documentType ?? '')) {
    body.documentType = draft.documentType || undefined;
  }
  if (draft.language !== ((initial as { language?: string }).language ?? '')) {
    body.language = draft.language || undefined;
  }
  const initialTags = initial.tags ?? [];
  const tagsChanged =
    draft.tags.length !== initialTags.length || draft.tags.some((t, i) => t !== initialTags[i]);
  if (tagsChanged) {
    body.tags = draft.tags;
  }
  return body;
}

export function KbEditorDesktop(props: KbEditorDesktopProps): JSX.Element {
  const { doc, onClose, labels } = props;
  const titleId = useId();
  const titleErrId = useId();
  const tagsId = useId();
  const tagsErrId = useId();

  const [title, setTitle] = useState(doc.title ?? '');
  const [documentType, setDocumentType] = useState(
    (doc as { documentType?: string }).documentType ?? ''
  );
  const [language, setLanguage] = useState((doc as { language?: string }).language ?? '');
  const [tagsInput, setTagsInput] = useState((doc.tags ?? []).join(', '));
  const [errors, setErrors] = useState<FieldError>({});
  const [topError, setTopError] = useState<string | null>(null);

  const mutation = useUpdateKbDocMeta();

  const docTypeOptions = Object.keys(labels.documentTypeOptions);
  const languageOptions = Object.keys(labels.languageOptions);

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setErrors({});
    setTopError(null);

    const tagsArray = tagsInput
      .split(',')
      .map(t => t.trim())
      .filter(t => t.length > 0);

    const body = buildPatchBody(doc, {
      title,
      documentType,
      language,
      tags: tagsArray,
    });

    mutation.mutate(
      { id: doc.id, body },
      {
        onSuccess: () => {
          onClose();
        },
        onError: (err: unknown) => {
          if (is404Error(err)) {
            setTopError(labels.notFoundError);
            return;
          }
          if (isHttpFieldError(err) && err.status === 422 && err.fieldErrors) {
            setErrors(err.fieldErrors as FieldError);
            return;
          }
          setTopError(labels.genericError);
        },
      }
    );
  }

  return (
    <section
      className="bg-card border border-border rounded-lg p-6 flex flex-col gap-4"
      aria-label={labels.heading}
    >
      <h2 className="font-semibold text-foreground">{labels.heading}</h2>
      {topError && (
        <div role="alert" className="text-sm text-destructive">
          {topError}
        </div>
      )}
      <form onSubmit={handleSubmit} className="flex flex-col gap-4">
        {/* Title field */}
        <div className="flex flex-col gap-1">
          <label htmlFor={titleId} className="text-sm font-medium">
            {labels.titleLabel}
          </label>
          <input
            id={titleId}
            type="text"
            value={title}
            onChange={e => setTitle(e.target.value)}
            aria-describedby={errors.title ? titleErrId : undefined}
            aria-invalid={errors.title ? true : undefined}
            className="border border-border rounded px-2 py-1"
          />
          {errors.title && (
            <p id={titleErrId} className="text-sm text-destructive">
              {errors.title}
            </p>
          )}
        </div>

        {/* DocumentType field */}
        <div className="flex flex-col gap-1">
          <label className="text-sm font-medium" htmlFor={`${titleId}-doctype`}>
            {labels.documentTypeLabel}
          </label>
          <select
            id={`${titleId}-doctype`}
            value={documentType}
            onChange={e => setDocumentType(e.target.value)}
            className="border border-border rounded px-2 py-1"
          >
            <option value="">—</option>
            {docTypeOptions.map(opt => (
              <option key={opt} value={opt}>
                {labels.documentTypeOptions[opt]}
              </option>
            ))}
          </select>
        </div>

        {/* Language field */}
        <div className="flex flex-col gap-1">
          <label className="text-sm font-medium" htmlFor={`${titleId}-lang`}>
            {labels.languageLabel}
          </label>
          <select
            id={`${titleId}-lang`}
            value={language}
            onChange={e => setLanguage(e.target.value)}
            className="border border-border rounded px-2 py-1"
          >
            <option value="">—</option>
            {languageOptions.map(opt => (
              <option key={opt} value={opt}>
                {labels.languageOptions[opt]}
              </option>
            ))}
          </select>
        </div>

        {/* Tags field */}
        <div className="flex flex-col gap-1">
          <label htmlFor={tagsId} className="text-sm font-medium">
            {labels.tagsLabel}
          </label>
          <input
            id={tagsId}
            type="text"
            value={tagsInput}
            onChange={e => setTagsInput(e.target.value)}
            placeholder="tag1, tag2, …"
            aria-describedby={errors.tags ? tagsErrId : undefined}
            aria-invalid={errors.tags ? true : undefined}
            className="border border-border rounded px-2 py-1"
          />
          {errors.tags && (
            <p id={tagsErrId} className="text-sm text-destructive">
              {errors.tags}
            </p>
          )}
        </div>

        {/* Action buttons */}
        <div className="flex items-center gap-3 mt-2">
          <button
            type="submit"
            disabled={mutation.isPending}
            className="bg-primary text-primary-foreground rounded px-4 py-2 hover:opacity-90 disabled:opacity-50"
          >
            {labels.saveLabel}
          </button>
          <button
            type="button"
            onClick={onClose}
            className="border border-border rounded px-4 py-2 hover:bg-muted"
          >
            {labels.cancelLabel}
          </button>
        </div>
      </form>
    </section>
  );
}
