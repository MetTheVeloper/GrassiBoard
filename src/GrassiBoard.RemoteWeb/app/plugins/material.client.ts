import '@material/web/button/filled-button.js'
import '@material/web/button/filled-tonal-button.js'
import '@material/web/button/outlined-button.js'
import '@material/web/button/text-button.js'
import '@material/web/iconbutton/icon-button.js'
import '@material/web/switch/switch.js'
import '@material/web/slider/slider.js'
import '@material/web/chips/chip-set.js'
import '@material/web/chips/assist-chip.js'
import '@material/web/fab/fab.js'

// Keep Material Web registration explicit. Nuxt discovers this .client plugin,
// and defineNuxtPlugin guarantees the module is executed as part of client app
// creation instead of relying on a side-effect-only plugin file.
export default defineNuxtPlugin(() => {})
