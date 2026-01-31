/**
 * Library Export Utilities Tests (Issue #2611)
 *
 * Test Coverage:
 * - CSV format generation
 * - JSON format generation
 * - Value escaping
 * - Minimal vs full scope
 * - Empty data handling
 * - Download trigger
 *
 * Target: ≥90% coverage
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import {
  toCsv,
  toJson,
  generateExport,
  exportLibrary,
  downloadFile,
} from '../libraryExport';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

// ============================================================================
// Test Data
// ============================================================================

const mockEntry: UserLibraryEntry = {
  gameId: 'game-1',
  gameTitle: 'Catan',
  gamePublisher: 'Kosmos',
  gameYearPublished: 1995,
  gameImageUrl: 'https://example.com/catan.jpg',
  addedAt: '2024-06-15T10:30:00Z',
  isFavorite: true,
  notes: 'Great game for beginners',
};

const mockEntryWithSpecialChars: UserLibraryEntry = {
  gameId: 'game-2',
  gameTitle: 'Ticket to Ride: Europe',
  gamePublisher: 'Days of Wonder',
  gameYearPublished: 2005,
  gameImageUrl: null,
  addedAt: '2024-07-20T14:00:00Z',
  isFavorite: false,
  notes: 'Notes with "quotes" and, commas',
};

const mockEntryWithNewlines: UserLibraryEntry = {
  gameId: 'game-3',
  gameTitle: 'Pandemic',
  gamePublisher: 'Z-Man Games',
  gameYearPublished: 2008,
  gameImageUrl: null,
  addedAt: '2024-08-10T09:00:00Z',
  isFavorite: true,
  notes: 'Line 1\nLine 2\nLine 3',
};

const mockEntryNullNotes: UserLibraryEntry = {
  gameId: 'game-4',
  gameTitle: 'Azul',
  gamePublisher: 'Plan B Games',
  gameYearPublished: 2017,
  gameImageUrl: null,
  addedAt: '2024-09-01T12:00:00Z',
  isFavorite: false,
  notes: null,
};

// ============================================================================
// CSV Format Tests
// ============================================================================

describe('toCsv - Minimal Scope', () => {
  it('generates correct headers for minimal scope', () => {
    const csv = toCsv([mockEntry], 'minimal');
    const firstLine = csv.split('\n')[0];

    expect(firstLine).toBe('"Titolo Gioco","Data Aggiunta","Preferito","Note"');
  });

  it('generates correct data row for minimal scope', () => {
    const csv = toCsv([mockEntry], 'minimal');
    const lines = csv.split('\n');

    expect(lines[1]).toContain('Catan');
    expect(lines[1]).toContain('2024-06-15');
    expect(lines[1]).toContain('Sì');
    expect(lines[1]).toContain('Great game for beginners');
  });

  it('excludes publisher and year in minimal scope', () => {
    const csv = toCsv([mockEntry], 'minimal');

    expect(csv).not.toContain('Kosmos');
    expect(csv).not.toContain('1995');
    expect(csv).not.toContain('game-1');
  });

  it('handles empty data with headers only', () => {
    const csv = toCsv([], 'minimal');

    expect(csv).toBe('"Titolo Gioco","Data Aggiunta","Preferito","Note"');
  });
});

describe('toCsv - Full Scope', () => {
  it('generates correct headers for full scope', () => {
    const csv = toCsv([mockEntry], 'full');
    const firstLine = csv.split('\n')[0];

    expect(firstLine).toBe(
      '"Titolo Gioco","Data Aggiunta","Preferito","Note","Editore","Anno Pubblicazione","ID Gioco"'
    );
  });

  it('includes publisher, year and ID in full scope', () => {
    const csv = toCsv([mockEntry], 'full');
    const lines = csv.split('\n');

    expect(lines[1]).toContain('Kosmos');
    expect(lines[1]).toContain('1995');
    expect(lines[1]).toContain('game-1');
  });

  it('handles empty data with full headers', () => {
    const csv = toCsv([], 'full');

    expect(csv).toContain('Editore');
    expect(csv).toContain('Anno Pubblicazione');
    expect(csv).toContain('ID Gioco');
  });
});

describe('toCsv - Value Escaping', () => {
  it('escapes values with quotes', () => {
    const csv = toCsv([mockEntryWithSpecialChars], 'minimal');

    // Quotes should be doubled and value wrapped in quotes
    expect(csv).toContain('""quotes""');
  });

  it('escapes values with commas', () => {
    const csv = toCsv([mockEntryWithSpecialChars], 'minimal');

    // Value with comma should be wrapped in quotes
    expect(csv).toContain('"Notes with ""quotes"" and, commas"');
  });

  it('escapes values with newlines', () => {
    const csv = toCsv([mockEntryWithNewlines], 'minimal');

    // Value with newlines should be wrapped in quotes
    expect(csv).toContain('"Line 1\nLine 2\nLine 3"');
  });

  it('handles null notes', () => {
    const csv = toCsv([mockEntryNullNotes], 'minimal');
    const lines = csv.split('\n');

    // Last field should be empty for null notes
    expect(lines[1].endsWith(',')).toBe(true);
  });

  it('handles null publisher in full scope', () => {
    const entryNullPublisher = { ...mockEntry, gamePublisher: null };
    const csv = toCsv([entryNullPublisher], 'full');

    // Should have empty value for publisher
    expect(csv).toContain(',,');
  });
});

describe('toCsv - Boolean Formatting', () => {
  it('formats true as Sì', () => {
    const csv = toCsv([mockEntry], 'minimal');

    expect(csv).toContain('Sì');
  });

  it('formats false as No', () => {
    const csv = toCsv([mockEntryNullNotes], 'minimal');

    expect(csv).toContain('No');
  });
});

describe('toCsv - Date Formatting', () => {
  it('formats date as YYYY-MM-DD', () => {
    const csv = toCsv([mockEntry], 'minimal');

    expect(csv).toContain('2024-06-15');
  });

  it('handles multiple entries', () => {
    const csv = toCsv([mockEntry, mockEntryWithSpecialChars], 'minimal');
    const lines = csv.split('\n');

    expect(lines).toHaveLength(3); // header + 2 data rows
    expect(lines[1]).toContain('Catan');
    expect(lines[2]).toContain('Ticket to Ride: Europe');
  });
});

// ============================================================================
// JSON Format Tests
// ============================================================================

describe('toJson - Minimal Scope', () => {
  it('includes only minimal fields', () => {
    const json = toJson([mockEntry], 'minimal');
    const parsed = JSON.parse(json);

    expect(parsed[0]).toHaveProperty('gameTitle', 'Catan');
    expect(parsed[0]).toHaveProperty('addedAt', '2024-06-15T10:30:00Z');
    expect(parsed[0]).toHaveProperty('isFavorite', true);
    expect(parsed[0]).toHaveProperty('notes', 'Great game for beginners');
  });

  it('excludes full scope fields', () => {
    const json = toJson([mockEntry], 'minimal');
    const parsed = JSON.parse(json);

    expect(parsed[0]).not.toHaveProperty('gameId');
    expect(parsed[0]).not.toHaveProperty('gamePublisher');
    expect(parsed[0]).not.toHaveProperty('gameYearPublished');
    expect(parsed[0]).not.toHaveProperty('gameImageUrl');
  });

  it('handles null notes', () => {
    const json = toJson([mockEntryNullNotes], 'minimal');
    const parsed = JSON.parse(json);

    expect(parsed[0].notes).toBeNull();
  });
});

describe('toJson - Full Scope', () => {
  it('includes all fields', () => {
    const json = toJson([mockEntry], 'full');
    const parsed = JSON.parse(json);

    expect(parsed[0]).toHaveProperty('gameTitle', 'Catan');
    expect(parsed[0]).toHaveProperty('gameId', 'game-1');
    expect(parsed[0]).toHaveProperty('gamePublisher', 'Kosmos');
    expect(parsed[0]).toHaveProperty('gameYearPublished', 1995);
    expect(parsed[0]).toHaveProperty('gameImageUrl', 'https://example.com/catan.jpg');
  });

  it('handles null optional fields', () => {
    const json = toJson([mockEntryNullNotes], 'full');
    const parsed = JSON.parse(json);

    expect(parsed[0].notes).toBeNull();
    expect(parsed[0].gameImageUrl).toBeNull();
  });
});

describe('toJson - Formatting', () => {
  it('produces valid JSON', () => {
    const json = toJson([mockEntry], 'minimal');

    expect(() => JSON.parse(json)).not.toThrow();
  });

  it('formats JSON with indentation', () => {
    const json = toJson([mockEntry], 'minimal');

    // Pretty-printed JSON has newlines
    expect(json).toContain('\n');
  });

  it('handles empty array', () => {
    const json = toJson([], 'minimal');
    const parsed = JSON.parse(json);

    expect(parsed).toEqual([]);
  });

  it('handles multiple entries', () => {
    const json = toJson([mockEntry, mockEntryWithSpecialChars], 'full');
    const parsed = JSON.parse(json);

    expect(parsed).toHaveLength(2);
  });
});

// ============================================================================
// generateExport Tests
// ============================================================================

describe('generateExport', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2024-10-15T12:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('generates CSV export result', () => {
    const result = generateExport([mockEntry], { format: 'csv', scope: 'minimal' });

    expect(result.mimeType).toBe('text/csv;charset=utf-8;');
    expect(result.filename).toBe('libreria-meepleai-2024-10-15.csv');
    expect(result.content).toContain('Catan');
  });

  it('generates JSON export result', () => {
    const result = generateExport([mockEntry], { format: 'json', scope: 'full' });

    expect(result.mimeType).toBe('application/json;charset=utf-8;');
    expect(result.filename).toBe('libreria-meepleai-2024-10-15.json');
    expect(() => JSON.parse(result.content)).not.toThrow();
  });

  it('uses custom filename when provided', () => {
    const result = generateExport([mockEntry], {
      format: 'csv',
      scope: 'minimal',
      filename: 'my-library.csv',
    });

    expect(result.filename).toBe('my-library.csv');
  });
});

// ============================================================================
// downloadFile Tests
// ============================================================================

describe('downloadFile', () => {
  let mockCreateObjectURL: ReturnType<typeof vi.fn>;
  let mockRevokeObjectURL: ReturnType<typeof vi.fn>;
  let mockAppendChild: ReturnType<typeof vi.fn>;
  let mockRemoveChild: ReturnType<typeof vi.fn>;
  let mockClick: ReturnType<typeof vi.fn>;
  let mockLink: HTMLAnchorElement;

  beforeEach(() => {
    mockCreateObjectURL = vi.fn().mockReturnValue('blob:mock-url');
    mockRevokeObjectURL = vi.fn();
    mockAppendChild = vi.fn();
    mockRemoveChild = vi.fn();
    mockClick = vi.fn();

    mockLink = {
      href: '',
      download: '',
      style: { display: '' },
      click: mockClick,
    } as unknown as HTMLAnchorElement;

    vi.stubGlobal('URL', {
      createObjectURL: mockCreateObjectURL,
      revokeObjectURL: mockRevokeObjectURL,
    });

    vi.spyOn(document, 'createElement').mockReturnValue(mockLink);
    vi.spyOn(document.body, 'appendChild').mockImplementation(mockAppendChild);
    vi.spyOn(document.body, 'removeChild').mockImplementation(mockRemoveChild);
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('creates blob with correct content and mime type', () => {
    downloadFile('test content', 'test.csv', 'text/csv');

    expect(mockCreateObjectURL).toHaveBeenCalledWith(
      expect.objectContaining({
        size: expect.any(Number),
        type: 'text/csv',
      })
    );
  });

  it('sets download filename on link', () => {
    downloadFile('test content', 'my-file.csv', 'text/csv');

    expect(mockLink.download).toBe('my-file.csv');
  });

  it('sets href to blob URL', () => {
    downloadFile('test content', 'test.csv', 'text/csv');

    expect(mockLink.href).toBe('blob:mock-url');
  });

  it('hides link element', () => {
    downloadFile('test content', 'test.csv', 'text/csv');

    expect(mockLink.style.display).toBe('none');
  });

  it('appends link to body', () => {
    downloadFile('test content', 'test.csv', 'text/csv');

    expect(mockAppendChild).toHaveBeenCalledWith(mockLink);
  });

  it('triggers click on link', () => {
    downloadFile('test content', 'test.csv', 'text/csv');

    expect(mockClick).toHaveBeenCalled();
  });

  it('removes link from body after click', () => {
    downloadFile('test content', 'test.csv', 'text/csv');

    expect(mockRemoveChild).toHaveBeenCalledWith(mockLink);
  });

  it('revokes blob URL after download', () => {
    downloadFile('test content', 'test.csv', 'text/csv');

    expect(mockRevokeObjectURL).toHaveBeenCalledWith('blob:mock-url');
  });
});

// ============================================================================
// exportLibrary Integration Tests
// ============================================================================

describe('exportLibrary', () => {
  let mockCreateObjectURL: ReturnType<typeof vi.fn>;
  let mockRevokeObjectURL: ReturnType<typeof vi.fn>;
  let mockClick: ReturnType<typeof vi.fn>;
  let mockLink: HTMLAnchorElement;

  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2024-10-15T12:00:00Z'));

    mockCreateObjectURL = vi.fn().mockReturnValue('blob:mock-url');
    mockRevokeObjectURL = vi.fn();
    mockClick = vi.fn();

    mockLink = {
      href: '',
      download: '',
      style: { display: '' },
      click: mockClick,
    } as unknown as HTMLAnchorElement;

    vi.stubGlobal('URL', {
      createObjectURL: mockCreateObjectURL,
      revokeObjectURL: mockRevokeObjectURL,
    });

    vi.spyOn(document, 'createElement').mockReturnValue(mockLink);
    vi.spyOn(document.body, 'appendChild').mockImplementation(() => mockLink);
    vi.spyOn(document.body, 'removeChild').mockImplementation(() => mockLink);
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
  });

  it('exports CSV with minimal scope', () => {
    exportLibrary([mockEntry], { format: 'csv', scope: 'minimal' });

    expect(mockLink.download).toBe('libreria-meepleai-2024-10-15.csv');
    expect(mockClick).toHaveBeenCalled();
  });

  it('exports JSON with full scope', () => {
    exportLibrary([mockEntry], { format: 'json', scope: 'full' });

    expect(mockLink.download).toBe('libreria-meepleai-2024-10-15.json');
    expect(mockClick).toHaveBeenCalled();
  });

  it('uses custom filename', () => {
    exportLibrary([mockEntry], {
      format: 'csv',
      scope: 'minimal',
      filename: 'custom-export.csv',
    });

    expect(mockLink.download).toBe('custom-export.csv');
  });
});
