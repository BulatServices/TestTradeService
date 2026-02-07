import {
  AlertRulesResponseDto,
  alertRulesResponseSchema,
  AlertsResponseDto,
  alertsResponseSchema,
  AlertRuleConfigDto
} from '../../../entities/alerts/model/types';
import { requestJson } from '../../../shared/api/httpClient';

interface AlertsQuery extends Record<string, string | undefined> {
  rule?: string;
  source?: string;
  symbol?: string;
  dateFrom?: string;
  dateTo?: string;
}

export function getAlerts(params: AlertsQuery): Promise<AlertsResponseDto> {
  return requestJson('/api/v1/alerts', alertsResponseSchema, {
    query: params
  });
}

export function getAlertRules(): Promise<AlertRulesResponseDto> {
  return requestJson('/api/v1/alerts/rules', alertRulesResponseSchema);
}

export function putAlertRules(payload: { items: AlertRuleConfigDto[] }): Promise<AlertRulesResponseDto> {
  return requestJson('/api/v1/alerts/rules', alertRulesResponseSchema, {
    method: 'PUT',
    body: JSON.stringify(payload)
  });
}

