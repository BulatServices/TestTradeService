import {
  candlesResponseSchema,
  CandlesResponseDto,
  metricsResponseSchema,
  MetricsResponseDto
} from '../../../entities/processed/model/types';
import { requestJson } from '../../../shared/api/httpClient';

interface ProcessedQuery extends Record<string, string | number | undefined> {
  exchange?: string;
  symbol?: string;
  window?: string;
  dateFrom?: string;
  dateTo?: string;
  page?: number;
  pageSize?: number;
}

export function getCandles(params: ProcessedQuery): Promise<CandlesResponseDto> {
  return requestJson('/api/v1/processed/candles', candlesResponseSchema, {
    query: params
  });
}

export function getMetrics(params: ProcessedQuery): Promise<MetricsResponseDto> {
  return requestJson('/api/v1/processed/metrics', metricsResponseSchema, {
    query: params
  });
}

