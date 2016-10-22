/// <reference path="jquery_dev.js" />
$(document).ready(main);

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
    content = $("#content");
    $("nav a").click(navbar_click);
    content_loaded();

    if (ev_devupdate !== null) {
        ev_devupdate.close();
        ev_devupdate = null;
    }
    if ($("#devupdate").length !== 0) {
        ev_devupdate = new EventSource("devupdate");
        ev_devupdate.onmessage = update_site;
    }
}

function navbar_click(event) {
    event.preventDefault();
    $(this).blur();
    var newSite = $(this).attr("href");
    var query = get_query(newSite.substr(newSite.indexOf('?') + 1));
    load("/" + query["page"]);
    window.history.pushState('mainpage', '', newSite);
}

function load(page) {
    lastreq = page;
    content.load(page, content_loaded);
}

// SPECIAL EVENT HANDLER

function content_loaded() {
    // History handler
    $("button[form='searchquery']").remove();
    $("#searchquery :input").each(function () {
        $(this).bind('keyup change click', history_search);
    });

    // PlayControls
    if (ev_playdata !== null) {
        ev_playdata.close();
        ev_playdata = null;
    }
    if (playticker !== null) {
        clearInterval(playticker);
        playticker = null;
    }

    var handler = $("#playhandler");
    if (handler.length !== 0) {
        handler.load("/playcontrols", function () {
            // gather all controls
            playcontrols = {};
            playcontrols.mute = handler.find("#playctrlmute");
            playcontrols.volume = handler.find("input[name='volume']");
            playcontrols.prev = handler.find("#playctrlprev");
            playcontrols.play = handler.find("#playctrlplay");
            playcontrols.next = handler.find("#playctrlnext");
            playcontrols.loop = handler.find("#playctrlloop");
            playcontrols.position = handler.find("input[name='position']");

            // register sse
            ev_playdata = new EventSource("playdata");
            ev_playdata.onmessage = update_song;
            playticker = setInterval(song_position_tick, 1000);

            // register events
            playcontrols.mute.click(function () { $.get("/control?op=volume&volume=0"); }); // todo on/off
            playcontrols.volume.on("input", function () { $.get("/control?op=volume&volume=" + value_to_logarithmic(this.value).toFixed(0)); });
            playcontrols.prev.click(function () { $.get("/control?op=prev"); });
            playcontrols.play.click(function () { $.get("/control?op=play"); });
            playcontrols.next.click(function () { $.get("/control?op=next"); });
            playcontrols.loop.click(function () { $.get("/control?op=loop"); }); // todo on/off
            playcontrols.position.on("change", function () { $.get("/control?op=seek&pos=" + this.value); });
        });
    }
}

function update_site(event) {
    if (event.data === "update") {
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
    playdata = jQuery.parseJSON(event.data);
    var jsSlider = playcontrols.position.get(0);

    if (playdata.hassong === true) {
        playcontrols.volume.get(0).value = parseInt(logarithmic_to_value(playdata.volume));
        jsSlider.max = parseInt(playdata.length.TotalSeconds);
        jsSlider.value = playdata.position.TotalSeconds;
    } else {
        jsSlider.max = 0;
        jsSlider.value = 0;
    }
}

function song_position_tick() {
    if (playdata.paused === false && playdata.hassong === true) {
        var jsSlider = playcontrols.position.get(0);
        if (parseInt(jsSlider.value) < playdata.length.TotalSeconds) {
            jsSlider.value = parseInt(jsSlider.value) + 1;
        }
    }
}

// HISTORY FUNCTIONS

function history_search() {
    var builder = {};
    $("#searchquery :input").each(function () {
        var inp = $(this);
        builder[inp.attr("name")] = inp.val();
    });

    var requestQuery = jQuery.param(builder);

    $.get("/historysearch?" + requestQuery, fill_history);
}

function fill_history(rawdata) {
    var data = jQuery.parseJSON(rawdata);
    hresult = $("#historylist tbody");
    hresult.empty();
    hresult.append(
        "<tr>" +
            "<th>Id</th>" +
            "<th>UserId</th>" +
            "<th class=\"fillwrap\">Title</th>" +
            "<th>Options</th>" +
       "</tr>");

    for (var i = 0; i < data.length; i++) {
        var elem = data[i];
        hresult.append(
            "<tr><td>" + elem["id"] +
            "</td><td>" + elem["userid"] +
            "</td><td class=\"fillwrap\">" + elem["title"] +
            "</td><td>Options</td></tr>");
    }
}
