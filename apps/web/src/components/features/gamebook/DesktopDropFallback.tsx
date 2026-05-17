/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or entity-colored CTA; mockup .e-bg pattern. DS-12 will introduce primitives encoding bg via className. */
/**
 * DesktopDropFallback — SP6 Phase C.2.B v2 component (Issue #789).
 *
 * Desktop-only drag-and-drop file picker shown for /gamebook/upload Step 2
 * when viewport ≥1024px (orchestrator detects via useMediaQuery). Pure
 * component (Wave D.3 pattern): all i18n labels are resolved by orchestrator
 * and injected via `labels` prop.
 *
 * Mapped from `admin-mockups/design_files/sp6-libro-game-photo-upload.jsx`
 * (`DesktopDropFallback` component).
 *
 * Behavior:
 *   - Drop zone with dashed orange border (game entity)
 *   - "Trascina le foto qui" hero text
 *   - "Sfoglia file" button → triggers hidden `<input type="file">`
 *   - Footer: "Invia link al telefono" — sends OTP to phone for camera handoff
 *     (orchestrator handles via separate hook; rendered as plain text/link
 *     here per pure-component pattern; orchestrator can wrap in `<a>` if URL
 *     prop provided in future)
 *
 * Drag-over visual feedback via `data-dragging="true"` attribute (CSS
 * driven, no inline state styling drift).
 *
 * Gate C — MeepleCard fit decision (DIVERGE):
 *   Drop zones are not entity cards. `MeepleCard` is for entity displays.
 *   This is a transient input affordance with bespoke dashed-border
 *   treatment + drag-over state. Documented per contract §13 Gate C.
 *
 * a11y:
 *   - `<div role="region" aria-label="...">` (drop zone landmark)
 *   - `<input type="file" multiple accept="image/*">` keyboard-activatable
 *     via "Sfoglia file" button which forwards click to hidden input via ref
 *   - Drag events have `dragenter/dragleave/dragover/drop` handlers; visual
 *     feedback via data-dragging attribute (no announcer needed — visual)
 *   - prefers-reduced-motion: drag-over scale animation disabled
 *
 * data-slot="desktop-drop-fallback" + data-dragging for E2E selectors.
 */

'use client';

import type { DragEvent, ReactElement } from 'react';
import { useCallback, useRef, useState } from 'react';

import clsx from 'clsx';

const DEFAULT_ACCEPTED_TYPES = ['image/jpeg', 'image/png', 'image/heic'] as const;
const DEFAULT_MAX_FILES = 50;

export interface DesktopDropFallbackLabels {
  /** Aria-label for the dropzone region (e.g. "Area trascinamento foto"). */
  readonly regionAria: string;
  /** Hero text (e.g. "Trascina le foto qui"). */
  readonly title: string;
  /** Description (e.g. "Su desktop puoi anche caricare foto già scattate. Formati supportati: JPG, PNG, HEIC"). */
  readonly description: string;
  /** Pre-resolved CTA button label with icon (e.g. "Sfoglia file"). */
  readonly cta: string;
  /** Pre-resolved aria-label for browse button. */
  readonly ctaAria: string;
  /** Footer hint text — interpolated max files (e.g. "Max 50 file"). */
  readonly maxFilesHint: string;
  /** Phone-handoff link text (e.g. "Invia link al telefono"). */
  readonly phoneHandoffText: string;
}

export interface DesktopDropFallbackProps {
  /** Files-selected handler. Receives array of accepted File objects. */
  readonly onFilesSelected: (files: File[]) => void;
  /** Optional accepted MIME types (default JPEG/PNG/HEIC). */
  readonly acceptedTypes?: readonly string[];
  /** Optional max files (default 50 per backend). Excess files dropped silently. */
  readonly maxFiles?: number;
  /** Optional handler for "Invia link al telefono" — when undefined, text rendered as plain span. */
  readonly onPhoneHandoffClick?: () => void;
  readonly labels: DesktopDropFallbackLabels;
  readonly className?: string;
}

// game entity colours replaced with Tailwind entity-token classes (P2 #807 Task 6+7+8)
// 'var(--color-entity-game)' → text-entity-game, GAME_HSL_DIM → border dynamic (kept inline), GAME_HSL_BG → bg-entity-game/[0.04]

