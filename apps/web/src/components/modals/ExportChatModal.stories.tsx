import type { Meta, StoryObj } from '@storybook/react';
import { ExportChatModal } from './ExportChatModal';
import { fn } from '@storybook/test';
import React from 'react';

/**
 * ExportChatModal - Modal dialog for exporting chat conversations to various formats.
 * Supports PDF, TXT, and Markdown with optional date range filtering (CHAT-05).
 */
const meta = {
  title: 'Chat/ExportChatModal',
  component: ExportChatModal,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
  argTypes: {
    isOpen: {
      control: 'boolean',
      description: 'Whether the modal is open',
    },
    chatId: {
      control: 'text',
      description: 'ID of the chat to export',
    },
    gameName: {
      control: 'text',
      description: 'Name of the game',
    },
  },
  args: {
    onClose: fn(),
  },
} satisfies Meta<typeof ExportChatModal>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Closed modal (default state)
 */
export const Closed: Story = {
  args: {
    isOpen: false,
    chatId: 'chat-123',
    gameName: 'Scacchi',
  },
};

/**
 * Open modal with default selection (PDF)
 */
export const Open: Story = {
  args: {
    isOpen: true,
    chatId: 'chat-123',
    gameName: 'Scacchi',
  },
};

/**
 * Exporting Chess chat
 */
export const ChessChat: Story = {
  args: {
    isOpen: true,
    chatId: 'chat-chess-456',
    gameName: 'Scacchi',
  },
};

/**
 * Exporting Gloomhaven chat
 */
export const GloomhavenChat: Story = {
  args: {
    isOpen: true,
    chatId: 'chat-gloomhaven-789',
    gameName: 'Gloomhaven',
  },
};

/**
 * Exporting Wingspan chat
 */
export const WingspanChat: Story = {
  args: {
    isOpen: true,
    chatId: 'chat-wingspan-abc',
    gameName: 'Wingspan',
  },
};

/**
 * Interactive modal demo
 */
const InteractiveModalComponent = () => {
  const [isOpen, setIsOpen] = React.useState(false);
  const [exportResult, setExportResult] = React.useState<string | null>(null);

  const handleClose = () => {
    setIsOpen(false);
    // Simulate successful export
    setExportResult('Chat esportato con successo!');
    setTimeout(() => setExportResult(null), 3000);
  };

  return (
    <div className="space-y-4">
      <div className="text-center">
        <button
          onClick={() => setIsOpen(true)}
          className="px-4 py-2 bg-blue-600 text-white rounded-lg hover:bg-blue-700"
        >
          Apri Modal Esportazione
        </button>
      </div>

      {exportResult && (
        <div className="p-3 bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800 rounded-lg text-center">
          <p className="text-sm text-green-800 dark:text-green-200">{exportResult}</p>
        </div>
      )}

      <ExportChatModal
        isOpen={isOpen}
        onClose={handleClose}
        chatId="chat-interactive-123"
        gameName="Terraforming Mars"
      />
    </div>
  );
};

export const Interactive: Story = {
  render: () => <InteractiveModalComponent />,
  parameters: {
    layout: 'padded',
  },
};

/**
 * With date range pre-filled
 */
const WithDateRangeComponent = () => {
  const [isOpen, setIsOpen] = React.useState(true);

  return (
    <div className="space-y-4">
      <ExportChatModal
        isOpen={isOpen}
        onClose={() => setIsOpen(false)}
        chatId="chat-date-range-123"
        gameName="Spirit Island"
      />

      <div className="p-4 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg">
        <p className="text-sm text-blue-800 dark:text-blue-200">
          Nota: Per testare il filtro data, apri il modal e seleziona un intervallo di date.
        </p>
      </div>
    </div>
  );
};

export const WithDateRange: Story = {
  render: () => <WithDateRangeComponent />,
  parameters: {
    layout: 'padded',
  },
};

/**
 * Format selection demo
 */
