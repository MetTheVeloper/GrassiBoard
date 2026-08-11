<script setup lang="ts">
withDefaults(defineProps<{
  label: string
  tone?: 'neutral' | 'primary' | 'success' | 'warning' | 'danger'
  icon?: string
  pulse?: boolean
}>(), {
  tone: 'neutral',
  icon: '',
  pulse: false
})
</script>

<template>
  <span class="gb-status-chip" :class="[`gb-status-chip--${tone}`, { 'gb-status-chip--pulse': pulse }]">
    <GbIcon v-if="icon" :name="icon" :size="15" />
    <span>{{ label }}</span>
  </span>
</template>
