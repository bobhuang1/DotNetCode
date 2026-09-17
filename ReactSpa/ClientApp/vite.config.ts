import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// In dev the SPA is served by the Vite dev server (http://localhost:5173) and
// API calls are proxied to the ASP.NET Core host. Override with e.g.
//   $env:VITE_API_PROXY_TARGET = "http://localhost:5080"
const apiTarget = process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:5080'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
      },
    },
  },
})