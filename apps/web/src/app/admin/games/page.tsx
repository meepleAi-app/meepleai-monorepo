/**
 * Admin Games Dashboard Page (Issue #2515)
 *
 * Server component for admin games management.
 * Delegates to client component for interactivity.
 */

import { Metadata } from 'next';
import { GamesClient } from './client';

export const metadata: Metadata = {
  title: 'Admin Games Management | MeepleAI',
  description: 'Manage shared games: configure, approve, and publish.',
};

export default function AdminGamesPage() {
  return <GamesClient />;
}
