import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { WizardSteps } from '@/components/wizard/WizardSteps';

describe('WizardSteps', () => {
  const mockSteps = [
    { id: 'upload', label: 'Upload', description: 'Select PDF file' },
    { id: 'parse', label: 'Parse', description: 'Extract rules' },
    { id: 'review', label: 'Review', description: 'Edit rules' },
    { id: 'publish', label: 'Publish', description: 'Finalize' }
  ];

  describe('Rendering', () => {
    it('renders all steps with labels', () => {
      render(<WizardSteps steps={mockSteps} currentStep="upload" />);

      expect(screen.getByText('Upload')).toBeInTheDocument();
      expect(screen.getByText('Parse')).toBeInTheDocument();
      expect(screen.getByText('Review')).toBeInTheDocument();
      expect(screen.getByText('Publish')).toBeInTheDocument();
    });

    it('renders step descriptions when provided', () => {
      render(<WizardSteps steps={mockSteps} currentStep="upload" />);

      expect(screen.getByText('Select PDF file')).toBeInTheDocument();
      expect(screen.getByText('Extract rules')).toBeInTheDocument();
      expect(screen.getByText('Edit rules')).toBeInTheDocument();
      expect(screen.getByText('Finalize')).toBeInTheDocument();
    });

    it('renders step numbers for incomplete steps', () => {
      render(<WizardSteps steps={mockSteps} currentStep="upload" />);

      const buttons = screen.getAllByRole('button');
      expect(buttons[0]).toHaveTextContent('1');
      expect(buttons[1]).toHaveTextContent('2');
      expect(buttons[2]).toHaveTextContent('3');
      expect(buttons[3]).toHaveTextContent('4');
    });
  });

  describe('Step States', () => {
    it('highlights the current step', () => {
      render(<WizardSteps steps={mockSteps} currentStep="parse" />);

      const parseButton = screen.getByRole('button', { name: /Step 2: Parse/i });
      expect(parseButton.firstChild).toHaveClass('bg-primary');
      expect(parseButton.firstChild).toHaveClass('ring-2');
    });

    it('shows completed steps with checkmark', () => {
      render(<WizardSteps steps={mockSteps} currentStep="review" />);

      const uploadButton = screen.getByRole('button', { name: /Step 1: Upload/i });
      const parseButton = screen.getByRole('button', { name: /Step 2: Parse/i });

      // Check for checkmark icon in completed steps
      expect(uploadButton.querySelector('svg')).toBeInTheDocument();
      expect(parseButton.querySelector('svg')).toBeInTheDocument();
    });

    it('shows pending steps with muted styling', () => {
      render(<WizardSteps steps={mockSteps} currentStep="upload" />);

      const reviewButton = screen.getByRole('button', { name: /Step 3: Review/i });
      expect(reviewButton.firstChild).toHaveClass('bg-muted');
    });

    it('applies aria-current to active step', () => {
      render(<WizardSteps steps={mockSteps} currentStep="parse" />);

      const parseButton = screen.getByRole('button', { name: /Step 2: Parse/i });
      expect(parseButton).toHaveAttribute('aria-current', 'step');
    });
  });

  describe('Accessibility', () => {
    it('renders navigation with aria-label', () => {
      render(<WizardSteps steps={mockSteps} currentStep="upload" />);

      const nav = screen.getByRole('navigation', { name: /Progress/i });
      expect(nav).toBeInTheDocument();
    });

    it('provides aria-labels for each step button', () => {
      render(<WizardSteps steps={mockSteps} currentStep="upload" />);

      expect(screen.getByRole('button', { name: 'Step 1: Upload' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Step 2: Parse' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Step 3: Review' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Step 4: Publish' })).toBeInTheDocument();
    });

    it('disables non-clickable steps by default', () => {
      render(<WizardSteps steps={mockSteps} currentStep="upload" />);

      const buttons = screen.getAllByRole('button');
      buttons.forEach((button) => {
        expect(button).toBeDisabled();
      });
    });
  });

  describe('Interactive Behavior', () => {
    it('calls onStepClick when allowSkip is true and step is clicked', async () => {
      const user = userEvent.setup();
      const onStepClick = jest.fn();

      render(
        <WizardSteps
          steps={mockSteps}
          currentStep="review"
          onStepClick={onStepClick}
          allowSkip={true}
        />
      );

      // Click on completed step (upload)
      const uploadButton = screen.getByRole('button', { name: /Step 1: Upload/i });
      await user.click(uploadButton);

      expect(onStepClick).toHaveBeenCalledWith('upload');
    });

    it('does not call onStepClick for future steps even with allowSkip', async () => {
      const user = userEvent.setup();
      const onStepClick = jest.fn();

      render(
        <WizardSteps
          steps={mockSteps}
          currentStep="upload"
          onStepClick={onStepClick}
          allowSkip={true}
        />
      );

      const publishButton = screen.getByRole('button', { name: /Step 4: Publish/i });
      await user.click(publishButton);

      expect(onStepClick).not.toHaveBeenCalled();
    });

    it('does not call onStepClick when allowSkip is false', async () => {
      const user = userEvent.setup();
      const onStepClick = jest.fn();

      render(
        <WizardSteps
          steps={mockSteps}
          currentStep="review"
          onStepClick={onStepClick}
          allowSkip={false}
        />
      );

      const uploadButton = screen.getByRole('button', { name: /Step 1: Upload/i });
      await user.click(uploadButton);

      expect(onStepClick).not.toHaveBeenCalled();
    });
  });

  describe('Visual States', () => {
    it('applies correct styling for different step states', () => {
      render(<WizardSteps steps={mockSteps} currentStep="parse" />);

      const uploadButton = screen.getByRole('button', { name: /Step 1: Upload/i });
      const parseButton = screen.getByRole('button', { name: /Step 2: Parse/i });
      const reviewButton = screen.getByRole('button', { name: /Step 3: Review/i });

      // Completed step (upload)
      expect(uploadButton.firstChild).toHaveClass('bg-green-600');

      // Active step (parse)
      expect(parseButton.firstChild).toHaveClass('bg-primary');

      // Pending step (review)
      expect(reviewButton.firstChild).toHaveClass('bg-muted');
    });
  });

  describe('Edge Cases', () => {
    it('handles empty steps array', () => {
      render(<WizardSteps steps={[]} currentStep="upload" />);

      const nav = screen.getByRole('navigation');
      expect(nav.querySelector('ol')).toBeEmptyDOMElement();
    });

    it('handles single step', () => {
      const singleStep = [{ id: 'only', label: 'Only Step' }];
      render(<WizardSteps steps={singleStep} currentStep="only" />);

      expect(screen.getByText('Only Step')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Step 1: Only Step/i })).toBeInTheDocument();
    });

    it('handles currentStep not in steps array', () => {
      render(<WizardSteps steps={mockSteps} currentStep="invalid" />);

      // Should not crash, all steps should be pending
      const buttons = screen.getAllByRole('button');
      buttons.forEach((button) => {
        expect(button.firstChild).not.toHaveClass('bg-primary');
      });
    });
  });
});
