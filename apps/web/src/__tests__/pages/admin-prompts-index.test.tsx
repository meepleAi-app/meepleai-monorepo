import React from 'react';
import { render, screen, fireEvent, waitFor, act } from '@testing-library/react';
import { useRouter } from 'next/router';
import AdminPrompts from '../../pages/admin/prompts/index';
import { api } from '../../lib/api';

// Mock dependencies
jest.mock('next/router', () => ({
  useRouter: jest.fn(),
}));

jest.mock('../../lib/api', () => ({
  api: {
    get: jest.fn(),
    post: jest.fn(),
    put: jest.fn(),
    delete: jest.fn(),
  },
}));

const mockPush = jest.fn();
const mockRouter = {
  push: mockPush,
  query: {},
};

const mockTemplates = [
  {
    id: 'template-1',
    name: 'QA System Prompt',
    description: 'System prompt for QA service',
    category: 'qa-system-prompt',
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-02T00:00:00Z',
    activeVersionId: 'version-1',
  },
  {
    id: 'template-2',
    name: 'Chess System Prompt',
    description: 'System prompt for chess agent',
    category: 'chess-system-prompt',
    createdAt: '2024-01-03T00:00:00Z',
    updatedAt: '2024-01-04T00:00:00Z',
    activeVersionId: null,
  },
];

