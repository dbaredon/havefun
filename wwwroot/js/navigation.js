(function (root) {
  'use strict';
  function create({ href, scriptUrl, staticSite = false, apiBaseUrl = '' }) {
    const location = new URL(href);
    const base = new URL('../', scriptUrl);
    const code = (staticSite ? location.searchParams.get('code') || '' : location.pathname.split('/')[2] || '').toUpperCase();
    const backend = apiBaseUrl.trim().replace(/\/+$/, '');
    if (backend) {
      const parsed = new URL(backend);
      if (parsed.protocol !== 'https:' || parsed.pathname !== '/' || parsed.search || parsed.hash || parsed.username || parsed.password)
        throw new Error('Spilserverens adresse skal være en HTTPS-adresse uden sti.');
    }
    return {
      code,
      ready: !staticSite || !!backend,
      // Tokens are scoped to the backend so independent servers cannot reuse another server's session.
      sessionKey: (role, room) => `gnist:${staticSite ? backend + ':' : ''}${role}:${room}`,
      api: path => {
        if (staticSite && !backend) throw new Error('Spillet er ikke åbnet endnu. Prøv igen senere.');
        return new URL(path, staticSite ? backend : location.origin).href;
      },
      route: (kind, room = '') => {
        if (!['home', 'join', 'host'].includes(kind)) throw new Error('Ukendt side.');
        if (staticSite) {
          const url = new URL(kind === 'home' ? './' : `${kind}/`, base);
          if (room) url.searchParams.set('code', room.toUpperCase());
          return url.href;
        }
        return new URL(kind === 'home' ? '/' : `/${kind}${room ? '/' + encodeURIComponent(room.toUpperCase()) : ''}`, location.origin).href;
      }
    };
  }
  root.GnistNavigation = { create };
  if (typeof module !== 'undefined' && module.exports) module.exports = { create };
})(typeof window !== 'undefined' ? window : globalThis);
