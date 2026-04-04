import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { AiReadySection } from './AiReadySection';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

describe('AiReadySection', () => {
  it('shows upload CTA when no PDF', () => {
    render(
      <AiReadySection
        gameId="123"
        hasCustomPdf={false}
        hasRagAccess={false}
        onUploadClick={() => {}}
      />
    );
    expect(screen.getByRole('button', { name: /carica regolamento/i })).toBeInTheDocument();
  });

  it('shows AI ready badge when RAG access is true', () => {
    render(
      <AiReadySection
        gameId="123"
        hasCustomPdf={true}
        hasRagAccess={true}
        onUploadClick={() => {}}
      />
    );
    expect(screen.getByText(/ai pronta/i)).toBeInTheDocument();
  });

  it('shows chat link when AI is ready', () => {
    render(
      <AiReadySection
        gameId="123"
        hasCustomPdf={true}
        hasRagAccess={true}
        onUploadClick={() => {}}
      />
    );
    expect(screen.getByRole('link', { name: /chiedi alle regole/i })).toBeInTheDocument();
  });

  it('shows processing state when PDF exists but no RAG', () => {
    render(
      <AiReadySection
        gameId="123"
        hasCustomPdf={true}
        hasRagAccess={false}
        onUploadClick={() => {}}
      />
    );
    expect(screen.getByText(/elaborazione/i)).toBeInTheDocument();
  });
});
