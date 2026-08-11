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

function setFx(enabled: boolean) {
  remote.sendCommand('voice.fx.set', { enabled })
}

function setPreserve(enabled: boolean) {
  remote.sendCommand('voice.preserveCharacter.set', { enabled })
}

function updatePitch(value: number) {
  pitch.value = value
  sendPitch({ value })
}

function updateFine(value: number) {
  finePitch.value = value
  sendFine({ value })
}

function updateFormant(value: number) {
  formant.value = value
  sendFormant({ value })
}

function applyPreset(id: string) {
  if (remote.sendCommand('preset.apply', { presetId: id })) remote.vibrate([10, 16, 10])
}

function resetVoice() {
  if (remote.sendCommand('voice.reset')) remote.vibrate(10)
}
</script>

<template>
  <section class="page-section">
    <div class="section-heading">
      <div>
        <p class="eyebrow">VOICE</p>
        <h2>Live Voice FX</h2>
        <p class="section-support">Fast controls for the active voice chain. Values stay synchronized with Windows.</p>
      </div>
    </div>

    <template v-if="state">
      <section class="gb-surface voice-master">
        <GbSwitch
          :model-value="state.enabled"
          label="Voice FX"
          supporting-text="Enable pitch and formant processing"
          active-text="Active"
          inactive-text="Bypassed"
          icon="voice"
          @update:model-value="setFx"
        />
      </section>

      <section class="gb-surface control-group">
        <div class="group-heading">
          <div><p class="eyebrow">TONE</p><h3>Voice shape</h3></div>
          <GbButton variant="text" icon="reset" @click="resetVoice">Reset</GbButton>
        </div>

        <GbSlider
          :model-value="pitch"
          :min="-12"
          :max="12"
          :step="0.1"
          label="Pitch"
          :value-text="`${pitch >= 0 ? '+' : ''}${pitch.toFixed(1)} st`"
          icon="voice"
          @input="updatePitch"
        />
        <GbSlider
          :model-value="finePitch"
          :min="-100"
          :max="100"
          :step="1"
          label="Fine Pitch"
          :value-text="`${finePitch >= 0 ? '+' : ''}${Math.round(finePitch)} ct`"
          @input="updateFine"
        />
        <GbSlider
          :model-value="formant"
          :min="-12"
          :max="12"
          :step="0.1"
          label="Formant"
          :value-text="`${formant >= 0 ? '+' : ''}${formant.toFixed(1)} st`"
          @input="updateFormant"
        />
      </section>

      <section class="gb-surface control-group control-group--compact">
        <GbSwitch
          :model-value="state.preserveVocalCharacter"
          label="Preserve vocal character"
          supporting-text="Keep the original vocal character while shifting"
          active-text="On"
          inactive-text="Off"
          @update:model-value="setPreserve"
        />
      </section>
    </template>

    <section class="subsection">
      <div class="group-heading">
        <div><p class="eyebrow">USER PRESETS</p><h3>Quick recall</h3></div>
      </div>
      <md-chip-set v-if="presets.length" class="preset-chip-set" aria-label="Voice presets">
        <GbActionChip v-for="preset in presets" :key="preset.id" :label="preset.name" icon="voice" @click="applyPreset(preset.id)" />
      </md-chip-set>
      <GbEmptyState v-else icon="voice" title="No user presets" message="Create presets on Windows and they will appear here automatically." />
    </section>
  </section>
</template>
