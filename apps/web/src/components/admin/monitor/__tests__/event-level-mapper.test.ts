import { describe, it, expect } from 'vitest';

import { mapEventLevel } from '../event-level-mapper';

describe('mapEventLevel', () => {
  // --- 'ok' — successful completions ---

  it('mapEventLevel_AgentCreated_ReturnsOk', () => {
    expect(mapEventLevel('agent.created')).toBe('ok');
  });

  it('mapEventLevel_KbDocIndexed_ReturnsOk', () => {
    expect(mapEventLevel('kb.doc.indexed')).toBe('ok');
  });

  it('mapEventLevel_SessionFinalized_ReturnsOk', () => {
    expect(mapEventLevel('session.finalized')).toBe('ok');
  });

  it('mapEventLevel_ChatSessionCreated_ReturnsOk', () => {
    expect(mapEventLevel('chat.session.created')).toBe('ok');
  });

  it('mapEventLevel_SessionCreated_ReturnsOk', () => {
    expect(mapEventLevel('session.created')).toBe('ok');
  });

  // --- 'err' — failures / errors ---

  it('mapEventLevel_ChatSessionFailed_ReturnsErr', () => {
    expect(mapEventLevel('chat.session.failed')).toBe('err');
  });

  it('mapEventLevel_GenericError_ReturnsErr', () => {
    expect(mapEventLevel('pipeline.error')).toBe('err');
  });

  // --- 'warn' — removals ---

  it('mapEventLevel_LibraryEntryRemoved_ReturnsWarn', () => {
    expect(mapEventLevel('library.entry.removed')).toBe('warn');
  });

  // --- 'info' — default / no matching suffix ---

  it('mapEventLevel_LibrarySessionRecorded_ReturnsInfo', () => {
    expect(mapEventLevel('library.session.recorded')).toBe('info');
  });

  it('mapEventLevel_UnknownEvent_ReturnsInfo', () => {
    expect(mapEventLevel('foo.bar.baz')).toBe('info');
  });

  // --- edge cases ---

  it('mapEventLevel_UppercaseEvent_HandledCaseInsensitive', () => {
    expect(mapEventLevel('AGENT.CREATED')).toBe('ok');
  });

  it('mapEventLevel_EmptyString_ReturnsInfo', () => {
    expect(mapEventLevel('')).toBe('info');
  });
});
