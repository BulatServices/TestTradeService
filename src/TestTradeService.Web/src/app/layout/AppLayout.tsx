import { Layout, Menu, Typography } from 'antd';
import { Link, useLocation } from 'react-router-dom';
import { PropsWithChildren } from 'react';
import { ConnectionBadge } from '../../shared/ui/ConnectionBadge';

const items = [
  { key: '/sources', label: <Link to="/sources">Настройка источников и тикеров</Link> },
  { key: '/stream', label: <Link to="/stream">Поток данных</Link> },
  { key: '/processed', label: <Link to="/processed">Обработанные данные</Link> },
  { key: '/monitoring', label: <Link to="/monitoring">Мониторинг и алертинг</Link> }
];

export function AppLayout({ children }: PropsWithChildren) {
  const location = useLocation();

  return (
    <Layout className="app-shell">
      <Layout.Sider width={320} breakpoint="lg" collapsedWidth={0} theme="light">
        <div className="logo">Панель TestTradeService</div>
        <Menu mode="inline" selectedKeys={[location.pathname]} items={items} />
      </Layout.Sider>
      <Layout>
        <Layout.Header className="app-header">
          <Typography.Title level={4} style={{ margin: 0 }}>
            Проверка работоспособности торгового сервиса
          </Typography.Title>
          <ConnectionBadge />
        </Layout.Header>
        <Layout.Content className="app-content">{children}</Layout.Content>
      </Layout>
    </Layout>
  );
}

