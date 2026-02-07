import {
  monitoringSnapshotSchema,
  MonitoringSnapshotDto
} from '../../../entities/monitoring/model/types';
import { requestJson } from '../../../shared/api/httpClient';

export function getMonitoringSnapshot(): Promise<MonitoringSnapshotDto> {
  return requestJson('/api/v1/monitoring/snapshot', monitoringSnapshotSchema);
}

