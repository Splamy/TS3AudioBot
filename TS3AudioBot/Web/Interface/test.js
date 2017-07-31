var currentRealm = undefined;
var currentNonce = undefined;

function extractNonce(request) {
    var digResponse = request.getResponseHeader("WWW-Authenticate");
    if (digResponse === null || digResponse === undefined)
        return;
    var digData = digResponse.match(/(realm|nonce)=\"\w+\"*/g);
    var realm = undefined;
    var nonce = undefined;
    for (var i = 0; i < digData.length; i++) {
        if (digData[i].startsWith("nonce="))
            currentNonce = digData[i].match(/=\"(\w+)\"/)[1];
        else if (digData[i].startsWith("realm="))
            currentRealm = digData[i].match(/=\"(\w+)\"/)[1];
    }
}

function httpGet(theUrl, username, token) {
    if (currentNonce === undefined) {
        var initReq = new XMLHttpRequest();
        initReq.open("GET", "/api/", false);
        initReq.setRequestHeader("Authorization", "Digest username=\"" + username + "\"");
        initReq.send(null);
        extractNonce(initReq);
    }
    if (currentNonce === undefined) {
        console.log("could not authenticate");
        return;
    }

    var ha1 = md5(username + ":" + currentRealm + ":" + token);
    var ha2 = md5("GET" + ":" + theUrl);
    var response = md5(ha1 + ":" + currentNonce + ":" + ha2);

    var xmlHttp = new XMLHttpRequest();
    xmlHttp.open("GET", theUrl, false);
    xmlHttp.setRequestHeader("Authorization", "Digest username=\"" + username + "\", realm=\"" + currentRealm + "\", nonce=\"" + currentNonce + "\", uri=\"" + theUrl + "\", response=\"" + response + "\"");

    xmlHttp.send(null);
    extractNonce(xmlHttp);
    return xmlHttp.responseText;
}

function send_request() {
    try {
        var username = document.getElementById("uid").value;
        var token = document.getElementById("tok").value;
        var request = document.getElementById("req").value;

        var result = httpGet("/api/" + request, username, token);
        console.log(result);
        var outfld = document.getElementById("output");
        outfld.innerText = result;
    }
    catch (err) {
        console.log("ERR" + err);
    }
    finally {
        return false;
    }
}

window.onload = function () {
    console.log("ok");
};