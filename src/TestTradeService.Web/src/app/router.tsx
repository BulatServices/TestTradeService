import { lazy, Suspense } from 'react';
import { Navigate, Route, Routes } from 'react-router-dom';
import { Spin } from 'antd';
import { AppLayout } from './layout/AppLayout';

const SourcesPage = lazy(() => import('../pages/sources/SourcesPage').then((module) => ({ default: module.SourcesPage })));
const StreamPage = lazy(() => import('../pages/stream/StreamPage').then((module) => ({ default: module.StreamPage })));
const ProcessedPage = lazy(() => import('../pages/processed/ProcessedPage').then((module) => ({ default: module.ProcessedPage })));
const MonitoringPage = lazy(() => import('../pages/monitoring/MonitoringPage').then((module) => ({ default: module.MonitoringPage })));

export function AppRouter() {
  return (
    <AppLayout>
      <Suspense fallback={<Spin size="large" style={{ marginTop: 48 }} />}>
        <Routes>
          <Route path="/sources" element={<SourcesPage />} />
          <Route path="/stream" element={<StreamPage />} />
          <Route path="/processed" element={<ProcessedPage />} />
          <Route path="/monitoring" element={<MonitoringPage />} />
          <Route path="*" element={<Navigate to="/sources" replace />} />
        </Routes>
      </Suspense>
    </AppLayout>
  );
}
