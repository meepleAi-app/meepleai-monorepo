import { describe, expect, it } from 'vitest';

import { inviteSubject } from '../invite-subject';

// #4910. The invitation heading read «<host> ti invita a giocare a » and stopped there for a night with no game: the
// game name was an empty string. The night's own title stands in when there is no game, and the sentence changes with
// it, because «giocare a Serata del sabato» is not Italian.

describe('inviteSubject', () => {
  it('names the game when there is one', () => {
    expect(inviteSubject({ primaryGameName: 'Catan', title: 'Serata del sabato' })).toEqual({
      verb: 'ti invita a giocare a',
      name: 'Catan',
    });
  });

  it.each([null, '', '   '])(
    'falls back to the title of the night, with a verb that fits it, when the game is %j',
    primaryGameName => {
      expect(inviteSubject({ primaryGameName, title: 'Serata del sabato' })).toEqual({
        verb: 'ti invita a',
        name: 'Serata del sabato',
      });
    }
  );

  it('still makes a whole sentence when the night has no title either', () => {
    expect(inviteSubject({ primaryGameName: null, title: '  ' })).toEqual({
      verb: 'ti invita a una serata di gioco',
      name: '',
    });
  });

  it('trims what it returns', () => {
    expect(inviteSubject({ primaryGameName: ' Catan ', title: '' }).name).toBe('Catan');
    expect(inviteSubject({ primaryGameName: null, title: ' Serata ' }).name).toBe('Serata');
  });
});
