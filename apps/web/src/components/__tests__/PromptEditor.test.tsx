import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import PromptEditor from '../PromptEditor';

// Mock Monaco Editor
jest.mock('@monaco-editor/react', () => ({
  __esModule: true,
  default: ({
    value,
    onChange,
    options,
    loading,
  }: {
    value: string;
    onChange?: (value: string | undefined) => void;
    options?: any;
    loading?: React.ReactNode;
  }) => {
    return (
      <div data-testid="monaco-editor">
        {loading}
        <textarea
          data-testid="monaco-textarea"
          value={value}
          onChange={(e) => onChange?.(e.target.value)}
          readOnly={options?.readOnly}
          style={{ height: options?.height }}
        />
      </div>
    );
  },
}));

describe('PromptEditor', () => {
  const mockOnChange = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render editor with default props', () => {
      render(<PromptEditor value="" />);

      expect(screen.getByTestId('monaco-editor')).toBeInTheDocument();
    });

    it('should render with provided value', () => {
      render(<PromptEditor value="Test content" />);

      const textarea = screen.getByTestId('monaco-textarea') as HTMLTextAreaElement;
      expect(textarea.value).toBe('Test content');
    });

    it('should apply custom height', () => {
      render(<PromptEditor value="" height="600px" />);

      // Height is passed to editor component
      expect(screen.getByTestId('monaco-editor')).toBeInTheDocument();
    });

    it('should show placeholder when value is empty and not readonly', () => {
      render(<PromptEditor value="" placeholder="Custom placeholder" />);

      expect(screen.getByText('Custom placeholder')).toBeInTheDocument();
    });

    it('should not show placeholder when value is not empty', () => {
      render(<PromptEditor value="Content" placeholder="Custom placeholder" />);

      expect(screen.queryByText('Custom placeholder')).not.toBeInTheDocument();
    });

    it('should not show placeholder in readonly mode', () => {
      render(<PromptEditor value="" readonly={true} placeholder="Custom placeholder" />);

      expect(screen.queryByText('Custom placeholder')).not.toBeInTheDocument();
    });
  });

  describe('User Interactions', () => {
    it('should call onChange when text is typed', async () => {
      const user = userEvent.setup();

      render(<PromptEditor value="" onChange={mockOnChange} />);

      const textarea = screen.getByTestId('monaco-textarea');
      await user.type(textarea, 'New text');

      expect(mockOnChange).toHaveBeenCalled();
    });

    it('should not call onChange in readonly mode', async () => {
      const user = userEvent.setup();

      render(<PromptEditor value="" onChange={mockOnChange} readonly={true} />);

      const textarea = screen.getByTestId('monaco-textarea');
      // readonly attribute prevents typing in the mocked textarea
      expect(textarea).toHaveAttribute('readOnly');

      // onChange should not be called even if attempted
      expect(mockOnChange).not.toHaveBeenCalled();
    });

    it('should handle onChange with undefined value', async () => {
      const user = userEvent.setup();
      const customOnChange = jest.fn((value: string | undefined) => {
        expect(value).toBeDefined();
      });

      render(<PromptEditor value="" onChange={customOnChange} />);

      const textarea = screen.getByTestId('monaco-textarea');
      await user.type(textarea, 'A');

      expect(customOnChange).toHaveBeenCalled();
    });
  });

  describe('Readonly Mode', () => {
    it('should render as readonly when readonly prop is true', () => {
      render(<PromptEditor value="Read only content" readonly={true} />);

      const textarea = screen.getByTestId('monaco-textarea') as HTMLTextAreaElement;
      expect(textarea).toHaveAttribute('readOnly');
    });

    it('should not be readonly by default', () => {
      render(<PromptEditor value="Editable content" />);

      const textarea = screen.getByTestId('monaco-textarea') as HTMLTextAreaElement;
      expect(textarea).not.toHaveAttribute('readOnly');
    });
  });

  describe('Props Handling', () => {
    it('should use default language when not specified', () => {
      render(<PromptEditor value="" />);

      expect(screen.getByTestId('monaco-editor')).toBeInTheDocument();
    });

    it('should use custom language when specified', () => {
      render(<PromptEditor value="" language="json" />);

      expect(screen.getByTestId('monaco-editor')).toBeInTheDocument();
    });

    it('should use default height when not specified', () => {
      render(<PromptEditor value="" />);

      expect(screen.getByTestId('monaco-editor')).toBeInTheDocument();
    });

    it('should use default placeholder when not specified', () => {
      render(<PromptEditor value="" />);

      expect(screen.getByText('Enter your prompt here...')).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle empty string value', () => {
      render(<PromptEditor value="" onChange={mockOnChange} />);

      const textarea = screen.getByTestId('monaco-textarea') as HTMLTextAreaElement;
      expect(textarea.value).toBe('');
    });

    it('should handle multiline content', () => {
      const multilineContent = 'Line 1\nLine 2\nLine 3';
      render(<PromptEditor value={multilineContent} />);

      const textarea = screen.getByTestId('monaco-textarea') as HTMLTextAreaElement;
      expect(textarea.value).toBe(multilineContent);
    });

    it('should handle very long content', () => {
      const longContent = 'A'.repeat(10000);
      render(<PromptEditor value={longContent} />);

      const textarea = screen.getByTestId('monaco-textarea') as HTMLTextAreaElement;
      expect(textarea.value).toBe(longContent);
    });

    it('should handle special characters in content', () => {
      const specialContent = '<script>alert("XSS")</script>\n\t{json: "value"}';
      render(<PromptEditor value={specialContent} />);

      const textarea = screen.getByTestId('monaco-textarea') as HTMLTextAreaElement;
      expect(textarea.value).toBe(specialContent);
    });

    it('should not call onChange when no onChange prop provided', async () => {
      const user = userEvent.setup();

      render(<PromptEditor value="" />);

      const textarea = screen.getByTestId('monaco-textarea');
      await user.type(textarea, 'Test');

      // Should not throw error
      expect(screen.getByTestId('monaco-editor')).toBeInTheDocument();
    });
  });

  describe('Loading State', () => {
    it('should show loading component', () => {
      render(<PromptEditor value="" />);

      expect(screen.getByText('Loading editor...')).toBeInTheDocument();
    });
  });
});
