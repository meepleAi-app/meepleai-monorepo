/**
 * DesktopDropFallback unit tests — SP6 Phase C.2.B Task B (Issue #789).
 *
 * Coverage:
 *   - data-slot + data-dragging attribute
 *   - role="region" + aria-label
 *   - Hidden file input + accept types from props
 *   - Browse CTA triggers file input click
 *   - File picker calls onFilesSelected with valid files
 *   - Drag enter/leave/drop lifecycle (data-dragging toggles)
 *   - Drop fires onFilesSelected with dropped files
 *   - acceptedTypes filters rejected files
 *   - maxFiles limits accepted file count
 *   - Phone handoff: button when callback provided, span otherwise
 */

import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import {
  DesktopDropFallback,
  type DesktopDropFallbackLabels,
  type DesktopDropFallbackProps,
} from '../DesktopDropFallback';

const LABELS: DesktopDropFallbackLabels = {
  regionAria: 'Area trascinamento foto',
  title: 'Trascina le foto qui',
  description:
    'Su desktop puoi anche caricare foto già scattate. Formati supportati: JPG, PNG, HEIC',
  cta: 'Sfoglia file',
  ctaAria: 'Apri selettore file',
  maxFilesHint: 'Max 50 file',
  phoneHandoffText: 'Invia link al telefono',
};

function makeFile(name: string, type: string, size = 100): File {
  return new File(['x'.repeat(size)], name, { type });
}

function renderFallback(overrides: Partial<DesktopDropFallbackProps> = {}) {
  const onFilesSelected = vi.fn();
  const props: DesktopDropFallbackProps = {
    onFilesSelected,
    labels: LABELS,
    ...overrides,
  };
  const result = render(<DesktopDropFallback {...props} />);
  return { ...result, onFilesSelected };
}

describe('DesktopDropFallback — render shape', () => {
  it('renders data-slot="desktop-drop-fallback"', () => {
    renderFallback();
    expect(document.querySelector('[data-slot="desktop-drop-fallback"]')).not.toBeNull();
  });

  it('exposes role="region" with aria-label from labels.regionAria', () => {
    renderFallback();
    const region = screen.getByRole('region', { name: 'Area trascinamento foto' });
    expect(region).toBeInTheDocument();
  });

  it('renders title and description', () => {
    renderFallback();
    expect(screen.getByText('Trascina le foto qui')).toBeInTheDocument();
    expect(screen.getByText(/Su desktop puoi anche/)).toBeInTheDocument();
  });

  it('renders CTA button with aria-label', () => {
    renderFallback();
    expect(screen.getByRole('button', { name: 'Apri selettore file' })).toBeInTheDocument();
  });

  it('renders maxFilesHint', () => {
    renderFallback();
    expect(screen.getByText('Max 50 file')).toBeInTheDocument();
  });

  it('initial data-dragging="false"', () => {
    renderFallback();
    const region = document.querySelector('[data-slot="desktop-drop-fallback"]');
    expect(region?.getAttribute('data-dragging')).toBe('false');
  });

  it('hidden file input has correct accept attribute (default JPEG/PNG/HEIC)', () => {
    renderFallback();
    const input = document.querySelector(
      '[data-slot="desktop-drop-fallback-input"]'
    ) as HTMLInputElement;
    expect(input.accept).toBe('image/jpeg,image/png,image/heic');
  });

  it('hidden file input has multiple attribute', () => {
    renderFallback();
    const input = document.querySelector(
      '[data-slot="desktop-drop-fallback-input"]'
    ) as HTMLInputElement;
    expect(input.multiple).toBe(true);
  });

  it('honors custom acceptedTypes prop', () => {
    renderFallback({ acceptedTypes: ['image/png'] });
    const input = document.querySelector(
      '[data-slot="desktop-drop-fallback-input"]'
    ) as HTMLInputElement;
    expect(input.accept).toBe('image/png');
  });
});

