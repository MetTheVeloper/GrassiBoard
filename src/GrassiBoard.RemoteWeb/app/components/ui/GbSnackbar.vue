<script setup lang="ts">
const props = withDefaults(defineProps<{
  open: boolean
  message: string
  tone?: 'neutral' | 'success' | 'warning' | 'danger'
}>(), {
  tone: 'neutral'
})

defineEmits<{ dismiss: [] }>()

const icon = computed(() => {
  if (props.tone === 'success') return 'check'
  if (props.tone === 'warning') return 'warning'
  if (props.tone === 'danger') return 'error'
  return 'info'
})
</script>

<template>
  <Transition name="gb-snackbar">
    <div
      v-if="open && message"
      class="gb-snackbar"
      :class="`gb-snackbar--${tone}`"
      :role="tone === 'danger' ? 'alert' : 'status'"
      :aria-live="tone === 'danger' ? 'assertive' : 'polite'"
    >
      <GbIcon :name="icon" :size="20" />
      <span>{{ message }}</span>
      <button class="gb-snackbar__close" type="button" aria-label="Dismiss message" @click="$emit('dismiss')">
        <GbIcon name="close" :size="19" />
      </button>
    </div>
  </Transition>
</template>
