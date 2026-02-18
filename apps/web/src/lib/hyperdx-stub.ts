/**
 * HyperDX Stub Module
 *
 * No-op stub for @hyperdx/browser when the SDK is not available or configured.
 * Used by Turbopack resolve alias to avoid module factory errors.
 */

 
const stub = {
  init(_config: Record<string, unknown>) {},
  setGlobalAttributes(_attrs: Record<string, unknown>) {},
  addAction(_name: string, _props?: Record<string, unknown>) {},
};

export default stub;
