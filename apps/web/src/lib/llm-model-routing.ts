/**
 * llm-model-routing — frontend mirror of the backend model-routability rule.
 *
 * Issue #4102: the agent builder offered a hardcoded model list of which only
 * one entry was routable. The backend rejects a bare cloud-provider id with
 * `422 "Model is not routable"`, so any option that fails this predicate makes
 * the form unsubmittable no matter what else the user fills in.
 *
 * Source of truth: `LlmModelRouting.IsUnroutableBareCloudId` in
 * `apps/api/src/Api/Services/LlmClients/LlmModelRouting.cs`, enforced by
 * `CreateAgentDefinitionCommandValidator` and `UpdateAgentDefinitionCommandValidator`.
 * Keep `BARE_CLOUD_PROVIDER_PREFIXES` in step with `BareCloudProviderPrefixes`
 * there — a divergence shows up as a 422 the user cannot act on.
 */

/** Mirrors `LlmModelRouting.BareCloudProviderPrefixes`. */
export const BARE_CLOUD_PROVIDER_PREFIXES = [
  'claude',
  'gpt-3',
  'gpt-4',
  'gpt-5',
  'chatgpt',
  'gemini',
  'grok',
  'o1-',
  'o3-',
  'o4-',
] as const;

/**
 * True when `modelId` is a bare (unprefixed) cloud-provider id that no LLM
 * client can route: OpenRouter needs a `provider/model` slug (so an id
 * containing `/` is always considered routable here), Ollama rejects these
 * prefixes, and DeepSeek only serves `deepseek-*`.
 *
 * Blank ids are NOT flagged, matching the backend: emptiness is a separate
 * validation concern.
 */
export function isUnroutableBareCloudId(modelId: string | null | undefined): boolean {
  if (!modelId || modelId.trim() === '' || modelId.includes('/')) {
    return false;
  }

  const lower = modelId.toLowerCase();
  return BARE_CLOUD_PROVIDER_PREFIXES.some(prefix => lower.startsWith(prefix));
}

/** Convenience inverse, for filtering option lists. */
export function isRoutableModelId(modelId: string | null | undefined): boolean {
  return !!modelId && modelId.trim() !== '' && !isUnroutableBareCloudId(modelId);
}
