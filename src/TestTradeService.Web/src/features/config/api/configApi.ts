import { sourceConfigSchema, SourceConfigDto } from '../../../entities/config/model/types';
import { requestJson } from '../../../shared/api/httpClient';

export function getSourceConfig(): Promise<SourceConfigDto> {
  return requestJson('/api/v1/config/sources', sourceConfigSchema);
}

export function putSourceConfig(payload: SourceConfigDto): Promise<SourceConfigDto> {
  return requestJson('/api/v1/config/sources', sourceConfigSchema, {
    method: 'PUT',
    body: JSON.stringify(payload)
  });
}

