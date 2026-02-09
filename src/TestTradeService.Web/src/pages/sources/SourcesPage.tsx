import { useEffect, useState } from 'react';
import {
  Button,
  Card,
  Form,
  Input,
  InputNumber,
  Select,
  Space,
  Switch,
  Table,
  Tag,
  Typography,
  message
} from 'antd';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getSourceConfig, putSourceConfig } from '../../features/config/api/configApi';
import { MarketInstrumentProfileDto, SourceConfigDto } from '../../entities/config/model/types';

const exchangeOptions = ['Kraken', 'Coinbase', 'Bybit'];
const marketTypeOptions = ['Spot', 'Perp'];
const transportOptions = ['WebSocket', 'Rest'];

type SourceProfileFormModel = Omit<MarketInstrumentProfileDto, 'symbols'> & {
  symbols: string;
};

function normalizeSymbols(value: string): string[] {
  return value
    .split(',')
    .map((symbol) => symbol.trim())
    .filter(Boolean)
    .filter((symbol, index, array) => array.indexOf(symbol) === index);
}

export function SourcesPage() {
  const queryClient = useQueryClient();
  const [form] = Form.useForm<SourceProfileFormModel>();
  const [editIndex, setEditIndex] = useState<number | null>(null);
  const [localProfiles, setLocalProfiles] = useState<MarketInstrumentProfileDto[]>([]);

  const configQuery = useQuery({
    queryKey: ['sources-config'],
    queryFn: getSourceConfig
  });

  useEffect(() => {
    if (configQuery.data) {
      setLocalProfiles(configQuery.data.profiles);
    }
  }, [configQuery.data]);

  const saveMutation = useMutation({
    mutationFn: (payload: SourceConfigDto) => putSourceConfig(payload),
    onSuccess: async () => {
      message.success('Конфигурация сохранена');
      await queryClient.invalidateQueries({ queryKey: ['sources-config'] });
    },
    onError: () => {
      message.error('Не удалось сохранить конфигурацию');
    }
  });

  const startEdit = (record: MarketInstrumentProfileDto, index: number) => {
    form.setFieldsValue({
      ...record,
      symbols: record.symbols.join(', ')
    });
    setEditIndex(index);
  };

  const saveLocalEdit = async () => {
    const values = await form.validateFields();
    const symbols = normalizeSymbols(values.symbols);

    if (symbols.length === 0) {
      message.error('Укажите минимум один тикер');
      return;
    }

    if ((values.targetUpdateIntervalMs ?? 0) <= 0) {
      message.error('Интервал должен быть больше нуля');
      return;
    }

    if (editIndex === null) {
      return;
    }

    const next = [...localProfiles];
    next[editIndex] = {
      ...values,
      symbols
    } as MarketInstrumentProfileDto;

    setLocalProfiles(next);
    setEditIndex(null);
    form.resetFields();
  };

  const tableData = localProfiles.map((profile, index) => ({ ...profile, key: `${profile.exchange}-${index}` }));

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Typography.Title level={3}>Настройка источников и тикеров</Typography.Title>

      <Card className="section-card" loading={configQuery.isLoading}>
        <Space style={{ marginBottom: 16 }}>
          <Button onClick={() => queryClient.invalidateQueries({ queryKey: ['sources-config'] })}>
            Перезагрузить из сервера
          </Button>
          <Button
            type="primary"
            loading={saveMutation.isPending}
            onClick={() => saveMutation.mutate({ profiles: localProfiles })}
          >
            Сохранить
          </Button>
          <Button
            onClick={() => {
              if (configQuery.data) {
                setLocalProfiles(configQuery.data.profiles);
              }
            }}
          >
            Отменить
          </Button>
        </Space>

        <Table
          dataSource={tableData}
          pagination={false}
          scroll={{ x: 900 }}
          columns={[
            { title: 'Биржа', dataIndex: 'exchange' },
            { title: 'Тип рынка', dataIndex: 'marketType' },
            { title: 'Транспорт', dataIndex: 'transport' },
            {
              title: 'Интервал (мс)',
              dataIndex: 'targetUpdateIntervalMs'
            },
            {
              title: 'Тикеры',
              dataIndex: 'symbols',
              render: (symbols: string[]) => symbols.join(', ')
            },
            {
              title: 'Активен',
              dataIndex: 'isEnabled',
              render: (active: boolean) => (active ? <Tag color="green">Да</Tag> : <Tag color="red">Нет</Tag>)
            },
            {
              title: 'Действие',
              render: (_, record, index) => (
                <Button type="link" onClick={() => startEdit(record as MarketInstrumentProfileDto, index)}>
                  Изменить
                </Button>
              )
            }
          ]}
        />
      </Card>

      <Card title="Редактирование профиля" className="section-card">
        <Form form={form} layout="vertical">
          <Space wrap style={{ width: '100%' }}>
            <Form.Item name="exchange" label="Биржа" rules={[{ required: true, message: 'Выберите биржу' }]}>
              <Select style={{ width: 180 }} options={exchangeOptions.map((value) => ({ value }))} />
            </Form.Item>
            <Form.Item
              name="marketType"
              label="Тип рынка"
              rules={[{ required: true, message: 'Выберите тип рынка' }]}
            >
              <Select style={{ width: 160 }} options={marketTypeOptions.map((value) => ({ value }))} />
            </Form.Item>
            <Form.Item name="transport" label="Транспорт" rules={[{ required: true, message: 'Выберите транспорт' }]}>
              <Select style={{ width: 160 }} options={transportOptions.map((value) => ({ value }))} />
            </Form.Item>
            <Form.Item
              name="targetUpdateIntervalMs"
              label="Интервал обновления (мс)"
              rules={[{ required: true, message: 'Укажите интервал' }]}
            >
              <InputNumber min={1} />
            </Form.Item>
            <Form.Item name="isEnabled" label="Активен" valuePropName="checked">
              <Switch />
            </Form.Item>
          </Space>

          <Form.Item
            name="symbols"
            label="Тикеры (через запятую)"
            rules={[{ required: true, message: 'Укажите тикеры' }]}
          >
            <Input placeholder="BTC-USD, ETH-USD" />
          </Form.Item>

          <Space>
            <Button type="primary" disabled={editIndex === null} onClick={saveLocalEdit}>
              Применить изменения
            </Button>
            <Button
              onClick={() => {
                form.resetFields();
                setEditIndex(null);
              }}
            >
              Очистить форму
            </Button>
          </Space>
        </Form>
      </Card>
    </Space>
  );
}
