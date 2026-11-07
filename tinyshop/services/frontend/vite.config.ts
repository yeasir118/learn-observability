/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig, type ProxyOptions } from 'vite'

// Where the dev server forwards /api. Default: the gateway's `dotnet run` port (architecture.md §8).
// The app itself never sees this address: it only calls relative /api/... URLs, so the same build
// works behind nginx in the container (TS-015), with no CORS.
const gatewayUrl = process.env.GATEWAY_URL ?? 'http://localhost:5100'

// The path is forwarded unchanged: the gateway owns /api/* (architecture.md Q5).
const proxy: Record<string, ProxyOptions> = {
  '/api': { target: gatewayUrl },
}

export default defineConfig({
  plugins: [react()],
  // npm run dev: http://localhost:5173
  server: { port: 5173, strictPort: true, proxy },
  // npm run preview: serves the production build (dist/), with the same proxy.
  preview: { port: 4173, strictPort: true, proxy },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    // Every test starts with the real fetch and no leftover mocks.
    unstubGlobals: true,
    restoreMocks: true,
  },
})
                                                                    