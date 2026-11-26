import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import tsconfigPaths from 'vite-tsconfig-paths';
import { readFileSync } from 'node:fs';

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
      chunkSizeWarningLimit: 900,
      sourcemap: mode !== 'production',
      cssCodeSplit: true,
      ...(mode === 'production' && {
        esbuild: {
          drop: ['console', 'debugger'],
          pure: ['console.log', 'console.info', 'console.debug', 'console.warn']
        }
      }),
      rollupOptions: {
        output: {
          manualChunks(id) {
            if (id.includes('/node_modules/@mui/material/')) return 'vendor-mui-material';
            if (id.includes('/node_modules/@mui/icons-material/')) return 'vendor-mui-icons';
            if (id.includes('/node_modules/framer-motion/')) return 'vendor-motion';
            if (id.includes('/node_modules/@tabler/icons-react/')) return 'vendor-tabler-icons';
            if (id.includes('/node_modules/react/') || id.includes('/node_modules/react-dom/') || id.includes('/node_modules/react-router')) {
              return 'vendor-react';
            }
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
    plugins: [
      react(),
      tsconfigPaths(),
      {
        name: 'emg-pwa-icons',
        apply: 'build',
        generateBundle() {
          for (const fileName of ['emg-logo.png', 'emg-logo-3d.png']) {
            this.emitFile({
              type: 'asset',
              fileName,
              source: readFileSync(new URL(`./src/assets/images/${fileName}`, import.meta.url))
            });
          }
        }
      }
    ],
    optimizeDeps: {
      include: ['@mui/material/Tooltip', 'react', 'react-dom', 'react-router-dom']
    }
  };
});
