<script setup lang="ts">
const remote = useRemoteConnection()
const state = computed(() => remote.snapshot.value?.voice)
const presets = computed(() => remote.snapshot.value?.presets ?? [])
const pitch = ref(0)
const finePitch = ref(0)
const formant = ref(0)
const sendPitch = useCoalescedCommand('voice.pitch.set')
const sendFine = useCoalescedCommand('voice.finePitch.set')
const sendFormant = useCoalescedCommand('voice.formant.set')

watch(() => state.value?.pitch, value => { if (typeof value === 'number') pitch.value = value }, { immediate: true })
watch(() => state.value?.finePitch, value => { if (typeof value === 'number') finePitch.value = value }, { immediate: true })
watch(() => state.value?.formant, value => { if (typeof value === 'number') formant.value = value }, { immediate: true })

function setFx() {
  if (!state.value) return
  remote.sendCommand('voice.fx.set', { enabled: !state.value.enabled })
}
function setPreserve() {
  if (!state.value) return
  remote.sendCommand('voice.preserveCharacter.set', { enabled: !state.value.preserveVocalCharacter })
}
function applyPreset(id: string) {
  if (remote.sendCommand('preset.apply', { presetId: id })) remote.vibrate([10, 16, 10])
}
</script>

<template>
  <section class="page-section">
    <div class="section-heading"><div><p class="eyebrow">VOICE</p><h1>Live Voice FX</h1></div></div>
    <div v-if="state" class="control-stack">
      <button class="toggle-card glass-card" :class="{ active: state.enabled }" type="button" @click="setFx">
        <span>Voice FX</span><strong>{{ state.enabled ? 'ON' : 'OFF' }}</strong>
      </button>

      <label class="slider-card glass-card">
        <span><strong>Pitch</strong><output>{{ pitch.toFixed(1) }} st</output></span>
        <input v-model.number="pitch" type="range" min="-12" max="12" step="0.1" @input="sendPitch({ value: pitch })">
      </label>
      <label class="slider-card glass-card">
        <span><strong>Fine Pitch</strong><output>{{ Math.round(finePitch) }} ct</output></span>
        <input v-model.number="finePitch" type="range" min="-100" max="100" step="1" @input="sendFine({ value: finePitch })">
      </label>
      <label class="slider-card glass-card">
        <span><strong>Formant</strong><output>{{ formant.toFixed(1) }} st</output></span>
        <input v-model.number="formant" type="range" min="-12" max="12" step="0.1" @input="sendFormant({ value: formant })">
      </label>

      <button class="toggle-card glass-card" :class="{ active: state.preserveVocalCharacter }" type="button" @click="setPreserve">
        <span>Preserve vocal character</span><strong>{{ state.preserveVocalCharacter ? 'ON' : 'OFF' }}</strong>
      </button>
      <button class="secondary-button" type="button" @click="remote.sendCommand('voice.reset')">Reset Voice</button>
    </div>

    <div class="subsection">
      <p class="eyebrow">USER PRESETS</p>
      <div v-if="presets.length" class="preset-grid">
        <button v-for="preset in presets" :key="preset.id" class="preset-button" type="button" @click="applyPreset(preset.id)">{{ preset.name }}</button>
      </div>
      <p v-else class="muted">No user presets in the active profile.</p>
    </div>
  </section>
</template>
