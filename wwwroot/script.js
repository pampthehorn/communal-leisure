document.addEventListener('DOMContentLoaded', function () {

    // 1. Capture References
    var startDateInput = document.getElementById('StartDate');
    var endDateInput = document.getElementById('EndDate');
    var tagSelect = document.getElementById('tagSelect');
    var tagsHiddenInput = document.getElementById('Tags');
    var posterInput = document.getElementById('Poster');
    var actsInput = document.getElementById('Acts');

    // 2. Capture Initial "Default" Values (Snapshot of Razor output)
    // We use these to check if the user has modified the fields manually.
    const defaults = {
        startDate: startDateInput ? startDateInput.value : "",
        endDate: endDateInput ? endDateInput.value : "",
        acts: actsInput ? actsInput.value : "",
        venue: document.getElementById('Venue') ? document.getElementById('Venue').value : "",
        description: document.getElementById('Description') ? document.getElementById('Description').value : "",
        link: document.getElementById('Link') ? document.getElementById('Link').value : ""
    };

    // Helper: Formats ISO date string to 'YYYY-MM-DDTHH:mm' (required for datetime-local)
    const toLocalISO = (dateStr) => {
        if (!dateStr) return "";
        const d = new Date(dateStr);
        // Adjust for timezone offset to prevent shifting when slicing
        const offsetMs = d.getTimezoneOffset() * 60 * 1000;
        const localISOTime = (new Date(d.getTime() - offsetMs)).toISOString().slice(0, 16);
        return localISOTime;
    };

    // 3. Date Logic (Adjust End Date based on Start Date)
    if (startDateInput && endDateInput) {
        startDateInput.addEventListener('change', function () {
            console.log("Start Date Changed, calculating new end date...");

            var startDateVal = startDateInput.value;
            var endDateVal = endDateInput.value;

            if (!startDateVal) return; // Exit if empty

            var startDate = new Date(startDateVal);
            var currentEndDate = new Date(endDateVal);

            // Calculate Start + 3 Hours
            var adjustedEndDate = new Date(startDate.getTime() + (3 * 60 * 60 * 1000));

            // Only update EndDate if it's currently earlier than the new minimum (or if it's still the default)
            // Note: We check if currentEndDate is invalid (user cleared it) or less than adjusted
            if (isNaN(currentEndDate.getTime()) || currentEndDate < startDate) {

                // Format strictly for input type="datetime-local"
                endDateInput.value = toLocalISO(adjustedEndDate);
            }
        });
    }

    // 4. Tag Selection Logic
    if (tagSelect) {
        tagSelect.addEventListener('change', function () {
            var selectedOptions = Array.from(this.selectedOptions).map(option => option.value);
            if (tagsHiddenInput) {
                tagsHiddenInput.value = selectedOptions.join(',');
            }
        });
        // Initialize hidden input
        tagSelect.dispatchEvent(new Event('change'));
    }

    // 5. Poster Upload & AI Extraction
    if (posterInput) {
        posterInput.addEventListener('change', function () {

            if (this.files && this.files[0]) {
                var file = this.files[0];

                var reader = new FileReader();
                reader.onload = function (e) {
                    var previewEl = document.getElementById('posterPreview');
                    if (previewEl) {
                        previewEl.src = e.target.result;
                        previewEl.style.display = 'block'; // Show the image
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

                        // --- Helper: Fuzzy Venue Matcher ---
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

                        // Helper to update field only if empty or equals default
                        const smartUpdate = (id, aiValue, defaultVal) => {
                            const el = document.getElementById(id);
                            if (el && aiValue) {
                                // Only overwrite if currently empty OR currently equals the server default
                                if (!el.value || el.value === defaultVal) {
                                    el.value = aiValue;
                                    return true; // indicated updated
                                }
                            }
                            return false; // indicated skipped
                        };

                        smartUpdate('Acts', data.acts, defaults.acts);
                        smartUpdate('Description', data.description, defaults.description);
                        smartUpdate('Link', data.link, defaults.link);

                        if (data.venue) {
                            const matchedVenue = findBestVenueMatch(data.venue);
                            smartUpdate('Venue', matchedVenue, defaults.venue);
                        }

                        // --- Date Handling ---
                        if (data.startDate && startDateInput) {
                            // Check if User has touched the start date
                            if (!startDateInput.value || startDateInput.value === defaults.startDate) {

                                // Format the AI date specifically for the input
                                const formattedDate = toLocalISO(data.startDate);

                                if (formattedDate) {
                                    startDateInput.value = formattedDate;
                                    // Manually trigger the change event so the EndDate logic runs
                                    startDateInput.dispatchEvent(new Event('change'));
                                }
                            }
                        }

                        // --- Tag Handling ---
                        // Only update tags if none are currently selected
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