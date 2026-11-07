// WisperKit site: feature tabs, copy buttons and the recording pill demo.
(() => {
  const reduce = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  /* ---------- Nav: transparent at the top, a bar once the page scrolls ---------- */
  const nav = document.querySelector('.nav');
  if (nav) {
    const onScroll = () => nav.classList.toggle('is-scrolled', window.scrollY > 8);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
  }

  /* ---------- Backdrop motes ---------- */
  const motes = document.querySelector('.backdrop__motes');
  if (motes && motes.getContext) {
    const mctx = motes.getContext('2d');
    const moteColor = getComputedStyle(document.documentElement).getPropertyValue('--color-mote').trim();
    let mw = 0, mh = 0, dots = [];
    const seed = () => {
      const dpr = Math.min(window.devicePixelRatio || 1, 2);
      const r = motes.getBoundingClientRect();
      mw = r.width; mh = r.height;
      motes.width = Math.round(mw * dpr); motes.height = Math.round(mh * dpr);
      mctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      const n = Math.round((mw * mh) / 16000);
      dots = Array.from({ length: n }, () => ({
        x: Math.random() * mw, y: Math.random() * mh,
        r: Math.random() * 1.1 + 0.3, a: Math.random() * 0.5 + 0.15,
        vy: -(Math.random() * 0.12 + 0.03), vx: (Math.random() - 0.5) * 0.05, tw: Math.random() * Math.PI * 2,
      }));
    };
    const paint = (t) => {
      mctx.clearRect(0, 0, mw, mh);
      mctx.fillStyle = moteColor;
      for (const d of dots) {
        mctx.globalAlpha = d.a * (0.65 + 0.35 * Math.sin(t / 1400 + d.tw));
        mctx.beginPath(); mctx.arc(d.x, d.y, d.r, 0, Math.PI * 2); mctx.fill();
      }
      mctx.globalAlpha = 1;
    };
    seed();
    window.addEventListener('resize', seed);
    if (reduce) paint(0);
    else {
      const loop = (t) => {
        for (const d of dots) {
          d.y += d.vy; d.x += d.vx;
          if (d.y < -4) { d.y = mh + 4; d.x = Math.random() * mw; }
        }
        if (window.scrollY < mh) paint(t);
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

  const css = getComputedStyle(document.documentElement);
  const channels = (name) => css.getPropertyValue(name).trim();
  const palette = ['--wave-1', '--wave-2', '--wave-3', '--wave-4'].map(channels);
  const col = (ch, a) => `oklch(${ch} / ${a})`;

  let w = 0, h = 0;
  const resize = () => {
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const r = canvas.getBoundingClientRect();
    w = r.width; h = r.height;
    canvas.width = Math.round(w * dpr); canvas.height = Math.round(h * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  };
  resize();
  window.addEventListener('resize', resize);

  const draw = (t, level, speed) => {
    ctx.clearRect(0, 0, w, h);
    const left = 4, right = w - 4, midY = h / 2;
    const amplitude = (h / 2 - 3) * Math.min(level, 1);
    ctx.globalCompositeOperation = 'lighter';
    palette.forEach((ch, i) => {
      const a = amplitude * (1 - i * 0.12);
      const freq = 1.1 + i * 0.35;
      const phase = t * (1.3 + i * 0.4) * speed + i * 2;
      const steps = 70;
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
      lg.addColorStop(0.5, col(ch, 0.62));
      lg.addColorStop(1, col(ch, 0));
      ctx.fillStyle = lg;
      ctx.fill();
    });
    ctx.globalCompositeOperation = 'source-over';
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
