/**
 * `settings.security` — the words of Settings → Security (#4813).
 *
 * Nothing forces the two catalogues to agree: they already differ by a handful of keys elsewhere. For this
 * block a key present in one language and not in the other is a screen that shows a raw key to half the
 * users, so the block is held to parity here: the same keys, none empty, and the same `{placeholders}`
 * in a message and its translation (a translation that drops `{minutes}` prints a sentence without the number).
 */

import { describe, expect, it } from 'vitest';

import en from '../en.json';
import it_ from '../it.json';

type Tree = { [key: string]: string | Tree };

function leaves(tree: Tree, prefix = ''): Record<string, string> {
  return Object.entries(tree).reduce<Record<string, string>>((acc, [key, value]) => {
    const path = prefix ? `${prefix}.${key}` : key;
    return typeof value === 'string'
      ? { ...acc, [path]: value }
      : { ...acc, ...leaves(value, path) };
  }, {});
}

function placeholders(message: string): string[] {
  return [...message.matchAll(/\{(\w+)\}/g)].map(m => m[1]).sort();
}

const english = leaves((en as unknown as { settings: { security: Tree } }).settings.security);
const italian = leaves((it_ as unknown as { settings: { security: Tree } }).settings.security);

describe('settings.security catalogue', () => {
  it('has the same keys in English and Italian', () => {
    expect(Object.keys(italian).sort()).toEqual(Object.keys(english).sort());
  });

  it('has no empty message', () => {
    const empty = [...Object.entries(english), ...Object.entries(italian)].filter(
      ([, v]) => v.trim() === ''
    );
    expect(empty).toEqual([]);
  });

  it.each(Object.keys(english))('%s carries the same placeholders in both languages', key => {
    expect(placeholders(italian[key])).toEqual(placeholders(english[key]));
  });

  it('is not empty', () => {
    expect(Object.keys(english).length).toBeGreaterThan(0);
  });
});
