<script setup lang="ts">
const remote = useRemoteConnection()
const media = computed(() => remote.snapshot.value?.media)
const position = ref(0)
const volume = ref(1)
const dragging = ref(false)
const sendVolume = useCoalescedCommand('media.volume.set')
watch(() => media.value?.position, value => { if (!dragging.value && typeof value === 'number') position.value = value }, { immediate: true })
watch(() => media.value?.volume, value => { if (typeof value === 'number') volume.value = value }, { immediate: true })
function seek() {
  dragging.value = false
  remote.sendCommand('media.seek', { seconds: position.value })
}
function format(seconds: number) {
  if (!Number.isFinite(seconds)) return '00:00'
  const total = Math.max(0, Math.round(seconds))
  return `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`
}
</script>

<template>
  <section class="page-section">
    <div class="section-heading"><div><p class="eyebrow">MEDIA</p><h1>Media Deck</h1></div></div>
    <div v-if="media?.hasMedia" class="control-stack">
      <div class="media-title glass-card"><span class="eyebrow">LOADED</span><strong>{{ media.displayName }}</strong></div>
      <div class="transport-row">
        <button type="button" @click="remote.sendCommand('media.skip', { seconds: -10 })">−10</button>
        <button class="transport-main" type="button" @click="remote.sendCommand('media.playPause')">{{ media.playing ? 'Pause' : 'Play' }}</button>
        <button type="button" @click="remote.sendCommand('media.skip', { seconds: 10 })">+10</button>
        <button type="button" @click="remote.sendCommand('media.stop')">Stop</button>
      </div>
      <label class="slider-card glass-card">
        <span><strong>Timeline</strong><output>{{ format(position) }} / {{ format(media.duration) }}</output></span>
        <input v-model.number="position" type="range" min="0" :max="Math.max(media.duration, 0.1)" step="0.1" @pointerdown="dragging = true" @change="seek">
      </label>
      <label class="slider-card glass-card">
        <span><strong>Media Volume</strong><output>{{ Math.round(volume * 100) }}%</output></span>
        <input v-model.number="volume" type="range" min="0" max="1.5" step="0.01" @input="sendVolume({ value: volume })">
      </label>
      <button class="toggle-card glass-card" :class="{ active: media.monitorEnabled }" type="button" @click="remote.sendCommand('media.monitor.set', { enabled: !media.monitorEnabled })"><span>Headphone Monitor</span><strong>{{ media.monitorEnabled ? 'ON' : 'OFF' }}</strong></button>
      <button class="toggle-card glass-card" :class="{ active: media.sendEnabled }" type="button" @click="remote.sendCommand('media.send.set', { enabled: !media.sendEnabled })"><span>Send to Virtual Mic</span><strong>{{ media.sendEnabled ? 'ON' : 'OFF' }}</strong></button>
      <p v-if="media.hasError" class="error-copy">The Windows Media Deck reports an error.</p>
    </div>
    <div v-else class="empty-card glass-card"><strong>No media loaded.</strong><span>Choose a file on the Windows app; the Remote will update automatically.</span></div>
  </section>
</template>
