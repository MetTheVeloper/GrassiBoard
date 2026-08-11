<script setup lang="ts">
withDefaults(defineProps<{
  icon: string
  label: string
  variant?: 'surface' | 'primary' | 'secondary' | 'tertiary'
  disabled?: boolean
  tone?: 'normal' | 'success' | 'danger'
}>(), {
  variant: 'surface',
  disabled: false,
  tone: 'normal'
})

defineEmits<{ click: [event: MouseEvent] }>()
</script>

<template>
  <md-fab
    class="gb-fab"
    :class="`gb-fab--${tone}`"
    :variant="variant"
    :disabled="disabled"
    :aria-label="label"
    :title="label"
    @click="$emit('click', $event)"
  >
    <GbIcon slot="icon" :name="icon" :size="25" />
  </md-fab>
</template>
