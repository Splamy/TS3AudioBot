class Get {
    public static site(site: string, callback?: (ret?: string) => void): void {
        const xhr = new XMLHttpRequest();
        xhr.open("GET", site, true);
        if (callback !== undefined) {
            xhr.onload = (ev) => callback(xhr.responseText);
            xhr.onerror = (ev) => callback(undefined);
        }
        xhr.setRequestHeader("X-Requested-With", "XMLHttpRequest");
        xhr.send();
    }

    public static api(
        site: string,
        callback?: (ret?: string) => void,
        login?: ApiAuth,
        status: AuthStatus = AuthStatus.None): void {

        if (login === undefined)
            throw new Error("Anonymous api call not supported yet");

        if (status === AuthStatus.Failed)
            throw new Error("Auth failed");

        if (!login.hasValidNonce() && status === AuthStatus.None) {
            Get.getNonce(login, (ok) => {
                if (ok)
                    Get.api(site, callback, login, AuthStatus.FirstTry);
                else if (callback !== undefined)
                    callback(undefined);
            });
        } else if (status === AuthStatus.None || status === AuthStatus.FirstTry) {
            const xhr = new XMLHttpRequest();
            const apiSite = "/api/" + site;
            xhr.open("GET", apiSite, true);
            if (callback !== undefined) {
                xhr.onload = (ev) => {
                    const ret = Get.extractNonce(xhr);
                    login.CachedNonce = ret.nonce;
                    login.CachedRealm = ret.realm;
                    callback(xhr.responseText);
                };
                xhr.onerror = (ev) => callback(undefined);
            }

            const response = login.generateResponse(apiSite);
            xhr.setRequestHeader("Authorization",
                "Digest username=\"" + login.UserUid +
                "\", realm=\"" + login.CachedRealm +
                "\", nonce=\"" + login.CachedNonce +
                "\", uri=\"" + apiSite +
                "\", response=\"" + response + "\"");
            login.CachedNonce = undefined;
            xhr.send();
        }
    }

    private static getNonce(login: ApiAuth, callback: (ok: boolean) => void): void {
        const initReq = new XMLHttpRequest();
        initReq.open("GET", "/api/", true);
        initReq.setRequestHeader("Authorization", "Digest username=\"" + login.UserUid + "\"");
        initReq.onload = (ev) => {
            const ret = Get.extractNonce(initReq);
            login.CachedNonce = ret.nonce;
            login.CachedRealm = ret.realm;
            if (callback !== undefined)
                callback(true);
        };
        initReq.onerror = (ev) => callback(false);
        initReq.send();
    }

    private static extractNonce(request: XMLHttpRequest): { nonce: string, realm: string } {
        const digResponse = request.getResponseHeader("WWW-Authenticate");
        if (digResponse === null)
            throw new Error("No auth extracted");
        const digData = digResponse.match(/(realm|nonce)=\"\w+\"*/g);
        if (digData === null)
            throw new Error("No auth extracted");
        let realm: string | undefined;
        let nonce: string | undefined;
        for (const param of digData) {
            const split = param.match(/([^=]*)=\"([^\"]*)\"/)!;
            if (split[1] === "nonce")
                nonce = split[2];
            else if (split[1] === "realm")
                realm = split[2];
        }
        if (realm === undefined || nonce === undefined)
            throw new Error("Invalid auth data");
        return { nonce, realm };
    }
}
