/**
 * CameraViewfinder — SP6 Phase C.2.B v2 component (Issue #789).
 *
 * Full-screen camera viewfinder for /gamebook/upload Step 2 (mobile-primary,
 * also rendered on desktop when DesktopDropFallback variant not chosen). Pure
 * component (Wave D.3 pattern): all i18n labels are resolved by orchestrator
 * and injected via `labels` prop.
 *
 * Mapped from `admin-mockups/design_files/sp6-libro-game-photo-upload.jsx`
 * (`CameraViewfinder` component for variants ready/low-light/detection-failed).
 *
 * Visual states (per contract §5.6 + light-meter contract §9):
 *   - ready             green frame + light=ok + shutter enabled
 *   - low-light         yellow frame + light=low (red bar) + shutter disabled
 *   - detection-failed  red dashed frame + corner accents red
 *   - capturing         shutter pressed flash (≤500ms transition)
 *
 * Ownership boundary:
 *   - This component is a PRESENTATIONAL viewfinder. It accepts a
 *     `videoStream: MediaStream | null` (orchestrator gets via
 *     `requestCameraStream`) and renders to a `<video>` element via ref.
 *   - It does NOT call `getUserMedia` itself — that lives in
 *     `lib/gamebook-upload/camera-capabilities.ts` and the orchestrator host
 *     hook (Phase C.2.C).
 *   - When `videoStream` is null, renders a placeholder gradient (mockup
 *     fallback) — orchestrator handles permission states upstream.
 *
 * Cleanup contract:
 *   - Component does NOT close MediaStream tracks on unmount — that is
 *     ORCHESTRATOR responsibility (Phase C.2.C). Stream lifetime is owned
 *     by the host hook so re-renders here don't tear down the camera.
 *
 * Gate C — MeepleCard fit decision (DIVERGE):
 *   The shared `MeepleCard` cannot express:
 *     1. Full-screen black background with overlay UI controls
 *     2. Page-detection frame overlay with corner accents + light-meter strip
 *     3. Bottom shutter row with disabled-state visual feedback
 *     4. Captured-pages thumb strip (horizontal scroll)
 *   This is a bespoke camera viewport surface, not a card. Documented per
 *   contract §13 Gate C.
 *
 * a11y:
 *   - `<section role="region" aria-label="...">` (camera viewfinder landmark)
 *   - `<video aria-label="...">` for SR description of live preview
 *   - Light-meter has `role="meter"` + `aria-valuenow={value}` +
 *     `aria-valuemin={0}` + `aria-valuemax={1}`
 *   - Captured strip = `<ul role="list">`
 *   - Hint badge has `role="status"` + `aria-live="polite"` for low-light
 *     announcements without interrupting interaction
 *   - Shutter button = `<button type="button" aria-label="...">` with
 *     explicit aria-disabled for low-light/failed states
 *
 * prefers-reduced-motion:
 *   - Shutter inner-scale animation disabled
 *   - Light-meter pulse animation disabled (orchestrator-driven)
 *
 * data-slot="camera-viewfinder" + data-light-level + data-capturing for E2E.
 */

'use client';

import type { ReactElement } from 'react';
import { useEffect, useRef } from 'react';

import clsx from 'clsx';

import type { CapturedPage, LightMeterReading } from '@/lib/gamebook-upload';

export interface CameraViewfinderLabels {
  /** Pre-resolved aria-label for `<section>` region (e.g. "Mirino fotocamera"). */
  readonly regionAria: string;
  /** Pre-resolved aria-label for `<video>` element (e.g. "Anteprima fotocamera dal vivo"). */
  readonly videoAria: string;
  /** Aria-label for shutter button (e.g. "Scatta foto"). */
  readonly shutterAria: string;
  /** Aria-label for gallery fallback button (e.g. "Apri galleria"). */
  readonly galleryAria: string;
  /** Aria-label for cancel/close button (e.g. "Annulla acquisizione"). */
  readonly cancelAria: string;
  /** Aria-label for done button (e.g. "Procedi all'indicizzazione"). */
  readonly doneAria: string;
  /** Aria-label for flash toggle (e.g. "Attiva flash"). */
  readonly flashAria: string;
  /** SR-only label for light meter (e.g. "Misuratore luce"). */
  readonly lightMeterAria: string;
  /** Pre-resolved announcement for page detected (e.g. "Pagina riconosciuta"). */
  readonly pageDetectedAria: string;
  /** Light-meter pre-resolved hints (3 levels). */
  readonly lightHintGood: string;
  readonly lightHintLow: string;
  readonly lightHintTooDark: string;
  /** Detection-failed status hint (e.g. "Bordi non rilevati"). */
  readonly detectionFailedHint: string;
  /** Pre-resolved counter text (e.g. "8 / 50"). */
  readonly capturedCount: string;
  /** Pre-resolved aria-label for captured pages list (e.g. "8 pagine acquisite"). */
  readonly capturedListAria: string;
  /** Light-meter discrete value labels (e.g. "OK", "BASSA", "MEDIA"). */
  readonly lightValueOk: string;
  readonly lightValueLow: string;
  readonly lightValueMedium: string;
}

