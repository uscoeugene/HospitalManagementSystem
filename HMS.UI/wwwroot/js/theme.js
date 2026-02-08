(function(){
  const toggle = document.getElementById('themeToggle');
  const link = document.getElementById('theme-stylesheet');
  const stored = localStorage.getItem('hms:theme') || 'light';
  link.href = `/css/themes/${stored}.css`;
  toggle.addEventListener('click', ()=>{
    const cur = localStorage.getItem('hms:theme') || 'light';
    const next = cur === 'light' ? 'dark' : 'light';
    localStorage.setItem('hms:theme', next);
    link.href = `/css/themes/${next}.css`;
  });
})();
