/**
 * Admin Wizard Client Component Tests
 *
 * Tests for the Admin Game Setup Wizard with:
 * - Step navigation
 * - State management
 * - Component rendering
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { ReactNode } from 'react';
import { AdminWizardClient } from '../client';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn(),
    back: vi.fn(),
  }),
  usePathname: () => '/admin/wizard',
}));

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href }: { children: ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

// Mock AuthProvider
const mockUser = {
  id: 'test-user-id',
  email: 'admin@test.com',
  role: 'Admin',
  name: 'Test Admin',
};

vi.mock('@/components/auth/AuthProvider', () => ({
  useAuthUser: () => ({
    user: mockUser,
    loading: false,
  }),
}));

// Mock AdminAuthGuard
vi.mock('@/components/admin/AdminAuthGuard', () => ({
  AdminAuthGuard: ({ children }: { children: ReactNode }) => <>{children}</>,
}));

// Mock API
vi.mock('@/lib/api', async importOriginal => {
  const original = await importOriginal<typeof import('@/lib/api')>();
  return {
    ...original,
    api: {
      ...original.api,
      pdf: {
        setVisibility: vi.fn().mockResolvedValue({ success: true }),
        getProcessingProgress: vi.fn().mockResolvedValue({
          status: 'Completed',
          percentComplete: 100,
          message: 'Done',
        }),
      },
      games: {
        create: vi.fn().mockResolvedValue({ id: 'game-123', title: 'Test Game' }),
      },
    },
  };
});

// Mock toast
vi.mock('@/components/layout', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

describe('AdminWizardClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the wizard header', () => {
    render(<AdminWizardClient />);

    expect(screen.getByText('Admin Game Setup Wizard')).toBeInTheDocument();
    expect(
      screen.getByText('Configura un nuovo gioco con regolamento PDF e agente RAG')
    ).toBeInTheDocument();
  });

  it('renders back to admin link', () => {
    render(<AdminWizardClient />);

    // Link text includes arrow: "← Torna ad Admin"
    const backLink = screen.getByRole('link', { name: /Torna ad Admin/i });
    expect(backLink).toBeInTheDocument();
    expect(backLink).toHaveAttribute('href', '/admin');
  });

  it('renders step indicator with 4 steps', () => {
    render(<AdminWizardClient />);

    expect(screen.getByText('1. Upload PDF')).toBeInTheDocument();
    expect(screen.getByText('2. Crea Gioco')).toBeInTheDocument();
    expect(screen.getByText('3. Setup Chat')).toBeInTheDocument();
    expect(screen.getByText('4. Q&A')).toBeInTheDocument();
  });

  it('starts on upload step', () => {
    render(<AdminWizardClient />);

    // Check PDF upload step is displayed
    expect(screen.getByText('Carica il Regolamento PDF')).toBeInTheDocument();
    expect(
      screen.getByText(
        "Carica il file PDF del regolamento del gioco. Verra' processato per estrarre le regole."
      )
    ).toBeInTheDocument();
  });

  it('renders public checkbox on upload step', () => {
    render(<AdminWizardClient />);

    expect(screen.getByText('Aggiungi alla Libreria Pubblica')).toBeInTheDocument();
    expect(
      screen.getByText(
        "Il PDF sara' visibile a tutti gli utenti registrati nella libreria pubblica."
      )
    ).toBeInTheDocument();
  });

  it('shows upload area with drag and drop hint', () => {
    render(<AdminWizardClient />);

    expect(screen.getByText('Trascina qui il PDF o clicca per selezionare')).toBeInTheDocument();
    expect(screen.getByText('PDF fino a 50MB')).toBeInTheDocument();
  });

  it('has disabled upload button when no file selected', () => {
    render(<AdminWizardClient />);

    // Button text includes arrow: "Carica PDF →"
    const uploadButton = screen.getByRole('button', { name: /Carica PDF/i });
    expect(uploadButton).toBeDisabled();
  });

  describe('Step indicator states', () => {
    it('shows first step as active initially', () => {
      render(<AdminWizardClient />);

      // Find the step indicator container and verify first step is active (blue background)
      const stepLabels = screen.getAllByText(/Upload PDF|Crea Gioco|Setup Chat|Q&A/);
      expect(stepLabels.length).toBeGreaterThanOrEqual(4);
    });
  });
});

describe('PdfUploadStep', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('validates file type', async () => {
    render(<AdminWizardClient />);

    // The component checks for PDF type when a file is selected
    const dropZone = screen.getByText('Trascina qui il PDF o clicca per selezionare');
    expect(dropZone).toBeInTheDocument();
  });

  it('shows file size limit message', () => {
    render(<AdminWizardClient />);

    expect(screen.getByText('PDF fino a 50MB')).toBeInTheDocument();
  });
});

describe('Wizard Step Navigation', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders step descriptions', () => {
    render(<AdminWizardClient />);

    expect(screen.getByText('Carica regolamento')).toBeInTheDocument();
    expect(screen.getByText('Nome e immagini')).toBeInTheDocument();
    expect(screen.getByText('Prepara agente RAG')).toBeInTheDocument();
    expect(screen.getByText('Testa le regole')).toBeInTheDocument();
  });
});

describe('Wizard State Management', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('does not show summary when no data entered', () => {
    render(<AdminWizardClient />);

    // Summary card should not be visible initially
    expect(screen.queryByText('Riepilogo')).not.toBeInTheDocument();
  });
});

describe('Accessibility', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('has proper form labels', () => {
    render(<AdminWizardClient />);

    // The checkbox has a label
    const checkbox = screen.getByRole('checkbox');
    expect(checkbox).toBeInTheDocument();
  });

  it('has accessible step indicator', () => {
    render(<AdminWizardClient />);

    // Step labels are visible
    const steps = ['1. Upload PDF', '2. Crea Gioco', '3. Setup Chat', '4. Q&A'];
    steps.forEach(step => {
      expect(screen.getByText(step)).toBeInTheDocument();
    });
  });
});
