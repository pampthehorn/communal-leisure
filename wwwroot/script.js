document.addEventListener('DOMContentLoaded', function () {
    var startDateInput = document.getElementById('StartDate');
    var endDateInput = document.getElementById('EndDate');

    if (startDateInput != undefined && endDateInput != undefined) {
        startDateInput.addEventListener('change', function () {
            var startDate = new Date(startDateInput.value);
            var endDate = new Date(endDateInput.value);

            var adjustedEndDate = new Date(startDate.getTime() + (3 * 60 * 60 * 1000));

            if (endDate < adjustedEndDate) {
                endDateInput.value = adjustedEndDate.toISOString().slice(0, 16);
            }
        });
    }

    var tagSelect = document.getElementById('tagSelect');

    if (tagSelect != undefined) {
        tagSelect.addEventListener('change', function () {
            var selectedOptions = Array.from(this.selectedOptions).map(option => option.value);
            document.getElementById('Tags').value = selectedOptions.join(',');
        });

        tagSelect.change();
    }
});
