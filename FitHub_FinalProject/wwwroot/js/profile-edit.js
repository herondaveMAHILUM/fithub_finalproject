(function () {
    const form = document.querySelector('#personal-info form');
    if (!form) return;

    const editableSelectors = [
        'input[name="fullName"]',
        'input[name="phoneNumber"]',
        'input[name="dateOfBirth"]',
        'select[name="gender"]',
        'input[name="address"]',
        'input[name="ProfilePhoto"]'
    ];

    function getEditables() {
        return editableSelectors
            .map(sel => form.querySelector(sel))
            .filter(el => el !== null);
    }

    const editables = getEditables();
    const original = {};
    editables.forEach(el => {
        original[el.name] = el.type === 'file' ? null : el.value;
        el.disabled = true;
    });

    const buttonRow = form.querySelector('button[type="submit"]').parentElement;
    const saveBtn = buttonRow.querySelector('button[type="submit"]');
    saveBtn.style.display = 'none';

    const editBtn = document.createElement('button');
    editBtn.type = 'button';
    editBtn.textContent = 'Edit';
    editBtn.id = 'profile-edit-btn';

    const cancelBtn = document.createElement('button');
    cancelBtn.type = 'button';
    cancelBtn.textContent = 'Cancel';
    cancelBtn.id = 'profile-cancel-btn';
    cancelBtn.style.display = 'none';

    buttonRow.insertBefore(editBtn, saveBtn);
    buttonRow.insertBefore(cancelBtn, saveBtn);

    editBtn.addEventListener('click', function () {
        editables.forEach(el => { el.disabled = false; });
        editBtn.style.display = 'none';
        saveBtn.style.display = '';
        cancelBtn.style.display = '';
    });

    cancelBtn.addEventListener('click', function () {
        editables.forEach(el => {
            if (el.type === 'file') {
                el.value = '';
            } else if (original[el.name] !== undefined) {
                el.value = original[el.name];
            }
            el.disabled = true;
        });
        editBtn.style.display = '';
        saveBtn.style.display = 'none';
        cancelBtn.style.display = 'none';
    });
})();
