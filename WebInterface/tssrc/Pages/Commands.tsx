/// <reference path="OpenApi.d.ts"/>

class Commands implements IPage {

    init(): Promise<void> {
        let elem = <script src="openapi/swagger-ui-bundle.js"> </script>;
        elem.onload = () => {
            const ui = SwaggerUIBundle({
                url: "/api/json/api",
                dom_id: '#swagger-ui',
                deepLinking: true,
                presets: [
                    SwaggerUIBundle.presets.apis,
                    SwaggerUIStandalonePreset
                ],
                plugins: [
                    SwaggerUIBundle.plugins.DownloadUrl
                ],
                layout: "StandaloneLayout"
            });

            (window as any).ui = ui;
        }
        document.head!.appendChild(elem);
        document.head!.appendChild(<script src="openapi/swagger-ui-standalone-preset.js"> </script>);
        document.head!.appendChild(<link rel="stylesheet" type="text/css" href="openapi/swagger-ui.css" />);

        return Promise.resolve();
    }
    refresh(): Promise<void> { return Promise.resolve(); }
}
