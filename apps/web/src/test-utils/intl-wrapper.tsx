/**
 * Real-catalogue Intl wrappers for component tests (#4813).
 *
 * `renderWithProviders` always loads the English messages whatever `locale` it is given, and the ad-hoc
 * `IntlProvider`s scattered in older tests carry a handful of hand-written strings. A test that wants to know
 * what a user with the interface in Italian reads needs the Italian catalogue, loaded the way the app loads
 * it — which is the app's own provider with an explicit locale.
 *
 * Use as a RTL wrapper, so `rerender` keeps the provider:
 *
 * ```tsx
 * render(<MyComponent />, { wrapper: ItalianIntl });
 * ```
 */

import type { ReactElement, ReactNode } from 'react';

import { render, type RenderOptions, type RenderResult } from '@testing-library/react';

import { IntlProvider } from '@/components/providers/IntlProvider';

interface WrapperProps {
  readonly children: ReactNode;
}

export function EnglishIntl({ children }: WrapperProps): React.JSX.Element {
  return <IntlProvider locale="en">{children}</IntlProvider>;
}

export function ItalianIntl({ children }: WrapperProps): React.JSX.Element {
  return <IntlProvider locale="it">{children}</IntlProvider>;
}

/**
 * `render` with the English catalogue: for the tests that assert English words and only need a provider to
 * mount a component that now reads its words from the catalogue. `rerender` keeps the provider.
 */
export const renderInEnglish = (
  ui: ReactElement,
  options?: Omit<RenderOptions, 'queries'>
): RenderResult => render(ui, { wrapper: EnglishIntl, ...options });
