import type { Meta, StoryObj } from '@storybook/react';
import { useState } from 'react';
import { QuestionInputForm, ResponseMode } from './QuestionInputForm';

/**
 * QuestionInputForm - Standalone question input component for board game queries.
 *
 * ## Design System
 * Uses **Playful Boardroom** design (Opzione A):
 * - Primary: #F97316 (Orange Catan)
 * - Secondary: #16A34A (Green Carcassonne)
 * - Accent: #A855F7 (Purple)
 *
 * ## Features
 * - **Mobile-first**: Touch-friendly targets (44x44px min)
 * - **Attachment support**: Optional paperclip button
 * - **Response modes**: Fast (Veloce) / Complete (Completa) toggle
 * - **Loading states**: Spinner during submission
 * - **Accessibility**: WCAG 2.1 AA, keyboard navigation, ARIA labels
 *
 * ## Visual Testing Coverage
 * - Default state (empty input)
 * - With text input
 * - Loading state
 * - Disabled state
 * - With attachment button
 * - With response mode toggle
 * - Response mode: Complete selected
 * - Dark theme variant
 *
 * @issue BGAI-061
 */

const meta = {
  title: 'Question/QuestionInputForm',
  component: QuestionInputForm,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'A reusable form component for submitting questions about board games. Mobile-first design with optional attachment and response mode features.',
      },
    },
  },
  tags: ['autodocs'],
  argTypes: {
    value: {
      control: 'text',
      description: 'Current input value',
    },
    placeholder: {
      control: 'text',
      description: 'Input placeholder text',
    },
    disabled: {
      control: 'boolean',
      description: 'Disable all interactions',
    },
    isLoading: {
      control: 'boolean',
      description: 'Show loading state on submit button',
    },
    showAttachment: {
      control: 'boolean',
      description: 'Show attachment button',
    },
    showResponseModeToggle: {
      control: 'boolean',
      description: 'Show response mode toggle',
    },
    responseMode: {
      control: 'radio',
      options: ['fast', 'complete'],
      description: 'Current response mode',
    },
    maxLength: {
      control: 'number',
      description: 'Maximum character length',
    },
  },
  decorators: [
    Story => (
      <div className="w-full max-w-2xl min-w-[320px]">
        <Story />
      </div>
    ),
  ],
} satisfies Meta<typeof QuestionInputForm>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Default state with empty input.
 * Send button is disabled when input is empty.
 */
export const Default: Story = {
  args: {
    value: '',
    onChange: () => {},
    onSubmit: () => {},
    placeholder: 'Scrivi domanda...',
  },
};

/**
 * With text input.
 * Shows enabled send button when question is typed.
 */
export const WithInput: Story = {
  args: {
    value: 'Come si piazzano le strade a Catan?',
    onChange: () => {},
    onSubmit: () => {},
  },
};

/**
 * Loading state during question submission.
 * Shows spinner and disabled controls.
 */
export const LoadingState: Story = {
  args: {
    value: 'Quanti punti vittoria servono per vincere?',
    onChange: () => {},
    onSubmit: () => {},
    isLoading: true,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Loading state shows spinner on send button. Input and all controls are disabled during submission.',
      },
    },
  },
};

/**
 * Disabled state.
 * All controls disabled, typically when no game is selected.
 */
export const DisabledState: Story = {
  args: {
    value: '',
    onChange: () => {},
    onSubmit: () => {},
    disabled: true,
    placeholder: 'Seleziona un gioco prima...',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Disabled state when user needs to select a game first. All interactions are blocked.',
      },
    },
  },
};

/**
 * With attachment button visible.
 * Shows paperclip icon for file attachment.
 */
export const WithAttachment: Story = {
  args: {
    value: 'Puoi vedere questa immagine del tabellone?',
    onChange: () => {},
    onSubmit: () => {},
    showAttachment: true,
    onAttach: () => {},
  },
  parameters: {
    docs: {
      description: {
        story: 'Shows attachment button for attaching images or PDFs to questions.',
      },
    },
  },
};

/**
 * With response mode toggle.
 * Fast mode (Veloce) selected by default.
 */