export interface CameraViewfinderProps {
  /** Live MediaStream from `requestCameraStream` — null when not yet granted. */
  readonly videoStream: MediaStream | null;
  /** Light meter reading (level + value 0..1). null while sampling. */
  readonly lightReading: LightMeterReading | null;
  /** True iff page-detection heuristic settled (green frame). */
  readonly pageDetected: boolean;
  /** Detection-failed flag (red dashed frame + corner accents). */
  readonly detectionFailed: boolean;
  /** Number of pages captured so far (drives counter + thumb strip). */
  readonly capturedCount: number;
  /** Captured pages thumbnails (most recent ≤5 shown). */
  readonly capturedThumbs: ReadonlyArray<CapturedPage>;
  /** True during shutter-press flash (≤500ms transition). */
  readonly isCapturing: boolean;
  /** Shutter button click handler. */
  readonly onShutterClick: () => void;
  /** Gallery fallback handler (opens file picker). */
  readonly onGalleryClick: () => void;
  /** Cancel/close handler (× button — opens CancelModal if capturedCount > 0). */
  readonly onCancelClick: () => void;
  /** Done handler (✓ button — disabled when capturedCount === 0). */
  readonly onDoneClick: () => void;
  readonly labels: CameraViewfinderLabels;
  readonly className?: string;
}

// Entity CSS vars (P2 #807 Task 6+7+8): replace inline HSL with CSS variable references
const ENTITY_TOOLKIT = 'var(--color-entity-toolkit)';
const ENTITY_EVENT = 'var(--color-entity-event)';
const ENTITY_AGENT = 'var(--color-entity-agent)';

function lightMeterFillColor(level: LightMeterReading['level'] | null): string {
  if (level === 'ok') return ENTITY_TOOLKIT;
  if (level === 'low' || level === 'too-dark') return ENTITY_EVENT;
  return ENTITY_AGENT;
}

function frameBorderColor(detectionFailed: boolean, pageDetected: boolean): string {
  if (detectionFailed) return ENTITY_EVENT;
  if (pageDetected) return ENTITY_TOOLKIT;
  return 'rgba(255, 255, 255, 0.4)';
}

