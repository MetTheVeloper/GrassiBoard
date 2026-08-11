<script setup lang="ts">
const props = withDefaults(defineProps<{
  name: string
  size?: number
  filled?: boolean
  label?: string
}>(), {
  size: 24,
  filled: false,
  label: ''
})

// App-level semantic names stay stable even if the underlying Material symbol
// changes. This keeps pages independent from the icon provider.
const symbols: Record<string, string> = {
  board: 'grid_view',
  voice: 'graphic_eq',
  mixer: 'tune',
  media: 'smart_display',
  desktop: 'desktop_windows',
  key: 'key',
  refresh: 'refresh',
  mic: 'mic',
  mic_off: 'mic_off',
  stop_all: 'stop_circle',
  power: 'power_settings_new',
  power_off: 'power_off',
  play: 'play_arrow',
  pause: 'pause',
  stop: 'stop',
  replay10: 'replay_10',
  forward10: 'forward_10',
  headphones: 'headphones',
  output: 'output',
  reset: 'restart_alt',
  qr: 'qr_code_scanner',
  wifi: 'wifi',
  wifi_off: 'wifi_off',
  loop: 'repeat',
  error: 'error',
  warning: 'warning',
  info: 'info',
  check: 'check',
  close: 'close',
  install: 'install_mobile',
  volume: 'volume_up'
}

const symbol = computed(() => symbols[props.name] || 'help')
const variation = computed(() => `'FILL' ${props.filled ? 1 : 0}, 'wght' ${props.filled ? 550 : 430}, 'GRAD' 0, 'opsz' ${Math.max(20, Math.min(48, props.size))}`)
</script>

<template>
  <span
    class="gb-icon material-symbols-rounded"
    :style="{ fontSize: `${size}px`, fontVariationSettings: variation }"
    :aria-hidden="label ? undefined : 'true'"
    :role="label ? 'img' : undefined"
    :aria-label="label || undefined"
  >{{ symbol }}</span>
</template>
