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

      // Clear previous calls
      (api.get as jest.Mock).mockClear();

      const categoryHeader = screen.getByText(/Category/);

      // First click - should sort ascending
      fireEvent.click(categoryHeader);

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          expect.stringContaining('sortBy=category&sortOrder=asc')
        );
      });

      // Clear calls again
      (api.get as jest.Mock).mockClear();

      // Second click - should toggle to descending
      fireEvent.click(categoryHeader);

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          expect.stringContaining('sortBy=category&sortOrder=desc')
        );
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

      // Use getAllByText and click the first one (the button, not the modal title)
      const createButtons = screen.getAllByText('Create Template');
      fireEvent.click(createButtons[0]);

      // Modal should open with form fields
      await waitFor(() => {
        const modalTitles = screen.getAllByText('Create Template');
        expect(modalTitles.length).toBeGreaterThan(1); // Button + Modal title
      });

      // Check for Name input (textbox) and Description/InitialContent (textarea)
      expect(screen.getAllByRole('textbox').length).toBeGreaterThan(0);
    });

    it('should close modal when Cancel button clicked', async () => {
      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const createButtons = screen.getAllByText('Create Template');
      fireEvent.click(createButtons[0]);

      // Wait for modal to open
      await waitFor(() => {
        const modalTitles = screen.getAllByText('Create Template');
        expect(modalTitles.length).toBeGreaterThan(1);
      });

      const cancelButton = screen.getByText('Cancel');
      fireEvent.click(cancelButton);

      await waitFor(() => {
        // Modal should close - only the button "Create Template" should remain
        const createTemplateElements = screen.queryAllByText('Create Template');
        expect(createTemplateElements.length).toBe(1); // Only the button
      });
    });

    it('should create template successfully', async () => {
      (api.post as jest.Mock).mockResolvedValue({ id: 'new-template' });

      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const createButtons = screen.getAllByText('Create Template');
      fireEvent.click(createButtons[0]);

      await waitFor(() => {
        const modalTitles = screen.getAllByText('Create Template');
        expect(modalTitles.length).toBeGreaterThan(1);
      });

      // Get all textboxes (Name input + Description textarea + InitialContent textarea)
      const textboxes = screen.getAllByRole('textbox');
      const nameInput = textboxes[0];
      const descriptionInput = textboxes[1];
      const initialContentInput = textboxes[2];

      fireEvent.change(nameInput, { target: { value: 'New Template' } });
      fireEvent.change(descriptionInput, { target: { value: 'New description' } });

      // Category is a select (combobox) - there are 2: filter dropdown and modal select
      // The second one is in the modal
      const comboboxes = screen.getAllByRole('combobox');
      const categorySelect = comboboxes[1];
      fireEvent.change(categorySelect, { target: { value: 'qa-system-prompt' } });

      fireEvent.change(initialContentInput, { target: { value: 'Prompt content' } });

      // Find and submit the form
      const submitButton = screen.getByText('Create');
      fireEvent.click(submitButton);

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

      const createButtons = screen.getAllByText('Create Template');
      fireEvent.click(createButtons[0]);

      await waitFor(() => {
        const modalTitles = screen.getAllByText('Create Template');
        expect(modalTitles.length).toBeGreaterThan(1);
      });

      // Get form elements
      const textboxes = screen.getAllByRole('textbox');
      const nameInput = textboxes[0];
      const descriptionInput = textboxes[1];
      const initialContentInput = textboxes[2];

      fireEvent.change(nameInput, { target: { value: 'New Template' } });
      fireEvent.change(descriptionInput, { target: { value: 'New description' } });

      // Category is a select (combobox) - there are 2: filter dropdown and modal select
      // The second one is in the modal
      const comboboxes = screen.getAllByRole('combobox');
      const categorySelect = comboboxes[1];
      fireEvent.change(categorySelect, { target: { value: 'qa-system-prompt' } });

      fireEvent.change(initialContentInput, { target: { value: 'Prompt content' } });

      // Submit by clicking the Create button
      const submitButton = screen.getByText('Create');
      fireEvent.click(submitButton);

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

      const editButtons = screen.getAllByText('Edit');
      fireEvent.click(editButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Edit Template')).toBeInTheDocument();
      });

      // Wait for form to be populated with values
      await waitFor(() => {
        const textboxes = screen.getAllByRole('textbox');
        expect(textboxes.length).toBe(2); // Name + Description
        expect(textboxes[0]).toHaveValue('QA System Prompt');
        expect(textboxes[1]).toHaveValue('System prompt for QA service');
      });
    });

    it('should update template successfully', async () => {
      (api.put as jest.Mock).mockResolvedValue({});

      render(<AdminPrompts />);

      await waitFor(() => {
        expect(screen.getByText('QA System Prompt')).toBeInTheDocument();
      });

      const editButtons = screen.getAllByText('Edit');
      fireEvent.click(editButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Edit Template')).toBeInTheDocument();
      });

      // Get textboxes: [Name, Description]
      const textboxes = screen.getAllByRole('textbox');
      const nameInput = textboxes[0];

      fireEvent.change(nameInput, { target: { value: 'Updated Template' } });

      // Submit by clicking the Update button
      const submitButton = screen.getByText('Update');
      fireEvent.click(submitButton);

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
