<script setup lang="ts">
const props = withDefaults(defineProps<{
  modelValue: boolean
  disabled?: boolean
  label: string
  supportingText?: string
  activeText?: string
  inactiveText?: string
  danger?: boolean
  icon?: string
}>(), {
  disabled: false,
  supportingText: '',
  activeText: 'On',
  inactiveText: 'Off',
  danger: false,
  icon: ''
})

const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()

function onChange(event: Event) {
  const target = event.currentTarget as HTMLElement & { selected?: boolean }
  emit('update:modelValue', Boolean(target.selected))
}
</script>

<template>
  <label class="gb-toggle-row" :class="{ 'gb-toggle-row--danger': danger && modelValue }">
    <span v-if="icon" class="gb-control-icon"><GbIcon :name="icon" :size="22" /></span>
    <span class="gb-control-copy">
      <strong>{{ label }}</strong>
      <small v-if="supportingText">{{ supportingText }}</small>
    </span>
    <span class="gb-control-state">{{ modelValue ? activeText : inactiveText }}</span>
    <md-switch
      :selected="modelValue"
      :disabled="disabled"
      icons
      show-only-selected-icon
      :aria-label="label"
      @change="onChange"
    />
  </label>
</template>
