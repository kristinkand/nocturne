
import { sveltekit } from '@sveltejs/kit/vite';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vite';
import fs from 'fs';


export default defineConfig({
  plugins: [tailwindcss(), sveltekit()],
  server: {
    https: process.env.SSL_CRT_FILE && process.env.SSL_KEY_FILE ? {
      cert: fs.readFileSync(process.env.SSL_CRT_FILE),
      key: fs.readFileSync(process.env.SSL_KEY_FILE),
    } : undefined,
    host: "0.0.0.0",
    port: parseInt(process.env.PORT || "5173", 10),
    strictPort: true,
    proxy: {
      '/api': {
        target: process.env.VITE_PORTAL_API_URL,
        secure: false,
        changeOrigin: true
      }
    }
  },
  ssr: {
    noExternal: ['@nocturne/app', 'lucide-svelte']
  }
});
