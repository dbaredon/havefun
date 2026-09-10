"""Check the exported artifact without needing a running .NET server."""
import sys
from pathlib import Path
from html.parser import HTMLParser
from urllib.parse import urlsplit

root = Path(sys.argv[1])

class Page(HTMLParser):
    def __init__(self):
        super().__init__()
        self.links = []
        self.static = False
        self.scripts = []
    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == 'body':
            self.static = attrs.get('data-static-site') == 'true'
        for key in ('src', 'href'):
            if key in attrs:
                self.links.append(attrs[key])
        if tag == 'script':
            self.scripts.append(attrs.get('src', ''))

assert (root / '.nojekyll').exists()
for filename in ['index.html', 'join/index.html', 'host/index.html']:
    text = (root / filename).read_text()
    page = Page()
    page.feed(text)
    assert page.static, filename
    assert '@RouteData' not in text and '@foreach' not in text and 'asp-append-version' not in text, filename
    app = next(s for s in page.scripts if s.endswith('/js/app.js'))
    base = app.removesuffix('js/app.js')
    assert page.scripts.index(base+'config.js') < page.scripts.index(app)
    assert page.scripts.index(base+'js/navigation.js') < page.scripts.index(app)
    for link in page.links:
        if link.startswith('#'):
            continue
        parsed = urlsplit(link)
        assert not parsed.scheme and not parsed.netloc, f'Unexpected external asset: {link}'
        assert parsed.path.startswith(base), f'Link escapes the Pages path: {link}'
        file = root / parsed.path.removeprefix(base)
        if file.is_dir():
            file /= 'index.html'
        assert file.is_file(), f'Missing exported asset/page: {link}'
    assert '/api/rooms/ROOM' not in text
assert 'Start en fest' in (root/'index.html').read_text()
assert 'id="join-form"' in (root/'join/index.html').read_text()
assert 'id="host-game-content"' in (root/'host/index.html').read_text()
assert not (root/'README.md').exists()
print('OK: landing, host, player, config, navigation and every referenced local asset.')
