import { useEffect, useState } from 'react';
import {
  Button,
  Card,
  Col,
  Form,
  Input,
  Row,
  Select,
  Space,
  Statistic,
  Switch,
  Table,
  Tag,
  Typography,
  message
} from 'antd';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getMonitoringSnapshot } from '../../features/monitoring/api/monitoringApi';
import { getAlertRules, getAlerts, putAlertRules } from '../../features/alerts/api/alertsApi';
import { getSourceConfig } from '../../features/config/api/configApi';
import { formatDateTime, formatNumber } from '../../shared/lib/format';
import { AlertRuleConfigDto } from '../../entities/alerts/model/types';
import { SourceConfigDto } from '../../entities/config/model/types';
import { SourceStatsDto } from '../../entities/monitoring/model/types';

function getStatusColor(status: SourceStatsDto['status']): 'green' | 'orange' | 'red' {
  switch (status) {
    case 'Ok':
      return 'green';
    case 'Warn':
      return 'orange';
    default:
      return 'red';
  }
}

const availableNotifierChannels = ['Console', 'File', 'EmailStub'] as const;
const channelsParameterKey = 'channels';
const priceThresholdRuleName = 'PriceThreshold';

function buildRuleScopeKey(rule: Pick<AlertRuleConfigDto, 'ruleName' | 'exchange' | 'symbol'>): string {
  return `${rule.ruleName}::${rule.exchange ?? '*'}::${rule.symbol ?? '*'}`;
}

function getAvailableScopes(config: SourceConfigDto | undefined): Array<{ exchange: string; symbol: string }> {
  if (!config) {
    return [];
  }

  const seen = new Set<string>();
  const scopes: Array<{ exchange: string; symbol: string }> = [];

  for (const profile of config.profiles) {
    for (const symbol of profile.symbols) {
      const key = `${profile.exchange}::${symbol}`;
      if (seen.has(key)) {
        continue;
      }

      seen.add(key);
      scopes.push({ exchange: profile.exchange, symbol });
    }
  }

  return scopes;
}

function normalizeRulesByTicker(items: AlertRuleConfigDto[], config: SourceConfigDto | undefined): AlertRuleConfigDto[] {
  const availableScopes = getAvailableScopes(config);
  const scopedPriceRules = items.filter((item) => item.ruleName === priceThresholdRuleName && item.exchange && item.symbol);
  const legacyPriceTemplate = items.find((item) => item.ruleName === priceThresholdRuleName && (!item.exchange || !item.symbol));

  const expandedLegacyPriceRules =
    legacyPriceTemplate && availableScopes.length > 0
      ? availableScopes.map((scope) => ({
          ...legacyPriceTemplate,
          exchange: scope.exchange,
          symbol: scope.symbol,
          parameters: { ...legacyPriceTemplate.parameters }
        }))
      : [];

  const map = new Map<string, AlertRuleConfigDto>();

  for (const item of [...scopedPriceRules, ...expandedLegacyPriceRules]) {
    map.set(buildRuleScopeKey(item), item);
  }

  const nonPriceRules = items.filter((item) => item.ruleName !== priceThresholdRuleName);
  const normalizedItems = [...nonPriceRules, ...map.values()];

  return normalizedItems.sort((a, b) => {
    const aKey = buildRuleScopeKey(a);
    const bKey = buildRuleScopeKey(b);
    return aKey.localeCompare(bKey);
  });
}

function parseChannels(rawValue: string | undefined): string[] {
  if (!rawValue) {
    return [];
  }

  return Array.from(
    new Set(
      rawValue
        .split(',')
        .map((value) => value.trim())
        .filter((value) => value.length > 0)
    )
  );
}

function toChannelsCsv(channels: string[]): string {
  return Array.from(
    new Set(
      channels
        .map((value) => value.trim())
        .filter((value) => value.length > 0)
    )
  ).join(',');
}

