$(document).ready(main);

var content;

function main() {
    content = $("#content");
    $("nav a").click(main_click);
    register_handler();
}

function load(page) {
    content.load(page, register_handler);
}

function register_handler() {
    // History handler
    $("button[form='searchquery']").remove();
    $("#searchquery :input").each(function () {
        $(this).bind('keyup change click', history_search);
    });
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
            "<th class=\"autowrap\">Id</th>" +
            "<th class=\"autowrap\">UserId</th>" +
            "<th class=\"fillwrap\">Title</th>" +
            "<th class=\"autowrap\">Options</th>" +
       "</tr>");

    for (var i = 0; i < data.length; i++) {
        var elem = data[i];
        hresult.append(
            "<tr><td class=\"autowrap\">" + elem["id"] +
            "</td><td class=\"autowrap\">" + elem["userid"] +
            "</td><td class=\"fillwrap\">" + elem["title"] +
            "</td><td class=\"autowrap\">Options</td></tr>");
    }
}