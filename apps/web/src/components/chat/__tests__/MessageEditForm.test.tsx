import { render, fireEvent, screen } from '@testing-library/react';
import React from 'react';
import { vi } from 'vitest';
import { MessageEditForm } from '../MessageEditForm';

const mockStore = vi.fn();

vi.mock('@/store/chat/store', () => ({
  useChatStore: (selector: (state: ReturnType<typeof mockStore>) => unknown) =>
    selector(mockStore()),
}));

const createState = (overrides: Partial<ReturnType<typeof mockStore>> = {}) => {
  const baseState = {
    editingMessageId: 'msg-1',
    editContent: 'original content',
    setEditContent: vi.fn(),
    saveEdit: vi.fn(),
    editMessage: { id: 'msg-1', body: 'original content' },
    cancelEdit: vi.fn(),
    loading: { updating: false },
  };

  mockStore.mockReturnValue({ ...baseState, ...overrides });
  return baseState;
};

describe('MessageEditForm', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  it('does not render when no message is being edited', () => {
    createState({ editingMessageId: null });

    const { container } = render(<MessageEditForm />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renders textarea, cancel and save buttons when editing', () => {
    const { setEditContent, saveEdit, cancelEdit, editMessage } = createState();

    render(<MessageEditForm />);

    const textarea = screen.getByLabelText('Edit message content') as HTMLTextAreaElement;
    expect(textarea.value).toBe('original content');

    fireEvent.change(textarea, { target: { value: 'new text' } });
    expect(setEditContent).toHaveBeenCalledWith('new text');

    const saveButton = screen.getByRole('button', { name: /salva/i });
    fireEvent.click(saveButton);
    expect(saveEdit).toHaveBeenCalledWith(editMessage);

    const cancelButton = screen.getByRole('button', { name: /annulla/i });
    fireEvent.click(cancelButton);
    expect(cancelEdit).toHaveBeenCalled();
  });

  it('disables save when content is blank or loading', () => {
    createState({
      editContent: '   ',
      loading: { updating: true },
    });

    render(<MessageEditForm />);

    const saveButton = screen.getByRole('button', { name: /salva/i });
    expect(saveButton).toBeDisabled();
  });
});
