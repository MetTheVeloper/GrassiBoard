<script setup lang="ts">
const remote = useRemoteConnection()
const pads = computed(() => remote.snapshot.value?.pads ?? [])
const feedbackPads = ref<Set<string>>(new Set())

function flashPad(id: string) {
  const next = new Set(feedbackPads.value)
  next.add(id)
  feedbackPads.value = next
  window.setTimeout(() => {
    const after = new Set(feedbackPads.value)
    after.delete(id)
    feedbackPads.value = after
  }, 180)
}

function play(id: string) {
  if (remote.sendCommand('pad.play', { padId: id })) {
    flashPad(id)
    remote.vibrate(12)
  }
}

function stop(id: string) {
  if (remote.sendCommand('pad.stop', { padId: id })) remote.vibrate(8)
}

</script>

<template>
  <section class="page-section board-page">
    <div class="section-heading">
      <div>
        <p class="eyebrow">BOARD</p>
        <h2>Sound Pads</h2>
        <p class="section-support">Tap to fire. Playing state always follows GrassiBoard.</p>
      </div>
    </div>

    <div v-if="pads.length" class="pad-grid">
      <article
        v-for="pad in pads"
        :key="pad.id"
        class="sound-pad"
        :class="{
          'sound-pad--playing': pad.playing,
          'sound-pad--unavailable': !pad.ready,
          'sound-pad--error': pad.hasError,
          'sound-pad--feedback': feedbackPads.has(pad.id)
        }"
      >
        <button class="sound-pad__trigger" type="button" :disabled="!pad.ready" @click="play(pad.id)">
          <span class="sound-pad__state-icon">
            <GbIcon :name="pad.hasError ? 'error' : pad.playing ? (pad.loop ? 'loop' : 'play') : 'board'" :size="22" />
          </span>
          <span class="sound-pad__copy">
            <small>{{ pad.playing ? (pad.loop ? 'LOOPING' : 'PLAYING') : pad.hasError ? 'ERROR' : pad.loop ? 'LOOP READY' : 'READY' }}</small>
            <strong>{{ pad.title }}</strong>
          </span>
        </button>
        <GbIconButton v-if="pad.playing" class="sound-pad__stop" icon="stop" label="Stop pad" @click="stop(pad.id)" />
      </article>
    </div>

    <GbEmptyState
      v-else
      icon="board"
      title="No Sound Pads yet"
      message="Add Pads in the Windows app. This board updates automatically without a refresh."
    />
  </section>
</template>
