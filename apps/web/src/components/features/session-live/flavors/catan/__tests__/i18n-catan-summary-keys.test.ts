import { describe, it, expect } from 'vitest';
import enMessages from '@/locales/en.json';
import itMessages from '@/locales/it.json';

const KEYS = ['winnerTemplate', 'vpUnit', 'durationTemplate', 'standingsTitle', 'empty'];

type Catalog = { pages: { sessionSummary: { flavor: { catan: Record<string, string> } } } };
const it_ = (itMessages as Catalog).pages.sessionSummary?.flavor?.catan ?? {};
const en_ = (enMessages as Catalog).pages.sessionSummary?.flavor?.catan ?? {};

describe('Catan summary i18n keys (#3022)', () => {
  it.each(KEYS)('IT has %s', k => expect(it_[k]).toBeTruthy());
  it.each(KEYS)('EN has %s', k => expect(en_[k]).toBeTruthy());
  it('IT and EN have the same keys (parity)', () => {
    expect(Object.keys(it_).sort()).toEqual(Object.keys(en_).sort());
  });
});
