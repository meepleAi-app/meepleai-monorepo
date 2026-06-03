/**
 * FlagCategoryTabs Component Tests (Issue #1836)
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  FlagCategoryTabs,
  computeCategoryCounts,
  detectFlagCategory,
  readCategoryFromHash,
  writeCategoryToHash,
} from '../FlagCategoryTabs';

describe('detectFlagCategory', () => {
  it.each([
    ['Features:RagCaching', 'ai'],
    ['Features:StreamingResponses', 'ai'],
    ['Features:DeepSeekProvider', 'ai'],
    ['Features:OpenAiProvider', 'ai'],
    ['Features:EmbeddingService', 'ai'],
    ['Features:LlmRouting', 'ai'],
    ['Features:OAuthGoogle', 'integrations'],
    ['Features:WebhookN8n', 'integrations'],
    ['Features:SSO', 'integrations'],
    ['Features:MFA', 'security'],
    ['Features:RateLimitProtection', 'security'],
    ['Features:PasswordPolicy', 'security'],
    ['Features:BetaSurvey', 'features'],
    ['Features:SetupGuide', 'features'],
    ['Features:NewLibraryHub', 'features'],
  ] as const)('classifies %s as %s', (key, expected) => {
    expect(detectFlagCategory(key)).toBe(expected);
  });
});

describe('computeCategoryCounts', () => {
  it('returns 0 for empty list except total', () => {
    expect(computeCategoryCounts([])).toEqual({
      all: 0,
      features: 0,
      ai: 0,
      integrations: 0,
      security: 0,
    });
  });

  it('aggregates counts per category and totals', () => {
    const counts = computeCategoryCounts([
      'Features:RagCaching',
      'Features:StreamingResponses',
      'Features:OAuthGoogle',
      'Features:BetaSurvey',
      'Features:MFA',
      'Features:Misc',
    ]);

    expect(counts).toEqual({
      all: 6,
      features: 2,
      ai: 2,
      integrations: 1,
      security: 1,
    });
  });
});

describe('FlagCategoryTabs', () => {
  const flagKeys = [
    'Features:RagCaching',
    'Features:OAuthGoogle',
    'Features:MFA',
    'Features:BetaSurvey',
  ];

  it('renders one tab per category plus the All facet', () => {
    render(
      <FlagCategoryTabs flagKeys={flagKeys} activeCategory="all" onCategoryChange={vi.fn()} />
    );

    expect(screen.getByTestId('flag-category-tab-all')).toBeInTheDocument();
    expect(screen.getByTestId('flag-category-tab-features')).toBeInTheDocument();
    expect(screen.getByTestId('flag-category-tab-ai')).toBeInTheDocument();
    expect(screen.getByTestId('flag-category-tab-integrations')).toBeInTheDocument();
    expect(screen.getByTestId('flag-category-tab-security')).toBeInTheDocument();
  });

  it('renders per-category counts', () => {
    render(
      <FlagCategoryTabs flagKeys={flagKeys} activeCategory="all" onCategoryChange={vi.fn()} />
    );

    expect(screen.getByTestId('flag-category-tab-all-count')).toHaveTextContent('4');
    expect(screen.getByTestId('flag-category-tab-ai-count')).toHaveTextContent('1');
    expect(screen.getByTestId('flag-category-tab-integrations-count')).toHaveTextContent('1');
    expect(screen.getByTestId('flag-category-tab-security-count')).toHaveTextContent('1');
    expect(screen.getByTestId('flag-category-tab-features-count')).toHaveTextContent('1');
  });

  it('marks the active tab with aria-selected=true', () => {
    render(<FlagCategoryTabs flagKeys={flagKeys} activeCategory="ai" onCategoryChange={vi.fn()} />);

    expect(screen.getByTestId('flag-category-tab-ai')).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByTestId('flag-category-tab-all')).toHaveAttribute('aria-selected', 'false');
  });

  it('invokes onCategoryChange with the clicked category', async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();

    render(
      <FlagCategoryTabs flagKeys={flagKeys} activeCategory="all" onCategoryChange={onChange} />
    );

    await user.click(screen.getByTestId('flag-category-tab-security'));
    expect(onChange).toHaveBeenCalledWith('security');
  });
});

describe('readCategoryFromHash / writeCategoryToHash', () => {
  beforeEach(() => {
    window.history.replaceState(null, '', '/admin/config?tab=flags');
  });

  afterEach(() => {
    window.history.replaceState(null, '', '/');
  });

  it('returns "all" when the hash is empty', () => {
    expect(readCategoryFromHash()).toBe('all');
  });

  it('returns "all" for an unknown category', () => {
    window.history.replaceState(null, '', '/admin/config?tab=flags#category=bogus');
    expect(readCategoryFromHash()).toBe('all');
  });

  it('round-trips a category through the hash', () => {
    writeCategoryToHash('integrations');
    expect(window.location.hash).toBe('#category=integrations');
    expect(readCategoryFromHash()).toBe('integrations');
  });

  it('drops the hash when writing "all"', () => {
    window.history.replaceState(null, '', '/admin/config?tab=flags#category=ai');
    writeCategoryToHash('all');
    expect(window.location.hash).toBe('');
  });
});
