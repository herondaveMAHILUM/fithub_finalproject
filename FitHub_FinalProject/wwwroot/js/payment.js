/**
 * FitHub – Simulated Payment Flow (payment.js)
 * No real APIs are called. This is for demo/school project purposes only.
 */

function initPaymentPage(amountStr) {

    /* ── Method tab switching ───────────────────────── */

    const tabs    = document.querySelectorAll('.method-tab');
    const panels  = document.querySelectorAll('.method-panel');
    const hidden  = document.getElementById('hidden-method');

    tabs.forEach(function (tab) {
        tab.addEventListener('click', function () {
            const method = tab.getAttribute('data-method');

            // Update active tab
            tabs.forEach(function (t) { t.classList.remove('active'); });
            tab.classList.add('active');

            // Show matching panel
            panels.forEach(function (p) { p.classList.remove('active'); });
            const panel = document.getElementById('panel-' + method);
            if (panel) panel.classList.add('active');

            // Update hidden input
            if (hidden) hidden.value = method;
        });
    });

    /* ── Card number formatting ─────────────────────── */

    const cardInput = document.getElementById('card-number');
    if (cardInput) {
        cardInput.addEventListener('input', function () {
            let v = this.value.replace(/\D/g, '');
            v = v.match(/.{1,4}/g)?.join(' ') ?? v;
            this.value = v;

            // Detect card brand
            const brandIcon = document.getElementById('card-brand-icon');
            if (brandIcon) {
                if (/^4/.test(v))       brandIcon.textContent = '💳 Visa';
                else if (/^5[1-5]/.test(v)) brandIcon.textContent = '💳 MC';
                else if (/^3[47]/.test(v))  brandIcon.textContent = '💳 Amex';
                else                        brandIcon.textContent = '💳';
            }
        });
    }

    /* ── Expiry date formatting ─────────────────────── */

    const expiryInput = document.getElementById('card-expiry');
    if (expiryInput) {
        expiryInput.addEventListener('input', function () {
            let v = this.value.replace(/\D/g, '');
            if (v.length >= 3) v = v.substring(0, 2) + ' / ' + v.substring(2, 4);
            this.value = v;
        });
    }

    /* ── Phone number — digits only ─────────────────── */

    ['gcash-number', 'maya-number'].forEach(function (id) {
        const el = document.getElementById(id);
        if (el) {
            el.addEventListener('input', function () {
                this.value = this.value.replace(/\D/g, '');
            });
        }
    });

    /* ── Pay Now button ─────────────────────────────── */

    const payBtn      = document.getElementById('pay-btn');
    const payBtnText  = document.getElementById('pay-btn-text');
    const payBtnLoad  = document.getElementById('pay-btn-loading');
    const overlay     = document.getElementById('payment-overlay');
    const overlayMsg  = document.getElementById('overlay-message');
    const form        = document.getElementById('payment-submit-form');

    if (payBtn && form) {
        payBtn.addEventListener('click', function () {
            const method = hidden ? hidden.value : 'GCash';

            // Show button loading state
            if (payBtnText) payBtnText.style.display = 'none';
            if (payBtnLoad) payBtnLoad.style.display = 'inline-flex';
            payBtn.disabled = true;

            // Show overlay
            if (overlay) overlay.style.display = 'flex';

            // Simulate realistic payment steps
            const steps = getPaymentSteps(method);
            let stepIndex = 0;

            function advanceStep() {
                if (stepIndex < steps.length) {
                    if (overlayMsg) overlayMsg.textContent = steps[stepIndex];
                    stepIndex++;
                    const delay = 600 + Math.random() * 600;  // 600-1200ms per step
                    setTimeout(advanceStep, delay);
                } else {
                    // All steps done — submit the form
                    if (overlayMsg) overlayMsg.textContent = 'Payment approved! Redirecting...';
                    setTimeout(function () {
                        form.submit();
                    }, 700);
                }
            }

            // Small initial delay before first step
            setTimeout(advanceStep, 400);
        });
    }

    /* ── Payment step messages per method ───────────── */

    function getPaymentSteps(method) {
        if (method === 'GCash') {
            return [
                'Connecting to GCash gateway...',
                'Authenticating your account...',
                'Sending payment request to app...',
                'Waiting for app confirmation...',
                'Transactions confirmed by GCash...',
                'Verifying payment reference...',
            ];
        } else if (method === 'Maya') {
            return [
                'Connecting to Maya gateway...',
                'Authenticating your account...',
                'Sending payment request to app...',
                'Waiting for app confirmation...',
                'Transactions confirmed by Maya...',
                'Verifying payment reference...',
            ];
        } else {
            return [
                'Encrypting card details...',
                'Contacting card issuer...',
                'Performing 3D Secure check...',
                'Authorization in progress...',
                'Transactions approved by bank...',
                'Verifying payment reference...',
            ];
        }
    }
}
