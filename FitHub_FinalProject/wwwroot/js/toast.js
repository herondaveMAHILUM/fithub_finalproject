(function () {
    let container = null;

    function getContainer() {
        if (!container) {
            container = document.createElement('div');
            container.id = 'toast-container';
            document.body.appendChild(container);
        }
        return container;
    }

    function dismiss(toast) {
        toast.style.opacity = '0';
        toast.style.transform = 'translateX(calc(100% + 2rem))';
        setTimeout(function () {
            if (toast.parentNode) toast.parentNode.removeChild(toast);
        }, 350);
    }

    window.showToast = function (message, type) {
        type = type || 'info';

        var toast = document.createElement('div');
        toast.className = 'toast toast-' + type;

        // Icon
        var icons = { success: '\u2713', error: '\u2717', warning: '\u26A0', info: '\u2139' };
        var icon = document.createElement('span');
        icon.className = 'toast-icon';
        icon.textContent = icons[type] || icons.info;

        // Message
        var text = document.createElement('span');
        text.className = 'toast-text';
        text.textContent = message;

        // Close button — type="button" so it never submits a form
        var close = document.createElement('button');
        close.type = 'button';
        close.className = 'toast-close';
        close.innerHTML = '&times;';
        close.addEventListener('click', function () { dismiss(toast); });

        toast.appendChild(icon);
        toast.appendChild(text);
        toast.appendChild(close);
        getContainer().appendChild(toast);

        // Use setTimeout(0) so the element is painted in its hidden state first,
        // then we flip to the visible state so the CSS transition actually plays.
        setTimeout(function () {
            toast.classList.add('toast-show');
        }, 20);

        // Auto-dismiss after 5s; pause on hover
        var timer = setTimeout(function () { dismiss(toast); }, 5000);
        toast.addEventListener('mouseenter', function () { clearTimeout(timer); });
        toast.addEventListener('mouseleave', function () {
            timer = setTimeout(function () { dismiss(toast); }, 2000);
        });
    };
})();
