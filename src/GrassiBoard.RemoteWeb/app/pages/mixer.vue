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

function toggleMute(value: boolean) {
  remote.sendCommand('mic.mute.set', { muted: value })
}
function setGain(channel: 'mic' | 'soundboard' | 'master', value: number) {
  if (channel === 'mic') mic.value = value
  if (channel === 'soundboard') soundboard.value = value
  if (channel === 'master') master.value = value
  sendGain({ channel, value })
}

function setMicGain(value: number) { setGain('mic', value) }
function setSoundboardGain(value: number) { setGain('soundboard', value) }
function setMasterGain(value: number) { setGain('master', value) }
</script>

<template>
  <section class="page-section">
    <div class="section-heading">
      <div>
        <p class="eyebrow">MIXER</p>
        <h2>Compact Mixer</h2>
        <p class="section-support">Program-mix controls only. Meter telemetry never moves the layout.</p>
      </div>
    </div>

    <template v-if="state">
      <section class="gb-surface control-group control-group--compact">
        <GbSwitch
          :model-value="Boolean(remote.snapshot.value?.microphoneMuted)"
          label="Microphone mute"
          supporting-text="Immediately mute the mic branch"
          active-text="Muted"
          inactive-text="Live"
          :danger="true"
          :icon="remote.snapshot.value?.microphoneMuted ? 'mic_off' : 'mic'"
          @update:model-value="toggleMute"
        />
      </section>

      <div class="mixer-channels">
        <section class="gb-surface mixer-channel">
          <div class="mixer-channel__header">
            <span class="gb-control-icon"><GbIcon name="mic" :size="22" /></span>
            <div><strong>Microphone</strong><small>{{ remote.snapshot.value?.meters.microphoneDb || '−∞ dBFS' }}</small></div>
          </div>
          <GbSlider :model-value="mic" :min="-24" :max="24" :step="0.5" label="Mic Gain" :value-text="`${mic >= 0 ? '+' : ''}${mic.toFixed(1)} dB`" @input="setMicGain" />
        </section>

        <section class="gb-surface mixer-channel">
          <div class="mixer-channel__header">
            <span class="gb-control-icon"><GbIcon name="board" :size="22" /></span>
            <div><strong>Soundboard</strong><small>{{ remote.snapshot.value?.meters.soundboardDb || '−∞ dBFS' }}</small></div>
          </div>
          <GbSlider :model-value="soundboard" :min="-24" :max="24" :step="0.5" label="Soundboard Gain" :value-text="`${soundboard >= 0 ? '+' : ''}${soundboard.toFixed(1)} dB`" @input="setSoundboardGain" />
        </section>

        <section class="gb-surface mixer-channel mixer-channel--master">
          <div class="mixer-channel__header">
            <span class="gb-control-icon"><GbIcon name="mixer" :size="22" /></span>
            <div><strong>Master</strong><small>{{ remote.snapshot.value?.meters.masterDb || '−∞ dBFS' }}</small></div>
          </div>
          <GbSlider :model-value="master" :min="-24" :max="12" :step="0.5" label="Master Gain" :value-text="`${master >= 0 ? '+' : ''}${master.toFixed(1)} dB`" @input="setMasterGain" />
        </section>
      </div>
    </template>
  </section>
</template>
