import react from '@vitejs/plugin-react'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { defineConfig, loadEnv } from 'vite'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const httpsEnabled = env.VITE_DEV_HTTPS === 'true'
  const certificatePath = resolve(process.cwd(), env.VITE_DEV_CERTIFICATE ?? '.certs/localhost.pfx')
  const certificatePassword = env.VITE_DEV_CERTIFICATE_PASSWORD

  return {
    plugins: [react()],
    server: {
      host: 'localhost',
      port: Number(env.VITE_DEV_PORT ?? 5173),
      strictPort: true,
      ...(httpsEnabled
        ? {
            https: {
              pfx: readFileSync(certificatePath),
              passphrase: certificatePassword,
            },
          }
        : {}),
    },
  }
})
