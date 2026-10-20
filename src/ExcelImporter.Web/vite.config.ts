/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

export default defineConfig({
  plugins: [react()],
  server: {
    // Adresa API-ja se podešava preko VITE_API_PROXY_TARGET; ovde je samo lokalni podrazumevani port.
    proxy: {
      '/api': process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5000',
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: { modules: { classNameStrategy: 'non-scoped' } },
  },
});
