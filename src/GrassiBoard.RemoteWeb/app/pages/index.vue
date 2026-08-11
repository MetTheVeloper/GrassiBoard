<script setup lang="ts">
const remote = useRemoteConnection()
const pads = computed(() => remote.snapshot.value?.pads ?? [])
const engine = computed(() => remote.snapshot.value?.engine)

function play(id: string) {
  if (remote.sendCommand('pad.play', { padId: id })) remote.vibrate(12)
}
function stop(id: string) {
  if (remote.sendCommand('pad.stop', { padId: id })) remote.vibrate(8)
}
function startEngine() {
  if (remote.sendCommand('engine.start')) remote.vibrate([12, 18, 12])
}
</script>

<template>
  <section class="page-section board-page">
    <div class="section-heading">
      <div><p class="eyebrow">BOARD</p><h1>Sound Pads</h1></div>
      <button v-if="engine && !engine.running" class="primary-button compact" type="button" @click="startEngine">Start Engine</button>
    </div>

    <div v-if="pads.length" class="pad-grid">
      <article v-for="pad in pads" :key="pad.id" class="pad" :class="{ playing: pad.playing, unavailable: !pad.ready }">
        <button class="pad-play" type="button" :disabled="!pad.ready" @click="play(pad.id)">
          <span class="pad-state">{{ pad.playing ? 'PLAYING' : pad.hasError ? 'ERROR' : pad.loop ? 'LOOP' : 'READY' }}</span>
          <strong>{{ pad.title }}</strong>
        </button>
        <button v-if="pad.playing" class="pad-stop" type="button" @click.stop="stop(pad.id)">Stop</button>
      </article>
    </div>
    <div v-else class="empty-card glass-card">
      <strong>No Sound Pads in this profile.</strong>
      <span>Add Pads on the Windows app; they will appear here automatically.</span>
    </div>
  </section>
</template>
