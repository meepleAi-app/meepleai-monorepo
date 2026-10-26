import { describe, expect, it } from 'vitest';

import { EXPIRED_QUERY_LABEL, formatExecutionQuery } from '../rag-execution-query';

describe('formatExecutionQuery (#4634)', () => {
  it('returns the question as it is', () => {
    expect(formatExecutionQuery('How many cards does each player draw?')).toBe(
      'How many cards does each player draw?'
    );
  });

  it.each([
    ['', 'an empty string'],
    [null, 'null'],
    [undefined, 'undefined'],
  ])('labels %j (%s) as expired', value => {
    expect(formatExecutionQuery(value)).toBe(EXPIRED_QUERY_LABEL);
  });

  it('does not label a question that is only whitespace: that is not what the server writes', () => {
    expect(formatExecutionQuery('   ')).toBe('   ');
  });
});
