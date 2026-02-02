export type RemoteType = 'query' | 'command';

export interface ParameterInfo {
  name: string;
  in: 'query' | 'path' | 'header';
  required: boolean;
  type: string;
  schema?: string;
}

export interface OperationInfo {
  operationId: string;
  tag: string;
  method: string;
  path: string;
  remoteType: RemoteType;
  invalidates: string[];
  parameters: ParameterInfo[];
  requestBodySchema?: string;
  responseSchema?: string;
  summary?: string;
}

export interface ParsedSpec {
  operations: OperationInfo[];
  tags: string[];
  schemas: Map<string, string>;
}
