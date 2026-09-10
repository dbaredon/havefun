const { test } = require('node:test');
const assert = require('node:assert/strict');
const { create } = require('../../wwwroot/js/navigation.js');
const settings = {
  href: 'https://dbaredon.github.io/havefun/host/?code=x7k2',
  scriptUrl: 'https://dbaredon.github.io/havefun/js/app.js',
  staticSite: true,
  apiBaseUrl: 'https://gnist.example.com'
};
test('Pages routes preserve /havefun/ and survive refresh via query parameters', () => {
  const nav = create(settings);
  assert.equal(nav.code, 'X7K2');
  assert.equal(nav.route('host', 'x7k2'), 'https://dbaredon.github.io/havefun/host/?code=X7K2');
  assert.equal(nav.route('join', 'x7k2'), 'https://dbaredon.github.io/havefun/join/?code=X7K2');
  assert.equal(nav.route('home'), 'https://dbaredon.github.io/havefun/');
  assert.equal(nav.api('/party'), 'https://gnist.example.com/party');
  assert.equal(nav.api('/api/rooms'), 'https://gnist.example.com/api/rooms');
});
test('unconfigured Pages displays the UI without treating GitHub as an API server', () => {
  const nav = create({ ...settings, apiBaseUrl: '' });
  assert.equal(nav.ready, false);
  assert.throws(() => nav.api('/api/rooms'), /ikke åbnet/);
  assert.equal(nav.route('join'), 'https://dbaredon.github.io/havefun/join/');
});
test('local Razor routes and saved identities remain compatible', () => {
  const nav = create({ href: 'http://localhost:5180/join/X7K2', scriptUrl: 'http://localhost:5180/js/app.js' });
  assert.equal(nav.ready, true);
  assert.equal(nav.code, 'X7K2');
  assert.equal(nav.api('/party'), 'http://localhost:5180/party');
  assert.equal(nav.route('host', 'x7k2'), 'http://localhost:5180/host/X7K2');
  assert.equal(nav.sessionKey('player', 'X7K2'), 'gnist:player:X7K2');
});
test('deployment at the domain root also works', () => {
  const nav = create({ ...settings, href: 'https://example.com/join/?code=X7K2', scriptUrl: 'https://example.com/js/app.js' });
  assert.equal(nav.route('home'), 'https://example.com/');
  assert.equal(nav.route('join', 'X7K2'), 'https://example.com/join/?code=X7K2');
});
test('changing backend does not send the previous backend its stored tokens', () => {
  assert.notEqual(create(settings).sessionKey('host','X7K2'), create({ ...settings, apiBaseUrl:'https://other.example.com' }).sessionKey('host','X7K2'));
});
test('rejects non-HTTPS URLs, credentials, query strings and backend paths', () => {
  for (const apiBaseUrl of ['http://example.com', 'https://user:pass@example.com', 'https://example.com/path', 'https://example.com?secret=x', 'javascript:alert(1)'])
    assert.throws(() => create({ ...settings, apiBaseUrl }));
});
