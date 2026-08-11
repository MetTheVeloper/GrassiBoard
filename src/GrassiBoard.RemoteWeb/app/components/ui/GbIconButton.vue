<script setup lang="ts">
withDefaults(defineProps<{
  icon: string
  label: string
  disabled?: boolean
  selected?: boolean
}>(), {
  disabled: false,
  selected: false
})

defineEmits<{ click: [event: MouseEvent] }>()
</script>

<template>
  <md-icon-button
    type="button"
    :disabled="disabled"
    :selected="selected"
    :aria-label="label"
    @click="$emit('click', $event)"
  >
    <GbIcon :name="icon" :size="24" />
  </md-icon-button>
</template>
