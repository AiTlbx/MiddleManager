/**
 * Login Page Script
 *
 * Handles login form submission and certificate TOFU display.
 */
(function () {
  const form = document.getElementById('login-form');
  const passwordInput = document.getElementById('password');
  const errorDiv = document.getElementById('error-message');
  const loginBtn = document.getElementById('login-btn');

  form.addEventListener('submit', async (e) => {
    e.preventDefault();

    const password = passwordInput.value;
    if (!password) {
      showError('Password required');
      return;
    }

    loginBtn.disabled = true;
    loginBtn.textContent = 'Logging in...';
    errorDiv.classList.add('hidden');

    try {
      const response = await fetch('/api/auth/login', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ password }),
      });

      const result = await response.json();

      if (response.ok && result.success) {
        window.location.href = '/';
      } else {
        showError(result.error || 'Login failed');
        passwordInput.value = '';
        passwordInput.focus();
      }
    } catch (err) {
      showError('Connection error. Please try again.');
    } finally {
      loginBtn.disabled = false;
      loginBtn.textContent = 'Login';
    }
  });

  function showError(msg) {
    errorDiv.textContent = msg;
    errorDiv.classList.remove('hidden');
  }

  // Certificate TOFU display
  const CERT_HIDDEN_KEY = 'mt-cert-info-hidden';
  const certInfoDiv = document.getElementById('cert-info');
  const certHideBtn = document.getElementById('cert-hide-btn');

  if (localStorage.getItem(CERT_HIDDEN_KEY) !== 'true') {
    loadCertificateInfo();
  }

  certHideBtn.addEventListener('click', () => {
    localStorage.setItem(CERT_HIDDEN_KEY, 'true');
    certInfoDiv.classList.add('hidden');
  });

  async function loadCertificateInfo() {
    try {
      const response = await fetch('/api/certificate/info');
      if (!response.ok) return;

      const info = await response.json();
      if (!info.fingerprint) return;

      // Format fingerprint with colons every 2 chars
      const fp = info.fingerprint.match(/.{1,2}/g).join(':');
      document.getElementById('cert-fingerprint').textContent = fp;

      // Format dates
      const formatDate = (iso) => {
        if (!iso) return '';
        const d = new Date(iso);
        return d.toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
      };

      document.getElementById('cert-valid-from').textContent = 'From: ' + formatDate(info.notBefore);
      document.getElementById('cert-valid-until').textContent = 'Until: ' + formatDate(info.notAfter);

      certInfoDiv.classList.remove('hidden');
    } catch (err) {
      // Silently fail - this is optional info
    }
  }
})();
