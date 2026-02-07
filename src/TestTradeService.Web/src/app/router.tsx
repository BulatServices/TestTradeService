import { Navigate, Route, Routes } from 'react-router-dom';
import { AppLayout } from './layout/AppLayout';
import { SourcesPage } from '../pages/sources/SourcesPage';
import { StreamPage } from '../pages/stream/StreamPage';
import { ProcessedPage } from '../pages/processed/ProcessedPage';
import { MonitoringPage } from '../pages/monitoring/MonitoringPage';

export function AppRouter() {
  return (
    <AppLayout>
      <Routes>
        <Route path="/sources" element={<SourcesPage />} />
        <Route path="/stream" element={<StreamPage />} />
        <Route path="/processed" element={<ProcessedPage />} />
        <Route path="/monitoring" element={<MonitoringPage />} />
        <Route path="*" element={<Navigate to="/sources" replace />} />
      </Routes>
    </AppLayout>
  );
}

