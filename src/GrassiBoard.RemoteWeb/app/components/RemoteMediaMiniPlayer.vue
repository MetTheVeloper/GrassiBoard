<script setup lang="ts">
const remote = useRemoteConnection()
const media = computed(() => remote.snapshot.value?.media)

const visible = computed(() => {
  const current = media.value
  if (!current?.hasMedia) return false
  return Boolean(current.playing || (current.position ?? 0) > 0.05)
})

const progress = computed(() => {
  const current = media.value
  if (!current || !Number.isFinite(current.duration) || current.duration <= 0) return 0
  return Math.max(0, Math.min(100, ((current.position ?? 0) / current.duration) * 100))
})

function format(seconds: number | undefined) {
  if (!Number.isFinite(seconds)) return '00:00'
  const total = Math.max(0, Math.round(seconds ?? 0))
  return `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`
}

function command(name: string, payload?: Record<string, unknown>) {
  if (remote.sendCommand(name, payload)) remote.vibrate(8)
}
</script>

<template>
  <section
    v-if="visible && media"
    class="remote-media-mini gb-surface"
    :class="{ 'remote-media-mini--playing': media.playing }"
    aria-label="Media playback controls"
  >
    <div class="remote-media-mini__progress" aria-hidden="true">
      <i :style="{ width: `${progress}%` }" />
    </div>

    <div class="remote-media-mini__body">
      <NuxtLink to="/media" class="remote-media-mini__identity" aria-label="Open full Media controls">
        <span class="remote-media-mini__art">
          <GbIcon name="media" :size="22" />
        </span>
        <span class="remote-media-mini__copy">
          <small>{{ media.playing ? 'NOW PLAYING' : 'PAUSED' }}</small>
          <strong>{{ media.displayName }}</strong>
          <span>{{ format(media.position) }} / {{ format(media.duration) }}</span>
        </span>
      </NuxtLink>

      <span class="remote-media-mini__state" :class="{ 'remote-media-mini__state--live': media.playing }">
        {{ media.playing ? 'LIVE' : 'HOLD' }}
      </span>
    </div>

    <div class="remote-media-mini__controls">
      <button type="button" class="remote-media-mini__control" aria-label="Skip back 10 seconds" @click="command('media.skip', { seconds: -10 })">
        <GbIcon name="replay10" :size="22" />
      </button>
      <button
        type="button"
        class="remote-media-mini__control remote-media-mini__control--primary"
        :aria-label="media.playing ? 'Pause media' : 'Play media'"
        @click="command('media.playPause')"
      >
        <GbIcon :name="media.playing ? 'pause' : 'play'" :size="24" />
      </button>
      <button type="button" class="remote-media-mini__control" aria-label="Skip forward 10 seconds" @click="command('media.skip', { seconds: 10 })">
        <GbIcon name="forward10" :size="22" />
      </button>
      <button type="button" class="remote-media-mini__control" aria-label="Stop media" @click="command('media.stop')">
        <GbIcon name="stop" :size="22" />
      </button>
    </div>
  </section>
</template>
