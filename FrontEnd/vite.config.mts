import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import tsconfigPaths from 'vite-tsconfig-paths';

export default defineConfig(({ mode }) => {
  // depending on your application, base can also be "/"
  const env = loadEnv(mode, process.cwd(), '');
  const API_URL = env.VITE_APP_BASE_NAME || '/';
  const BACKEND_URL = env.VITE_APP_BACKEND_URL || 'http://localhost:5121';
  // Dedicated EMG frontend port. Keeping it explicit prevents collisions
  // with other local React/Vite applications.
  const PORT = 3100;

  return {
    server: {
      // this ensures that the browser opens upon server start
      open: true,
      // this sets a dedicated default port for EMG
      port: PORT,
      host: true,
      proxy: {
        '/api': { target: BACKEND_URL, changeOrigin: true, secure: false }
      }
    },
    build: {
      // maplibre-gl is a single unavoidable dependency of about 991 KiB.
      chunkSizeWarningLimit: 1100,
      sourcemap: true,
      // Vite-specific optimizations
      cssCodeSplit: true,
      // esbuild options for production
      ...(mode === 'production' && {
        esbuild: {
          drop: ['console', 'debugger'],
          pure: ['console.log', 'console.info', 'console.debug', 'console.warn']
        }
      }),
      rollupOptions: {
        output: {
          // Split only the heaviest isolated libraries. Keeping React and the
          // general dependency graph together avoids circular vendor chunks.
          manualChunks(id) {
            if (id.includes('/node_modules/maplibre-gl/')) return 'vendor-maplibre';
            if (id.includes('/node_modules/@mui/x-data-grid')) return 'vendor-data-grid';
            if (id.includes('/node_modules/@mui/material/')) return 'vendor-mui-material';
            if (id.includes('/node_modules/@mui/icons-material/')) return 'vendor-mui-icons';
            if (id.includes('/node_modules/@fullcalendar/')) return 'vendor-calendar';
            if (id.includes('/node_modules/apexcharts/') || id.includes('/node_modules/react-apexcharts/')) return 'vendor-charts';
            if (id.includes('/node_modules/react/') || id.includes('/node_modules/react-dom/') || id.includes('/node_modules/react-router/')) return 'vendor-react';
            return undefined;
          }
        }
      }
    },
    preview: {
      open: true,
      host: true
    },
    define: {
      global: 'globalThis'
    },
    resolve: {
      alias: {
        // { find: '', replacement: path.resolve(__dirname, 'src') },
        // {
        //   find: /^~(.+)/,
        //   replacement: path.join(process.cwd(), 'node_modules/$1')
        // },
        // {
        //   find: /^src(.+)/,
        //   replacement: path.join(process.cwd(), 'src/$1')
        // }
        // {
        //   find: 'assets',
        //   replacement: path.join(process.cwd(), 'src/assets')
        // },
        '@tabler/icons-react': '@tabler/icons-react/dist/esm/icons/index.mjs'
      }
    },
    base: API_URL,
    plugins: [react(), tsconfigPaths()],
    optimizeDeps: {
      include: ['@mui/material/Tooltip', 'react', 'react-dom', 'react-router-dom']
    }
  };
});
