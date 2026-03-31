import { isValidEmail } from './validation';
import { INVITATION_ROLES } from '../api/schemas/invitation.schemas';

export interface ParsedCsvRow {
  email: string;
  role: string;
  valid: boolean;
  error?: string;
}

/**
 * Parses CSV text (one "email,role" per line) into validated rows.
 * Blank lines are skipped. Role defaults to 'User' if omitted.
 */
export function parseCsvInvitations(text: string): ParsedCsvRow[] {
  const lines = text
    .split('\n')
    .map(l => l.trim())
    .filter(l => l.length > 0);

  return lines.map(line => {
    const parts = line.split(',').map(p => p.trim());
    const email = parts[0] ?? '';
    const role = parts[1] ?? 'User';

    if (!email) {
      return { email, role, valid: false, error: 'Email is empty' };
    }
    if (!isValidEmail(email)) {
      return { email, role, valid: false, error: 'Invalid email format' };
    }
    if (!(INVITATION_ROLES as readonly string[]).includes(role)) {
      return { email, role, valid: false, error: `Invalid role: ${role}` };
    }

    return { email, role, valid: true };
  });
}
