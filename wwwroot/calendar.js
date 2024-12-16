function addToCalendar(event) {
  var file = formatIcsFile(event);
  downloadIcsFile(file, event.name);
}

function formatIcsFile(event) {
  const uid = `${new Date().toISOString()}@communalleisure.com`;
  const start = formatDateForIcs(new Date(event.startDate));
  const end = formatDateForIcs(new Date(event.endDate));
  const now = formatDateForIcs(new Date());

  const calendarEvent = [
    "BEGIN:VCALENDAR",
    "PRODID:Calendar",
    'BEGIN:VEVENT',
    'UID:' + uid,
    'CLASS:PUBLIC',
    'DESCRIPTION:' + event.description,
    'DTSTAMP;VALUE=DATE-TIME:' + now,
    'DTSTART;VALUE=DATE-TIME:' + start,
    'DTEND;VALUE=DATE-TIME:' + end,
    'LOCATION:' + event.venue,
    'SUMMARY;LANGUAGE=en-us:' + event.name,
    'TRANSP:TRANSPARENT',
    'END:VEVENT',
    'END:VCALENDAR'
  ];

  return calendarEvent.join("\n");
}

function downloadIcsFile(content, name) {
  const a = document.createElement("a");
  const blob = new Blob([content], {type: "text/calendar"});
  const url = URL.createObjectURL(blob);

  a.setAttribute("href", url);
  a.setAttribute("download", `${name}.ics`);

  a.click();
}

function formatDateForIcs(date) {
  const year = ("0000" + (date.getFullYear().toString())).slice(-4);
  const month = ("00" + ((date.getMonth() + 1).toString())).slice(-2);
  const day = ("00" + ((date.getDate()).toString())).slice(-2);
  const hours = ("00" + (date.getHours().toString())).slice(-2);
  const minutes = ("00" + (date.getMinutes().toString())).slice(-2);
  const seconds = ("00" + (date.getSeconds().toString())).slice(-2);
  const time = 'T' + hours + minutes + seconds;

  return year + month + day + time;
}

export { addToCalendar } 
