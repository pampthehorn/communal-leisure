import { addToCalendar } from "./calendar.js";


if (document.getElementById("filters") != undefined) {
    document.getElementById("filters").onchange = function (e) { this.submit(); }

}
