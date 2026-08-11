export default defineNuxtConfig({
  ssr: false,
  components: [
    { path: '~/components', pathPrefix: false }
  ],
  devtools: { enabled: false },
  vue: {
    compilerOptions: {
      isCustomElement: tag => tag.startsWith('md-')
    }
  },
  css: ['~/assets/main.css'],
  runtimeConfig: {
    public: {
      remoteOrigin: process.env.NUXT_PUBLIC_REMOTE_ORIGIN ?? ''
    }
  },
  app: {
    head: {
      title: 'GrassiMote',
      link: [
        { rel: 'manifest', href: '/manifest.webmanifest' },
        { rel: 'icon', type: 'image/png', sizes: '192x192', href: '/icons/grassimote-192.png' },
        { rel: 'apple-touch-icon', href: '/icons/grassimote-192.png' }
      ],
      meta: [
        { name: 'viewport', content: 'width=device-width, initial-scale=1, viewport-fit=cover' },
        { name: 'theme-color', content: '#07111f' },
        { name: 'color-scheme', content: 'dark' },
        { name: 'application-name', content: 'GrassiMote' },
        { name: 'apple-mobile-web-app-capable', content: 'yes' },
        { name: 'apple-mobile-web-app-status-bar-style', content: 'black-translucent' }
      ]
    }
  }
})
