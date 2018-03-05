class ApiAuth {
    public CachedRealm?: string;
    public CachedNonce?: string;
    public AuthStatus: AuthStatus;
    private ha1?: string;

    constructor(
        public readonly UserUid: string,
        public readonly Token: string) {
    }

    public hasValidNonce() {
        return this.CachedNonce !== undefined;
    }

    public generateResponse(url: string, realm: string | undefined = this.CachedRealm): string {
        if (!this.hasValidNonce())
            throw new Error("Cannot generate response without nonce");

        if (this.ha1 === undefined || this.CachedRealm !== realm) {
            this.CachedRealm = realm;
            this.ha1 = md5(this.UserUid + ":" + realm + ":" + this.Token);
        }
        const ha2 = md5("GET" + ":" + url);
        return md5(this.ha1 + ":" + this.CachedNonce + ":" + ha2);
    }
}

enum AuthStatus {
    None,
    FirstTry,
    Failed,
}
