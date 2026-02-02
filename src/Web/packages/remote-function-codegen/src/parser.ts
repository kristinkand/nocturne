import type { OpenAPIV3 } from 'openapi-types';
import type { OperationInfo, ParsedSpec, ParameterInfo, RemoteType } from './types.js';

export function parseOpenApiSpec(spec: OpenAPIV3.Document): ParsedSpec {
  const operations: OperationInfo[] = [];
  const tagsSet = new Set<string>();
  const schemas = new Map<string, string>();

  if (spec.components?.schemas) {
    for (const schemaName of Object.keys(spec.components.schemas)) {
      schemas.set(schemaName, `${schemaName}Schema`);
    }
  }

  for (const [path, pathItem] of Object.entries(spec.paths ?? {})) {
    if (!pathItem) continue;

    const methods = ['get', 'post', 'put', 'patch', 'delete'] as const;

    for (const method of methods) {
      const operation = pathItem[method] as OpenAPIV3.OperationObject | undefined;
      if (!operation) continue;

      const remoteType = operation['x-remote-type'] as RemoteType | undefined;
      if (!remoteType) continue;

      const tag = operation.tags?.[0] ?? 'Default';
      tagsSet.add(tag);

      const invalidates = (operation['x-remote-invalidates'] as string[]) ?? [];

      const parameters = parseParameters(operation.parameters ?? [], pathItem.parameters ?? []);
      const requestBodySchema = parseRequestBody(operation.requestBody as OpenAPIV3.RequestBodyObject | undefined);
      const responseSchema = parseResponse(operation.responses?.['200'] as OpenAPIV3.ResponseObject | undefined);

      operations.push({
        operationId: operation.operationId ?? `${method}_${path}`,
        tag,
        method,
        path,
        remoteType,
        invalidates,
        parameters,
        requestBodySchema,
        responseSchema,
        summary: operation.summary,
      });
    }
  }

  return {
    operations,
    tags: Array.from(tagsSet),
    schemas,
  };
}

function parseParameters(
  opParams: (OpenAPIV3.ParameterObject | OpenAPIV3.ReferenceObject)[],
  pathParams: (OpenAPIV3.ParameterObject | OpenAPIV3.ReferenceObject)[]
): ParameterInfo[] {
  const allParams = [...pathParams, ...opParams];
  const result: ParameterInfo[] = [];

  for (const param of allParams) {
    if ('$ref' in param) continue;

    result.push({
      name: param.name,
      in: param.in as 'query' | 'path' | 'header',
      required: param.required ?? false,
      type: getSchemaType(param.schema as OpenAPIV3.SchemaObject | undefined),
    });
  }

  return result;
}

function parseRequestBody(body: OpenAPIV3.RequestBodyObject | undefined): string | undefined {
  if (!body?.content?.['application/json']?.schema) return undefined;

  const schema = body.content['application/json'].schema;
  if ('$ref' in schema) {
    const refName = schema.$ref.split('/').pop();
    return refName ? `${refName}Schema` : undefined;
  }

  return undefined;
}

function parseResponse(response: OpenAPIV3.ResponseObject | undefined): string | undefined {
  if (!response?.content?.['application/json']?.schema) return undefined;

  const schema = response.content['application/json'].schema;
  if ('$ref' in schema) {
    const refName = schema.$ref.split('/').pop();
    return refName;
  }

  return undefined;
}

function getSchemaType(schema: OpenAPIV3.SchemaObject | undefined): string {
  if (!schema) return 'unknown';

  if (schema.type === 'string') {
    if (schema.format === 'uuid') return 'string';
    if (schema.format === 'date-time') return 'Date';
    return 'string';
  }
  if (schema.type === 'integer' || schema.type === 'number') return 'number';
  if (schema.type === 'boolean') return 'boolean';
  if (schema.type === 'array') return 'array';

  return 'unknown';
}
