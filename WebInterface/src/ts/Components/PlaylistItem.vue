<template>
  <li>
    {{ todo.Title }}
  </li>
</template>

<script lang="ts">
import Vue from "vue";

export default Vue.extend({
  props: {
    todo: {
      type: Object,
      required: true
    }
  }
});
</script>
