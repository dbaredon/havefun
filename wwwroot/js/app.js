(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const page = document.body.dataset.page;
  const site = GnistNavigation.create({
    href: location.href,
    scriptUrl: document.querySelector('script[data-gnist-app]').src,
    staticSite: document.body.dataset.staticSite === 'true',
    apiBaseUrl: window.GNIST_CONFIG?.apiBaseUrl || 'https://gnist-l1pd.onrender.com'
  });
  const escape = value => String(value ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
  const games = {
    cookie: ['Klikamok', 'Tryk så hurtigt, du kan. Hvert klik tæller.', '◉', 'HURTIGE FINGRE'],
    timing: ['På sekundet', 'Start dit indre ur. Stop så tæt på målet som muligt.', '◷', 'MAVEFORNEMMELSE'],
    reaction: ['Lynhurtig', 'Vent på NU. Trykker du for tidligt, er det en tyvstart.', 'ϟ', 'REFLEKSER'],
    math: ['Hovedbrud', 'Regn den ud. Før alle andre.', '+', 'HURTIGE HOVEDER'],
    pattern: ['Tal mønster', 'Find det manglende tal i rækken.', '#', 'LOGIK'],
    duel: ['Duellen', 'Sten slår saks. Saks slår papir. Papir slår sten.', '⚔', 'ÉN MOD ÉN'],
    wheel: ['Skæbnehjulet', 'Læn jer tilbage. Lad skæbnen tage over.', '✳', 'REN TILFÆLDIGHED'],
    bomb: ['Tikkende bombe', 'Send den videre. Ingen ved, hvornår den springer.', '✹', 'VARME HÆNDER']
  };
  let connection, room, me, own = {}, ownRound, renderKey = '', offset = 0, sequence = Date.now(), lastBuzz = '', lastStarted = '', hydratedSettings = false, toastTimer;
  const code = site.code;
  const storageKey = site.sessionKey('player', code);
  const getSession = key => { try { return JSON.parse(sessionStorage.getItem(key) || 'null'); } catch { return null; } };
  const saveSession = (key, value) => sessionStorage.setItem(key, JSON.stringify(value));
  const nameOf = id => room?.players.find(p => p.id === id)?.name || 'Spiller';
  const now = () => Date.now() + offset;
  const buzz = pattern => { try { navigator.vibrate?.(pattern); } catch {} };
  function toast(message) {
    $('toast').textContent = message; $('toast').hidden = false;
    clearTimeout(toastTimer); toastTimer = setTimeout(() => $('toast').hidden = true, 6500);
  }
  function friendly(error) {
    const message = error?.message || '';
    if (message.includes('HubException:')) return message.split('HubException:').pop().trim();
    return 'Vi kunne ikke forbinde. Tjek dit netværk, og prøv igen.';
  }
  function status(text, online = false) { if ($('connection')) { $('connection').textContent = text; $('connection').classList.toggle('online', online); } }
  async function invoke(method, ...args) {
    if (connection?.state !== signalR.HubConnectionState.Connected) { toast('Forbindelsen er afbrudt. Vi prøver at finde festen igen.'); return false; }
    try { await connection.invoke(method, ...args); return true; } catch (error) { toast(friendly(error)); return false; }
  }
  async function create() {
    $('create').disabled = true;
    try {
      const response = await fetch(site.api('/api/rooms'), { method:'POST', headers:{'X-Gnist-Request':'create'} });
      if (!response.ok) { const data = await response.json().catch(() => ({})); throw new Error(data.error || 'Der er travlt lige nu. Prøv igen om lidt.'); }
      const data = await response.json();
      saveSession(site.sessionKey('host', data.code), data.hostToken);
      location.href = site.route('host', data.code);
    } catch (error) { toast(error instanceof TypeError ? 'Spilserveren svarer ikke endnu. Vent et øjeblik, og prøv igen.' : error.message); $('create').disabled = false; }
  }
  $('create')?.addEventListener('click', create);
  $('fullscreen')?.addEventListener('click', async () => {
    try { if (document.fullscreenElement) await document.exitFullscreen(); else await document.documentElement.requestFullscreen(); }
    catch { toast('Fuld skærm understøttes ikke i denne browser.'); }
  });
  if ($('room-code')) $('room-code').value = code;
  if (!site.ready) {
    const notice = document.createElement('div');
    notice.className = 'notice'; notice.setAttribute('role', 'status');
    notice.textContent = 'Velkommen til GNIST. Spillet er ikke åbnet endnu — kom tilbage snart!';
    document.querySelector('main')?.prepend(notice);
    document.querySelectorAll('#create, #join-form button, #quick-start, [data-game]').forEach(button => button.disabled = true);
    status('Spillet åbner snart');
    return;
  }
  if (page === 'home' || !['host', 'player'].includes(page)) return;

  if (!window.signalR) { toast('Spilforbindelsen kunne ikke indlæses. Genindlæs siden.'); return; }
  connection = new signalR.HubConnectionBuilder().withUrl(site.api('/party'), { withCredentials: false })
    .withAutomaticReconnect({ nextRetryDelayInMilliseconds: context => Math.min(500 + context.previousRetryCount * 1000, 5000) })
    .configureLogging(signalR.LogLevel.Warning).build();
  connection.on(page === 'host' ? 'HostState' : 'State', state => {
    room = state; offset = Date.parse(state.serverNow) - Date.now();
    if (page === 'host') renderHost(); else if (me) renderPlayer();
  });
  connection.on('Own', message => {
    own = message.state || {}; ownRound = message.roundId;
    if (page === 'player' && me) renderPlayer();
  });
  connection.on('Closed', message => {
    toast(message); status('Festen er slut'); connection.stop(); room = null;
    if (page === 'host') { $('host-error').textContent = message; $('host-error').hidden = false; }
    else { sessionStorage.removeItem(storageKey); $('player-room').hidden = true; $('join-panel').hidden = false; }
  });
  connection.onreconnecting(() => { status('Forbindelsen er væk · prøver igen …'); document.querySelectorAll('.action-pad').forEach(b => b.disabled = true); });
  connection.onreconnected(async () => {
    renderKey = '';
    try { await identify(); status('Forbundet', true); } catch (error) { toast(friendly(error)); }
  });
  connection.onclose(() => status('Ikke forbundet · genindlæs for at prøve igen'));
  async function identify() {
    if (page === 'host') await connection.invoke('Host', code, getSession(site.sessionKey('host', code)) || '');
    else if (me) {
      me = await connection.invoke('Join', me.code, me.name, me.playerToken);
      saveSession(site.sessionKey('player', me.code), me); showPlayer();
    }
  }
  async function connect() {
    if (connection.state === signalR.HubConnectionState.Disconnected) { status('Forbinder …'); await connection.start(); }
    status('Forbundet', true);
  }
  function showPlayer() { $('join-panel').hidden = true; $('player-room').hidden = false; if (room) renderPlayer(); }
  $('join-form')?.addEventListener('submit', async event => {
    event.preventDefault();
    const button = event.target.querySelector('button'); button.disabled = true;
    try {
      await connect();
      const joinCode = $('room-code').value.trim().toUpperCase();
      const saved = getSession(site.sessionKey('player', joinCode));
      me = await connection.invoke('Join', joinCode, $('player-name').value.trim(), saved?.playerToken || null);
      saveSession(site.sessionKey('player', me.code), me);
      // Keep the joined URL on refresh, without creating another player.
      if (code !== me.code) { location.replace(site.route('join', me.code)); return; }
      showPlayer(); buzz([40, 50, 40]);
    } catch (error) { toast(friendly(error)); }
    finally { button.disabled = false; }
  });
  $('leave')?.addEventListener('click', async () => {
    if (!await invoke('Leave')) return;
    sessionStorage.removeItem(site.sessionKey('player', me.code)); me = null; await connection.stop(); location.href = site.route('join');
  });
  function settings() { return { clickSeconds: 10 }; }
  async function start(kind, quick) { await invoke('Start', kind, quick, settings()); }
  $('quick-start')?.addEventListener('click', () => start(null, true));
  $('back-lobby')?.addEventListener('click', () => invoke('Lobby'));
  $('show-leaderboard')?.addEventListener('click', () => invoke('ShowLeaderboard'));
  $('show-results')?.addEventListener('click', () => invoke('ShowResults'));
  $('end-results')?.addEventListener('click', () => invoke('EndResults'));
  $('copy-link')?.addEventListener('click', async () => {
    try { await navigator.clipboard.writeText(site.route('join', code)); toast('Invitationslinket er kopieret.'); }
    catch { toast(`Invitér vennerne: ${site.route('join', code)}`); }
  });
  if (page === 'host') {
    $('host-code').textContent = code;
    document.querySelector('.big-room-code').textContent = code;
    $('qr').src = site.api(`/api/rooms/${encodeURIComponent(code)}/qr`) + (document.body.dataset.staticSite === 'true' ? '?frontend=pages' : '');
    $('join-address').textContent = site.route('join').replace(/^https?:\/\//, '');
    $('lan-note').hidden = !['localhost','127.0.0.1','[::1]'].includes(location.hostname);
    connect().then(identify).catch(error => { $('host-error').textContent = friendly(error); $('host-error').hidden = false; });
  } else {
    me = getSession(storageKey);
    if (me) connect().then(identify).catch(error => { toast(friendly(error)); me = null; sessionStorage.removeItem(storageKey); });
  }
  function confetti() {
    if (matchMedia('(prefers-reduced-motion: reduce)').matches) return;
    for (let i=0;i<35;i++) {
      const dot = document.createElement('i'); dot.className='confetti';
      dot.style.left = `${Math.random()*100}%`; dot.style.background = ['#c4f563','#c5b6f4','#efa99a'][i%3];
      dot.style.animationDelay = `${Math.random()*.6}s`; document.body.append(dot); setTimeout(() => dot.remove(), 3600);
    }
  }
  function intro(game, phone = false) {
    const info = games[game.kind];
    const target = game.kind === 'timing' ? `<p class="lime">Dit mål: ${Number(game.state.target).toLocaleString('da-DK', { minimumFractionDigits: 1, maximumFractionDigits: 1 })} sekunder</p>` : '';
    return `<div class="${phone?'controller':'intro'}"><div class="eyebrow">${info[3]}</div>${game.phase==='Countdown' ? `<h1>${info[0]}</h1><div class="countdown" data-countdown="${game.startsAt}">3</div>` : `<div class="huge-icon">${info[2]}</div><h1>${info[0]}</h1><p>${info[1]}</p>`}${target}</div>`;
  }
  function heading(game, subtitle='') {
    return `<div class="game-heading"><h1>${games[game.kind][0]}</h1>${subtitle?`<p>${subtitle}</p>`:''}${game.state.endsAt && !['timing','duel','wheel'].includes(game.kind) ? `<div class="timer" data-timer="${game.state.endsAt}"></div>`:''}</div>`;
  }
  function resultsHtml(game) {
    const top = game.results.find(r => r.winner) || game.results.find(r => r.bottom) || game.results[0];
    const title = game.kind === 'wheel' ? `${escape(top?.name)} — det blev dig!` : game.kind === 'bomb' ? `${escape(game.results.find(r=>r.bottom)?.name)} fik bomben!` : top?.winner ? `${escape(top.name)} tager den!` : 'Sikke en runde!';
    const duelReveal = game.kind === 'duel' ? `<div class="duel-stage">${game.state.selected.map((id,i)=>`${i?'<div class="versus">VS</div>':''}<div class="duelist"><h2>${escape(nameOf(id))}</h2><span class="duel-choice">${choiceSymbol(game.state.choices?.[id])}</span></div>`).join('')}</div>` : '';
    const elapsed = room.resultsLeaderboardOpen ? room.resultsElapsedMs : (room.resultsStartedAt ? Math.max(0, now() - Date.parse(room.resultsStartedAt)) : 0);
    const roundBoard = `<section class="scoreboard"><h2>Rundens leaderboard</h2>${game.results.map((r,i)=>`<div class="score-line"><span>${r.rank}. ${escape(r.name)}</span><span>${r.points ? `+${r.points} point · ` : ''}${escape(r.detail)}</span></div>`).join('')}</section>`;
    const totalBoard = `<section class="scoreboard"><h2>Samlet leaderboard</h2><p class="muted">Point fra alle runder</p>${[...room.players].sort((a,b)=>b.score-a.score).map((p,i)=>`<div class="score-line"><span>${i+1}. ${escape(p.name)}</span><strong>${p.score} point</strong></div>`).join('')}</section>`;
    const spotlightResults = [top, [...game.results].reverse().find(r => r.playerId !== top?.playerId) || top].filter(Boolean);
    const drinkerResults = game.kind === 'bomb' ? game.results.filter(r => r.bottom) : game.kind === 'wheel' ? [] : game.results.slice(-2);
    const drinkers = drinkerResults.map(r => escape(r.name)).join(' & ');
    const drinkingMessage = drinkerResults.length ? `<div class="notice drinking-message"><strong>${drinkers}, skål!</strong><br />Tag en tår for holdet.</div>` : '';
    const spotlight = `<div class="results-list">${spotlightResults.map((r,i)=>`<div class="result-row ${i===0?'winner':'bottom'}"><span class="rank">${i===0?'✦':'!'}</span><div class="result-person"><strong>${escape(r.name)}</strong>${r.points?`<small>+${r.points} point</small>`:''}</div><div class="result-detail">${escape(r.detail)}</div></div>`).join('')}</div>`;
    const body = room.resultsLeaderboardOpen ? totalBoard : `${duelReveal}${spotlight}${drinkingMessage}<p class="muted" style="text-align:center">Se leaderboardet, når I vil.</p>`;
    return `<div class="results-title"><div class="eyebrow">RUNDE ${room.round} · ${games[game.kind][0]}</div><h1>${title}</h1>${game.kind==='math'?`<p class="muted">${escape(game.state.expression)} = ${game.state.answer}</p>`:''}</div>${body}`;
  }
  function renderHost() {
    $('host-error').hidden = true;
    $('recovered-notice').hidden = !room.recovered;
    const history = room.history || [];
    $('round-history').hidden = history.length === 0;
    const historyKey = history.map(r => `${r.number}:${r.status}`).join(',');
    if ($('round-history-list').dataset.key !== historyKey) {
      $('round-history-list').dataset.key = historyKey;
      $('round-history-list').innerHTML = history.map(r => `<div class="game-card"><h3>Runde ${r.number} · ${games[r.kind]?.[0] || escape(r.kind)}</h3><p>${({Completed:'Afsluttet', Interrupted:'Afbrudt ved genstart', Cancelled:'Stoppet af værten'})[r.status] || 'I gang'}</p>${r.results.map(result => `<div class="score-line"><span>${result.rank}. ${escape(result.name)}</span><span>${result.points ? `+${result.points} point · ` : ''}${escape(result.detail)}</span></div>`).join('')}</div>`).join('');
    }
    hydratedSettings = true;
    const game = room.game;
    $('host-lobby').hidden = !!game; $('host-game').hidden = !game;
    $('player-count').textContent = `${room.players.filter(p=>p.connected).length} spillere`;
    const listKey = room.players.map(p=>p.id+p.connected).join();
    if ($('player-list').dataset.key !== listKey) {
      $('player-list').dataset.key = listKey;
      $('player-list').innerHTML = room.players.length ? room.players.map(p=>`<div class="player-badge ${p.connected?'':'offline'}"><span class="avatar">${escape(p.name[0].toUpperCase())}</span><span>${escape(p.name)}${p.connected?'':' · offline'}</span></div>`).join('') : '<p class="empty-state">De første rivaler er på vej …</p>';
    }
    const connected = room.players.filter(p=>p.connected).length;
    $('quick-start').disabled = !connected;
    if (!game) { renderKey = ''; return; }
    $('round-label').textContent = `RUNDE ${room.round} · ${room.quickPlay?'AUTOMATISK SPIL':'JERES VALG'}`;
    $('result-actions').hidden = game.phase !== 'Results';
    $('show-leaderboard').hidden = game.phase !== 'Results' || room.resultsLeaderboardOpen;
    $('show-results').hidden = game.phase !== 'Results' || !room.resultsLeaderboardOpen;
    $('end-results').hidden = game.phase !== 'Results' || !room.quickPlay;
    const state = game.state;
    const key = [game.id,game.phase,game.phase==='Results' ? `${room.resultsLeaderboardOpen}:${room.resultsElapsedMs}` : '',game.kind==='reaction'?state.go:'',game.kind==='bomb'?state.holder:'',game.kind==='wheel'?state.selectedIndex:'',game.kind==='math'?state.question:'',game.kind==='duel'?`${state.attempt}:${state.tie}:${JSON.stringify(state.choices)}`:''].join(':');
    if (key !== renderKey) {
      renderKey = key;
      let html = '';
      if (['Intro','Countdown'].includes(game.phase)) html = intro(game);
      else if (game.phase==='Finished') html = `<div class="intro"><div class="huge-icon">${game.kind==='bomb'?'✹':'✦'}</div><h1>${game.kind==='bomb'?'BOOM!':'Sådan!'}</h1><p>Resultaterne er på vej …</p></div>`;
      else if (game.phase==='Results') { html = resultsHtml(game); confetti(); }
      else switch(game.kind) {
        case 'cookie': html = heading(game,'Hvem har de hurtigste fingre?')+'<div id="live-leaderboard" class="leaderboard"></div>'; break;
        case 'timing': html = heading(game,'Ingen ure. Bare mavefornemmelse.')+`<div class="expression">${Number(state.target).toLocaleString('da-DK', { minimumFractionDigits: 1, maximumFractionDigits: 1 })} <small>s</small></div><p class="muted" style="text-align:center" id="answered"></p>`; break;
        case 'reaction': html = heading(game)+`<div class="signal-word ${state.go?'go':''}">${state.go?'NU!':'VENT …'}</div><p class="muted" style="text-align:center" id="answered"></p>`; break;
        case 'math': html = heading(game)+`<div class="eyebrow" style="text-align:center">${state.question}/${state.totalQuestions}</div><div class="expression">${escape(state.expression)} = ?</div><p class="muted" style="text-align:center" id="answered"></p>`; break;
        case 'pattern': html = heading(game,'Hvad skal stå på spørgsmålstegnets plads?')+`<div class="pattern-sequence">${state.sequence?.map(n=>n===null?'?':n).join(' · ') || '…'}</div><p class="muted" style="text-align:center" id="answered"></p>`; break;
        case 'duel': html = heading(game,state.tie?'Uafgjort! Vi tager den igen …':'Kun de to udvalgte kan se deres valg.')+`<div class="duel-stage">${state.selected.map((id,i)=>`${i?'<div class="versus">VS</div>':''}<div class="duelist"><h2>${escape(nameOf(id))}</h2><span class="duel-choice">${choiceSymbol(state.choices?.[id])}</span></div>`).join('')}</div>`; break;
        case 'wheel': html = heading(game,'Hvem peger pilen på?')+wheelHtml(state); break;
        case 'bomb': html = heading(game,'Ingen kender tiden. Send den videre!')+`<div class="bomb-stage"><div class="bomb-orb">✹</div><h2>${escape(nameOf(state.holder))}</h2><p class="muted">har bomben lige nu</p></div>`; break;
      }
      $('host-game-content').innerHTML = html;
      if (game.kind==='wheel' && game.phase==='Playing') animateWheel(game);
    }
    if (game.kind==='cookie' && game.phase==='Playing') updateLeaderboard(state.counts);
    if ($('answered')) $('answered').textContent = `${state.answered || 0} af ${game.participants.length} har svaret`;
    updateClocks();
  }
  function choiceSymbol(choice) { return {rock:'✊',paper:'✋',scissors:'✌️'}[choice] || '？'; }
  function updateLeaderboard(counts) {
    const list = $('live-leaderboard'); if (!list) return;
    const sorted = Object.entries(counts).sort((a,b)=>b[1]-a[1]); list.style.height=`${sorted.length*72}px`;
    sorted.forEach(([id,count],i)=> {
      let row = document.getElementById(`rank-${id}`);
      if (!row) { row=document.createElement('div'); row.id=`rank-${id}`; row.className='leader-row'; row.innerHTML=`<span class="leader-rank"></span><span class="avatar">${escape(nameOf(id)[0])}</span><strong>${escape(nameOf(id))}</strong><span class="leader-value"></span>`; list.append(row); }
      row.style.transform=`translateY(${i*72}px)`; row.classList.toggle('first',i===0);
      row.querySelector('.leader-rank').textContent=i+1; row.querySelector('.leader-value').textContent=`${count} klik`;
    });
  }
  function wheelHtml(state) {
    const colors=['#c4f563','#c5b6f4','#efa99a','#80cbb5','#edd482'];
    const size=360/state.players.length;
    return `<div class="wheel-stage"><div class="wheel-pointer">▼</div><div class="wheel-disc" id="wheel-disc" style="background:conic-gradient(${state.players.map((id,i)=>`${colors[i%colors.length]} ${i*size}deg ${(i+1)*size}deg`).join(',')})">${state.players.map((id,i)=>`<span class="wheel-label" style="transform:rotate(${(i+.5)*size}deg)">${escape(state.players.length>16?i+1:nameOf(id).slice(0,12))}</span>`).join('')}</div><div class="wheel-center">✳</div></div><div class="wheel-names">${state.players.map((id,i)=>`<span>${i+1}. ${escape(nameOf(id))}</span>`).join('')}</div>`;
  }
  function animateWheel(game) {
    const wheel=$('wheel-disc'); if (!wheel || game.state.selectedIndex == null) return;
    const rotation = 360*6 + 360 - (game.state.selectedIndex+.5)*360/game.state.players.length;
    const remaining = Math.max(0,Date.parse(game.state.endsAt)-now()-350);
    // Restore the same landing position after a host refresh.
    wheel.style.transitionDuration=`${remaining}ms`;
    requestAnimationFrame(()=>requestAnimationFrame(()=>{ if(wheel.isConnected) wheel.style.transform=`rotate(${rotation}deg)`; }));
  }
  function act(action,value=null) { if (!room?.game) return Promise.resolve(false); return invoke('Act',{roundId:room.game.id,action,value,sequence:++sequence}); }
  function renderPlayer() {
    if (!me || !room) return;
    $('player-room-label').textContent=`${me.name} · RUM ${room.code}`;
    const game=room.game; const mine=ownRound===game?.id ? own : {};
    const enrolled=game?.participants.includes(me.playerId);
    const key=[game?.id,game?.phase,room.hostConnected,enrolled,game?.state.go,game?.state.holder,game?.state.attempt,game?.state.tie,game?.kind==='bomb'?room.players.filter(p=>p.connected).map(p=>p.id).join(','):'',!!mine.started,!!mine.submitted,!!mine.falseStart].join(':');
    if (key !== renderKey) {
      renderKey=key;
      let html=!room.hostConnected?'<div class="notice host-missing">Værten er offline. Runden fortsætter, og værten kan vende tilbage.</div>':'';
      if (!game) html+=`<div class="waiting"><div class="waiting-symbol">✳</div><h1>Du er med.</h1><p>Find en god plads.<br />Værten starter lige om lidt.</p><div class="name-tag"><span class="avatar">${escape(me.name[0])}</span>${escape(me.name)}</div><p class="privacy-note">Hold siden åben — din telefon er din controller.</p></div>`;
      else if (!enrolled) html+='<div class="waiting"><div class="waiting-symbol">↗</div><h1>Du er klar<br />til næste runde.</h1><p>Følg med på den store skærm imens.</p></div>';
      else if (['Intro','Countdown'].includes(game.phase)) html+=intro(game,true);
      else if (game.phase==='Finished') html+='<div class="waiting"><div class="waiting-symbol">✦</div><h1>Runden er slut!</h1><p>Kig op. Resultaterne kommer nu.</p></div>';
      else if(game.phase==='Results') {
        const result=game.results.find(r=>r.playerId===me.playerId);
        html+=`<div class="controller"><div class="eyebrow">${games[game.kind][0]} · RESULTAT</div><div class="player-result"><h2>${result?.winner?'Du tager den! ✦':result?.bottom?'Det blev dig!':result?'Godt spillet.':'Sikke en duel!'}</h2>${result?`<p>${escape(result.detail)}</p>${!['wheel','bomb'].includes(game.kind)?`<p>Placering: ${result.rank}</p>`:''}${result.points?`<p class="lime">+${result.points} point</p>`:''}`:''}</div><p>Se alle resultaterne på den store skærm.<br />Alle er med i næste runde.</p></div>`;
        if(result?.winner) confetti();
      } else {
        html+=`<div class="controller ${game.kind==='bomb'?'bomb-controller':''}"><div class="eyebrow">${games[game.kind][3]}</div><h1>${games[game.kind][0]}</h1>`;
        switch(game.kind) {
          case 'cookie': html+=`<div class="timer" data-timer="${game.state.endsAt}"></div><button id="tap" class="action-pad"><span class="pad-symbol">🍪</span>Giv den gas!</button><div class="own-count" id="own-count">${mine.count||0}</div><p>godkendte klik</p>`; break;
          case 'timing': html+=mine.submitted?submitted():`<p>Målet er ${Number(game.state.target).toLocaleString('da-DK', { minimumFractionDigits: 1, maximumFractionDigits: 1 })} sekunder.<br />Tiden starter, når skærmen bliver grøn. Tryk STOP, når du tror, tiden er gået.</p><button id="timing-action" class="action-pad">STOP</button>`; break;
          case 'reaction': html+=mine.submitted?submitted(mine.falseStart?'Tyvstart! Vent på NU næste gang.':'Din reaktion er registreret.'): `<button id="react" class="action-pad ${game.state.go?'':'wait-pad'}"><span class="pad-symbol">ϟ</span>${game.state.go?'NU! TRYK!':'VENT …'}</button><p style="margin-top:22px">${game.state.go?'Så hurtigt du kan!':'Tryk først, når knappen bliver grøn.'}</p>`; break;
          case 'math': html+=mine.submitted?submitted():`<div class="eyebrow">${game.state.question}/${game.state.totalQuestions}</div><p class="expression" style="font-size:48px;color:var(--lime)">${escape(game.state.expression)}</p><form id="answer-form"><label for="answer">Dit svar</label><input id="answer" type="text" inputmode="numeric" pattern="[0-9]+" maxlength="6" autocomplete="off" required /><button class="button primary full">Send svar →</button></form>`; break;
          case 'pattern': html+=mine.submitted?submitted():`<div class="pattern-sequence">${game.state.sequence?.map(n=>n===null?'?':n).join(' · ') || '…'}</div><form id="answer-form"><label for="answer">Dit svar</label><input id="answer" type="text" inputmode="numeric" pattern="-?[0-9]+" maxlength="8" autocomplete="off" required /><button class="button primary full">Send svar →</button></form>`; break;
          case 'duel': html+=!game.state.selected.includes(me.playerId)?'<div class="submitted-mark">⚔</div><p>Se duellen på den store skærm.</p>':game.state.tie?'<p>Uafgjort! Gør dig klar til at vælge igen …</p>':mine.submitted?submitted('Dit valg er hemmeligt. Vi venter på din modstander.'):'<p>Vælg i hemmelighed.</p><div class="choice-grid"><button class="choice-button" data-choice="rock">✊ <span>Sten</span></button><button class="choice-button" data-choice="paper">✋ <span>Papir</span></button><button class="choice-button" data-choice="scissors">✌️ <span>Saks</span></button></div>'; break;
          case 'wheel': html+='<div class="submitted-mark">✳</div><p>Hjulet drejer …<br />Kig op på den store skærm.</p>'; break;
          case 'bomb': html+=game.state.holder===me.playerId?`<h2 style="color:var(--danger);margin-bottom:20px">DU HAR BOMBEN!</h2><p>Send den videre. Hurtigt!</p><div class="pass-grid">${room.players.filter(p=>p.connected&&p.id!==me.playerId&&game.participants.includes(p.id)).map(p=>`<button data-pass="${p.id}">${escape(p.name)} ↗</button>`).join('')}</div><p class="privacy-note">Vent et kort øjeblik mellem afleveringer. Ingen øjeblikkelig returpasning.</p>`:`<div class="submitted-mark">✓</div><h2>I sikkerhed. Lige nu.</h2><p style="margin-top:20px">${escape(nameOf(game.state.holder))} har bomben.</p>`; break;
        }
        html+='</div>';
      }
      $('player-content').innerHTML=html;
      bindControls(game,mine);
      if (game?.phase === 'Playing' && lastStarted !== game.id) { lastStarted = game.id; buzz([35, 40, 35]); }
      if (game?.phase==='Playing' && game.kind==='bomb' && game.state.holder===me.playerId) buzz([150,60,150]);
    }
    if ($('own-count')) $('own-count').textContent=mine.count||0;
    updateClocks();
  }
  function submitted(message='Dit svar er sendt. Kig op på den store skærm.') { return `<div class="submitted-mark">✓</div><p>${escape(message)}</p>`; }
  function bindControls(game,mine) {
    $('tap')?.addEventListener('pointerdown',event=>{ event.preventDefault(); buzz(8); act('tap'); });
    $('tap')?.addEventListener('keydown',event=>{ if([' ','Enter'].includes(event.key)&&!event.repeat){event.preventDefault();act('tap');} });
    $('timing-action')?.addEventListener('click',async event=>{event.currentTarget.disabled=true;if(!await act('stop')){renderKey='';renderPlayer();}});
    $('react')?.addEventListener('pointerdown',event=>{event.preventDefault();act('react');buzz(30);});
    $('react')?.addEventListener('keydown',event=>{if([' ','Enter'].includes(event.key)){event.preventDefault();act('react');}});
    $('answer-form')?.addEventListener('submit',async event=>{event.preventDefault();event.target.querySelector('button').disabled=true;if(!await act('answer',$('answer').value)){event.target.querySelector('button').disabled=false;}});
    document.querySelectorAll('[data-choice]').forEach(button=>button.addEventListener('click',async()=>{
      document.querySelectorAll('[data-choice]').forEach(b=>b.disabled=true);
      if(!await act(`choose:${game.state.attempt}`,button.dataset.choice)){renderKey='';renderPlayer();}
    }));
    document.querySelectorAll('[data-pass]').forEach(button=>button.addEventListener('click',()=>{act('pass',button.dataset.pass);buzz(25);}));
  }
  function updateClocks() {
    document.querySelectorAll('[data-countdown]').forEach(el=>{
      const seconds=Math.max(1,Math.ceil((Date.parse(el.dataset.countdown)-now())/1000));el.textContent=seconds;
      const key=`${room?.game?.id}:${seconds}`;if(page==='player'&&key!==lastBuzz){lastBuzz=key;buzz(25);}
    });
    document.querySelectorAll('[data-timer]').forEach(el=>el.textContent=`${Math.max(0,Math.ceil((Date.parse(el.dataset.timer)-now())/1000))} sek.`);
    if($('next-countdown')) $('next-countdown').textContent=room?.nextRoundAt?`Næste spil om ${Math.max(0,Math.ceil((Date.parse(room.nextRoundAt)-now())/1000))} sek.`:'';
  }
  setInterval(updateClocks,100);
})();
