/**
 * Add New Game Page - New Admin Dashboard
 *
 * Route: /admin/shared-games/new
 * Simple form to create a new shared game entry.
 */

import { type Metadata } from 'next';

import { NewGameClient } from './client';

export const metadata: Metadata = {
  title: 'Add New Game',
  description: 'Add a new game to the shared catalog',
};

export default function NewGamePage() {
  return <NewGameClient />;
}
