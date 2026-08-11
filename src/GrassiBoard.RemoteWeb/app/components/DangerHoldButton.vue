<script setup lang="ts">
const props = withDefaults(defineProps<{ label: string, duration?: number, icon?: string, compact?: boolean }>(), {
  duration: 650,
  icon: 'stop_all',
  compact: false
})
const emit = defineEmits<{ activate: [] }>()
const progress = ref(0)
let frame = 0
let started = 0

function cancel() {
  cancelAnimationFrame(frame)
  progress.value = 0
  started = 0
}

function tick(now: number) {
  if (!started) return
  progress.value = Math.min(1, (now - started) / props.duration)
  if (progress.value >= 1) {
    cancel()
    emit('activate')
    return
  }
  frame = requestAnimationFrame(tick)
}

function start() {
  cancel()
  started = performance.now()
  frame = requestAnimationFrame(tick)
}

function keyboardActivate(event: KeyboardEvent) {
  if (event.key === 'Enter' || event.key === ' ') {
    event.preventDefault()
    emit('activate')
  }
}

onBeforeUnmount(cancel)
</script>

<template>
  <button
    class="danger-hold"
    :class="{ 'danger-hold--compact': compact }"
    type="button"
    :style="{ '--hold-progress': `${progress * 100}%` }"
    :aria-label="`${label}. Hold to activate.`"
    @pointerdown="start"
    @pointerup="cancel"
    @pointercancel="cancel"
    @pointerleave="cancel"
    @keydown="keyboardActivate"
  >
    <span class="danger-hold__progress" aria-hidden="true" />
    <span class="danger-hold__content">
      <GbIcon :name="icon" :size="21" />
      <span>{{ label }}</span>
      <small>hold</small>
    </span>
  </button>
</template>
