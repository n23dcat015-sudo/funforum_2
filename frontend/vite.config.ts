import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // Proxy /api sang backend ASP.NET Core khi chạy dev, để frontend và API
    // cùng origin (bắt buộc cho cookie refresh token HttpOnly — xem
    // current_state.md mục 5). Đổi target nếu backend chạy cổng khác.
    proxy: {
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
      '/uploads': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
})
