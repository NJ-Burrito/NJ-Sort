(function () {
  var io = new IntersectionObserver(function (es) { es.forEach(function (e) { if (e.isIntersecting) { e.target.classList.add('in'); io.unobserve(e.target); } }); }, { threshold: .15 });
  document.querySelectorAll('.reveal').forEach(function (el) { io.observe(el); });
  var v = document.getElementById('hero'), b = document.getElementById('pause'), ar = document.documentElement.lang === 'ar';
  if (matchMedia('(prefers-reduced-motion: reduce)').matches) { v.removeAttribute('autoplay'); v.pause(); b.hidden = true; return; }
  var L = ar ? ['إيقاف العرض مؤقتاً', 'تشغيل العرض'] : ['Pause the demo', 'Play the demo'];
  b.addEventListener('click', function () {
    var p = b.getAttribute('aria-pressed') !== 'true';
    b.setAttribute('aria-pressed', p); b.setAttribute('aria-label', p ? L[1] : L[0]);
    if (p) v.pause(); else v.play();
  });
})();