describe('AdminPrompts Index Page', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
    (useRouter as jest.Mock).mockReturnValue(mockRouter);
  });

  afterEach(async () => {
    await act(async () => {
      jest.runOnlyPendingTimers();
    });
    jest.useRealTimers();
  });

  describe('Initial Load', () => {
    it('should render loading state initially', () => {
      (api.get as jest.Mock).mockImplementation(() => new Promise(() => {}));

      render(<AdminPrompts />);

      expect(screen.getByText('Loading...')).toBeInTheDocument();
    });

    it('should fetch and display templates on mount', async () => {
      (api.get as jest.Mock).mockResolvedValue({
        templates: mockTemplates,
        totalPages: 1,
        page: 1,
        total: 2,
      });

      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
        expect(screen.getByText('Chess System Prompt')).toBeInTheDocument();
      });

      expect(api.get).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/admin/prompts')
      );
    });

    it('should display error message when fetch fails', async () => {
      (api.get as jest.Mock).mockRejectedValue(new Error('Network error'));

      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('Network error')).toBeInTheDocument();
      });

      // Toast should also appear
      await waitFor(() => {
        expect(screen.getByText('Failed to fetch templates')).toBeInTheDocument();
      });
    });

    it('should display unauthorized error when API returns null', async () => {
      (api.get as jest.Mock).mockResolvedValue(null);

      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('Unauthorized')).toBeInTheDocument();
      });
    });
  });

  describe('Empty State', () => {
    it('should display empty state when no templates exist', async () => {
      (api.get as jest.Mock).mockResolvedValue({
        templates: [],
        totalPages: 1,
        page: 1,
        total: 0,
      });

      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('No templates found')).toBeInTheDocument();
        expect(
          screen.getByText('Create your first prompt template to get started')
        ).toBeInTheDocument();
      });
    });
  });

  describe('Search and Filters', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue({
        templates: mockTemplates,
        totalPages: 1,
        page: 1,
        total: 2,
      });
    });

    it('should update search input and trigger fetch', async () => {
      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const searchInput = screen.getByPlaceholderText('Search by name...');
      fireEvent.change(searchInput, { target: { value: 'QA' } });

      expect(searchInput).toHaveValue('QA');

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          expect.stringContaining('search=QA')
        );
      });
    });

    it('should filter by category', async () => {
      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const categorySelect = screen.getByDisplayValue('All Categories');
      fireEvent.change(categorySelect, { target: { value: 'qa-system-prompt' } });

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          expect.stringContaining('category=qa-system-prompt')
        );
      });
    });

    it('should reset page to 1 when search changes', async () => {
      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const searchInput = screen.getByPlaceholderText('Search by name...');
      fireEvent.change(searchInput, { target: { value: 'test' } });

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          expect.stringContaining('page=1')
        );
      });
    });
  });

  describe('Sorting', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue({
        templates: mockTemplates,
        totalPages: 1,
        page: 1,
        total: 2,
      });
    });

    it('should sort by name ascending when name header clicked', async () => {
      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const nameHeader = screen.getByText(/Name/);
      fireEvent.click(nameHeader);

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          expect.stringContaining('sortBy=name&sortOrder=asc')
        );
      });
    });

    it('should toggle sort order when same header clicked twice', async () => {
      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      // First click - should sort ascending and show ↑
      const categoryHeader = screen.getByText(/Category/);

      await act(async () => {
        fireEvent.click(categoryHeader);
      });

      await waitFor(() => {
        // Check for ascending arrow in header by finding the updated element
        const updatedHeader = screen.getByText(/Category/);
        expect(updatedHeader.textContent).toContain('↑');
      });

      // Verify API was called with ascending sort
      await waitFor(() => {
        const calls = (api.get as jest.Mock).mock.calls;
        const ascCall = calls.find(call =>
          call[0].includes('sortBy=category') && call[0].includes('sortOrder=asc')
        );
        expect(ascCall).toBeDefined();
      });

      // Second click - should toggle to descending and show ↓
      const updatedCategoryHeader = screen.getByText(/Category/);

      await act(async () => {
        fireEvent.click(updatedCategoryHeader);
      });

      await waitFor(() => {
        // Check for descending arrow in header by finding the updated element
        const finalHeader = screen.getByText(/Category/);
        expect(finalHeader.textContent).toContain('↓');
      });

      // Verify API was called with descending sort
      await waitFor(() => {
        const calls = (api.get as jest.Mock).mock.calls;
        const descCall = calls.find(call =>
          call[0].includes('sortBy=category') && call[0].includes('sortOrder=desc')
        );
        expect(descCall).toBeDefined();
      });
    });
  });

  describe('Pagination', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue({
        templates: mockTemplates,
        totalPages: 3,
        page: 1,
        total: 60,
      });
    });

    it('should navigate to next page', async () => {
      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const nextButton = screen.getByText('Next');
      fireEvent.click(nextButton);

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          expect.stringContaining('page=2')
        );
      });
    });

    it('should navigate to previous page', async () => {
      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      // Navigate to page 2 first
      const nextButton = screen.getByText('Next');
      fireEvent.click(nextButton);

      await waitFor(() => {
        expect(screen.getByText('Page 2 of 3')).toBeInTheDocument();
      });

      // Now navigate back to page 1
      const prevButton = screen.getByText('Previous');
      fireEvent.click(prevButton);

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          expect.stringContaining('page=1')
        );
      });
    });

    it('should disable previous button on first page', async () => {
      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const prevButton = screen.getByText('Previous');
      expect(prevButton).toBeDisabled();
    });

    it('should disable next button on last page', async () => {
      (api.get as jest.Mock).mockResolvedValue({
        templates: mockTemplates,
        totalPages: 1,
        page: 1,
        total: 2,
      });

      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const nextButton = screen.getByText('Next');
      expect(nextButton).toBeDisabled();
    });
  });

  describe('Create Template Modal', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue({
        templates: mockTemplates,
        totalPages: 1,
        page: 1,
        total: 2,
      });
    });

    it('should open create modal when Create Template button clicked', async () => {
      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      // Use getByRole to find the specific Create Template button (not modal title)
      const createButton = screen.getByRole('button', { name: /create template/i });

      await act(async () => {
        fireEvent.click(createButton);
      });

      // Modal should open with form fields
      await waitFor(() => {
        // Verify form fields are present (modal is open)
        const textboxes = screen.getAllByRole('textbox');
        expect(textboxes.length).toBeGreaterThan(0);
      });
    });

    it('should close modal when Cancel button clicked', async () => {
      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const createButton = screen.getByRole('button', { name: /create template/i });

      await act(async () => {
        fireEvent.click(createButton);
      });

      // Wait for modal to open - check for modal title (heading)
      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /create template/i })).toBeInTheDocument();
      });

      const cancelButton = screen.getByRole('button', { name: /cancel/i });

      await act(async () => {
        fireEvent.click(cancelButton);
      });

      await waitFor(() => {
        // Modal should close - check that modal heading is gone
        expect(screen.queryByRole('heading', { name: /create template/i })).not.toBeInTheDocument();
      });

      // Additionally verify the Create Template button remains
      expect(screen.getByRole('button', { name: /create template/i })).toBeInTheDocument();
    });

    it('should create template successfully', async () => {
      (api.post as jest.Mock).mockResolvedValue({ id: 'new-template' });

      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const createButton = screen.getByRole('button', { name: /create template/i });

      await act(async () => {
        fireEvent.click(createButton);
      });

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /create template/i })).toBeInTheDocument();
      });

      // Get all textboxes (search input + Name input + Description textarea + InitialContent textarea)
      // The search input is at index 0, modal fields start at index 1
      const textboxes = screen.getAllByRole('textbox');
      const nameInput = textboxes[1]; // First modal textbox
      const descriptionInput = textboxes[2]; // Second modal textbox
      const initialContentInput = textboxes[3]; // Third modal textbox

      await act(async () => {
        fireEvent.change(nameInput, { target: { value: 'New Template' } });
        fireEvent.change(descriptionInput, { target: { value: 'New description' } });
        fireEvent.change(initialContentInput, { target: { value: 'Prompt content' } });
      });

      // Category is a select (combobox) - there are 2: filter dropdown and modal select
      // The second one is in the modal
      const comboboxes = screen.getAllByRole('combobox');
      const categorySelect = comboboxes[1];

      await act(async () => {
        fireEvent.change(categorySelect, { target: { value: 'qa-system-prompt' } });
      });

      // Find the submit button - get all buttons with "create" and find the one that's not "Create Template"
      const allButtons = screen.getAllByRole('button');
      const submitButton = allButtons.find(btn => btn.textContent === 'Create');
      expect(submitButton).toBeDefined();

      await act(async () => {
        fireEvent.click(submitButton!);
      });

      await waitFor(() => {
        expect(api.post).toHaveBeenCalledWith('/api/v1/admin/prompts', {
          name: 'New Template',
          description: 'New description',
          category: 'qa-system-prompt',
          initialContent: 'Prompt content',
        });
      });

      await waitFor(() => {
        expect(screen.getByText('Template created successfully')).toBeInTheDocument();
      });

      act(() => {
        jest.advanceTimersByTime(5000);
      });
      await waitFor(() => {
        expect(screen.queryByText('Template created successfully')).not.toBeInTheDocument();
      });
    });

    it('should handle create error', async () => {
      (api.post as jest.Mock).mockRejectedValue(new Error('Creation failed'));

      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const createButton = screen.getByRole('button', { name: /create template/i });

      await act(async () => {
        fireEvent.click(createButton);
      });

      await waitFor(() => {
        expect(screen.getByRole('heading', { name: /create template/i })).toBeInTheDocument();
      });

      // Get form elements (search input + Name input + Description textarea + InitialContent textarea)
      const textboxes = screen.getAllByRole('textbox');
      const nameInput = textboxes[1]; // First modal textbox
      const descriptionInput = textboxes[2]; // Second modal textbox
      const initialContentInput = textboxes[3]; // Third modal textbox

      await act(async () => {
        fireEvent.change(nameInput, { target: { value: 'New Template' } });
        fireEvent.change(descriptionInput, { target: { value: 'New description' } });
        fireEvent.change(initialContentInput, { target: { value: 'Prompt content' } });
      });

      // Category is a select (combobox) - there are 2: filter dropdown and modal select
      // The second one is in the modal
      const comboboxes = screen.getAllByRole('combobox');
      const categorySelect = comboboxes[1];

      await act(async () => {
        fireEvent.change(categorySelect, { target: { value: 'qa-system-prompt' } });
      });

      // Find the submit button
      const allButtons = screen.getAllByRole('button');
      const submitButton = allButtons.find(btn => btn.textContent === 'Create');
      expect(submitButton).toBeDefined();

      await act(async () => {
        fireEvent.click(submitButton!);
      });

      await waitFor(() => {
        expect(screen.getByText('Creation failed')).toBeInTheDocument();
      });
    });
  });

  describe('Edit Template Modal', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue({
        templates: mockTemplates,
        totalPages: 1,
        page: 1,
        total: 2,
      });
    });

    it('should open edit modal with pre-filled data', async () => {
      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const editButtons = screen.getAllByRole('button', { name: /edit/i });

      await act(async () => {
        fireEvent.click(editButtons[0]);
      });

      await waitFor(() => {
        expect(screen.getByText('Edit Template')).toBeInTheDocument();
      });

      // Wait for form to be populated with values
      // Textboxes: search input (index 0) + Name (index 1) + Description (index 2)
      // Edit mode does NOT have Initial Content field
      await waitFor(() => {
        const textboxes = screen.getAllByRole('textbox');
        expect(textboxes.length).toBe(3); // Search + Name + Description
        expect(textboxes[1]).toHaveValue('QA System Prompt');
        expect(textboxes[2]).toHaveValue('System prompt for QA service');
      });
    });

    it('should update template successfully', async () => {
      (api.put as jest.Mock).mockResolvedValue({});

      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const editButtons = screen.getAllByRole('button', { name: /edit/i });

      await act(async () => {
        fireEvent.click(editButtons[0]);
      });

      await waitFor(() => {
        expect(screen.getByText('Edit Template')).toBeInTheDocument();
      });

      // Get textboxes: search input (index 0) + Name (index 1) + Description (index 2)
      await waitFor(() => {
        const textboxes = screen.getAllByRole('textbox');
        expect(textboxes.length).toBe(3);
        expect(textboxes[1]).toHaveValue('QA System Prompt');
      });

      const textboxes = screen.getAllByRole('textbox');
      const nameInput = textboxes[1]; // Name is the second textbox (after search input)

      await act(async () => {
        fireEvent.change(nameInput, { target: { value: 'Updated Template' } });
      });

      // Submit by clicking the Update button
      const submitButton = screen.getByRole('button', { name: /update/i });

      await act(async () => {
        fireEvent.click(submitButton);
      });

      await waitFor(() => {
        expect(api.put).toHaveBeenCalledWith('/api/v1/admin/prompts/template-1', {
          name: 'Updated Template',
          description: 'System prompt for QA service',
          category: 'qa-system-prompt',
        });
      });

      await waitFor(() => {
        expect(screen.getByText('Template updated successfully')).toBeInTheDocument();
      });
    });

    it('should not show Initial Content field in edit mode', async () => {
      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const editButtons = screen.getAllByText('Edit');
      fireEvent.click(editButtons[0]);

      expect(screen.queryByLabelText('Initial Content')).not.toBeInTheDocument();
    });
  });

  describe('Delete Template', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue({
        templates: mockTemplates,
        totalPages: 1,
        page: 1,
        total: 2,
      });
    });

    it('should open delete confirmation dialog', async () => {
      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const deleteButtons = screen.getAllByText('Delete');
      fireEvent.click(deleteButtons[0]);

      expect(screen.getByText('Confirm Delete')).toBeInTheDocument();
      expect(
        screen.getByText(/Are you sure you want to delete/)
      ).toBeInTheDocument();
    });

    it('should close delete dialog when Cancel clicked', async () => {
      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const deleteButtons = screen.getAllByText('Delete');
      fireEvent.click(deleteButtons[0]);

      const cancelButton = screen.getAllByText('Cancel')[0];
      fireEvent.click(cancelButton);

      await waitFor(() => {
        expect(screen.queryByText('Confirm Delete')).not.toBeInTheDocument();
      });
    });

    it('should delete template successfully', async () => {
      (api.delete as jest.Mock).mockResolvedValue({});

      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const deleteButtons = screen.getAllByText('Delete');
      fireEvent.click(deleteButtons[0]);

      // Wait for dialog to appear
      await waitFor(() => {
        expect(screen.getByText('Confirm Delete')).toBeInTheDocument();
      });

      // After dialog opens, there will be more Delete buttons (in dialog)
      const allDeleteButtons = screen.getAllByText('Delete');
      // The last one is the confirm button in the dialog
      fireEvent.click(allDeleteButtons[allDeleteButtons.length - 1]);

      await waitFor(() => {
        expect(api.delete).toHaveBeenCalledWith('/api/v1/admin/prompts/template-1');
      });

      await waitFor(() => {
        expect(screen.getByText('Template deleted successfully')).toBeInTheDocument();
      });
    });

    it('should handle delete error', async () => {
      (api.delete as jest.Mock).mockRejectedValue(new Error('Delete failed'));

      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const deleteButtons = screen.getAllByText('Delete');
      fireEvent.click(deleteButtons[0]);

      // Wait for dialog to appear
      await waitFor(() => {
        expect(screen.getByText('Confirm Delete')).toBeInTheDocument();
      });

      // After dialog opens, there will be more Delete buttons (in dialog)
      const allDeleteButtons = screen.getAllByText('Delete');
      // The last one is the confirm button in the dialog
      fireEvent.click(allDeleteButtons[allDeleteButtons.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('Delete failed')).toBeInTheDocument();
      });
    });
  });

  describe('Navigation', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue({
        templates: mockTemplates,
        totalPages: 1,
        page: 1,
        total: 2,
      });
    });

    it('should navigate to template details when View button clicked', async () => {
      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const viewButtons = screen.getAllByText('View');
      fireEvent.click(viewButtons[0]);

      expect(mockPush).toHaveBeenCalledWith('/admin/prompts/template-1');
    });
  });

  describe('Active Version Display', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue({
        templates: mockTemplates,
        totalPages: 1,
        page: 1,
        total: 2,
      });
    });

    it('should display active version badge', async () => {
      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('✓ Active')).toBeInTheDocument();
      });
    });

    it('should display no active version text', async () => {
      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('No active version')).toBeInTheDocument();
      });
    });
  });

  describe('Toast Notifications', () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValue({
        templates: mockTemplates,
        totalPages: 1,
        page: 1,
        total: 2,
      });
    });

    it('should auto-hide toast after 5 seconds', async () => {
      (api.delete as jest.Mock).mockResolvedValue({});

      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const deleteButtons = screen.getAllByText('Delete');
      fireEvent.click(deleteButtons[0]);

      // Wait for dialog to appear
      await waitFor(() => {
        expect(screen.getByText('Confirm Delete')).toBeInTheDocument();
      });

      // After dialog opens, there will be more Delete buttons (in dialog)
      const allDeleteButtons = screen.getAllByText('Delete');
      // The last one is the confirm button in the dialog
      fireEvent.click(allDeleteButtons[allDeleteButtons.length - 1]);

      await waitFor(() => {
        expect(screen.getByText('Template deleted successfully')).toBeInTheDocument();
      });

      act(() => {
        jest.advanceTimersByTime(5000);
      });

      await waitFor(() => {
        expect(screen.queryByText('Template deleted successfully')).not.toBeInTheDocument();
      });
    });
  });
});