const FormatSelectionComponent = () => {
  const [isOpen, setIsOpen] = React.useState(true);
  const [selectedFormat, setSelectedFormat] = React.useState<string | null>(null);

  const handleClose = () => {
    setIsOpen(false);
    setTimeout(() => {
      setSelectedFormat('PDF');
      setIsOpen(true);
    }, 1000);
  };

  return (
    <div className="space-y-4">
      {selectedFormat && (
        <div className="p-3 bg-blue-50 dark:bg-blue-900/20 border border-blue-200 dark:border-blue-800 rounded-lg text-center">
          <p className="text-sm text-blue-800 dark:text-blue-200">
            Formato selezionato: {selectedFormat}
          </p>
        </div>
      )}

      <ExportChatModal
        isOpen={isOpen}
        onClose={handleClose}
        chatId="chat-format-123"
        gameName="Catan"
      />

      <div className="p-4 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-lg">
        <p className="text-sm text-slate-700 dark:text-slate-300">
          Prova a selezionare diversi formati: PDF, TXT, o Markdown
        </p>
      </div>
    </div>
  );
};

export const FormatSelection: Story = {
  render: () => <FormatSelectionComponent />,
  parameters: {
    layout: 'padded',
  },
};

/**
 * Complete export workflow
 */
const CompleteWorkflowComponent = () => {
  const [isOpen, setIsOpen] = React.useState(false);
  const [chats] = React.useState([
    { id: 'chat-1', game: 'Scacchi', messages: 42 },
    { id: 'chat-2', game: 'Gloomhaven', messages: 128 },
    { id: 'chat-3', game: 'Wingspan', messages: 67 },
  ]);
  const [selectedChat, setSelectedChat] = React.useState<{
    id: string;
    game: string;
  } | null>(null);
  const [exportHistory, setExportHistory] = React.useState<string[]>([]);

  const handleExport = (chatId: string, gameName: string) => {
    setSelectedChat({ id: chatId, game: gameName });
    setIsOpen(true);
  };

  const handleClose = () => {
    setIsOpen(false);
    if (selectedChat) {
      setExportHistory([...exportHistory, selectedChat.game]);
      setSelectedChat(null);
    }
  };

  return (
    <div className="space-y-4 max-w-2xl">
      <div className="p-4 bg-white dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg">
        <h3 className="text-lg font-semibold mb-4">Le tue chat</h3>
        <div className="space-y-2">
          {chats.map((chat) => (
            <div
              key={chat.id}
              className="flex items-center justify-between p-3 bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-lg"
            >
              <div>
                <p className="font-medium">{chat.game}</p>
                <p className="text-sm text-slate-600 dark:text-slate-400">
                  {chat.messages} messaggi
                </p>
              </div>
              <button
                onClick={() => handleExport(chat.id, chat.game)}
                className="px-3 py-1.5 text-sm bg-blue-600 text-white rounded hover:bg-blue-700"
              >
                Esporta
              </button>
            </div>
          ))}
        </div>
      </div>

      {exportHistory.length > 0 && (
        <div className="p-3 bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800 rounded-lg">
          <p className="text-sm font-medium text-green-800 dark:text-green-200 mb-2">
            Chat esportate:
          </p>
          <ul className="text-xs text-green-700 dark:text-green-300 space-y-0.5">
            {exportHistory.map((game, idx) => (
              <li key={idx}>
                {idx + 1}. {game}
              </li>
            ))}
          </ul>
        </div>
      )}

      {selectedChat && (
        <ExportChatModal
          isOpen={isOpen}
          onClose={handleClose}
          chatId={selectedChat.id}
          gameName={selectedChat.game}
        />
      )}
    </div>
  );
};

export const CompleteWorkflow: Story = {
  render: () => <CompleteWorkflowComponent />,
  parameters: {
    layout: 'padded',
  },
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
        <Story />
      </div>
    ),
  ],
  args: {
    isOpen: true,
    chatId: 'chat-dark-123',
    gameName: 'Terraforming Mars',
  },
};
