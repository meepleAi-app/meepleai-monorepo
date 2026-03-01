/**
 * AgentCreationWizard Tests
 * Issue #4915: 4-step wizard to create a user-owned custom agent.
 *
 * Tests:
 * 1. Renders step 1 (Game Collection Picker) on initial load
 * 2. Shows loading state while fetching library
 * 3. Renders game cards from library data
 * 4. Shows empty state when library is empty
 * 5. Advances to step 2 on Avanti after game selection
 * 6. Renders all 3 agent type options in step 2
 * 7. Advances to step 3 after agent type selected + Avanti
 * 8. Shows PDF list in step 3
 * 9. Shows empty state when no PDFs available
 * 10. Back button navigates to previous step
 * 11. Step 4 shows review summary with selected game and type
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

// ─── Mocks ────────────────────────────────────────────────────────────────────

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
  useSearchParams: () => ({ get: () => null }),
}));

vi.mock('@/components/ui/data-display/meeple-card', () => ({
  MeepleCard: ({ title }: { title: string }) => <span>{title}</span>,
}));

const mockGetLibrary = vi.fn();
const mockGetGamePdfs = vi.fn();
const mockCreateUserAgent = vi.fn();
const mockUpdateDocuments = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    library: {
      getLibrary: () => mockGetLibrary(),
      getGamePdfs: (gameId: string) => mockGetGamePdfs(gameId),
    },
    agents: {
      createUserAgent: (req: unknown) => mockCreateUserAgent(req),
      updateDocuments: (id: string, docs: unknown) => mockUpdateDocuments(id, docs),
    },
  },
}));

import { AgentCreationWizard } from '../AgentCreationWizard';
import {
  WIZARD_AGENT_TYPE,
  WIZARD_BTN,
  WIZARD_TESTID,
  WIZARD_TYPE_TO_BACKEND,
} from '../wizard-constants';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import type { GamePdfDto } from '@/lib/api/schemas/pdf.schemas';

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const GAME_TITLE = 'Catan';
const GAME_ID = 'game-uuid-1';

function makeLibraryEntry(overrides: Partial<UserLibraryEntry> = {}): UserLibraryEntry {
  return {
    gameId: GAME_ID,
    gameTitle: GAME_TITLE,
    gameImageUrl: null,
    bggId: null,
    addedAt: '2025-01-01T00:00:00Z',
    ...overrides,
  } as UserLibraryEntry;
}

function makeGamePdf(overrides: Partial<GamePdfDto> = {}): GamePdfDto {
  return {
    id: 'pdf-uuid-1',
    name: 'Catan Rulebook.pdf',
    pageCount: 24,
    fileSizeBytes: 512000,
    uploadedAt: '2025-01-01T00:00:00Z',
    source: 'Custom',
    language: 'it',
    ...overrides,
  };
}

// getLibrary returns PaginatedLibraryResponse: { items, total, page, pageSize }
function makeLibraryResponse(entries: UserLibraryEntry[] = [makeLibraryEntry()]) {
  return { items: entries, total: entries.length, page: 1, pageSize: 100 };
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

async function advanceToStep2() {
  await waitFor(() => screen.getByTestId(WIZARD_TESTID.GameCard(GAME_TITLE)));
  fireEvent.click(screen.getByTestId(WIZARD_TESTID.GameCard(GAME_TITLE)));
  fireEvent.click(screen.getByRole('button', { name: WIZARD_BTN.Next }));
}

async function advanceToStep3() {
  await advanceToStep2();
  fireEvent.click(screen.getByTestId(WIZARD_TESTID.AgentTypeBtn(WIZARD_AGENT_TYPE.Tutor)));
  fireEvent.click(screen.getByRole('button', { name: WIZARD_BTN.Next }));
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('AgentCreationWizard', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetLibrary.mockResolvedValue(makeLibraryResponse());
    mockGetGamePdfs.mockResolvedValue([makeGamePdf()]);
    mockCreateUserAgent.mockResolvedValue({ id: 'agent-new-uuid', name: 'My Catan Tutor' });
    mockUpdateDocuments.mockResolvedValue(undefined);
  });

  // ── Step 1 ────────────────────────────────────────────────────────────────

  it('renders step 1 (game picker) on initial load', async () => {
    render(<AgentCreationWizard />);
    await waitFor(() => expect(mockGetLibrary).toHaveBeenCalledOnce());
    expect(screen.getByText('Gioco')).toBeInTheDocument();
  });

  it('shows loading indicator while fetching library', () => {
    mockGetLibrary.mockReturnValue(new Promise(() => {}));
    render(<AgentCreationWizard />);
    expect(screen.getByTestId(WIZARD_TESTID.LibraryLoading)).toBeInTheDocument();
  });

  it('renders game cards once library data loads', async () => {
    render(<AgentCreationWizard />);
    await waitFor(() =>
      expect(screen.getByTestId(WIZARD_TESTID.GameCard(GAME_TITLE))).toBeInTheDocument()
    );
  });

  it('shows empty state when library is empty', async () => {
    mockGetLibrary.mockResolvedValue(makeLibraryResponse([]));
    render(<AgentCreationWizard />);
    await waitFor(() =>
      expect(screen.getByTestId(WIZARD_TESTID.LibraryEmpty)).toBeInTheDocument()
    );
  });

  // ── Step 2 ────────────────────────────────────────────────────────────────

  it('advances to step 2 when Avanti is clicked after game selection', async () => {
    render(<AgentCreationWizard />);
    await advanceToStep2();
    expect(screen.getByText(WIZARD_AGENT_TYPE.Tutor)).toBeInTheDocument();
    expect(screen.getByText(WIZARD_AGENT_TYPE.Arbitro)).toBeInTheDocument();
    expect(screen.getByText(WIZARD_AGENT_TYPE.Decisore)).toBeInTheDocument();
  });

  it('renders all 3 agent type option buttons in step 2', async () => {
    render(<AgentCreationWizard />);
    await advanceToStep2();
    expect(screen.getByTestId(WIZARD_TESTID.AgentTypeBtn(WIZARD_AGENT_TYPE.Tutor))).toBeInTheDocument();
    expect(screen.getByTestId(WIZARD_TESTID.AgentTypeBtn(WIZARD_AGENT_TYPE.Arbitro))).toBeInTheDocument();
    expect(screen.getByTestId(WIZARD_TESTID.AgentTypeBtn(WIZARD_AGENT_TYPE.Decisore))).toBeInTheDocument();
  });

  // ── Step 3 ────────────────────────────────────────────────────────────────

  it('advances to step 3 (name & KB) when agent type is selected', async () => {
    render(<AgentCreationWizard />);
    await advanceToStep3();
    await waitFor(() =>
      expect(screen.getByTestId(WIZARD_TESTID.NameInput)).toBeInTheDocument()
    );
  });

  it('shows PDF list after loading in step 3', async () => {
    render(<AgentCreationWizard />);
    await advanceToStep3();
    await waitFor(() =>
      expect(screen.getByText('Catan Rulebook.pdf')).toBeInTheDocument()
    );
  });

  it('shows empty state when no PDFs available for the game', async () => {
    mockGetGamePdfs.mockResolvedValue([]);
    render(<AgentCreationWizard />);
    await advanceToStep3();
    await waitFor(() =>
      expect(screen.getByTestId(WIZARD_TESTID.PdfsEmpty)).toBeInTheDocument()
    );
  });

  // ── Navigation ────────────────────────────────────────────────────────────

  it('back button in step 2 returns to step 1', async () => {
    render(<AgentCreationWizard />);
    await advanceToStep2();
    fireEvent.click(screen.getByRole('button', { name: WIZARD_BTN.Back }));
    await waitFor(() => screen.getByTestId(WIZARD_TESTID.GameCard(GAME_TITLE)));
  });

  // ── Step 4 ────────────────────────────────────────────────────────────────

  it('shows review summary in step 4', async () => {
    render(<AgentCreationWizard />);
    await advanceToStep3();
    // Fill in agent name (required for Avanti)
    const nameInput = await waitFor(() => screen.getByTestId(WIZARD_TESTID.NameInput));
    fireEvent.change(nameInput, { target: { value: 'Il mio Tutor di Catan' } });
    fireEvent.click(screen.getByRole('button', { name: WIZARD_BTN.Next }));
    // Step 4: review should show the game title (exact text) and agent type
    await waitFor(() =>
      expect(screen.getByText(GAME_TITLE)).toBeInTheDocument()
    );
    expect(screen.getByText(WIZARD_AGENT_TYPE.Tutor)).toBeInTheDocument();
  });

  // ── Submit (type mapping) ──────────────────────────────────────────────────

  it('maps wizard persona type to backend AgentType on submit', async () => {
    render(<AgentCreationWizard />);
    await advanceToStep3();

    const nameInput = await waitFor(() => screen.getByTestId(WIZARD_TESTID.NameInput));
    fireEvent.change(nameInput, { target: { value: 'Il mio Tutor' } });
    fireEvent.click(screen.getByRole('button', { name: WIZARD_BTN.Next }));

    // Step 4 → Submit
    await waitFor(() => screen.getByRole('button', { name: WIZARD_BTN.Submit }));
    fireEvent.click(screen.getByRole('button', { name: WIZARD_BTN.Submit }));

    await waitFor(() => {
      expect(mockCreateUserAgent).toHaveBeenCalledWith(
        expect.objectContaining({
          gameId: GAME_ID,
          agentType: WIZARD_TYPE_TO_BACKEND[WIZARD_AGENT_TYPE.Tutor], // 'RAG', not 'Tutor'
          name: 'Il mio Tutor',
        })
      );
    });
  });
});
