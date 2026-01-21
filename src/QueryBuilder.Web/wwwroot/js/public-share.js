(() => {
  const config = window.publicShareConfig;
  if (!config) {
    return;
  }

  const toggle = document.getElementById('publicShareToggle');
  const expiry = document.getElementById('publicShareExpiry');
  const urlInput = document.getElementById('publicShareUrl');
  const embedInput = document.getElementById('publicShareEmbed');
  const regenerateBtn = document.getElementById('publicShareRegenerate');

  const csrf = document.querySelector('input[name="__RequestVerificationToken"]');
  const csrfToken = csrf ? csrf.value : '';

  const setUrls = (token) => {
    if (!token) {
      urlInput.value = '';
      embedInput.value = '';
      return;
    }

    const baseUrl = `${window.location.origin}/public/d/${token}`;
    const embedUrl = `${window.location.origin}/public/embed/d/${token}`;
    urlInput.value = baseUrl;
    embedInput.value = `<iframe src="${embedUrl}" width="100%" height="600" frameborder="0" loading="lazy"></iframe>`;
  };

  const setExpiryValue = (expiresAt) => {
    if (!expiresAt) {
      expiry.value = '';
      return;
    }
    const expiryDate = new Date(expiresAt);
    const days = Math.round((expiryDate - new Date()) / (1000 * 60 * 60 * 24));
    if (days <= 7) {
      expiry.value = '7';
    } else if (days <= 30) {
      expiry.value = '30';
    } else {
      expiry.value = '';
    }
  };

  setUrls(config.enabled ? config.token : '');
  setExpiryValue(config.expiresAt);

  const buildExpiry = () => {
    if (!expiry.value) {
      return null;
    }
    const days = parseInt(expiry.value, 10);
    const future = new Date();
    future.setDate(future.getDate() + days);
    return future.toISOString();
  };

  const post = async (path, payload) => {
    const response = await fetch(path, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'RequestVerificationToken': csrfToken
      },
      body: JSON.stringify(payload || {})
    });
    if (!response.ok) {
      throw new Error('Failed to update share settings.');
    }
    return await response.json();
  };

  const refreshFromResponse = (data) => {
    config.token = data.token;
    config.enabled = data.isEnabled;
    config.expiresAt = data.expiresAt;
    setUrls(data.isEnabled ? data.token : '');
    setExpiryValue(data.expiresAt);
  };

  if (toggle) {
    toggle.addEventListener('change', async () => {
      try {
        if (toggle.checked) {
          const data = await post(`/dashboards/${config.dashboardId}/share/enable`, { expiresAt: buildExpiry() });
          refreshFromResponse(data);
        } else {
          const data = await post(`/dashboards/${config.dashboardId}/share/disable`, {});
          refreshFromResponse(data);
        }
      } catch (err) {
        toggle.checked = config.enabled;
        console.error(err);
      }
    });
  }

  if (expiry) {
    expiry.addEventListener('change', async () => {
      if (!config.enabled) {
        return;
      }
      try {
        const data = await post(`/dashboards/${config.dashboardId}/share/set-expiration`, { expiresAt: buildExpiry() });
        refreshFromResponse(data);
      } catch (err) {
        setExpiryValue(config.expiresAt);
        console.error(err);
      }
    });
  }

  if (regenerateBtn) {
    regenerateBtn.addEventListener('click', async () => {
      if (!config.enabled) {
        toggle.checked = true;
      }
      try {
        const data = await post(`/dashboards/${config.dashboardId}/share/regenerate`, { expiresAt: buildExpiry() });
        refreshFromResponse(data);
      } catch (err) {
        console.error(err);
      }
    });
  }

  document.querySelectorAll('[data-copy-target]').forEach((button) => {
    button.addEventListener('click', async () => {
      const targetId = button.getAttribute('data-copy-target');
      const field = document.getElementById(targetId);
      if (!field) {
        return;
      }
      try {
        await navigator.clipboard.writeText(field.value);
        button.textContent = 'Copied';
        setTimeout(() => {
          button.textContent = 'Copy';
        }, 1500);
      } catch (err) {
        console.error(err);
      }
    });
  });
})();
