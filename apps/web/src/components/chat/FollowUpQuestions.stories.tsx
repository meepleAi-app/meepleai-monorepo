import type { Meta, StoryObj } from '@storybook/react';
import { FollowUpQuestions } from './FollowUpQuestions';
import { fn } from '@storybook/test';
import React from 'react';

/**
 * FollowUpQuestions - Displays AI-generated follow-up questions as clickable pill-style buttons.
 * Clicking a question populates the chat input with the question text (CHAT-02).
 */
const meta = {
  title: 'Chat/FollowUpQuestions',
  component: FollowUpQuestions,
  parameters: {
    layout: 'padded',
  },
  tags: ['autodocs'],
  argTypes: {
    disabled: {
      control: 'boolean',
      description: 'Whether the buttons are disabled',
    },
  },
  args: {
    onQuestionClick: fn(),
  },
} satisfies Meta<typeof FollowUpQuestions>;

export default meta;
type Story = StoryObj<typeof meta>;

// Mock questions in Italian
const sampleQuestions = [
  'Come si muove il cavaliere?',
  'Posso fare l\'arrocco dopo aver mosso il re?',
  'Quali sono le regole della presa en passant?',
];

const longQuestions = [
  'Quali sono tutte le condizioni necessarie per effettuare l\'arrocco corto in una partita di scacchi standard?',
  'Come funziona la promozione del pedone quando raggiunge l\'ottava traversa?',
  'In che modo il movimento del cavallo differisce da quello degli altri pezzi?',
];

const manyQuestions = [
  'Come si muove il re?',
  'Come si muove la regina?',
  'Come si muove il cavallo?',
  'Come si muove l\'alfiere?',
  'Come si muove la torre?',
  'Come si muove il pedone?',
  'Cos\'è lo scacco matto?',
  'Cos\'è lo stallo?',
];

/**
 * Default follow-up questions (3 questions)
 */
export const Default: Story = {
  args: {
    questions: sampleQuestions,
  },
};

/**
 * Empty questions array (renders nothing)
 */
export const Empty: Story = {
  args: {
    questions: [],
  },
};

/**
 * Null questions (renders nothing)
 */
export const NullQuestions: Story = {
  args: {
    questions: null as unknown as string[],
  },
};

/**
 * Single question
 */
export const SingleQuestion: Story = {
  args: {
    questions: ['Come si muove la regina?'],
  },
};

/**
 * Long questions (text wrapping)
 */
export const LongQuestions: Story = {
  args: {
    questions: longQuestions,
  },
};

/**
 * Many questions (wrapping layout)
 */
export const ManyQuestions: Story = {
  args: {
    questions: manyQuestions,
  },
};

/**
 * Disabled state
 */
export const Disabled: Story = {
  args: {
    questions: sampleQuestions,
    disabled: true,
  },
};

/**
 * Interactive demo with click tracking
 */
const InteractiveFollowUpComponent = () => {
  const [selectedQuestions, setSelectedQuestions] = React.useState<string[]>([]);
  const [currentInput, setCurrentInput] = React.useState('');

  const questions = [
    'Come si muove il cavaliere?',
    'Posso fare l\'arrocco?',
    'Cos\'è la presa en passant?',
    'Quali sono le regole del pedone?',
  ];

  const handleQuestionClick = (question: string) => {
    setSelectedQuestions([...selectedQuestions, question]);
    setCurrentInput(question);
  };

  return (
    <div className="space-y-4">
      <div className="p-4 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-lg">
        <p className="text-sm font-medium mb-2">Chat input:</p>
        <input
          type="text"
          value={currentInput}
          onChange={(e) => setCurrentInput(e.target.value)}
          className="w-full px-3 py-2 border border-slate-300 dark:border-slate-600 rounded-lg bg-white dark:bg-slate-800"
          placeholder="Click a follow-up question..."
        />
      </div>

      <FollowUpQuestions questions={questions} onQuestionClick={handleQuestionClick} />

      {selectedQuestions.length > 0 && (
        <div className="p-3 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg">
          <p className="text-sm font-medium text-blue-800 dark:text-blue-200 mb-2">
            Clicked questions:
          </p>
          <ul className="text-xs text-blue-700 dark:text-blue-300 space-y-1">
            {selectedQuestions.map((q, idx) => (
              <li key={idx}>
                {idx + 1}. {q}
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
};

export const Interactive: Story = {
  render: () => <InteractiveFollowUpComponent />,
};

/**
 * In chat context
 */
const ChatContextComponent = () => {
  const [messages, setMessages] = React.useState<Array<{ text: string; isUser: boolean }>>([
    {
      text: 'Come si gioca a scacchi?',
      isUser: true,
    },
    {
      text: 'Gli scacchi sono un gioco da tavolo per due giocatori. L\'obiettivo è dare scacco matto al re avversario.',
      isUser: false,
    },
  ]);

  const [currentInput, setCurrentInput] = React.useState('');

  const followUpQuestions = [
    'Come si muove il re?',
    'Come si muove la regina?',
    'Cos\'è lo scacco matto?',
  ];

  const handleQuestionClick = (question: string) => {
    setCurrentInput(question);
  };

  const handleSend = () => {
    if (!currentInput.trim()) return;

    setMessages([...messages, { text: currentInput, isUser: true }]);
    setCurrentInput('');
  };

  return (
    <div className="max-w-2xl mx-auto border border-slate-200 dark:border-slate-700 rounded-lg overflow-hidden">
      {/* Chat messages */}
      <div className="p-4 space-y-3 bg-slate-50 dark:bg-slate-900 min-h-[300px] max-h-[400px] overflow-y-auto">
        {messages.map((msg, idx) => (
          <div
            key={idx}
            className={`flex ${msg.isUser ? 'justify-end' : 'justify-start'}`}
          >
            <div
              className={`max-w-[80%] px-4 py-2 rounded-lg ${
                msg.isUser
                  ? 'bg-blue-600 text-white'
                  : 'bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700'
              }`}
            >
              <p className="text-sm">{msg.text}</p>
            </div>
          </div>
        ))}

        {/* Follow-up questions after last AI message */}
        {!messages[messages.length - 1]?.isUser && (
          <FollowUpQuestions
            questions={followUpQuestions}
            onQuestionClick={handleQuestionClick}
          />
        )}
      </div>

      {/* Input area */}
      <div className="p-4 bg-white dark:bg-slate-800 border-t border-slate-200 dark:border-slate-700">
        <div className="flex gap-2">
          <input
            type="text"
            value={currentInput}
            onChange={(e) => setCurrentInput(e.target.value)}
            onKeyDown={(e) => e.key === 'Enter' && handleSend()}
            className="flex-1 px-3 py-2 border border-slate-300 dark:border-slate-600 rounded-lg bg-white dark:bg-slate-900"
            placeholder="Scrivi un messaggio..."
          />
          <button
            onClick={handleSend}
            className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700"
          >
            Invia
          </button>
        </div>
      </div>
    </div>
  );
};

export const InChatContext: Story = {
  render: () => <ChatContextComponent />,
};

/**
 * Dark mode
 */
export const DarkMode: Story = {
  parameters: {
    backgrounds: { default: 'dark' },
  },
  decorators: [
    (Story) => (
      <div className="dark">
        <div className="bg-slate-800 p-6 rounded-lg">
          <Story />
        </div>
      </div>
    ),
  ],
  args: {
    questions: sampleQuestions,
  },
};
