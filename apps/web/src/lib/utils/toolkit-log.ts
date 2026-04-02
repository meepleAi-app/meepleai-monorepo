import type { ToolLogEntry } from '@/lib/types/standalone-toolkit';

const STORAGE_KEY = 'meepleai:toolkit:log';
const MAX_ENTRIES = 100;
const MAX_AGE_DAYS = 7;

export function getToolLog(): ToolLogEntry[] {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as ToolLogEntry[]) : [];
  } catch {
    return [];
  }
}

export function appendToolLog(entry: ToolLogEntry): void {
  let log = getToolLog();
  log.push(entry);
  if (log.length > MAX_ENTRIES) {
    log = log.slice(log.length - MAX_ENTRIES);
  }
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(log));
  } catch {
    // localStorage pieno — rimuovi metà delle voci più vecchie e riprova
    log = log.slice(Math.floor(log.length / 2));
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(log));
    } catch {
      // retry also failed — discard silently to avoid propagating storage errors
    }
  }
}

export function clearOldEntries(): void {
  const cutoff = new Date();
  cutoff.setDate(cutoff.getDate() - MAX_AGE_DAYS);
  const log = getToolLog().filter(e => new Date(e.timestamp) >= cutoff);
  localStorage.setItem(STORAGE_KEY, JSON.stringify(log));
}

export function generateLogId(): string {
  return `${Date.now()}-${Math.random().toString(36).slice(2, 7)}`;
}
