$(document).ready(main);

var content = null;
var playevent = null;

var devupdate = null;
var lastreq = null;

function main() {
    content = $("#content");
    $("nav a").click(main_click);
    register_handler();

    if (devupdate !== null) {
        devupdate.close();
    }
    if ($("#devupdate").length !== 0) {
        devupdate = new EventSource("devupdate");
        devupdate.onmessage = function (event) {
            if (event.data == "update") {
                load(lastreq);
            }
        };
    }
}

function load(page) {
    lastreq = page;
    content.load(page, register_handler);
}

function register_handler() {
    // History handler
    $("button[form='searchquery']").remove();
    $("#searchquery :input").each(function () {
        $(this).bind('keyup change click', history_search);
    });
    // PlayControls
    if (playevent !== null) {
        playevent.close();
    }
    var handler = $("#playhandler");
    if (handler.length != 0) {
        playevent = new EventSource("playdata");
        playevent.onmessage = function (event) {
            var data = jQuery.parseJSON(event.data);
            if (data.hassong) {
                // TODO ...
            }
        };
    }
}

function main_click(event) {
    event.preventDefault();
    $(this).blur();
    var newSite = $(this).attr("href");
    var query = get_query(newSite.substr(newSite.indexOf('?') + 1));
    load("/" + query["page"]);
    window.history.pushState('mainpage', '', newSite);
}

function get_query(url) {
    var match,
        pl = /\+/g,  // Regex for replacing addition symbol with a space
        search = /([^&=]+)=?([^&]*)/g,
        decode = function (s) { return decodeURIComponent(s.replace(pl, " ")); },
    urlParams = {};
    while (match = search.exec(url))
        urlParams[decode(match[1])] = decode(match[2]);
    return urlParams;
}

function history_search_click(event) {
    event.preventDefault();
    history_search();
}

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

function log_slide(val) {
    const top = 7.0;
    const scale = 100.0;

    if (val < 0) val = 0;
    else if (val > top) val = top;

    return (1.0 / Math.log10(10 - val) - 1) * (scale / (1.0 / Math.log10(10 - top) - 1));
}