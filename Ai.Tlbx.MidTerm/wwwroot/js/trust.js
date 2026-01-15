/**
 * Trust Page Script
 *
 * Handles platform detection, certificate download, and UI interactions.
 */
(function () {
  // Platform detection
  const ua = navigator.userAgent.toLowerCase();
  const isIOS =
    /iphone|ipad|ipod/.test(ua) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
  const isAndroid = /android/.test(ua);
  const isMac = /macintosh|mac os x/.test(ua) && !isIOS;
  const isWindows = /windows/.test(ua);
  const isLinux = /linux/.test(ua) && !isAndroid;

  const detectedPlatformEl = document.getElementById('detected-platform');
  const iosPanel = document.getElementById('ios-panel');
  const androidPanel = document.getElementById('android-panel');
  const desktopPanel = document.getElementById('desktop-panel');

  if (isIOS) {
    detectedPlatformEl.textContent = 'iOS / iPadOS';
    iosPanel.classList.remove('hidden');
  } else if (isAndroid) {
    detectedPlatformEl.textContent = 'Android';
    androidPanel.classList.remove('hidden');
  } else {
    if (isMac) {
      detectedPlatformEl.textContent = 'macOS';
      showDesktopTab('macos');
    } else if (isLinux) {
      detectedPlatformEl.textContent = 'Linux';
      showDesktopTab('linux');
    } else {
      detectedPlatformEl.textContent = 'Windows';
      showDesktopTab('windows');
    }
    desktopPanel.classList.remove('hidden');
  }

  // Desktop OS tabs
  document.querySelectorAll('.os-tab').forEach((tab) => {
    tab.addEventListener('click', () => showDesktopTab(tab.dataset.os));
  });

  function showDesktopTab(os) {
    document.querySelectorAll('.os-tab').forEach((t) => t.classList.remove('active'));
    document.querySelector(`.os-tab[data-os="${os}"]`).classList.add('active');

    document.querySelectorAll('.os-instructions').forEach((el) => el.classList.add('hidden'));
    document.getElementById(`${os}-instructions`).classList.remove('hidden');
  }

  // Download buttons
  document.getElementById('btn-install-ios').addEventListener('click', () => {
    window.location.href = '/api/certificate/download/mobileconfig';
  });

  document.getElementById('btn-install-android').addEventListener('click', () => {
    window.location.href = '/api/certificate/download/pem';
  });

  document.getElementById('btn-download-pem-desktop').addEventListener('click', () => {
    window.location.href = '/api/certificate/download/pem';
  });
  document.getElementById('btn-download-pem-macos').addEventListener('click', () => {
    window.location.href = '/api/certificate/download/pem';
  });
  document.getElementById('btn-download-pem-linux').addEventListener('click', () => {
    window.location.href = '/api/certificate/download/pem';
  });

  // Copy fingerprint
  document.getElementById('copy-fingerprint').addEventListener('click', async () => {
    const fp = document.getElementById('fingerprint').textContent;
    try {
      await navigator.clipboard.writeText(fp);
      const btn = document.getElementById('copy-fingerprint');
      btn.textContent = 'Copied!';
      setTimeout(() => (btn.textContent = 'Copy'), 2000);
    } catch (err) {
      // Fallback for older browsers
      const textarea = document.createElement('textarea');
      textarea.value = fp;
      document.body.appendChild(textarea);
      textarea.select();
      document.execCommand('copy');
      document.body.removeChild(textarea);
    }
  });

  // Load certificate info
  loadCertificateInfo();

  async function loadCertificateInfo() {
    try {
      const response = await fetch('/api/certificate/share-packet');
      if (!response.ok) return;

      const info = await response.json();

      // Display fingerprint
      document.getElementById('fingerprint').textContent = info.certificate.fingerprintFormatted;

      // Display validity
      const validUntil = new Date(info.certificate.notAfter);
      document.getElementById('cert-valid-until').textContent =
        'Certificate valid until: ' +
        validUntil.toLocaleDateString(undefined, {
          year: 'numeric',
          month: 'long',
          day: 'numeric',
        });

      // Display trusted addresses from certificate SANs
      const endpointsList = document.getElementById('endpoints-list');
      const cert = info.certificate;
      const allAddresses = [...(cert.dnsNames || []), ...(cert.ipAddresses || [])];
      if (allAddresses.length > 0) {
        endpointsList.innerHTML = allAddresses
          .map(
            (addr) =>
              `<div class="endpoint-item">
                <span class="endpoint-addr">${escapeHtml(addr)}</span>
              </div>`
          )
          .join('');
      } else {
        endpointsList.innerHTML = '<p>No addresses in certificate</p>';
      }
    } catch (err) {
      document.getElementById('fingerprint').textContent = 'Error loading certificate info';
    }
  }

  // Simple HTML escaping for addresses
  function escapeHtml(text) {
    const div = document.createElement('div');
    div.textContent = text;
    return div.innerHTML;
  }
})();
