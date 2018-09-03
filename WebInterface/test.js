/// @ts-check
/// <reference path="tssrc/Get.ts" />
/// <reference path="tssrc/ApiAuth.ts" />

async function send_request() {
    try {
        //@ts-ignore
        var token = document.getElementById("tok").value;
        //@ts-ignore
        var request = document.getElementById("req").value;
        var outfld = document.getElementById("output");

        var auth = ApiAuth.Create(token);

        console.log("sending");
        var result = await Get.api("/" + request, auth);

        console.log("yay: " + result);
        outfld.innerText = result;
    }
    catch (err) {
        console.log("ERR " + err.status);
    }
    finally {
        return false;
    }
}

window.onload = function () {
    console.log("ok");
};
