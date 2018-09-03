class Get {
    public static site(site: string): Promise<string> {
        return new Promise<string>((resolve, reject) => {
            const xhr = new XMLHttpRequest();

            xhr.open("GET", site, true);
            xhr.setRequestHeader("X-Requested-With", "XMLHttpRequest");
            xhr.onload = (_) => {
                if (xhr.status >= 200 && xhr.status < 300) {
                    resolve(xhr.responseText);
                } else {
                    reject(xhr.responseText);
                }
            };
            xhr.onerror = (_) => {
                reject(xhr.responseText);
            };
            xhr.send();
        });
    }

    public static api(
        site: string | Api,
        login: ApiAuth = Main.AuthData): Promise<any> {

        if (site instanceof Api) {
            site = site.done();
        }

        return new Promise<string>((resolve, reject) => {
            const xhr = new XMLHttpRequest();
            if (!login.IsAnonymous) {
                xhr.setRequestHeader("Authorization", login.getBasic());
            }

            const apiSite = "/api" + site;
            xhr.open("GET", apiSite);
            xhr.onload = (_) => {
                if (xhr.status >= 200 && xhr.status < 300) {
                    resolve(JSON.parse(xhr.responseText));
                } else {
                    const error = JSON.parse(xhr.responseText);
                    error.statusCode = xhr.status;
                    reject(error);
                }
            };
            xhr.onerror = (_) => {
                const error = JSON.parse(xhr.responseText);
                error.statusCode = xhr.status;
                reject(error);
            };
            xhr.send();
        });
    }
}