export const WithResponseModeToggle: Story = {
  args: {
    value: '',
    onChange: () => {},
    onSubmit: () => {},
    showResponseModeToggle: true,
    responseMode: 'fast',
    onResponseModeChange: () => {},
  },
  parameters: {
    docs: {
      description: {
        story:
          'Response mode toggle allows users to choose between fast (concise) and complete (detailed) answers.',
      },
    },
  },
};

/**
 * Complete response mode selected.
 * Shows the secondary (green) color for complete mode.
 */
export const ResponseModeComplete: Story = {
  args: {
    value: 'Spiega tutte le regole del commercio',
    onChange: () => {},
    onSubmit: () => {},
    showResponseModeToggle: true,
    responseMode: 'complete',
    onResponseModeChange: () => {},
  },
  parameters: {
    docs: {
      description: {
        story: 'Complete mode (green) selected for detailed, comprehensive answers.',
      },
    },
  },
};

/**
 * Full featured variant.
 * Shows all features: attachment, response mode toggle, and input with value.
 */
export const FullFeatured: Story = {
  args: {
    value: 'Come funziona lo scambio con la banca?',
    onChange: () => {},
    onSubmit: () => {},
    showAttachment: true,
    onAttach: () => {},
    showResponseModeToggle: true,
    responseMode: 'fast',
    onResponseModeChange: () => {},
  },
  parameters: {
    docs: {
      description: {
        story: 'Full featured variant with attachment button and response mode toggle enabled.',
      },
    },
  },
};

/**
 * Interactive example with state management.
 * Demonstrates real usage with controlled state.
 */
export const Interactive: Story = {
  render: function InteractiveStory() {
    const [value, setValue] = useState('');
    const [isLoading, setIsLoading] = useState(false);
    const [responseMode, setResponseMode] = useState<ResponseMode>('fast');

    const handleSubmit = (question: string) => {
      setIsLoading(true);
      // Simulate API call
      setTimeout(() => {
        setIsLoading(false);
        setValue('');
        console.log('Submitted:', question, 'Mode:', responseMode);
      }, 1500);
    };

    return (
      <QuestionInputForm
        value={value}
        onChange={setValue}
        onSubmit={handleSubmit}
        isLoading={isLoading}
        showResponseModeToggle
        responseMode={responseMode}
        onResponseModeChange={setResponseMode}
        showAttachment
        onAttach={() => console.log('Attach clicked')}
        placeholder="Scrivi la tua domanda..."
      />
    );
  },
  parameters: {
    docs: {
      description: {
        story:
          'Interactive example with state management. Type a question, toggle response mode, and click submit to see loading state.',
      },
    },
  },
};

/**
 * Long text input.
 * Shows behavior with long question text.
 */
export const LongText: Story = {
  args: {
    value:
      'Vorrei capire meglio come funziona il meccanismo di scambio delle risorse quando si hanno piu di sette carte in mano e il ladrone viene mosso su un territorio occupato da piu giocatori contemporaneamente',
    onChange: () => {},
    onSubmit: () => {},
  },
  parameters: {
    docs: {
      description: {
        story: 'Shows input behavior with long question text.',
      },
    },
  },
};

/**
 * Dark theme variant.
 * Shows component with dark theme applied.
 */
export const DarkTheme: Story = {
  args: {
    value: 'Come si costruisce un insediamento?',
    onChange: () => {},
    onSubmit: () => {},
    showResponseModeToggle: true,
    responseMode: 'fast',
    onResponseModeChange: () => {},
  },
  decorators: [
    Story => (
      <div className="dark">
        <div className="w-full max-w-2xl min-w-[320px] bg-background rounded-lg">
          <Story />
        </div>
      </div>
    ),
  ],
  parameters: {
    backgrounds: { default: 'dark' },
    docs: {
      description: {
        story: 'Dark theme variant showing component on dark background.',
      },
    },
  },
};

/**
 * Mobile width simulation.
 * Shows component at mobile viewport width (375px).
 */
export const MobileWidth: Story = {
  args: {
    value: '',
    onChange: () => {},
    onSubmit: () => {},
    showResponseModeToggle: true,
    responseMode: 'fast',
    onResponseModeChange: () => {},
  },
  decorators: [
    Story => (
      <div className="w-[375px]">
        <Story />
      </div>
    ),
  ],
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
    docs: {
      description: {
        story: 'Mobile width (375px) showing responsive behavior.',
      },
    },
  },
};
