<script setup lang="ts">
const props = withDefaults(defineProps<{
  modelValue: number
  min: number
  max: number
  step?: number
  label: string
  valueText: string
  ariaValueText?: string
  disabled?: boolean
  ticks?: boolean
  icon?: string
  showScale?: boolean
}>(), {
  step: 1,
  ariaValueText: '',
  disabled: false,
  ticks: false,
  icon: '',
  showScale: true
})

const emit = defineEmits<{
  'update:modelValue': [value: number]
  input: [value: number]
  change: [value: number]
  pointerdown: [event: PointerEvent]
}>()

function readValue(event: Event) {
  const target = event.currentTarget as HTMLElement & { value?: number }
  return typeof target.value === 'number' ? target.value : props.modelValue
}

function onInput(event: Event) {
  const value = readValue(event)
  emit('update:modelValue', value)
  emit('input', value)
}

function onChange(event: Event) {
  const value = readValue(event)
  emit('update:modelValue', value)
  emit('change', value)
}
</script>

<template>
  <div class="gb-slider-block">
    <div class="gb-slider-heading">
      <div class="gb-slider-label">
        <span v-if="icon" class="gb-control-icon"><GbIcon :name="icon" :size="21" /></span>
        <strong>{{ label }}</strong>
      </div>
      <output>{{ valueText }}</output>
    </div>
    <md-slider
      :min="min"
      :max="max"
      :step="step"
      :value="modelValue"
      :disabled="disabled"
      :ticks="ticks"
      :aria-label="label"
      :aria-valuetext="ariaValueText || valueText"
      @input="onInput"
      @change="onChange"
      @pointerdown="$emit('pointerdown', $event)"
    />
    <div v-if="showScale" class="gb-slider-scale" aria-hidden="true">
      <span>{{ min }}</span><span>0</span><span>{{ max > 0 ? `+${max}` : max }}</span>
    </div>
  </div>
</template>