export function DesktopDropFallback({
  onFilesSelected,
  acceptedTypes = DEFAULT_ACCEPTED_TYPES,
  maxFiles = DEFAULT_MAX_FILES,
  onPhoneHandoffClick,
  labels,
  className,
}: DesktopDropFallbackProps): ReactElement {
  const [isDragging, setIsDragging] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);
  // Use ref counter to handle nested-element drag enter/leave correctly.
  const dragCounterRef = useRef(0);

  const handleFiles = useCallback(
    (fileList: FileList | null) => {
      if (!fileList || fileList.length === 0) return;
      const accepted: File[] = [];
      for (let i = 0; i < fileList.length && accepted.length < maxFiles; i++) {
        const file = fileList[i];
        if (acceptedTypes.length === 0 || acceptedTypes.includes(file.type)) {
          accepted.push(file);
        }
      }
      if (accepted.length > 0) {
        onFilesSelected(accepted);
      }
    },
    [acceptedTypes, maxFiles, onFilesSelected]
  );

  const handleDragEnter = useCallback((e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    dragCounterRef.current += 1;
    if (dragCounterRef.current === 1) {
      setIsDragging(true);
    }
  }, []);

  const handleDragLeave = useCallback((e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    dragCounterRef.current = Math.max(0, dragCounterRef.current - 1);
    if (dragCounterRef.current === 0) {
      setIsDragging(false);
    }
  }, []);

  const handleDragOver = useCallback((e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
  }, []);

  const handleDrop = useCallback(
    (e: DragEvent<HTMLDivElement>) => {
      e.preventDefault();
      e.stopPropagation();
      dragCounterRef.current = 0;
      setIsDragging(false);
      handleFiles(e.dataTransfer.files);
    },
    [handleFiles]
  );

  const handleBrowseClick = useCallback(() => {
    fileInputRef.current?.click();
  }, []);

  return (
    <div
      data-slot="desktop-drop-fallback"
      data-dragging={isDragging ? 'true' : 'false'}
      role="region"
      aria-label={labels.regionAria}
      onDragEnter={handleDragEnter}
      onDragLeave={handleDragLeave}
      onDragOver={handleDragOver}
      onDrop={handleDrop}
      className={clsx(
        'flex flex-col items-center gap-3 rounded-xl border-2 border-dashed bg-entity-game/[0.04] px-8 py-14 text-center',
        'transition-colors motion-reduce:transition-none',
        isDragging ? 'border-entity-game' : 'border-entity-game/40',
        className
      )}
    >
      {/* Hidden file input — keyboard-activated via Browse button */}
      <input
        ref={fileInputRef}
        type="file"
        multiple
        accept={acceptedTypes.join(',')}
        onChange={e => handleFiles(e.target.files)}
        data-slot="desktop-drop-fallback-input"
        className="sr-only"
        tabIndex={-1}
      />

      {/* Icon */}
      <div
        aria-hidden="true"
        data-slot="desktop-drop-fallback-icon"
        className="text-6xl leading-none text-entity-game"
      >
        📁
      </div>

      {/* Title */}
      <h3
        data-slot="desktop-drop-fallback-title"
        className="font-display text-xl font-bold text-foreground leading-tight"
      >
        {labels.title}
      </h3>

      {/* Description (pre-resolved including types + max-files) */}
      <p
        data-slot="desktop-drop-fallback-description"
        className="text-sm leading-relaxed text-foreground"
      >
        {labels.description}
      </p>

      {/* CTA */}
      <button
        type="button"
        data-slot="desktop-drop-fallback-cta"
        onClick={handleBrowseClick}
        aria-label={labels.ctaAria}
        className={clsx(
          'rounded-lg border-none px-6 py-3 text-sm font-bold text-white',
          'cursor-pointer transition-opacity motion-reduce:transition-none',
          'hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500'
        )}
        style={{ backgroundColor: 'var(--color-entity-game)' }}
      >
        🖼️ {labels.cta}
      </button>

      {/* Max files hint */}
      <p className="text-[11px] text-foreground">{labels.maxFilesHint}</p>

      {/* Footer: phone handoff */}
      <p className="mt-2 text-[11px] text-foreground">
        {onPhoneHandoffClick ? (
          <button
            type="button"
            data-slot="desktop-drop-fallback-phone-handoff"
            onClick={onPhoneHandoffClick}
            className="cursor-pointer underline transition-colors hover:opacity-80 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500"
            style={{ color: 'var(--color-entity-game)' }}
          >
            {labels.phoneHandoffText}
          </button>
        ) : (
          <span style={{ color: 'var(--color-entity-game)' }}>{labels.phoneHandoffText}</span>
        )}
      </p>
    </div>
  );
}
