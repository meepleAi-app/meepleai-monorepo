/**
 * Storybook stories for PlayerModeHelpModal - Issue #2475
 */

import type { Meta, StoryObj } from '@storybook/react';
import { PlayerModeHelpModal } from './PlayerModeHelpModal';

const meta: Meta<typeof PlayerModeHelpModal> = {
  title: 'Components/PlayerMode/HelpModal',
  component: PlayerModeHelpModal,
  tags: ['autodocs'],
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Comprehensive help dialog for Player Mode AI feature with FAQ, guides, and confidence scoring explanation.',
      },
    },
  },
  argTypes: {
    variant: {
      control: 'select',
      options: ['default', 'outline', 'ghost'],
      description: 'Button variant for the trigger',
    },
    size: {
      control: 'select',
      options: ['default', 'sm', 'lg', 'icon'],
      description: 'Button size for the trigger',
    },
  },
};

export default meta;
type Story = StoryObj<typeof PlayerModeHelpModal>;

// ========== Stories ==========

/**
 * Default help modal with ghost icon button
 */
export const Default: Story = {
  args: {
    variant: 'ghost',
    size: 'icon',
  },
};

/**
 * Help modal with labeled button
 */
export const WithLabel: Story = {
  args: {
    variant: 'outline',
    size: 'default',
  },
};

/**
 * Large primary button variant
 */
export const PrimaryButton: Story = {
  args: {
    variant: 'default',
    size: 'lg',
  },
};

/**
 * Small outline button for compact layouts
 */
export const SmallButton: Story = {
  args: {
    variant: 'outline',
    size: 'sm',
  },
};

/**
 * Help modal with custom trigger element
 */
export const CustomTrigger: Story = {
  render: () => (
    <PlayerModeHelpModal>
      <button className="rounded-md border border-dashed border-muted-foreground p-4 hover:bg-muted">
        Clicca qui per aiuto personalizzato
      </button>
    </PlayerModeHelpModal>
  ),
};
