import { describe, it, expect } from 'vitest';

import { parseCsvInvitations } from '../parseCsvInvitations';

describe('parseCsvInvitations', () => {
  it('parses a valid row with email and role', () => {
    const result = parseCsvInvitations('alice@example.com,User');
    expect(result).toHaveLength(1);
    expect(result[0]).toEqual({ email: 'alice@example.com', role: 'User', valid: true });
  });

  it('defaults role to User when missing', () => {
    const result = parseCsvInvitations('alice@example.com');
    expect(result[0]).toEqual({ email: 'alice@example.com', role: 'User', valid: true });
  });

  it('trims whitespace from email and role', () => {
    const result = parseCsvInvitations('  alice@example.com , Editor  ');
    expect(result[0]).toEqual({ email: 'alice@example.com', role: 'Editor', valid: true });
  });

  it('marks row as invalid for bad email', () => {
    const result = parseCsvInvitations('not-an-email,User');
    expect(result[0].valid).toBe(false);
    expect(result[0].error).toMatch(/invalid email/i);
  });

  it('marks row as invalid for unknown role', () => {
    const result = parseCsvInvitations('alice@example.com,SuperAdmin');
    expect(result[0].valid).toBe(false);
    expect(result[0].error).toMatch(/invalid role/i);
  });

  it('marks row as invalid for empty email', () => {
    const result = parseCsvInvitations(',User');
    expect(result[0].valid).toBe(false);
    expect(result[0].error).toMatch(/email is empty/i);
  });

  it('parses multiple rows', () => {
    const csv = 'alice@example.com,User\nbob@example.com,Admin\nbad-email,Editor';
    const result = parseCsvInvitations(csv);
    expect(result).toHaveLength(3);
    expect(result[0].valid).toBe(true);
    expect(result[1].valid).toBe(true);
    expect(result[2].valid).toBe(false);
  });

  it('skips blank lines', () => {
    const result = parseCsvInvitations('alice@example.com,User\n\n\nbob@example.com,Admin');
    expect(result).toHaveLength(2);
  });

  it('returns empty array for empty string', () => {
    expect(parseCsvInvitations('')).toHaveLength(0);
    expect(parseCsvInvitations('   ')).toHaveLength(0);
  });
});
