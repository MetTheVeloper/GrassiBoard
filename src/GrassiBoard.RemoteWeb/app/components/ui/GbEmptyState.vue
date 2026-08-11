<script setup lang="ts">
withDefaults(defineProps<{
  icon?: string
  title: string
  message: string
}>(), { icon: 'board' })
</script>

<template>
  <div class="gb-empty-state">
    <span class="gb-empty-state__icon"><GbIcon :name="icon" :size="28" /></span>
    <div>
      <strong>{{ title }}</strong>
      <p>{{ message }}</p>
    </div>
  </div>
</template>
