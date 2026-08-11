<script setup lang="ts">
const props = withDefaults(defineProps<{ label: string, duration?: number }>(), { duration: 650 })
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

onBeforeUnmount(cancel)
</script>

<template>
  <button
    class="danger-hold"
    type="button"
    :style="{ '--hold-progress': `${progress * 100}%` }"
    @pointerdown="start"
    @pointerup="cancel"
    @pointercancel="cancel"
    @pointerleave="cancel"
  >
    {{ label }}
    <small>hold</small>
  </button>
</template>