export function CameraViewfinder({
  videoStream,
  lightReading,
  pageDetected,
  detectionFailed,
  capturedCount,
  capturedThumbs,
  isCapturing,
  onShutterClick,
  onGalleryClick,
  onCancelClick,
  onDoneClick,
  labels,
  className,
}: CameraViewfinderProps): ReactElement {
  const videoRef = useRef<HTMLVideoElement>(null);

  // Wire MediaStream → <video srcObject> via ref. Orchestrator owns lifecycle.
  useEffect(() => {
    const video = videoRef.current;
    if (!video) return;
    if (videoStream) {
      video.srcObject = videoStream;
      // play() returns a promise — ignore rejections from interrupted plays
      video.play().catch(() => {
        // expected on user-initiated tab switch / permission prompt
      });
    } else {
      video.srcObject = null;
    }
    return () => {
      // DO NOT stop tracks here — orchestrator owns stream lifetime
    };
  }, [videoStream]);

  const lightLevel = lightReading?.level ?? null;
  const lightValue = lightReading?.value ?? 0;
  const isOk = pageDetected && !detectionFailed && lightLevel === 'ok';
  const isLow = lightLevel === 'low' || lightLevel === 'too-dark';
  const shutterDisabled = isLow || detectionFailed;
  const doneDisabled = capturedCount === 0;
  const frameColor = frameBorderColor(detectionFailed, pageDetected);

  // Hint text (priority: detection-failed > low-light > page-detected)
  const hintText = detectionFailed
    ? labels.detectionFailedHint
    : isLow
      ? lightLevel === 'too-dark'
        ? labels.lightHintTooDark
        : labels.lightHintLow
      : isOk
        ? labels.pageDetectedAria
        : labels.lightHintGood;

  const lightValueText =
    lightLevel === 'ok'
      ? labels.lightValueOk
      : lightLevel === 'low' || lightLevel === 'too-dark'
        ? labels.lightValueLow
        : labels.lightValueMedium;

  return (
    <section
      data-slot="camera-viewfinder"
      data-light-level={lightLevel ?? 'unknown'}
      data-page-detected={pageDetected ? 'true' : 'false'}
      data-detection-failed={detectionFailed ? 'true' : 'false'}
      data-capturing={isCapturing ? 'true' : 'false'}
      role="region"
      aria-label={labels.regionAria}
      className={clsx('relative flex h-full w-full flex-col bg-black text-white', className)}
    >
      {/* Top toolbar: cancel + counter + flash */}
      <div
        data-slot="camera-viewfinder-top"
        className="flex shrink-0 items-center justify-between gap-3 px-3.5 py-3"
        style={{ background: 'linear-gradient(to bottom, rgba(0,0,0,0.6), transparent)' }}
      >
        <button
          type="button"
          data-slot="camera-viewfinder-cancel"
          onClick={onCancelClick}
          aria-label={labels.cancelAria}
          className={clsx(
            'flex h-9 w-9 cursor-pointer items-center justify-center rounded-full border-none text-lg text-white',
            'bg-black/50 transition-colors motion-reduce:transition-none',
            'hover:bg-black/70 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white'
          )}
        >
          ×
        </button>

        <span
          data-slot="camera-viewfinder-counter"
          aria-live="polite"
          className="inline-flex items-center gap-1.5 rounded-full bg-black/60 px-3 py-1.5 font-mono text-xs font-bold text-white tabular-nums"
        >
          <span aria-hidden="true">📸</span>
          <span>{labels.capturedCount}</span>
        </span>

        <button
          type="button"
          data-slot="camera-viewfinder-flash"
          aria-label={labels.flashAria}
          className={clsx(
            'flex h-9 w-9 cursor-pointer items-center justify-center rounded-full border-none text-lg text-white',
            'bg-black/50 transition-colors motion-reduce:transition-none',
            'hover:bg-black/70 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white'
          )}
        >
          ⚡
        </button>
      </div>

      {/* Viewfinder area */}
      <div
        data-slot="camera-viewfinder-vf"
        className="relative flex flex-1 min-h-0 items-center justify-center p-4"
        style={{
          background: isLow
            ? 'radial-gradient(ellipse at center, hsl(28, 30%, 12%) 0%, hsl(0, 0%, 4%) 100%)'
            : 'radial-gradient(ellipse at center, hsl(28, 35%, 22%) 0%, hsl(0, 0%, 8%) 100%)',
        }}
      >
        {/* Live video underlay (when stream available) */}
        <video
          ref={videoRef}
          data-slot="camera-viewfinder-video"
          aria-label={labels.videoAria}
          muted
          playsInline
          autoPlay
          className="absolute inset-0 h-full w-full object-cover opacity-50"
        />

        {/* Page-detection frame overlay */}
        <div
          data-slot="camera-viewfinder-frame"
          className={clsx(
            'relative aspect-[3/4] w-[78%] rounded-sm border-2 p-4',
            'transition-colors motion-reduce:transition-none'
          )}
          style={{
            borderColor: frameColor,
            borderStyle: detectionFailed ? 'dashed' : 'solid',
            background: detectionFailed ? 'hsla(350, 50%, 30%, 0.15)' : 'rgba(255, 250, 240, 0.06)',
          }}
        >
          {/* Corner accents */}
          {(['tl', 'tr', 'bl', 'br'] as const).map(c => (
            <span
              key={c}
              data-corner={c}
              aria-hidden="true"
              className={clsx(
                'absolute h-[18px] w-[18px]',
                c === 'tl' && '-left-0.5 -top-0.5 border-l-[3px] border-t-[3px]',
                c === 'tr' && '-right-0.5 -top-0.5 border-r-[3px] border-t-[3px]',
                c === 'bl' && '-bottom-0.5 -left-0.5 border-b-[3px] border-l-[3px]',
                c === 'br' && '-bottom-0.5 -right-0.5 border-b-[3px] border-r-[3px]'
              )}
              style={{ borderColor: frameColor }}
            />
          ))}

          {/* Status hint badge */}
          <div
            data-slot="camera-viewfinder-hint"
            role="status"
            aria-live="polite"
            className={clsx(
              'absolute -bottom-9 left-1/2 -translate-x-1/2 rounded-full px-3.5 py-1.5 text-xs font-bold text-white whitespace-nowrap font-display',
              detectionFailed ? 'bg-entity-event/95' : isOk ? 'bg-entity-toolkit/95' : 'bg-black/70'
            )}
          >
            {hintText}
          </div>
        </div>

        {/* Light meter (top-right pill) */}
        <div
          data-slot="camera-viewfinder-light-meter"
          className="absolute right-3.5 top-3.5 flex items-center gap-1.5 rounded-full bg-black/60 px-2.5 py-1"
          aria-label={labels.lightMeterAria}
        >
          <span aria-hidden="true" className="text-xs">
            ☀️
          </span>
          <div
            role="meter"
            aria-valuenow={Math.round(lightValue * 100) / 100}
            aria-valuemin={0}
            aria-valuemax={1}
            aria-valuetext={lightValueText}
            className="h-1 w-12 overflow-hidden rounded-full bg-white/20"
          >
            <div
              data-slot="camera-viewfinder-light-fill"
              className="h-full transition-[width,background-color] motion-reduce:transition-none"
              style={{
                width: `${Math.round(lightValue * 100)}%`,
                backgroundColor: lightMeterFillColor(lightLevel),
              }}
            />
          </div>
          <span
            data-slot="camera-viewfinder-light-value"
            className={clsx(
              'font-mono text-[9px] font-bold tabular-nums',
              isLow ? 'text-entity-event' : 'text-white'
            )}
          >
            {lightValueText}
          </span>
        </div>
      </div>

      {/* Captured strip (only when capturedCount > 0) */}
      {capturedCount > 0 && (
        <div
          data-slot="camera-viewfinder-strip"
          className="shrink-0 px-0 py-2"
          style={{ background: 'linear-gradient(to top, rgba(0,0,0,0.7), transparent)' }}
        >
          <ul
            role="list"
            aria-label={labels.capturedListAria}
            className="flex gap-1.5 overflow-x-auto px-3.5"
            style={{ scrollbarWidth: 'none' }}
          >
            {capturedThumbs.slice(0, 5).map(thumb => (
              <li
                key={thumb.pageNumber}
                data-slot="camera-viewfinder-thumb"
                className="relative shrink-0 overflow-hidden rounded-sm border-[1.5px] border-white/40"
                style={{ width: 44, height: 56 }}
              >
                <div
                  aria-hidden="true"
                  className="h-full w-full"
                  style={{
                    background: thumb.thumbObjectUrl
                      ? `url("${thumb.thumbObjectUrl}") center/cover no-repeat`
                      : `linear-gradient(135deg, hsl(${30 + thumb.pageNumber * 8}, 40%, 75%), hsl(${30 + thumb.pageNumber * 8}, 30%, 55%))`,
                  }}
                />
                <span className="absolute bottom-0.5 right-1 font-mono text-[9px] font-bold text-white drop-shadow-md">
                  {thumb.pageNumber}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}

      {/* Shutter row */}
      <div
        data-slot="camera-viewfinder-shutter-row"
        className="flex shrink-0 items-center justify-around bg-black/60 px-6 py-3.5"
      >
        <button
          type="button"
          data-slot="camera-viewfinder-gallery"
          onClick={onGalleryClick}
          aria-label={labels.galleryAria}
          className={clsx(
            'flex h-11 w-11 cursor-pointer items-center justify-center rounded-full border-none text-lg text-white',
            'bg-white/15 transition-colors motion-reduce:transition-none',
            'hover:bg-white/25 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white'
          )}
        >
          🖼️
        </button>

        <button
          type="button"
          data-slot="camera-viewfinder-shutter"
          onClick={onShutterClick}
          aria-label={labels.shutterAria}
          aria-disabled={shutterDisabled}
          disabled={shutterDisabled}
          className={clsx(
            'group flex h-16 w-16 items-center justify-center rounded-full border-[4px] border-white p-0',
            'transition-opacity motion-reduce:transition-none',
            shutterDisabled
              ? 'cursor-not-allowed opacity-50'
              : 'cursor-pointer bg-white/20 hover:bg-white/30 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white'
          )}
          style={{
            backgroundColor: shutterDisabled ? 'rgba(255,255,255,0.1)' : 'rgba(255,255,255,0.2)',
          }}
        >
          <span
            data-slot="camera-viewfinder-shutter-inner"
            aria-hidden="true"
            className={clsx(
              'h-12 w-12 rounded-full bg-white',
              'transition-transform motion-reduce:transition-none',
              !shutterDisabled && 'group-hover:scale-95',
              isCapturing && 'scale-90'
            )}
          />
        </button>

        <button
          type="button"
          data-slot="camera-viewfinder-done"
          onClick={onDoneClick}
          aria-label={labels.doneAria}
          aria-disabled={doneDisabled}
          disabled={doneDisabled}
          className={clsx(
            'flex h-11 w-11 items-center justify-center rounded-full border-none text-lg text-white',
            'transition-colors motion-reduce:transition-none',
            doneDisabled
              ? 'cursor-not-allowed bg-white/15 opacity-50'
              : 'cursor-pointer hover:opacity-90 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-white'
          )}
          style={{
            backgroundColor: doneDisabled ? 'rgba(255,255,255,0.15)' : ENTITY_TOOLKIT,
          }}
        >
          ✓
        </button>
      </div>
    </section>
  );
}
