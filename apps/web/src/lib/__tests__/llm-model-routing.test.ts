import { describe, expect, it } from 'vitest';

import {
  BARE_CLOUD_PROVIDER_PREFIXES,
  isRoutableModelId,
  isUnroutableBareCloudId,
} from '../llm-model-routing';

/**
 * Issue #4102. These cases mirror `LlmModelRouting.IsUnroutableBareCloudId`
 * (`apps/api/src/Api/Services/LlmClients/LlmModelRouting.cs`). The point of the
 * suite is not the predicate in isolation: it is that the four model ids the
 * agent builder used to offer are rejected here, so the form can no longer put
 * the user in front of an option that always answers `422`.
 */
describe('isUnroutableBareCloudId', () => {
  it('rejects the four unroutable ids the hardcoded dropdown used to offer', () => {
    // The fifth, 'deepseek-chat', was the only routable one — see below.
    for (const id of ['gpt-4', 'gpt-4-turbo', 'claude-3-opus', 'claude-3-sonnet']) {
      expect(isUnroutableBareCloudId(id), id).toBe(true);
      expect(isRoutableModelId(id), id).toBe(false);
    }
  });

  it('accepts the ids the backend can actually route', () => {
    for (const id of [
      'deepseek-chat',
      'deepseek-reasoner',
      'anthropic/claude-3.5-haiku',
      'openai/gpt-4o',
      'llama3',
      'mistral',
    ]) {
      expect(isUnroutableBareCloudId(id), id).toBe(false);
      expect(isRoutableModelId(id), id).toBe(true);
    }
  });

  it('treats any id containing a slash as routable — it is an OpenRouter slug', () => {
    // This is why 'anthropic/claude-…' passes while bare 'claude-…' does not:
    // the slash is the whole distinction the backend draws.
    expect(isUnroutableBareCloudId('claude-3-opus')).toBe(true);
    expect(isUnroutableBareCloudId('anthropic/claude-3-opus')).toBe(false);
  });

  it('is case-insensitive on the prefix', () => {
    expect(isUnroutableBareCloudId('GPT-4')).toBe(true);
    expect(isUnroutableBareCloudId('Claude-3-Opus')).toBe(true);
  });

  it('does not flag blank ids — emptiness is a separate validation concern', () => {
    // Matches the backend, which leaves blanks to its NotEmpty rule. The two
    // helpers diverge here on purpose: `isRoutableModelId` is for filtering
    // option lists, so a blank is not an option.
    for (const id of ['', '   ', null, undefined]) {
      expect(isUnroutableBareCloudId(id)).toBe(false);
      expect(isRoutableModelId(id)).toBe(false);
    }
  });

  it('flags every declared prefix, so the mirror cannot silently lose one', () => {
    // Guards against a prefix being dropped from the array without the suite
    // noticing: each one must still produce an unroutable id.
    for (const prefix of BARE_CLOUD_PROVIDER_PREFIXES) {
      expect(isUnroutableBareCloudId(`${prefix}something`), prefix).toBe(true);
    }
  });
});
