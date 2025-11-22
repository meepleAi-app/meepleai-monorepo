#!/usr/bin/env node

/**
 * Genera documentazione API da OpenAPI spec
 *
 * Prerequisiti:
 * - API deve essere in esecuzione in development mode (http://localhost:8080)
 * - npm install -g @redocly/cli (opzionale, per Redoc HTML)
 *
 * Output:
 * - docs/api-reference.json - OpenAPI spec JSON
 * - docs/api-reference.md - Documentazione Markdown generata
 */

const fs = require('fs');
const path = require('path');
const http = require('http');

const API_BASE_URL = process.env.API_BASE_URL || 'http://localhost:8080';
const SPEC_PATH = '/swagger/v1/swagger.json';
const OUTPUT_JSON = path.join(__dirname, '../docs/api-reference.json');
const OUTPUT_MD = path.join(__dirname, '../docs/api-reference.md');

function fetchSpec() {
  return new Promise((resolve, reject) => {
    console.log(`Fetching OpenAPI spec from ${API_BASE_URL}${SPEC_PATH}...`);

    http.get(`${API_BASE_URL}${SPEC_PATH}`, (res) => {
      if (res.statusCode !== 200) {
        reject(new Error(`HTTP ${res.statusCode}: ${res.statusMessage}`));
        return;
      }

      let data = '';
      res.on('data', chunk => data += chunk);
      res.on('end', () => {
        try {
          const spec = JSON.parse(data);
          resolve(spec);
        } catch (err) {
          reject(err);
        }
      });
    }).on('error', reject);
  });
}

function generateMarkdown(spec) {
  let md = `# MeepleAI API Reference

**Version**: ${spec.info.version}
**Description**: ${spec.info.description}

Generated on: ${new Date().toISOString()}

---

## Table of Contents

`;

  // Generate TOC
  const tags = {};
  Object.entries(spec.paths).forEach(([path, methods]) => {
    Object.entries(methods).forEach(([method, operation]) => {
      if (!operation.tags) operation.tags = ['General'];
      operation.tags.forEach(tag => {
        if (!tags[tag]) tags[tag] = [];
        tags[tag].push({ path, method: method.toUpperCase(), operation });
      });
    });
  });

  Object.keys(tags).forEach(tag => {
    md += `- [${tag}](#${tag.toLowerCase().replace(/\s+/g, '-')})\n`;
  });

  md += '\n---\n\n';

  // Generate endpoint documentation by tag
  Object.entries(tags).forEach(([tag, endpoints]) => {
    md += `## ${tag}\n\n`;

    endpoints.forEach(({ path, method, operation }) => {
      md += `### ${method} \`${path}\`\n\n`;

      if (operation.summary) {
        md += `**Summary**: ${operation.summary}\n\n`;
      }

      if (operation.description) {
        md += `**Description**: ${operation.description}\n\n`;
      }

      // Parameters
      if (operation.parameters && operation.parameters.length > 0) {
        md += '**Parameters**:\n\n';
        md += '| Name | In | Type | Required | Description |\n';
        md += '|------|----|----- |----------|-------------|\n';

        operation.parameters.forEach(param => {
          md += `| \`${param.name}\` | ${param.in} | ${param.schema?.type || 'N/A'} | ${param.required ? 'Yes' : 'No'} | ${param.description || ''} |\n`;
        });

        md += '\n';
      }

      // Request body
      if (operation.requestBody) {
        md += '**Request Body**:\n\n';
        const contentTypes = Object.keys(operation.requestBody.content);

        contentTypes.forEach(contentType => {
          md += `Content-Type: \`${contentType}\`\n\n`;
          const schema = operation.requestBody.content[contentType].schema;

          if (schema.$ref) {
            const refName = schema.$ref.split('/').pop();
            md += `Schema: [\`${refName}\`](#schema-${refName.toLowerCase()})\n\n`;
          } else {
            md += '```json\n';
            md += JSON.stringify(schema, null, 2);
            md += '\n```\n\n';
          }
        });
      }

      // Responses
      if (operation.responses) {
        md += '**Responses**:\n\n';

        Object.entries(operation.responses).forEach(([code, response]) => {
          md += `- **${code}**: ${response.description || 'No description'}\n`;

          if (response.content) {
            const contentTypes = Object.keys(response.content);
            contentTypes.forEach(contentType => {
              const schema = response.content[contentType].schema;
              if (schema.$ref) {
                const refName = schema.$ref.split('/').pop();
                md += `  - Schema: [\`${refName}\`](#schema-${refName.toLowerCase()})\n`;
              }
            });
          }
        });

        md += '\n';
      }

      // Security
      if (operation.security) {
        md += '**Security**: ';
        const schemes = operation.security.map(s => Object.keys(s).join(', ')).join(' OR ');
        md += `${schemes}\n\n`;
      }

      md += '---\n\n';
    });
  });

  // Schemas
  if (spec.components?.schemas) {
    md += '## Schemas\n\n';

    Object.entries(spec.components.schemas).forEach(([name, schema]) => {
      md += `### ${name}\n\n`;

      if (schema.description) {
        md += `${schema.description}\n\n`;
      }

      if (schema.type === 'object' && schema.properties) {
        md += '**Properties**:\n\n';
        md += '| Name | Type | Required | Description |\n';
        md += '|------|------|----------|-------------|\n';

        const required = schema.required || [];

        Object.entries(schema.properties).forEach(([propName, propSchema]) => {
          const type = propSchema.type || (propSchema.$ref ? propSchema.$ref.split('/').pop() : 'N/A');
          const isRequired = required.includes(propName) ? 'Yes' : 'No';
          const desc = propSchema.description || '';

          md += `| \`${propName}\` | ${type} | ${isRequired} | ${desc} |\n`;
        });

        md += '\n';
      } else {
        md += '```json\n';
        md += JSON.stringify(schema, null, 2);
        md += '\n```\n\n';
      }

      md += '---\n\n';
    });
  }

  return md;
}

async function main() {
  try {
    // Fetch OpenAPI spec
    const spec = await fetchSpec();
    console.log('✓ OpenAPI spec fetched successfully');

    // Save JSON
    fs.writeFileSync(OUTPUT_JSON, JSON.stringify(spec, null, 2), 'utf-8');
    console.log(`✓ Saved JSON spec to: ${path.relative(process.cwd(), OUTPUT_JSON)}`);

    // Generate Markdown
    const markdown = generateMarkdown(spec);
    fs.writeFileSync(OUTPUT_MD, markdown, 'utf-8');
    console.log(`✓ Generated Markdown docs: ${path.relative(process.cwd(), OUTPUT_MD)}`);

    console.log('\n✓ API documentation generated successfully!');
    console.log('\nFiles created:');
    console.log(`  - ${path.relative(process.cwd(), OUTPUT_JSON)}`);
    console.log(`  - ${path.relative(process.cwd(), OUTPUT_MD)}`);

  } catch (err) {
    console.error('✗ Error generating API documentation:');
    console.error(err.message);

    if (err.code === 'ECONNREFUSED') {
      console.error('\nMake sure the API is running in development mode at http://localhost:8080');
      console.error('Run: cd apps/api/src/Api && dotnet run');
    }

    process.exit(1);
  }
}

main();
