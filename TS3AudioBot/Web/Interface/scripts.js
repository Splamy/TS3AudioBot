// in case the document is already rendered
if (document.readyState!='loading') main();
// modern browsers
else if (document.addEventListener) document.addEventListener('DOMContentLoaded', main);
// IE <= 8
else document.attachEvent('onreadystatechange', function(){
    if (document.readyState=='complete') main();
});

//+ main page div/section
var content = null;
//+ playcontrols event handler
var ev_playdata = null;
var playcontrols = null;
var playdata = null;
var playticker = null;
//+ devupdate event handler
var ev_devupdate = null;
var lastreq = null;

// MAIN FUNCTIONS

function main() {
    content = document.querySelector("#content");
    addEventAll(document.querySelectorAll("nav a"), "click", navbar_click);
    content_loaded();

    if (ev_devupdate !== null) {
        ev_devupdate.close();
        ev_devupdate = null;
    }
    if (document.querySelector("#devupdate") !== null) {
        ev_devupdate = new EventSource("devupdate");
        ev_devupdate.onmessage = update_site;
    }
}

function navbar_click(event) {
    event.preventDefault();
    //$(this).blur();
    var newSite = this.getAttribute("href");
    var query = get_query(newSite.substr(newSite.indexOf('?') + 1));
    load("/" + query["page"]);
    window.history.pushState('mainpage', '', newSite);
}

function load(page) {
    lastreq = page;
    getAjax(page, function(text) { content.innerHTML = text; content_loaded(); });
}

// SPECIAL EVENT HANDLER

function content_loaded() {
    // History handler
    var searchElem = document.querySelector("button[form='searchquery']");
    if(searchElem !== null) {
        searchElem.parentNode.removeChild(searchElem);
    }

    var inputFields = document.querySelectorAll("#searchquery input");
    addEventAll(inputFields, "click", history_search);
    addEventAll(inputFields, "change", history_search);
    addEventAll(inputFields, "keyup", history_search);

    // PlayControls
    if (ev_playdata !== null) {
        ev_playdata.close();
        ev_playdata = null;
    }
    if (playticker !== null) {
        clearInterval(playticker);
        playticker = null;
    }

    var handler = document.querySelector("#playhandler");
    if (handler !== null) {
            getAjax("/playcontrols.html", function(text) {
            handler.innerHTML = text;
            // gather all controls
            playcontrols = {};
            playcontrols.mute =     handler.querySelector("#playctrlmute");
            playcontrols.volume =   handler.querySelector("input[name='volume']");
            playcontrols.prev =     handler.querySelector("#playctrlprev");
            playcontrols.play =     handler.querySelector("#playctrlplay");
            playcontrols.next =     handler.querySelector("#playctrlnext");
            playcontrols.loop =     handler.querySelector("#playctrlloop");
            playcontrols.position = handler.querySelector("input[name='position']");

            // register sse
            ev_playdata = new EventSource("playdata");
            ev_playdata.onmessage = update_song;
            playticker = setInterval(song_position_tick, 1000);

            // register events
            addEvent(playcontrols.mute,     "click",  function () { getAjax("/control?op=volume&volume=0", null); }); // todo on/off
            addEvent(playcontrols.volume,   "input",  function () { getAjax("/control?op=volume&volume=" + value_to_logarithmic(this.value).toFixed(0), null); });
            addEvent(playcontrols.prev,     "click",  function () { getAjax("/control?op=prev", null); });
            addEvent(playcontrols.play,     "click",  function () { getAjax("/control?op=play", null); });
            addEvent(playcontrols.next,     "click",  function () { getAjax("/control?op=next", null); });
            addEvent(playcontrols.loop,     "click",  function () { getAjax("/control?op=loop", null); }); // todo on/off
            addEvent(playcontrols.position, "change", function () { getAjax("/control?op=seek&pos=" + this.value, null); });
        });
    }
}

function update_site(event) {
    if (event.data === "update" && lastreq !== null) {
        load(lastreq);
    }
}

// HELPER

const slmax = 7.0;
const scale = 100.0;

function value_to_logarithmic(val) {
    if (val < 0) val = 0;
    else if (val > slmax) val = slmax;

    return (1.0 / Math.log10(10 - val) - 1) * (scale / (1.0 / Math.log10(10 - slmax) - 1));
}

function logarithmic_to_value(val) {
    if (val < 0) val = 0;
    else if (val > scale) val = scale;

    return 10 - Math.pow(10, 1.0 / (val / (scale / (1.0 / Math.log10(10 - slmax) - 1)) + 1));
}

function get_query(url) {
    var match,
        search = /([^&=]+)=?([^&]*)/g,
        decode = function (s) { return decodeURIComponent(s.replace(/\+/g, " ")); },
    urlParams = {};
    while (match = search.exec(url))
        urlParams[decode(match[1])] = decode(match[2]);
    return urlParams;
}

// PLAYCONTROLS

function update_song(event) {
    playdata = JSON.parse(event.data);
    var jsSlider = playcontrols.position;

    if (playdata.hassong === true) {
        playcontrols.volume.value = parseInt(logarithmic_to_value(playdata.volume));
        if(playdata.hasOwnProperty("length")) {
            jsSlider.max = parseInt(playdata.length.TotalSeconds);
        }
        if(playdata.hasOwnProperty("position")) {
            jsSlider.value = playdata.position.TotalSeconds;
        }
    } else {
        jsSlider.max = 0;
        jsSlider.value = 0;
    }
}

function song_position_tick() {
    if (playdata.paused === false && playdata.hassong === true) {
        var jsSlider = playcontrols.position;
        if (parseInt(jsSlider.value) < playdata.length.TotalSeconds) {
            jsSlider.value = parseInt(jsSlider.value) + 1;
        }
    }
}

// HISTORY FUNCTIONS

function history_search() {
    var builder = "/historysearch?";
    var elArr = document.querySelectorAll("#searchquery input");
    for (var i = 0; i < elArr.length; i++) {
        var element = elArr[i];
        builder += element.getAttribute("name") + "=" + element.value + "&";
    }

    getAjax(builder.slice(0, -1), fill_history);
}

function fill_history(rawdata) {
    var data = JSON.parse(rawdata);
    hresult = document.querySelector("#historylist tbody");
    hresult.innerHTML =
        "<tr>" +
            "<th>Id</th>" +
            "<th>UserId</th>" +
            "<th class=\"fillwrap\">Title</th>" +
            "<th>Options</th>" +
       "</tr>";

    for (var i = 0; i < data.length; i++) {
        var elem = data[i];
        hresult.innerHTML +=
            "<tr><td>" + elem["id"] +
            "</td><td>" + elem["userid"] +
            "</td><td class=\"fillwrap\">" + elem["title"] +
            "</td><td>Options</td></tr>";
    }
}

// JS HELPER

function addEventAll(elArr, type, handler) {
    var len = elArr.length;
    for (var i=0; i<len; i++) {
        addEvent(elArr[i], type, handler);
    }
}

function addEvent(el, type, handler) {
    if (el.attachEvent) el.attachEvent('on'+type, handler); else el.addEventListener(type, handler);
}

function getAjax(url, success) {
    var xhr = window.XMLHttpRequest ? new XMLHttpRequest() : new ActiveXObject('Microsoft.XMLHTTP');
    xhr.open('GET', url);
    if(success !== null) {
        xhr.onreadystatechange = function() {
            if (xhr.readyState>3 && xhr.status==200) success(xhr.responseText);
        };
    }
    xhr.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
    xhr.send();
    return xhr;
}