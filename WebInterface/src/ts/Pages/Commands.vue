<template>
	<div>
		<div id="swagger-ui"></div>
	</div>
</template>

<script>
import Vue from "vue";
import { SwaggerUIBundle, SwaggerUIStandalonePreset } from "swagger-ui-dist";

export default Vue.extend({
	created() {
		console.log(SwaggerUIBundle);
		
		const ui = SwaggerUIBundle({
			url: "/api/json/api",
			dom_id: "#swagger-ui",
			//deepLinking: true,
			presets: [SwaggerUIBundle.presets.apis, SwaggerUIStandalonePreset],
			//plugins: [SwaggerUIBundle.plugins.DownloadUrl],
			layout: "StandaloneLayout"
		});
	}
});
</script>
