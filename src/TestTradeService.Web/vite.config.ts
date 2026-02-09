import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  build: {
    rollupOptions: {
      output: {
        manualChunks(id) {
          if (!id.includes('node_modules')) {
            return;
          }

          const [, modulePath] = id.split('node_modules/');
          const pathParts = modulePath.split('/');
          const packageName = pathParts[0]?.startsWith('@') ? `${pathParts[0]}/${pathParts[1]}` : pathParts[0];

          if (!packageName) {
            return 'vendor';
          }

          if (packageName === 'react' || packageName === 'react-dom' || packageName === 'scheduler') {
            return 'react-vendor';
          }

          if (packageName === 'react-router' || packageName === 'react-router-dom') {
            return 'router-vendor';
          }

          if (packageName === '@tanstack/react-query') {
            return 'query-vendor';
          }

          if (packageName === 'antd') {
            return 'antd-vendor';
          }

          if (packageName === '@ant-design/icons') {
            return 'antd-icons-vendor';
          }

          if (
            packageName.startsWith('@ant-design/') ||
            packageName.startsWith('rc-') ||
            packageName.startsWith('@rc-component/')
          ) {
            return 'antd-ecosystem-vendor';
          }

          if (packageName === 'recharts') {
            return 'charts-vendor';
          }

          if (packageName === '@microsoft/signalr') {
            return 'signalr-vendor';
          }

          if (packageName === 'dayjs') {
            return 'dayjs-vendor';
          }

          return 'vendor';
        }
      }
    }
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: './vitest.setup.ts',
    css: true
  }
});
