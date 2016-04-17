$(document).ready(main);

var content;

function main()
{
    content = $("#content");
    $("nav a").click(main_click);
}

function load(page)
{
    content.load(page);
}

function main_click(event)
{
    event.preventDefault();
    $(this).blur();
    var newSite = $(this).attr("href");
    load(newSite + "&content=true");
    window.history.pushState('mainpage', '', newSite);
}