import { describe, expect, it } from 'vitest';

import { sendIfChanged } from './sendIfChanged';

describe('sendIfChanged (#4798)', () => {
  it.each([
    ['a field stored as null, left empty', '', null],
    ['a field stored as undefined, left empty', '', undefined],
    ['a field stored as empty text, left empty', '', ''],
    ['a field with text, left as it was', 'Padova', 'Padova'],
    ['a field stored as null, given only spaces (the server trims them to nothing)', '   ', null],
    ['a field with text, given a trailing space (the server trims it)', 'Padova ', 'Padova'],
    ['a field the form did not deliver, over null', undefined, null],
    [
      'a field the form did not deliver, over text: absent still means «leave it»',
      undefined,
      'Padova',
    ],
  ])('sends nothing for %s', (_label, typed, loaded) => {
    expect(sendIfChanged(typed, loaded)).toBeUndefined();
  });

  it('sends the text typed over a field that had none', () => {
    expect(sendIfChanged('Verona', null)).toBe('Verona');
  });

  it('sends the new text for a field that was changed', () => {
    expect(sendIfChanged('Verona', 'Padova')).toBe('Verona');
  });

  it('sends the empty text for a field that was cleared, so it can still be emptied', () => {
    expect(sendIfChanged('', 'Padova')).toBe('');
  });

  it('sends the text as typed, not trimmed: trimming is the server’s job', () => {
    expect(sendIfChanged(' Verona ', 'Padova')).toBe(' Verona ');
  });
});