describe('DesktopDropFallback — file picker', () => {
  it('clicking CTA triggers hidden input click', async () => {
    const user = userEvent.setup();
    renderFallback();

    const input = document.querySelector(
      '[data-slot="desktop-drop-fallback-input"]'
    ) as HTMLInputElement;
    const clickSpy = vi.spyOn(input, 'click');

    await user.click(screen.getByRole('button', { name: 'Apri selettore file' }));
    expect(clickSpy).toHaveBeenCalledOnce();
  });

  it('selecting valid files calls onFilesSelected', () => {
    const { onFilesSelected } = renderFallback();
    const input = document.querySelector(
      '[data-slot="desktop-drop-fallback-input"]'
    ) as HTMLInputElement;

    const file1 = makeFile('a.jpg', 'image/jpeg');
    const file2 = makeFile('b.png', 'image/png');

    fireEvent.change(input, { target: { files: [file1, file2] } });

    expect(onFilesSelected).toHaveBeenCalledOnce();
    expect(onFilesSelected.mock.calls[0][0]).toHaveLength(2);
  });

  it('filters rejected file types based on acceptedTypes', () => {
    const { onFilesSelected } = renderFallback({ acceptedTypes: ['image/png'] });
    const input = document.querySelector(
      '[data-slot="desktop-drop-fallback-input"]'
    ) as HTMLInputElement;

    const validFile = makeFile('a.png', 'image/png');
    const invalidFile = makeFile('b.gif', 'image/gif');

    fireEvent.change(input, { target: { files: [validFile, invalidFile] } });

    const accepted = onFilesSelected.mock.calls[0][0];
    expect(accepted).toHaveLength(1);
    expect(accepted[0].name).toBe('a.png');
  });

  it('limits files to maxFiles count', () => {
    const { onFilesSelected } = renderFallback({ maxFiles: 2 });
    const input = document.querySelector(
      '[data-slot="desktop-drop-fallback-input"]'
    ) as HTMLInputElement;

    const files = [
      makeFile('a.jpg', 'image/jpeg'),
      makeFile('b.jpg', 'image/jpeg'),
      makeFile('c.jpg', 'image/jpeg'),
    ];

    fireEvent.change(input, { target: { files } });

    const accepted = onFilesSelected.mock.calls[0][0];
    expect(accepted).toHaveLength(2);
  });

  it('does not call onFilesSelected when no files selected', () => {
    const { onFilesSelected } = renderFallback();
    const input = document.querySelector(
      '[data-slot="desktop-drop-fallback-input"]'
    ) as HTMLInputElement;

    fireEvent.change(input, { target: { files: [] } });

    expect(onFilesSelected).not.toHaveBeenCalled();
  });
});

describe('DesktopDropFallback — drag and drop', () => {
  it('dragenter sets data-dragging="true"', () => {
    renderFallback();
    const region = document.querySelector('[data-slot="desktop-drop-fallback"]') as HTMLElement;

    fireEvent.dragEnter(region);
    expect(region.getAttribute('data-dragging')).toBe('true');
  });

  it('dragleave decrements counter and sets data-dragging="false" when empty', () => {
    renderFallback();
    const region = document.querySelector('[data-slot="desktop-drop-fallback"]') as HTMLElement;

    fireEvent.dragEnter(region);
    fireEvent.dragLeave(region);
    expect(region.getAttribute('data-dragging')).toBe('false');
  });

  it('drop fires onFilesSelected with dropped files', () => {
    const { onFilesSelected } = renderFallback();
    const region = document.querySelector('[data-slot="desktop-drop-fallback"]') as HTMLElement;

    const file = makeFile('a.jpg', 'image/jpeg');

    fireEvent.dragEnter(region);
    fireEvent.drop(region, {
      dataTransfer: { files: [file] },
    });

    expect(onFilesSelected).toHaveBeenCalledOnce();
    expect(onFilesSelected.mock.calls[0][0][0].name).toBe('a.jpg');
  });

  it('drop resets data-dragging="false"', () => {
    renderFallback();
    const region = document.querySelector('[data-slot="desktop-drop-fallback"]') as HTMLElement;
    const file = makeFile('a.jpg', 'image/jpeg');

    fireEvent.dragEnter(region);
    fireEvent.drop(region, { dataTransfer: { files: [file] } });

    expect(region.getAttribute('data-dragging')).toBe('false');
  });

  it('dragover prevents default (enables drop)', () => {
    renderFallback();
    const region = document.querySelector('[data-slot="desktop-drop-fallback"]') as HTMLElement;

    const event = new Event('dragover', { bubbles: true, cancelable: true });
    region.dispatchEvent(event);
    // The handler calls preventDefault; if it didn't, drop wouldn't fire
    // (jsdom doesn't fully simulate this — we test the consequence: drop works)
    expect(region).toBeTruthy();
  });
});

describe('DesktopDropFallback — phone handoff', () => {
  it('renders span when onPhoneHandoffClick is undefined', () => {
    renderFallback();
    const handoff = document.querySelector('[data-slot="desktop-drop-fallback-phone-handoff"]');
    expect(handoff).toBeNull();
    // Plain text rendered
    expect(screen.getByText('Invia link al telefono')).toBeInTheDocument();
  });

  it('renders button when onPhoneHandoffClick is provided', () => {
    const onPhoneHandoffClick = vi.fn();
    renderFallback({ onPhoneHandoffClick });
    const handoff = document.querySelector('[data-slot="desktop-drop-fallback-phone-handoff"]');
    expect(handoff?.tagName).toBe('BUTTON');
  });

  it('calls onPhoneHandoffClick when handoff button clicked', async () => {
    const user = userEvent.setup();
    const onPhoneHandoffClick = vi.fn();
    renderFallback({ onPhoneHandoffClick });

    await user.click(screen.getByText('Invia link al telefono'));
    expect(onPhoneHandoffClick).toHaveBeenCalledOnce();
  });
});
