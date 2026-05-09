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

    window.showToast = function (message, type) {
        type = type || 'info';
        const toast = document.createElement('div');
        toast.className = 'toast toast-' + type;
        toast.textContent = message;

        getContainer().appendChild(toast);

        requestAnimationFrame(function () {
            toast.classList.add('toast-show');
        });

        setTimeout(function () {
            toast.classList.remove('toast-show');
            toast.addEventListener('transitionend', function () {
                toast.remove();
            }, { once: true });
        }, 4000);
    };
})();
