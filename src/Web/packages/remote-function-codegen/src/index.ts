#!/usr/bin/env node
import { readFileSync, existsSync } from 'fs';
import { defaultConfig } from './config.js';
import { parseOpenApiSpec } from './parser.js';

async function main() {
  console.log('Remote Function Generator');
  console.log('=========================\n');

  if (!existsSync(defaultConfig.openApiPath)) {
    console.error(`OpenAPI spec not found: ${defaultConfig.openApiPath}`);
    console.error('Run "aspire run" first to generate the OpenAPI spec.');
    process.exit(1);
  }

  const spec = JSON.parse(readFileSync(defaultConfig.openApiPath, 'utf-8'));
  console.log(`Loaded OpenAPI spec: ${spec.info.title} v${spec.info.version}`);

  const parsed = parseOpenApiSpec(spec);

  console.log(`\nFound ${parsed.operations.length} annotated operations across ${parsed.tags.length} tags:`);

  const byTag = new Map<string, typeof parsed.operations>();
  for (const op of parsed.operations) {
    const existing = byTag.get(op.tag) ?? [];
    existing.push(op);
    byTag.set(op.tag, existing);
  }

  for (const [tag, ops] of byTag) {
    const queries = ops.filter(o => o.remoteType === 'query').length;
    const commands = ops.filter(o => o.remoteType === 'command').length;
    console.log(`  - ${tag}: ${queries} queries, ${commands} commands`);
  }

  console.log('\nParser ready. Code generation coming next.');
}

main().catch(console.error);
