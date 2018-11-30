class Get {
    public static async site(site: string): Promise<string> {
        const response = await fetch(site);
        return response.text();
    }

    public static async api<T extends ApiRet>(
        site: Api<T>,
        login: ApiAuth = Main.AuthData): Promise<T | ApiErr> {

        let requestData: RequestInit = {
            cache: "no-cache",
        };

        if (!login.IsAnonymous) {
            requestData.headers = {
                "Authorization": login.getBasic(),
            };
        }

        const apiSite = "/api" + site.done();
        let response: Response;
        try {
            response = await fetch(apiSite, requestData);
        } catch (err) {
            return new ErrorObject(err);
        }

        let json;
        if (response.status === 204) { // || response.headers.get("Content-Length") === "0"
            json = {};
        }
        else {
            try {
                json = await response.json();
            } catch (err) {
                return new ErrorObject(err);
            }
        }

        if (!response.ok) {
            json._httpStatusCode = response.status;
            return new ErrorObject(json);
        } else {
            return json as T;
        }
    }
}

type ApiRet = {} | null | void;
type ApiErr = ErrorObject<ApiError>;
