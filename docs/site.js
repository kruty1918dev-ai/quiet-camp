(function () {
  "use strict";
  var supported = ["uk", "en", "de"], language = "uk", dictionary = {}, records = [], currentFilter = "all", currentView = null;
  var cards = Array.from(document.querySelectorAll("[data-screen]"));
  var search = document.getElementById("screen-search"), viewer = document.getElementById("image-viewer");
  function text(key) { return (dictionary[language] || {})[key] || key; }
  function detect() {
    try { var saved = localStorage.getItem("qc-lang"); if (supported.indexOf(saved) >= 0) return saved; } catch (_) {}
    var languages = navigator.languages || [navigator.language || "uk"];
    for (var i = 0; i < languages.length; i++) { var code = String(languages[i]).toLowerCase().split("-")[0]; if (supported.indexOf(code) >= 0) return code; }
    return "uk";
  }
  function record(id) { return records.find(function (item) { return item.id === id; }); }
  function filter() {
    var query = search.value.trim().toLowerCase(), count = 0;
    cards.forEach(function (card) {
      var item = record(card.dataset.screen);
      var searchable = item ? Object.values(item.title).concat(Object.values(item.description)).join(" ").toLowerCase() : card.textContent.toLowerCase();
      var visible = (currentFilter === "all" || card.dataset.group === currentFilter) && (!query || searchable.indexOf(query) >= 0);
      card.hidden = !visible; if (visible) count++;
    });
    document.getElementById("result-count").textContent = count + " " + (dictionary[language] ? text("screens.count") : "екранів і станів");
    document.getElementById("empty-results").hidden = count !== 0;
    document.querySelectorAll("[data-filter]").forEach(function (button) { button.setAttribute("aria-pressed", button.dataset.filter === currentFilter ? "true" : "false"); });
  }
  function updateViewer() {
    var item = record(currentView); if (!item) return;
    document.getElementById("viewer-title").textContent = item.title[language];
    document.getElementById("viewer-description").textContent = item.description[language];
    document.getElementById("viewer-image").alt = item.title[language];
  }
  function apply(lang) {
    if (supported.indexOf(lang) < 0 || !dictionary[lang]) return;
    language = lang; document.documentElement.lang = lang;
    document.title = "Quiet Camp · " + text("brand.small");
    document.querySelector('meta[name="description"]').content = text("hero.lede");
    document.querySelectorAll("[data-i18n]").forEach(function (element) {
      var key = element.dataset.i18n; if (dictionary[lang][key] !== undefined) element.textContent = text(key);
    });
    search.placeholder = text("search.placeholder");
    cards.forEach(function (card) {
      var item = record(card.dataset.screen); if (!item) return;
      card.querySelector("[data-card-title]").textContent = item.title[lang];
      card.querySelector("[data-card-description]").textContent = item.description[lang];
      card.querySelector("[data-group-label]").textContent = text("filter." + item.group);
      card.querySelector("[data-view]").setAttribute("aria-label", text("card.open") + ": " + item.title[lang]);
      card.querySelector("img").alt = item.title[lang];
    });
    document.querySelectorAll("[data-lang]").forEach(function (button) { button.setAttribute("aria-pressed", button.dataset.lang === lang ? "true" : "false"); });
    try { localStorage.setItem("qc-lang", lang); } catch (_) {}
    updateViewer(); filter();
  }
  search.addEventListener("input", filter);
  document.querySelectorAll("[data-filter]").forEach(function (button) { button.addEventListener("click", function () { currentFilter = button.dataset.filter; filter(); }); });
  document.querySelectorAll("[data-lang]").forEach(function (button) { button.addEventListener("click", function () { apply(button.dataset.lang); }); });
  document.querySelectorAll("[data-view]").forEach(function (button) {
    button.addEventListener("click", function () {
      currentView = button.dataset.view;
      var item = record(currentView), card = button.closest("[data-screen]");
      document.getElementById("viewer-title").textContent = item ? item.title[language] : card.querySelector("h3").textContent;
      document.getElementById("viewer-description").textContent = item ? item.description[language] : card.querySelector("[data-card-description]").textContent;
      var path = "images/captures/2026-10-08/" + currentView;
      var image = document.getElementById("viewer-image"); image.src = path + ".png"; image.alt = document.getElementById("viewer-title").textContent;
      document.getElementById("viewer-original").href = path + ".png"; document.getElementById("viewer-metadata").href = path + ".json";
      if (typeof viewer.showModal === "function") viewer.showModal(); else viewer.setAttribute("open", "");
    });
  });
  function closeViewer() { if (typeof viewer.close === "function") viewer.close(); else viewer.removeAttribute("open"); }
  document.getElementById("viewer-close").addEventListener("click", closeViewer);
  viewer.addEventListener("click", function (event) {
    var bounds = viewer.getBoundingClientRect();
    if (event.target === viewer && (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom)) closeViewer();
  });
  viewer.addEventListener("close", function () { currentView = null; document.getElementById("viewer-image").removeAttribute("src"); });
  if (typeof IntersectionObserver === "function") {
    var observer = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) { if (entry.isIntersecting) document.querySelectorAll('.sidebar a[href^="#"]').forEach(function (a) { if (a.hash === "#" + entry.target.id) a.setAttribute("aria-current", "true"); else a.removeAttribute("aria-current"); }); });
    }, { rootMargin: "-15% 0px -65% 0px" });
    document.querySelectorAll("main>section[id]").forEach(function (section) { observer.observe(section); });
  }
  Promise.all([fetch("site-i18n.json").then(function (r) { if (!r.ok) throw new Error("Translations unavailable"); return r.json(); }), fetch("site-content.json").then(function (r) { if (!r.ok) throw new Error("Guide content unavailable"); return r.json(); })]).then(function (values) {
    dictionary = values[0]; records = values[1]; apply(detect());
  }).catch(function () { filter(); /* Static Ukrainian content remains available. */ });
})();
