/**
 * ResourceEditor - Issue #2420
 *
 * Editor for managing game resources: tokens, cards, and resources.
 * Features add/remove resources, type selection, and player assignment.
 */

import { fn } from 'storybook/test';

import { ResourceEditor } from './ResourceEditor';

import type { Meta, StoryObj } from '@storybook/react';

const meta = {
  title: 'State/ResourceEditor',
  component: ResourceEditor,
  parameters: {
    layout: 'padded',
    chromatic: {
      viewports: [768, 1024],
      delay: 500,
    },
  },
  tags: ['autodocs'],
  decorators: [
    Story => (
      <div className="max-w-3xl mx-auto p-6">
        <Story />
      </div>
    ),
  ],
  argTypes: {
    resources: { control: 'object' },
    players: { control: 'object' },
    readonly: { control: 'boolean' },
    onChange: { action: 'resources changed' },
    validationErrors: { control: 'object' },
  },
  args: {
    onChange: fn(),
    readonly: false,
    validationErrors: {},
    players: [
      { id: '1', name: 'Alice', score: 0 },
      { id: '2', name: 'Bob', score: 0 },
    ],
  },
} satisfies Meta<typeof ResourceEditor>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * Empty - No resources added yet
 */
export const Empty: Story = {
  args: {
    resources: [],
  },
};

/**
 * Tokens Only - Game with token resources
 */
export const TokensOnly: Story = {
  args: {
    resources: [
      { id: 'r1', type: 'token', name: 'Gold Coin', quantity: 25 },
      { id: 'r2', type: 'token', name: 'Silver Coin', quantity: 15 },
      { id: 'r3', type: 'token', name: 'Bronze Coin', quantity: 50 },
    ],
  },
};

/**
 * Cards Only - Game with card resources
 */
export const CardsOnly: Story = {
  args: {
    resources: [
      { id: 'r1', type: 'card', name: 'Action Card', quantity: 12 },
      { id: 'r2', type: 'card', name: 'Event Card', quantity: 8 },
      { id: 'r3', type: 'card', name: 'Victory Point Card', quantity: 10 },
    ],
  },
};

/**
 * Mixed Types - Game with all resource types
 */
export const MixedTypes: Story = {
  args: {
    resources: [
      { id: 'r1', type: 'token', name: 'Gold Coin', quantity: 25 },
      { id: 'r2', type: 'card', name: 'Action Card', quantity: 12 },
      { id: 'r3', type: 'resource', name: 'Wood', quantity: 30 },
      { id: 'r4', type: 'resource', name: 'Stone', quantity: 20 },
      { id: 'r5', type: 'token', name: 'Energy', quantity: 15 },
    ],
  },
};

/**
 * With Owners - Resources assigned to players
 */
export const WithOwners: Story = {
  args: {
    resources: [
      { id: 'r1', type: 'token', name: 'Gold Coin', quantity: 10, ownerId: '1' },
      { id: 'r2', type: 'card', name: 'Victory Point', quantity: 5, ownerId: '1' },
      { id: 'r3', type: 'resource', name: 'Wood', quantity: 8, ownerId: '2' },
      { id: 'r4', type: 'token', name: 'Silver Coin', quantity: 15, ownerId: '2' },
      { id: 'r5', type: 'resource', name: 'Stone', quantity: 12 }, // Unassigned
    ],
  },
};

/**
 * High Quantities - Resources with large quantities
 */
export const HighQuantities: Story = {
  args: {
    resources: [
      { id: 'r1', type: 'token', name: 'Gold', quantity: 1000 },
      { id: 'r2', type: 'resource', name: 'Wood', quantity: 500 },
      { id: 'r3', type: 'card', name: 'Cards', quantity: 250 },
    ],
  },
};

/**
 * Zero Quantities - Resources depleted to zero
 */
export const ZeroQuantities: Story = {
  args: {
    resources: [
      { id: 'r1', type: 'token', name: 'Gold', quantity: 0 },
      { id: 'r2', type: 'card', name: 'Action', quantity: 0 },
      { id: 'r3', type: 'resource', name: 'Wood', quantity: 0 },
    ],
  },
};

/**
 * Readonly Mode - View-only, no editing allowed
 */
export const ReadonlyMode: Story = {
  args: {
    resources: [
      { id: 'r1', type: 'token', name: 'Gold', quantity: 25, ownerId: '1' },
      { id: 'r2', type: 'card', name: 'Victory Point', quantity: 10, ownerId: '2' },
    ],
    readonly: true,
  },
};

/**
 * With Validation Errors - Shows validation feedback
 */
export const WithValidationErrors: Story = {
  args: {
    resources: [
      { id: 'r1', type: 'token', name: '', quantity: 10 }, // Empty name error
      { id: 'r2', type: 'card', name: 'Victory Point', quantity: -5 }, // Negative quantity error
    ],
    validationErrors: {
      'resources.0.name': 'Nome risorsa obbligatorio',
      'resources.1.quantity': 'Quantità non può essere negativa',
    },
  },
};

/**
 * No Players - Resources without player assignment option
 */
export const NoPlayers: Story = {
  args: {
    resources: [
      { id: 'r1', type: 'token', name: 'Gold', quantity: 25 },
      { id: 'r2', type: 'card', name: 'Action', quantity: 10 },
    ],
    players: [], // No players available
  },
};

/**
 * Many Resources - Stress test with many resources
 */
export const ManyResources: Story = {
  args: {
    resources: Array.from({ length: 12 }, (_, i) => ({
      id: `r${i}`,
      type: (['token', 'card', 'resource'] as const)[i % 3],
      name: `Resource ${i + 1}`,
      quantity: Math.floor(Math.random() * 100),
      ownerId: Math.random() > 0.5 ? ['1', '2'][Math.floor(Math.random() * 2)] : undefined,
    })),
  },
};

/**
 * Long Names - Resources with very long names
 */
export const LongNames: Story = {
  args: {
    resources: [
      {
        id: 'r1',
        type: 'token',
        name: 'Legendary Ancient Golden Treasure Coin of Power',
        quantity: 5,
      },
      {
        id: 'r2',
        type: 'card',
        name: 'Super Ultra Rare Magical Victory Point Card',
        quantity: 2,
      },
    ],
  },
};
