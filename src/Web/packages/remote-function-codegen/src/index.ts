#!/usr/bin/env node
import { readFileSync, existsSync } from 'fs';
import { defaultConfig } from './config.js';

async function main() {
  console.log('Remote Function Generator');
  console.log('=========================\n');

  // Verify OpenAPI spec exists
  if (!existsSync(defaultConfig.openApiPath)) {
    console.error(`OpenAPI spec not found: ${defaultConfig.openApiPath}`);
    console.error('Run "aspire run" first to generate the OpenAPI spec.');
    process.exit(1);
  }

  const spec = JSON.parse(readFileSync(defaultConfig.openApiPath, 'utf-8'));
  console.log(`Loaded OpenAPI spec: ${spec.info.title} v${spec.info.version}`);

  // Count operations with remote annotations
  let queryCount = 0;
  let commandCount = 0;

  for (const [path, methods] of Object.entries(spec.paths)) {
    for (const [method, operation] of Object.entries(methods as Record<string, any>)) {
      if (operation['x-remote-type'] === 'query') queryCount++;
      if (operation['x-remote-type'] === 'command') commandCount++;
    }
  }

  console.log(`Found ${queryCount} queries and ${commandCount} commands with remote annotations.`);
  console.log('\nGenerator scaffold ready. Implementation coming next.');
}

main().catch(console.error);
