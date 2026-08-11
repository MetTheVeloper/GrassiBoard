<script setup lang="ts">
const remote = useRemoteConnection()
const state = computed(() => remote.snapshot.value?.mixer)
const mic = ref(0)
const soundboard = ref(0)
const master = ref(0)
const sendGain = useCoalescedCommand('mixer.gain.set')
watch(() => state.value?.micGain, value => { if (typeof value === 'number') mic.value = value }, { immediate: true })
watch(() => state.value?.soundboardGain, value => { if (typeof value === 'number') soundboard.value = value }, { immediate: true })
watch(() => state.value?.masterGain, value => { if (typeof value === 'number') master.value = value }, { immediate: true })
function toggleMute() {
  if (!remote.snapshot.value) return
  remote.sendCommand('mic.mute.set', { muted: !remote.snapshot.value.microphoneMuted })
}
</script>

<template>
  <section class="page-section">
    <div class="section-heading"><div><p class="eyebrow">MIXER</p><h1>Compact Mixer</h1></div></div>
    <div v-if="state" class="control-stack">
      <button class="toggle-card glass-card" :class="{ danger: remote.snapshot.value?.microphoneMuted }" type="button" @click="toggleMute">
        <span>Microphone</span><strong>{{ remote.snapshot.value?.microphoneMuted ? 'MUTED' : 'LIVE' }}</strong>
      </button>
      <label class="slider-card glass-card">
        <span><strong>Mic Gain</strong><output>{{ mic.toFixed(1) }} dB</output></span>
        <input v-model.number="mic" type="range" min="-24" max="24" step="0.5" @input="sendGain({ channel: 'mic', value: mic })">
      </label>
      <label class="slider-card glass-card">
        <span><strong>Soundboard Gain</strong><output>{{ soundboard.toFixed(1) }} dB</output></span>
        <input v-model.number="soundboard" type="range" min="-24" max="24" step="0.5" @input="sendGain({ channel: 'soundboard', value: soundboard })">
      </label>
      <label class="slider-card glass-card">
        <span><strong>Master Gain</strong><output>{{ master.toFixed(1) }} dB</output></span>
        <input v-model.number="master" type="range" min="-24" max="12" step="0.5" @input="sendGain({ channel: 'master', value: master })">
      </label>
    </div>
  </section>
</template>
