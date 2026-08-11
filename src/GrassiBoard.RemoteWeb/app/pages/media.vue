<script setup lang="ts">
const remote = useRemoteConnection()
const media = computed(() => remote.snapshot.value?.media)
const position = ref(0)
const volume = ref(1)
const dragging = ref(false)
const sendVolume = useCoalescedCommand('media.volume.set')

watch(() => media.value?.position, value => { if (!dragging.value && typeof value === 'number') position.value = value }, { immediate: true })
watch(() => media.value?.volume, value => { if (typeof value === 'number') volume.value = value }, { immediate: true })

function seek(value: number) {
  position.value = value
  dragging.value = false
  remote.sendCommand('media.seek', { seconds: value })
}

function updateVolume(value: number) {
  volume.value = value
  sendVolume({ value })
}

function setMonitor(enabled: boolean) {
  remote.sendCommand('media.monitor.set', { enabled })
}

function setSend(enabled: boolean) {
  remote.sendCommand('media.send.set', { enabled })
}

function format(seconds: number) {
  if (!Number.isFinite(seconds)) return '00:00'
  const total = Math.max(0, Math.round(seconds))
  return `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`
}
</script>

<template>
  <section class="page-section">
    <div class="section-heading">
      <div>
        <p class="eyebrow">MEDIA</p>
        <h2>Media Deck</h2>
        <p class="section-support">Transport controls stay familiar; monitoring and program send remain explicit.</p>
      </div>
    </div>

    <template v-if="media?.hasMedia">
      <section class="gb-surface media-now-playing" :class="{ 'media-now-playing--active': media.playing }">
        <span class="media-now-playing__art"><GbIcon name="media" :size="30" /></span>
        <div class="media-now-playing__copy">
          <p class="eyebrow">{{ media.playing ? 'NOW PLAYING' : 'LOADED' }}</p>
          <strong>{{ media.displayName }}</strong>
          <span>{{ format(position) }} / {{ format(media.duration) }}</span>
        </div>
        <GbStatusChip :label="media.playing ? 'Playing' : 'Paused'" :tone="media.playing ? 'success' : 'neutral'" :icon="media.playing ? 'play' : 'pause'" />
      </section>

      <section class="media-transport" aria-label="Media transport controls">
        <GbIconButton icon="replay10" label="Skip back 10 seconds" @click="remote.sendCommand('media.skip', { seconds: -10 })" />
        <button class="media-transport__main" type="button" :aria-label="media.playing ? 'Pause media' : 'Play media'" @click="remote.sendCommand('media.playPause')">
          <GbIcon :name="media.playing ? 'pause' : 'play'" :size="30" />
        </button>
        <GbIconButton icon="forward10" label="Skip forward 10 seconds" @click="remote.sendCommand('media.skip', { seconds: 10 })" />
        <GbIconButton icon="stop" label="Stop media" @click="remote.sendCommand('media.stop')" />
      </section>

      <section class="gb-surface control-group">
        <GbSlider
          :model-value="position"
          :min="0"
          :max="Math.max(media.duration, 0.1)"
          :step="0.1"
          label="Timeline"
          :show-scale="false"
          :value-text="`${format(position)} / ${format(media.duration)}`"
          @pointerdown="dragging = true"
          @change="seek"
        />
        <GbSlider
          :model-value="volume"
          :min="0"
          :max="1.5"
          :step="0.01"
          label="Media Volume"
          :show-scale="false"
          :value-text="`${Math.round(volume * 100)}%`"
          icon="volume"
          @input="updateVolume"
        />
      </section>

      <section class="gb-surface control-group control-group--compact media-routing">
        <GbSwitch
          :model-value="media.monitorEnabled"
          label="Headphone Monitor"
          supporting-text="Hear Media on the Windows monitor output"
          icon="headphones"
          @update:model-value="setMonitor"
        />
        <div class="gb-divider" />
        <GbSwitch
          :model-value="media.sendEnabled"
          label="Send to Virtual Mic"
          supporting-text="Include Media in the program mix"
          icon="output"
          @update:model-value="setSend"
        />
      </section>

      <div v-if="media.hasError" class="network-banner network-banner--inline" role="alert">
        <div><GbIcon name="error" :size="19" /><span>The Windows Media Deck reports an error.</span></div>
      </div>
    </template>

    <GbEmptyState
      v-else
      icon="media"
      title="No media loaded"
      message="Choose a file in the Windows Media Deck. GrassiMote will update automatically."
    />
  </section>
</template>
