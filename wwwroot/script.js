document.addEventListener('DOMContentLoaded', function () {

    var startDateInput = document.getElementById('StartDate');
    var endDateInput = document.getElementById('EndDate');
    var tagSelect = document.getElementById('tagSelect');
    var tagsHiddenInput = document.getElementById('Tags');
    var posterInput = document.getElementById('Poster');
    var actsInput = document.getElementById('Acts');

    const defaults = {
        startDate: startDateInput ? startDateInput.value : "",
        endDate: endDateInput ? endDateInput.value : "",
        acts: actsInput ? actsInput.value : "",
        venue: document.getElementById('Venue') ? document.getElementById('Venue').value : "",
        description: document.getElementById('Description') ? document.getElementById('Description').value : "",
        link: document.getElementById('Link') ? document.getElementById('Link').value : ""
    };

    const toLocalISO = (dateStr) => {
        if (!dateStr) return "";
        const d = new Date(dateStr);
        const offsetMs = d.getTimezoneOffset() * 60 * 1000;
        const localISOTime = (new Date(d.getTime() - offsetMs)).toISOString().slice(0, 16);
        return localISOTime;
    };

    if (startDateInput && endDateInput) {
        startDateInput.addEventListener('change', function () {
            console.log("Start Date Changed, calculating new end date...");

            var startDateVal = startDateInput.value;
            var endDateVal = endDateInput.value;

            if (!startDateVal) return; 

            var startDate = new Date(startDateVal);
            var currentEndDate = new Date(endDateVal);

            var adjustedEndDate = new Date(startDate.getTime() + (3 * 60 * 60 * 1000));

           
            if (isNaN(currentEndDate.getTime()) || currentEndDate < startDate) {

                endDateInput.value = toLocalISO(adjustedEndDate);
            }
        });
    }

    if (tagSelect) {
        tagSelect.addEventListener('change', function () {
            var selectedOptions = Array.from(this.selectedOptions).map(option => option.value);
            if (tagsHiddenInput) {
                tagsHiddenInput.value = selectedOptions.join(',');
            }
        });
        tagSelect.dispatchEvent(new Event('change'));
    }

    if (posterInput) {
        posterInput.addEventListener('change', function () {

            if (this.files && this.files[0]) {
                var file = this.files[0];

                var reader = new FileReader();
                reader.onload = function (e) {
                    var previewEl = document.getElementById('posterPreview');
                    if (previewEl) {
                        previewEl.src = e.target.result;
                        previewEl.style.display = 'block';
                    }
                }
                reader.readAsDataURL(file);

                var formData = new FormData();
                formData.append('file', file);

                if (actsInput) actsInput.placeholder = "Scanning poster for details...";

                fetch('/umbraco/surface/EventSurface/ExtractFromPoster', {
                    method: 'POST',
                    body: formData
                })
                    .then(response => response.json())
                    .then(data => {
                        console.log("AI Data:", data);

                        const findBestVenueMatch = (aiVenueName) => {
                            if (!aiVenueName) return "";


                            let bestMatch = aiVenueName;

                            const datalist = document.getElementById('venues');
                            if (!datalist) return bestMatch;

                            const clean = (str) => str.toLowerCase().replace(/[^a-z0-9]/g, '');
                            const search = clean(aiVenueName);

                 
                            if (search.length < 3) return bestMatch;

                            for (let i = 0; i < datalist.options.length; i++) {
                                const optVal = datalist.options[i].value; 
                                const optClean = clean(optVal);           

                                if (optClean.includes(search)) {
                                    bestMatch = optVal; 
                                    break;
                                }
                            }

                            return bestMatch;
                        };

                        const smartUpdate = (id, aiValue, defaultVal) => {
                            const el = document.getElementById(id);
                            if (el && aiValue) {
                                if (!el.value || el.value === defaultVal) {
                                    el.value = aiValue;
                                    return true;
                                }
                            }
                            return false;
                        };

                        smartUpdate('Acts', data.acts, defaults.acts);
                        smartUpdate('Description', data.description, defaults.description);
                        smartUpdate('Link', data.link, defaults.link);

                        if (data.venue) {
                            const matchedVenue = findBestVenueMatch(data.venue);
                            smartUpdate('Venue', matchedVenue, defaults.venue);
                        }

                        if (data.startDate && startDateInput) {
                            if (!startDateInput.value || startDateInput.value === defaults.startDate) {

                                const formattedDate = toLocalISO(data.startDate);

                                if (formattedDate) {
                                    startDateInput.value = formattedDate;
                                    startDateInput.dispatchEvent(new Event('change'));
                                }
                            }
                        }

      
                        if (data.tag && tagSelect && tagSelect.selectedOptions.length === 0) {
                            const tagMap = {
                                'Gig': '4974eb1e-77f3-4fa5-8e3d-68ba5ea250e9',
                                'Club': '229d6142-0ee3-4f45-bb10-57059f24ce32',
                                'Activity': '0a328ab9-1882-4f62-8059-e857b322bcbc'
                            };

                            const targetUuid = tagMap[data.tag];

                            if (targetUuid) {
                                for (let i = 0; i < tagSelect.options.length; i++) {
                                    if (tagSelect.options[i].value === targetUuid) {
                                        tagSelect.options[i].selected = true;
                                        break;
                                    }
                                }
                                tagSelect.dispatchEvent(new Event('change'));
                            }
                        }
                    })
                    .catch(error => {
                        console.error('Error parsing poster:', error);
                        if (actsInput) actsInput.placeholder = "";
                    });
            }
        });
    }
});