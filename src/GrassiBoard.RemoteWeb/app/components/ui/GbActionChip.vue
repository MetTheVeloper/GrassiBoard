<script setup lang="ts">
withDefaults(defineProps<{
  label: string
  disabled?: boolean
  icon?: string
}>(), { disabled: false, icon: '' })

defineEmits<{ click: [event: MouseEvent] }>()
</script>

<template>
  <md-assist-chip
    :label="label"
    :disabled="disabled"
    :has-icon="Boolean(icon)"
    @click="$emit('click', $event)"
  >
    <GbIcon v-if="icon" slot="icon" :name="icon" :size="18" />
  </md-assist-chip>
</template>
