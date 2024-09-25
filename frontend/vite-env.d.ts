/// <reference types="vite/client" />
/// <reference types="vite/types/importMeta.d.ts" />

interface ImportMetaEnv {
	readonly VITE_BACKEND_URL: string
	readonly VITE_APP_VERSION: string
	readonly VITE_TELEGRAM_URL?: string
	readonly VITE_IMPRINT_URL?: string
	readonly VITE_PRIVACY_URL?: string
  }
  
  interface ImportMeta {
	readonly env: ImportMetaEnv
  }