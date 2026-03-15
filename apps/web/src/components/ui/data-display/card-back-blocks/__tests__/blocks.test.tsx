/**
 * Card Back Blocks Test Suite
 * Tests for all 9 block components in card-back-blocks/blocks/
 */

import React from 'react';

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ContentsBlock } from '../blocks/ContentsBlock';
import { DetailLinkBlock } from '../blocks/DetailLinkBlock';
import { HistoryBlock } from '../blocks/HistoryBlock';
import { KBPreviewBlock } from '../blocks/KBPreviewBlock';
import { MembersBlock } from '../blocks/MembersBlock';
import { NotesBlock } from '../blocks/NotesBlock';
import { ProgressBlock } from '../blocks/ProgressBlock';
import { RankingBlock } from '../blocks/RankingBlock';
import { TimelineBlock } from '../blocks/TimelineBlock';

// Mock next/link for test environment
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: React.PropsWithChildren<{ href: string }>) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

const COLOR = '25 95% 45%';

describe('Card Back Blocks', () => {
  it('TimelineBlock renders events', () => {
    render(
      <TimelineBlock
        title="Timeline"
        entityColor={COLOR}
        data={{ type: 'timeline', events: [{ time: '20:30', label: 'Start', icon: '▶' }] }}
      />
    );
    expect(screen.getByText('20:30')).toBeInTheDocument();
    expect(screen.getByText('Start')).toBeInTheDocument();
  });

  it('TimelineBlock renders title', () => {
    render(
      <TimelineBlock
        title="Game Events"
        entityColor={COLOR}
        data={{ type: 'timeline', events: [] }}
      />
    );
    expect(screen.getByText('Game Events')).toBeInTheDocument();
    expect(screen.getByText('No data yet')).toBeInTheDocument();
  });

  it('RankingBlock renders players with positions', () => {
    render(
      <RankingBlock
        title="Classifica"
        entityColor={COLOR}
        data={{
          type: 'ranking',
          players: [
            { name: 'Marco', score: 87, position: 1, isLeader: true },
            { name: 'Sara', score: 72, position: 2 },
          ],
        }}
      />
    );
    expect(screen.getByText('Marco')).toBeInTheDocument();
    expect(screen.getByText('87')).toBeInTheDocument();
    expect(screen.getByText('Sara')).toBeInTheDocument();
    expect(screen.getByText('72')).toBeInTheDocument();
  });

  it('RankingBlock shows empty state when no players', () => {
    render(
      <RankingBlock
        title="Classifica"
        entityColor={COLOR}
        data={{ type: 'ranking', players: [] }}
      />
    );
    expect(screen.getByText('No data yet')).toBeInTheDocument();
  });

  it('KBPreviewBlock renders doc count and status', () => {
    render(
      <KBPreviewBlock
        title="KB"
        entityColor={COLOR}
        data={{ type: 'kbPreview', docsCount: 12, chunksCount: 245, indexStatus: 'indexed' }}
      />
    );
    expect(screen.getByText(/12/)).toBeInTheDocument();
    expect(screen.getByText(/245/)).toBeInTheDocument();
    expect(screen.getByText('indexed')).toBeInTheDocument();
  });

  it('KBPreviewBlock renders optional lastQuery', () => {
    render(
      <KBPreviewBlock
        title="KB"
        entityColor={COLOR}
        data={{
          type: 'kbPreview',
          docsCount: 5,
          chunksCount: 100,
          indexStatus: 'indexed',
          lastQuery: 'How many players?',
        }}
      />
    );
    expect(screen.getByText('How many players?')).toBeInTheDocument();
  });

  it('MembersBlock renders member names', () => {
    render(
      <MembersBlock
        title="Members"
        entityColor={COLOR}
        data={{ type: 'members', members: [{ name: 'Luca', role: 'Host' }] }}
      />
    );
    expect(screen.getByText('Luca')).toBeInTheDocument();
    expect(screen.getByText('Host')).toBeInTheDocument();
  });

  it('MembersBlock shows empty state when no members', () => {
    render(
      <MembersBlock
        title="Members"
        entityColor={COLOR}
        data={{ type: 'members', members: [] }}
      />
    );
    expect(screen.getByText('No data yet')).toBeInTheDocument();
  });

  it('ContentsBlock renders items with titles', () => {
    render(
      <ContentsBlock
        title="Giochi"
        entityColor={COLOR}
        data={{
          type: 'contents',
          items: [{ title: 'Catan', entityType: 'game', id: '1' }],
        }}
      />
    );
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('ContentsBlock renders item status when provided', () => {
    render(
      <ContentsBlock
        title="Giochi"
        entityColor={COLOR}
        data={{
          type: 'contents',
          items: [{ title: 'Pandemic', entityType: 'game', id: '2', status: 'active' }],
        }}
      />
    );
    expect(screen.getByText('Pandemic')).toBeInTheDocument();
    expect(screen.getByText('active')).toBeInTheDocument();
  });

  it('ContentsBlock shows empty state when no items', () => {
    render(
      <ContentsBlock
        title="Contents"
        entityColor={COLOR}
        data={{ type: 'contents', items: [] }}
      />
    );
    expect(screen.getByText('No data yet')).toBeInTheDocument();
  });

  it('HistoryBlock renders entries', () => {
    render(
      <HistoryBlock
        title="History"
        entityColor={COLOR}
        data={{
          type: 'history',
          entries: [{ timestamp: '14:30', message: 'Query about rules', sender: 'User' }],
        }}
      />
    );
    expect(screen.getByText('Query about rules')).toBeInTheDocument();
    expect(screen.getByText('User')).toBeInTheDocument();
    expect(screen.getByText('14:30')).toBeInTheDocument();
  });

  it('HistoryBlock shows empty state when no entries', () => {
    render(
      <HistoryBlock
        title="History"
        entityColor={COLOR}
        data={{ type: 'history', entries: [] }}
      />
    );
    expect(screen.getByText('No data yet')).toBeInTheDocument();
  });

  it('ProgressBlock renders progress bar', () => {
    render(
      <ProgressBlock
        title="Progress"
        entityColor={COLOR}
        data={{ type: 'progress', current: 7, target: 10, label: 'Level 5' }}
      />
    );
    expect(screen.getByText('Level 5')).toBeInTheDocument();
    expect(screen.getByText('70%')).toBeInTheDocument();
  });

  it('ProgressBlock renders milestones when provided', () => {
    render(
      <ProgressBlock
        title="Progress"
        entityColor={COLOR}
        data={{
          type: 'progress',
          current: 5,
          target: 10,
          label: 'Quest',
          milestones: [{ at: 3, label: 'Checkpoint A' }],
        }}
      />
    );
    expect(screen.getByText(/Checkpoint A/)).toBeInTheDocument();
  });

  it('ProgressBlock clamps to 100% when current exceeds target', () => {
    render(
      <ProgressBlock
        title="Progress"
        entityColor={COLOR}
        data={{ type: 'progress', current: 15, target: 10, label: 'Done' }}
      />
    );
    expect(screen.getByText('100%')).toBeInTheDocument();
  });

  it('NotesBlock renders content', () => {
    render(
      <NotesBlock
        title="Notes"
        entityColor={COLOR}
        data={{ type: 'notes', content: 'Remember to use blue strategy', updatedAt: '2026-03-15' }}
      />
    );
    expect(screen.getByText('Remember to use blue strategy')).toBeInTheDocument();
    expect(screen.getByText(/2026-03-15/)).toBeInTheDocument();
  });

  it('NotesBlock shows empty state when no content', () => {
    render(
      <NotesBlock
        title="Notes"
        entityColor={COLOR}
        data={{ type: 'notes', content: '', updatedAt: '2026-03-15' }}
      />
    );
    expect(screen.getByText('No data yet')).toBeInTheDocument();
  });

  it('DetailLinkBlock renders link', () => {
    render(
      <DetailLinkBlock
        title=""
        entityColor={COLOR}
        data={{ type: 'detailLink', href: '/games/1', label: 'Vai al dettaglio' }}
      />
    );
    expect(screen.getByText('Vai al dettaglio')).toBeInTheDocument();
    const link = screen.getByRole('link');
    expect(link).toHaveAttribute('href', '/games/1');
  });

  it('DetailLinkBlock renders with title when provided', () => {
    render(
      <DetailLinkBlock
        title="Dettaglio"
        entityColor={COLOR}
        data={{ type: 'detailLink', href: '/games/2', label: 'Apri' }}
      />
    );
    expect(screen.getByText('Dettaglio')).toBeInTheDocument();
    expect(screen.getByText('Apri')).toBeInTheDocument();
  });
});
