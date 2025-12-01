import type { Meta, StoryObj } from '@storybook/react';
import { useState } from 'react';
import { SessionSetupModal } from './SessionSetupModal';
import { Button } from '@/components/ui/button';
import type { Game } from '@/lib/api';

const mockGame: Game = {
  id: 'game-1',
  title: 'Catan',
  publisher: 'Catan Studio',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  minPlayTimeMinutes: 60,
  maxPlayTimeMinutes: 120,
  bggId: 13,
  createdAt: new Date().toISOString(),
  imageUrl: null,
  faqCount: null,
  averageRating: null,
};

function InteractiveWrapper({ game }: { game: Game }) {
  const [isOpen, setIsOpen] = useState(false);

  return (
    <>
      <Button onClick={() => setIsOpen(true)}>Start Game Session</Button>
      <SessionSetupModal
        isOpen={isOpen}
        onClose={() => setIsOpen(false)}
        game={game}
        onSessionCreated={session => console.log('Session created:', session)}
      />
    </>
  );
}

const meta = {
  title: 'Modals/SessionSetupModal',
  component: SessionSetupModal,
  parameters: {
    layout: 'centered',
  },
  tags: ['autodocs'],
} satisfies Meta<typeof SessionSetupModal>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  render: () => <InteractiveWrapper game={mockGame} />,
};

export const OpenState: Story = {
  args: {
    isOpen: true,
    onClose: () => {},
    game: mockGame,
  },
};

export const MinPlayers: Story = {
  args: {
    isOpen: true,
    onClose: () => {},
    game: { ...mockGame, minPlayers: 2, maxPlayers: 4 },
  },
};

export const MaxPlayers: Story = {
  args: {
    isOpen: true,
    onClose: () => {},
    game: { ...mockGame, minPlayers: 2, maxPlayers: 8 },
  },
};

export const DarkTheme: Story = {
  args: {
    isOpen: true,
    onClose: () => {},
    game: mockGame,
  },
  decorators: [
    Story => (
      <div className="dark bg-background p-6">
        <Story />
      </div>
    ),
  ],
  parameters: {
    backgrounds: { default: 'dark' },
  },
};
