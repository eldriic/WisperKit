// WisperKit site: wave backdrop, feature tabs, copy buttons and the recording pill demo.
(() => {
  const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  /* ---------- Nav: transparent at the top, a bar once the page scrolls ---------- */
  const nav = document.querySelector('.nav');
  if (nav) {
    const onScroll = () => nav.classList.toggle('is-scrolled', window.scrollY > 8);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
  }

  /* ---------- Waves (port of WaveRenderer in RecordingHUD.swift) ---------- */
  const css = getComputedStyle(document.documentElement);
  const palette = ['--wave-1', '--wave-2', '--wave-3', '--wave-4'].map((n) => css.getPropertyValue(n).trim());
  const col = (ch, a) => `oklch(${ch} / ${a})`;
  // Superposed waves, faded at both ends, additive blend.
  const drawWaves = (ctx, left, right, midY, amplitude, t, speed, peak = 0.62) => {
    ctx.globalCompositeOperation = 'lighter';
    palette.forEach((ch, i) => {
      const a = amplitude * (1 - i * 0.12);
      const freq = 1.1 + i * 0.35;
      const phase = t * (1.3 + i * 0.4) * speed + i * 2;
      const steps = 90;
      ctx.beginPath();
      for (let k = 0; k <= steps; k++) {
        const u = k / steps;
        const y = midY + Math.sin(u * Math.PI * 2 * freq + phase) * a * Math.pow(Math.sin(Math.PI * u), 1.4);
        const x = left + u * (right - left);
        k === 0 ? ctx.moveTo(x, y) : ctx.lineTo(x, y);
      }
      for (let k = steps; k >= 0; k--) {
        const u = k / steps;
        const y = midY - Math.sin(u * Math.PI * 2 * freq + phase + 1) * a * Math.pow(Math.sin(Math.PI * u), 1.4) * 0.7;
        ctx.lineTo(left + u * (right - left), y);
      }
      ctx.closePath();
      const lg = ctx.createLinearGradient(left, midY, right, midY);
      lg.addColorStop(0, col(ch, 0));
      lg.addColorStop(0.5, col(ch, peak));
      lg.addColorStop(1, col(ch, 0));
      ctx.fillStyle = lg;
      ctx.fill();
    });
    ctx.globalCompositeOperation = 'source-over';
  };
  const fitCanvas = (canvas, ctx, scale = 1) => {
    const dpr = Math.min(window.devicePixelRatio || 1, 2) * scale;
    // Layout size, not getBoundingClientRect(): the backdrop canvas is scaled by a CSS transform.
    const w = canvas.clientWidth, h = canvas.clientHeight;
    canvas.width = Math.round(w * dpr); canvas.height = Math.round(h * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    return { w, h };
  };

  /* ---------- Backdrop: the waves at page scale, standing upright, breathing like a slow voice ---------- */
  const bg = document.querySelector('.backdrop__waves');
  if (bg && bg.getContext) {
    const bctx = bg.getContext('2d');
    const short = bg.closest('.backdrop--short');
    let size = { w: 0, h: 0 };
    // The canvas is blurred afterwards: half resolution is plenty and much cheaper.
    const fit = () => { size = fitCanvas(bg, bctx, 0.5); };
    const paint = (t) => {
      const { w, h } = size;
      bctx.clearRect(0, 0, w, h);
      const level = 0.62 + 0.22 * Math.sin(t * 0.55) + 0.12 * Math.sin(t * 1.7 + 1);
      // Turned a quarter: the wave runs from the top down, centred, its widest part behind the hero.
      bctx.save();
      bctx.translate(w / 2, 0);
      bctx.rotate(Math.PI / 2);
      const top = short ? -h * 0.45 : -h * 0.3;
      drawWaves(bctx, top, h * 0.95, 0, Math.min(w * 0.2, 260) * level, t, 0.35, 0.55);
      bctx.restore();
    };
    fit();
    window.addEventListener('resize', () => { fit(); if (reduce) paint(1.7); });
    if (reduce) paint(1.7);
    else {
      let visible = true;
      new IntersectionObserver(([e]) => { visible = e.isIntersecting; }).observe(bg.parentElement);
      const loop = (now) => {
        if (visible && !document.hidden) paint(now / 1000);
        requestAnimationFrame(loop);
      };
      requestAnimationFrame(loop);
    }
  }

  /* ---------- Docs: filter the sidebar, mark the section in view ---------- */
  const search = document.querySelector('.docs-search input');
  const tocLinks = [...document.querySelectorAll('.docs-nav a[href^="#"]')];
  if (search) {
    search.addEventListener('input', () => {
      const q = search.value.trim().toLowerCase();
      tocLinks.forEach((a) => {
        const target = document.querySelector(a.getAttribute('href'));
        const hay = (a.textContent + ' ' + (target ? target.textContent : '')).toLowerCase();
        a.parentElement.hidden = q !== '' && !hay.includes(q);
      });
    });
  }
  if (tocLinks.length) {
    const byId = new Map(tocLinks.map((a) => [a.getAttribute('href').slice(1), a]));
    const spy = new IntersectionObserver((entries) => {
      entries.forEach((e) => {
        if (!e.isIntersecting) return;
        tocLinks.forEach((a) => a.removeAttribute('aria-current'));
        const a = byId.get(e.target.id);
        if (a) a.setAttribute('aria-current', 'location');
      });
    }, { rootMargin: '-20% 0px -70% 0px' });
    byId.forEach((_, id) => { const el = document.getElementById(id); if (el) spy.observe(el); });
  }

  /* ---------- Tabs ---------- */
  const tabs = [...document.querySelectorAll('[role="tab"]')];
  const select = (tab, focus) => {
    tabs.forEach((t) => {
      const on = t === tab;
      t.setAttribute('aria-selected', String(on));
      t.tabIndex = on ? 0 : -1;
      document.getElementById(t.getAttribute('aria-controls')).classList.toggle('is-active', on);
    });
    if (focus) tab.focus();
  };
  tabs.forEach((tab, i) => {
    tab.addEventListener('click', () => select(tab));
    tab.addEventListener('keydown', (e) => {
      const step = { ArrowRight: 1, ArrowLeft: -1 }[e.key];
      if (e.key === 'Home') { e.preventDefault(); select(tabs[0], true); }
      else if (e.key === 'End') { e.preventDefault(); select(tabs[tabs.length - 1], true); }
      else if (step) { e.preventDefault(); select(tabs[(i + step + tabs.length) % tabs.length], true); }
    });
  });
  if (tabs.length) select(tabs[0]);

  /* ---------- « Also for » : menu des autres téléchargements ---------- */
  const also = document.querySelector('[data-also]');
  const alsoToggle = also && also.querySelector('.also__toggle');
  const alsoMenu = also && also.querySelector('.also__menu');
  // Masque l'entrée déjà proposée par le bouton principal et recalcule le libellé
  const setAlso = (primary) => {
    if (!also) return;
    const shown = [];
    also.querySelectorAll('[data-os]').forEach((item) => {
      item.hidden = item.dataset.os === primary;
      if (!item.hidden) shown.push(item.dataset.short);
    });
    // « Windows » couvre déjà ARM64 quand les deux sont proposés
    const label = shown.includes('Windows') ? shown.filter((s) => s !== 'Windows ARM64') : shown;
    also.querySelector('[data-also-label]').textContent = 'Also for ' + label.join(' · ');
  };
  if (also) {
    const open = (state) => {
      alsoToggle.setAttribute('aria-expanded', String(state));
      alsoMenu.hidden = !state;
    };
    alsoToggle.addEventListener('click', () => open(alsoMenu.hidden));
    alsoMenu.addEventListener('click', (e) => { if (e.target.closest('a')) open(false); });
    document.addEventListener('click', (e) => { if (!also.contains(e.target)) open(false); });
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Escape' && !alsoMenu.hidden) { open(false); alsoToggle.focus(); }
    });
    setAlso('mac');
  }

  /* ---------- Download: the right file for this OS and, on Windows, this CPU ---------- */
  const DL = 'https://github.com/eldriic/WisperKit/releases/latest/download/';
  const uaData = navigator.userAgentData;
  const ua = navigator.userAgent;
  const isWindows = uaData ? uaData.platform === 'Windows' : /Windows NT/.test(ua);
  const isMac = uaData ? uaData.platform === 'macOS' : /Macintosh/.test(ua) && !('ontouchend' in document);
  const navBtn = document.querySelector('[data-download="nav"]');
  if (isMac && navBtn) navBtn.href = DL + 'WisperKit.dmg';
  if (isWindows) {
    // Chromium (Edge, Chrome) gives the real CPU; Firefox always says x64, so x64 stays the default.
    const detect = async () => {
      try {
        const { architecture } = await uaData.getHighEntropyValues(['architecture']);
        if (architecture === 'arm') return 'arm64';
        if (architecture === 'x86') return 'x64';
      } catch { /* no Client Hints */ }
      return /ARM64|aarch64/i.test(ua) ? 'arm64' : null;
    };
    const apply = (arch) => {
      const file = DL + `WisperKit-Setup-${arch || 'x64'}.exe`;
      const hero = document.querySelector('[data-download="hero"]');
      if (hero) {
        hero.href = file;
        hero.querySelector('[data-download-label]').textContent = 'Download for Windows';
        setAlso(`win-${arch || 'x64'}`);
      }
      if (navBtn) navBtn.href = file;
      document.querySelectorAll('[data-arch]').forEach((a) => {
        if (a.dataset.arch === (arch || 'x64')) a.dataset.detected = ''; else delete a.dataset.detected;
      });
    };
    apply(null);
    detect().then(apply);
  }

  /* ---------- Copy ---------- */
  document.querySelectorAll('[data-copy]').forEach((btn) => {
    btn.addEventListener('click', async () => {
      try {
        await navigator.clipboard.writeText(btn.dataset.copy);
        btn.textContent = 'Copied';
        btn.dataset.done = '';
      } catch {
        btn.textContent = 'Select and copy';
      }
      setTimeout(() => { btn.textContent = 'Copy'; delete btn.dataset.done; }, 1800);
    });
  });

  /* ---------- Recording pill (port of WaveRenderer in RecordingHUD.swift) ---------- */
  const pill = document.querySelector('.pill');
  const canvas = document.querySelector('.pill__waves');
  if (!pill || !canvas || !canvas.getContext) return;
  const ctx = canvas.getContext('2d');
  const timeEl = pill.querySelector('.pill__time');
  const textEl = document.querySelector('.demo__text');

  let w = 0, h = 0;
  const resize = () => { ({ w, h } = fitCanvas(canvas, ctx)); };
  resize();
  window.addEventListener('resize', resize);

  const draw = (t, level, speed) => {
    ctx.clearRect(0, 0, w, h);
    drawWaves(ctx, 4, w - 4, h / 2, (h / 2 - 3) * Math.min(level, 1), t, speed);
  };

  if (reduce) { draw(1.7, 0.55, 1); return; }

  const phrases = [
    'Can you review the pull request before lunch?',
    'Remind me to send the invoice on Friday.',
    'Shall we catch up tomorrow at ten?',
  ];
  const LISTEN = 3600, TRANSCRIBE = 1100, HOLD = 2600;
  let start = performance.now(), idx = 0, typed = -1, level = 0, running = true;

  // A fake voice: syllable bursts, attack fast, release slow (like LevelSmoother).
  const voice = (s) => {
    const syll = Math.max(0, Math.sin(s * 9.5) * 0.5 + Math.sin(s * 4.1 + 1) * 0.35 + 0.3);
    const breath = Math.sin(s * 1.3) > -0.85 ? 1 : 0.15;
    return Math.min(0.95, 0.15 + syll * 0.75 * breath);
  };

  textEl.textContent = '';
  const frame = (now) => {
    if (!running) return;
    const e = now - start;
    const t = now / 1000;
    let target, speed = 1;
    if (e < LISTEN) {
      pill.dataset.state = 'listening';
      timeEl.textContent = `0:0${Math.floor(e / 1000)}`;
      target = voice(t);
      typed = -1;
    } else if (e < LISTEN + TRANSCRIBE) {
      pill.dataset.state = 'transcribing';
      timeEl.textContent = '·'.repeat(Math.floor(t * 3) % 3 + 1);
      target = 0.12; speed = 0.45;
    } else {
      const phrase = phrases[idx];
      const n = Math.min(phrase.length, Math.floor((e - LISTEN - TRANSCRIBE) / 28));
      if (n !== typed) { textEl.textContent = phrase.slice(0, n); typed = n; }
      pill.dataset.state = 'idle';
      timeEl.textContent = '0:00';
      target = 0.06; speed = 0.4;
      if (e > LISTEN + TRANSCRIBE + phrase.length * 28 + HOLD) {
        idx = (idx + 1) % phrases.length; start = now; textEl.textContent = '';
      }
    }
    level += (target - level) * (target > level ? 0.3 : 0.08);
    draw(t, Math.max(level, 0.06), speed);
    requestAnimationFrame(frame);
  };

  const resume = () => { if (!running) { running = true; start = performance.now(); textEl.textContent = ''; requestAnimationFrame(frame); } };
  new IntersectionObserver(([entry]) => {
    if (entry.isIntersecting && !document.hidden) resume(); else running = false;
  }).observe(pill);
  document.addEventListener('visibilitychange', () => { if (document.hidden) running = false; else resume(); });
  requestAnimationFrame(frame);
})();
