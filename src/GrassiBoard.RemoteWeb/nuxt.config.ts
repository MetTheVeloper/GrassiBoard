export default defineNuxtConfig({
  ssr: false,
  devtools: { enabled: false },
  css: ['~/assets/main.css'],
  runtimeConfig: {
    public: {
      remoteOrigin: process.env.NUXT_PUBLIC_REMOTE_ORIGIN ?? ''
    }
  },
  app: {
    head: {
      title: 'GrassiBoard Remote',
      meta: [
        { name: 'viewport', content: 'width=device-width, initial-scale=1, viewport-fit=cover' },
        { name: 'theme-color', content: '#07111f' },
        { name: 'color-scheme', content: 'dark' }
      ]
    }
  }
})