export function MonitoringPage() {
  const queryClient = useQueryClient();
  const [ruleFilter, setRuleFilter] = useState<string>();
  const [sourceFilter, setSourceFilter] = useState<string>();
  const [symbolFilter, setSymbolFilter] = useState<string>();
  const [editingRules, setEditingRules] = useState<AlertRuleConfigDto[]>([]);
  const [globalChannels, setGlobalChannels] = useState<string[]>([]);

  const snapshotQuery = useQuery({
    queryKey: ['monitoring-snapshot'],
    queryFn: getMonitoringSnapshot,
    refetchInterval: 10000
  });

  const alertsQuery = useQuery({
    queryKey: ['alerts', ruleFilter, sourceFilter, symbolFilter],
    queryFn: () =>
      getAlerts({
        rule: ruleFilter,
        source: sourceFilter,
        symbol: symbolFilter
      })
  });

  const rulesQuery = useQuery({
    queryKey: ['alert-rules'],
    queryFn: getAlertRules
  });

  const sourceConfigQuery = useQuery({
    queryKey: ['source-config'],
    queryFn: getSourceConfig
  });

  useEffect(() => {
    if (rulesQuery.data) {
      setEditingRules(normalizeRulesByTicker(rulesQuery.data.items, sourceConfigQuery.data));
      setGlobalChannels(rulesQuery.data.globalChannels);
    }
  }, [rulesQuery.data, sourceConfigQuery.data]);

  const saveRulesMutation = useMutation({
    mutationFn: () => putAlertRules({ items: editingRules, globalChannels }),
    onSuccess: async () => {
      message.success('Правила алертинга сохранены');
      await queryClient.invalidateQueries({ queryKey: ['alert-rules'] });
    },
    onError: () => {
      message.error('Не удалось сохранить правила алертинга');
    }
  });

  const sourceStatsRows = snapshotQuery.data ? Object.values(snapshotQuery.data.sourceStats) : [];
  const exchangeStatsRows = snapshotQuery.data ? Object.values(snapshotQuery.data.exchangeStats) : [];
  const performanceReport = snapshotQuery.data?.performanceReport;

  const onlineCount = sourceStatsRows.filter((item) => Date.now() - new Date(item.lastTickTime).getTime() < 120_000).length;

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Typography.Title level={3}>Мониторинг и алертинг</Typography.Title>

      <Row gutter={16}>
        <Col xs={24} md={6}>
          <Card loading={snapshotQuery.isLoading}>
            <Statistic
              title="Средняя задержка, мс"
              value={
                sourceStatsRows.length
                  ? formatNumber(
                      sourceStatsRows.reduce((sum, item) => sum + item.averageDelayMs, 0) /
                        sourceStatsRows.length,
                      0
                    )
                  : '0'
              }
            />
          </Card>
        </Col>
        <Col xs={24} md={6}>
          <Card loading={snapshotQuery.isLoading}>
            <Statistic title="Предупреждений" value={snapshotQuery.data?.warnings.length ?? 0} />
          </Card>
        </Col>
        <Col xs={24} md={6}>
          <Card loading={snapshotQuery.isLoading}>
            <Statistic title="Онлайн источников" value={onlineCount} />
          </Card>
        </Col>
        <Col xs={24} md={6}>
          <Card loading={alertsQuery.isLoading}>
            <Statistic title="Алертов за период" value={alertsQuery.data?.items.length ?? 0} />
          </Card>
        </Col>
      </Row>

      <Card title={`Отчет о производительности (${performanceReport?.windowMinutes ?? 5} минут)`} className="section-card" loading={snapshotQuery.isLoading}>
        <Row gutter={16}>
          <Col xs={24} md={6}>
            <Statistic title="Tick/s (5m)" value={formatNumber(performanceReport?.totalWindowTickRatePerSec ?? 0, 2)} />
          </Col>
          <Col xs={24} md={6}>
            <Statistic title="Aggregate/s (5m)" value={formatNumber(performanceReport?.totalWindowAggregateRatePerSec ?? 0, 2)} />
          </Col>
          <Col xs={24} md={6}>
            <Statistic title="Avg delay (5m), мс" value={formatNumber(performanceReport?.totalWindowAvgDelayMs ?? 0, 0)} />
          </Col>
          <Col xs={24} md={6}>
            <Statistic title="Max delay (5m), мс" value={formatNumber(performanceReport?.totalWindowMaxDelayMs ?? 0, 0)} />
          </Col>
        </Row>
        <Space style={{ marginTop: 16 }} wrap>
          <Tag color="green">OK: {performanceReport?.sourcesOk ?? 0}</Tag>
          <Tag color="orange">Warn: {performanceReport?.sourcesWarn ?? 0}</Tag>
          <Tag color="red">Critical: {performanceReport?.sourcesCritical ?? 0}</Tag>
          <Tag>Ticks (5m): {performanceReport?.totalWindowTickCount ?? 0}</Tag>
          <Tag>Aggregates (5m): {performanceReport?.totalWindowAggregateCount ?? 0}</Tag>
        </Space>
      </Card>

      <Card title="Статистика по источникам" className="section-card" loading={snapshotQuery.isLoading}>
        <Table
          rowKey={(row) => row.source}
          dataSource={sourceStatsRows}
          pagination={false}
          columns={[
            { title: 'Источник', dataIndex: 'source' },
            {
              title: 'Статус',
              dataIndex: 'status',
              render: (value: SourceStatsDto['status']) => <Tag color={getStatusColor(value)}>{value}</Tag>
            },
            { title: 'Тики', dataIndex: 'tickCount' },
            { title: 'Агрегаты', dataIndex: 'aggregateCount' },
            { title: 'Средняя задержка, мс', dataIndex: 'averageDelayMs', render: (v) => formatNumber(v, 0) },
            { title: 'Возраст последнего тика, мс', dataIndex: 'lastTickAgeMs', render: (v) => formatNumber(v, 0) },
            { title: 'Tick/s (5m)', dataIndex: 'windowTickRatePerSec', render: (v) => formatNumber(v, 2) },
            { title: 'Agg/s (5m)', dataIndex: 'windowAggregateRatePerSec', render: (v) => formatNumber(v, 2) },
            { title: 'Avg delay (5m), мс', dataIndex: 'windowAvgDelayMs', render: (v) => formatNumber(v, 0) },
            { title: 'Max delay (5m), мс', dataIndex: 'windowMaxDelayMs', render: (v) => formatNumber(v, 0) },
            { title: 'Последний тик', dataIndex: 'lastTickTime', render: (v) => formatDateTime(v) }
          ]}
        />
      </Card>

      <Card title="Статистика по биржам" className="section-card" loading={snapshotQuery.isLoading}>
        <Table
          rowKey={(row) => row.exchange}
          dataSource={exchangeStatsRows}
          pagination={false}
          columns={[
            { title: 'Биржа', dataIndex: 'exchange' },
            { title: 'Тики', dataIndex: 'tickCount' },
            { title: 'Агрегаты', dataIndex: 'aggregateCount' },
            { title: 'Средняя задержка, мс', dataIndex: 'averageDelayMs', render: (v) => formatNumber(v, 0) },
            { title: 'Tick/s (5m)', dataIndex: 'windowTickRatePerSec', render: (v) => formatNumber(v, 2) },
            { title: 'Agg/s (5m)', dataIndex: 'windowAggregateRatePerSec', render: (v) => formatNumber(v, 2) },
            { title: 'Avg delay (5m), мс', dataIndex: 'windowAvgDelayMs', render: (v) => formatNumber(v, 0) },
            { title: 'Max delay (5m), мс', dataIndex: 'windowMaxDelayMs', render: (v) => formatNumber(v, 0) },
            { title: 'Последний тик', dataIndex: 'lastTickTime', render: (v) => formatDateTime(v) }
          ]}
        />
      </Card>

      <Card title="Предупреждения" className="section-card" loading={snapshotQuery.isLoading}>
        <Space wrap>
          {(snapshotQuery.data?.warnings ?? []).map((warning) => (
            <Tag color="orange" key={warning}>
              {warning}
            </Tag>
          ))}
          {(snapshotQuery.data?.warnings.length ?? 0) === 0 && <Typography.Text>Предупреждений нет</Typography.Text>}
        </Space>
      </Card>

      <Card title="Лента алертов" className="section-card" loading={alertsQuery.isLoading}>
        <Space wrap style={{ marginBottom: 16 }}>
          <Select
            placeholder="Правило"
            allowClear
            style={{ width: 180 }}
            value={ruleFilter}
            onChange={setRuleFilter}
            options={Array.from(new Set((alertsQuery.data?.items ?? []).map((item) => item.rule))).map((value) => ({
              value
            }))}
          />
          <Input
            placeholder="Источник"
            value={sourceFilter}
            onChange={(event) => setSourceFilter(event.target.value || undefined)}
            style={{ width: 180 }}
          />
          <Input
            placeholder="Тикер"
            value={symbolFilter}
            onChange={(event) => setSymbolFilter(event.target.value || undefined)}
            style={{ width: 180 }}
          />
        </Space>

        <Table
          rowKey={(row) => `${row.rule}-${row.source}-${row.timestamp}`}
          dataSource={alertsQuery.data?.items ?? []}
          pagination={{ pageSize: 10 }}
          columns={[
            { title: 'Правило', dataIndex: 'rule' },
            { title: 'Источник', dataIndex: 'source' },
            { title: 'Тикер', dataIndex: 'symbol' },
            { title: 'Сообщение', dataIndex: 'message' },
            { title: 'Время', dataIndex: 'timestamp', render: (value) => formatDateTime(value) }
          ]}
        />
      </Card>

      <Card title="Управление правилами алертинга" className="section-card" loading={rulesQuery.isLoading}>
        <Form layout="vertical">
          <Form.Item label="Глобальные каналы уведомлений">
            <Select
              mode="multiple"
              value={globalChannels}
              onChange={setGlobalChannels}
              options={availableNotifierChannels.map((channel) => ({ value: channel, label: channel }))}
              placeholder="Выберите каналы по умолчанию"
              style={{ width: 360, maxWidth: '100%' }}
            />
          </Form.Item>

          <Table
            rowKey={(row) => buildRuleScopeKey(row)}
            dataSource={editingRules}
            pagination={false}
            columns={[
              { title: 'Правило', dataIndex: 'ruleName' },
              { title: 'Биржа', dataIndex: 'exchange', render: (value) => value ?? '-' },
              { title: 'Тикер', dataIndex: 'symbol', render: (value) => value ?? '-' },
              {
                title: 'Включено',
                dataIndex: 'enabled',
                render: (value, row) => (
                  <Switch
                    checked={value}
                    onChange={(checked) => {
                      setEditingRules((prev) =>
                        prev.map((rule) =>
                          buildRuleScopeKey(rule) === buildRuleScopeKey(row)
                            ? {
                                ...rule,
                                enabled: checked
                              }
                            : rule
                        )
                      );
                    }}
                  />
                )
              },
              {
                title: 'Каналы',
                dataIndex: 'parameters',
                render: (value, row) => (
                  <Select
                    mode="multiple"
                    value={parseChannels(String(value[channelsParameterKey] ?? ''))}
                    onChange={(channels) => {
                      const nextCsv = toChannelsCsv(channels);
                      setEditingRules((prev) =>
                        prev.map((rule) =>
                          buildRuleScopeKey(rule) === buildRuleScopeKey(row)
                            ? {
                                ...rule,
                                parameters: {
                                  ...rule.parameters,
                                  [channelsParameterKey]: nextCsv
                                }
                              }
                            : rule
                        )
                      );
                    }}
                    options={availableNotifierChannels.map((channel) => ({ value: channel, label: channel }))}
                    placeholder="Наследовать глобальные"
                    style={{ width: 280, maxWidth: '100%' }}
                  />
                )
              },
              {
                title: 'Параметры',
                dataIndex: 'parameters',
                render: (value, row) => (
                  <Space direction="vertical" style={{ width: '100%' }}>
                    {Object.entries(value)
                      .filter(([key]) => key !== channelsParameterKey)
                      .map(([key, paramValue]) => (
                      <Input
                        key={`${buildRuleScopeKey(row)}-${key}`}
                        addonBefore={key}
                        value={String(paramValue)}
                        onChange={(event) => {
                          const nextValue = event.target.value;
                          setEditingRules((prev) =>
                            prev.map((rule) =>
                              buildRuleScopeKey(rule) === buildRuleScopeKey(row)
                                ? {
                                    ...rule,
                                    parameters: {
                                      ...rule.parameters,
                                      [key]: nextValue
                                    }
                                  }
                                : rule
                            )
                          );
                        }}
                      />
                    ))}
                  </Space>
                )
              }
            ]}
          />
        </Form>

        <Button type="primary" style={{ marginTop: 16 }} onClick={() => saveRulesMutation.mutate()}>
          Сохранить правила
        </Button>
      </Card>
    </Space>
  );
}
