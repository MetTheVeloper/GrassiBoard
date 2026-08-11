import '@fontsource-variable/material-symbols-rounded/full.css'
import GbIcon from '~/components/ui/GbIcon.vue'

// Material Symbols Rounded is bundled by Vite from the pinned Fontsource package.
// No Google Fonts/CDN request is made at runtime; the generated WOFF2 is served
// by GrassiBoard and then cached by the PWA service worker.
export default defineNuxtPlugin((nuxtApp) => {
  nuxtApp.vueApp.component('GbIcon', GbIcon)
})
