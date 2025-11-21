#!/usr/bin/env tsx
/**
 * Generate Zod validation schemas from OpenAPI specification
 *
 * Post-NSwag Migration (Issue #1543):
 * This script generates Zod schemas only. The TypeScript API client is manually
 * maintained in src/lib/api/clients/ (modular API SDK).
 *
 * This script:
 * 1. Fetches the OpenAPI spec from the running API (or uses committed static file)
 * 2. Generates Zod validation schemas using openapi-zod-client
 *
 * Usage:
 *   pnpm generate:api
 *
 * Note: The openapi.json file is committed to the repo (Option C - Hybrid).
 * Regenerate it when API changes by:
 *   1. Fix build errors (ActiveSession, EndpointFilter issues)
 *   2. Run API: cd apps/api && dotnet run
 *   3. Download spec: curl http://localhost:5080/swagger/v1/swagger.json -o apps/api/src/Api/openapi.json
 *
 * Architecture:
 * - Zod Schemas: Generated automatically (this script)
 * - API Client: Manually maintained modular SDK in src/lib/api/clients/
 */

import { generateZodClientFromOpenAPI } from 'openapi-zod-client';
import * as fs from 'fs/promises';
import * as path from 'path';

const OPENAPI_URL = process.env.OPENAPI_URL || 'http://localhost:8080/swagger/v1/swagger.json';
const OPENAPI_FILE = process.env.OPENAPI_FILE || '../../api/src/Api/openapi.json';
const OUTPUT_DIR = path.join(__dirname, '../src/lib/api/generated');

async function fetchOpenApiSpec(): Promise<string> {
  console.log('📥 Fetching OpenAPI specification...');

  try {
    // Try to fetch from running API first with timeout
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), 10000); // 10s timeout

    try {
      const response = await fetch(OPENAPI_URL, {
        signal: controller.signal,
        headers: { 'Accept': 'application/json' }
      });
      clearTimeout(timeoutId);

      if (response.ok) {
        const spec = await response.text();
        console.log('✅ Fetched OpenAPI spec from running API');
        return spec;
      }
    } catch (fetchError) {
      clearTimeout(timeoutId);
      const errorMsg = fetchError instanceof Error ? fetchError.message : String(fetchError);
      console.log(`⚠️  Could not fetch from ${OPENAPI_URL} (${errorMsg}), trying local file...`);
    }
  } catch (error) {
    const errorMsg = error instanceof Error ? error.message : String(error);
    console.log(`⚠️  Fetch setup failed (${errorMsg}), trying local file...`);
  }

  // Fallback to local file
  const localPath = path.join(__dirname, OPENAPI_FILE);
  try {
    const spec = await fs.readFile(localPath, 'utf-8');
    console.log('✅ Loaded OpenAPI spec from local file');
    return spec;
  } catch (error) {
    const errorMsg = error instanceof Error ? error.message : String(error);
    throw new Error(
      `Failed to fetch OpenAPI spec from ${OPENAPI_URL} or read from ${localPath}.\n` +
      `Error: ${errorMsg}\n` +
      `Please ensure the API is running or build the API first to generate openapi.json`
    );
  }
}

async function generateZodSchemas(openApiSpec: string): Promise<void> {
  console.log('🔨 Generating Zod schemas...');

  // Parse and validate OpenAPI spec
  let parsedSpec: unknown;
  try {
    parsedSpec = JSON.parse(openApiSpec);
  } catch (error) {
    const errorMsg = error instanceof Error ? error.message : String(error);
    throw new Error(`Invalid OpenAPI JSON: ${errorMsg}`);
  }

  // Type guard: Ensure parsedSpec is an object
  if (typeof parsedSpec !== 'object' || parsedSpec === null || Array.isArray(parsedSpec)) {
    throw new Error(
      'Invalid OpenAPI specification format (expected object). ' +
      'Ensure the backend is generating a valid OpenAPI document.'
    );
  }

  // Basic validation - check for required OpenAPI fields
  if (!('openapi' in parsedSpec) && !('swagger' in parsedSpec)) {
    throw new Error(
      'Not a valid OpenAPI specification (missing openapi/swagger field). ' +
      'Ensure the backend is generating a valid OpenAPI document.'
    );
  }

  const version = ('openapi' in parsedSpec && typeof parsedSpec.openapi === 'string')
    ? parsedSpec.openapi
    : ('swagger' in parsedSpec && typeof parsedSpec.swagger === 'string')
      ? parsedSpec.swagger
      : 'unknown';

  console.log(`📋 OpenAPI version: ${version}`);

  const outputPath = path.join(OUTPUT_DIR, 'zod-schemas.ts');

  await generateZodClientFromOpenAPI({
    openApiDoc: parsedSpec,
    distPath: outputPath,
    options: {
      withAlias: true,
      withDefaultValues: true,
      withDocs: true,
      withImplicitRequiredProps: true,
      groupStrategy: 'tag',
      complexityThreshold: 15,
      defaultStatusBehavior: 'spec-compliant',
      withDeprecatedEndpoints: false,
    },
  });

  console.log(`✅ Generated Zod schemas at ${outputPath}`);
}

async function main(): Promise<void> {
  console.log('🚀 Starting API client generation...\n');

  try {
    // Ensure output directory exists
    await fs.mkdir(OUTPUT_DIR, { recursive: true });

    // Fetch OpenAPI spec
    const openApiSpec = await fetchOpenApiSpec();

    // Save OpenAPI spec for reference
    const specPath = path.join(OUTPUT_DIR, 'openapi.json');
    await fs.writeFile(specPath, openApiSpec, 'utf-8');
    console.log(`📄 Saved OpenAPI spec to ${specPath}\n`);

    // Generate Zod schemas
    await generateZodSchemas(openApiSpec);

    console.log('\n✨ Zod schema generation completed successfully!');
    console.log('📁 Generated files in:', OUTPUT_DIR);
    console.log('   - openapi.json: OpenAPI specification');
    console.log('   - zod-schemas.ts: Zod validation schemas');
    console.log('\n💡 Note: TypeScript API client is manually maintained in src/lib/api/clients/\n');
  } catch (error) {
    console.error('❌ Error generating API client:');
    console.error(error);
    process.exit(1);
  }
}

main();
