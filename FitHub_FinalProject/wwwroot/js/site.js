// Page transition animation
(function () {
    var main = document.querySelector('main');
    if (!main) return;

    document.addEventListener('click', function (e) {
        var link = e.target.closest('a');
        if (!link) return;

        var href = link.getAttribute('href');
        // Skip: no href, external, anchor-only, new tab, or javascript:
        if (!href || href.startsWith('#') || href.startsWith('javascript') ||
            href.startsWith('http') || href.startsWith('mailto') ||
            link.target === '_blank' || e.ctrlKey || e.metaKey || e.shiftKey) {
            return;
        }

        e.preventDefault();
        main.classList.add('page-exit');

        setTimeout(function () {
            window.location.href = href;
        }, 220);
    });
})();
