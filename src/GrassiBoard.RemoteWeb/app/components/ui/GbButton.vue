<script setup lang="ts">
withDefaults(defineProps<{
  variant?: 'filled' | 'tonal' | 'outlined' | 'text'
  disabled?: boolean
  icon?: string
  trailingIcon?: boolean
  ariaLabel?: string
}>(), {
  variant: 'tonal',
  disabled: false,
  icon: '',
  trailingIcon: false,
  ariaLabel: ''
})

defineEmits<{ click: [event: MouseEvent] }>()
</script>

<template>
  <md-filled-button
    v-if="variant === 'filled'"
    type="button"
    :disabled="disabled"
    :aria-label="ariaLabel || undefined"
    :trailing-icon="trailingIcon"
    :has-icon="Boolean(icon)"
    @click="$emit('click', $event)"
  >
    <GbIcon v-if="icon" slot="icon" :name="icon" :size="18" />
    <slot />
  </md-filled-button>

  <md-outlined-button
    v-else-if="variant === 'outlined'"
    type="button"
    :disabled="disabled"
    :aria-label="ariaLabel || undefined"
    :trailing-icon="trailingIcon"
    :has-icon="Boolean(icon)"
    @click="$emit('click', $event)"
  >
    <GbIcon v-if="icon" slot="icon" :name="icon" :size="18" />
    <slot />
  </md-outlined-button>

  <md-text-button
    v-else-if="variant === 'text'"
    type="button"
    :disabled="disabled"
    :aria-label="ariaLabel || undefined"
    :trailing-icon="trailingIcon"
    :has-icon="Boolean(icon)"
    @click="$emit('click', $event)"
  >
    <GbIcon v-if="icon" slot="icon" :name="icon" :size="18" />
    <slot />
  </md-text-button>

  <md-filled-tonal-button
    v-else
    type="button"
    :disabled="disabled"
    :aria-label="ariaLabel || undefined"
    :trailing-icon="trailingIcon"
    :has-icon="Boolean(icon)"
    @click="$emit('click', $event)"
  >
    <GbIcon v-if="icon" slot="icon" :name="icon" :size="18" />
    <slot />
  </md-filled-tonal-button>
</template>
