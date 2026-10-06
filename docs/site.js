(function () {
  "use strict";

  var I18N = {
    uk: {
      "title": "Тихий кемпінг",
      "tagline": "Місце для спокою",
      "lede": "Спокійна просторова головоломка: розстав намети всіх гостей, зваж на тінь і тишу — і дивись, як галявина занурюється у вечір.",
      "gallery": "Кадри з гри",
      "cap.menu": "Головне меню",
      "cap.map": "Мапа мандрівки",
      "cap.spring": "Весняна галявина",
      "cap.summer": "Літній табір",
      "cap.autumn": "Осінній ліс",
      "cap.winter": "Зимова тиша",
      "cap.album": "Альбом твого табору",
      "about": "Про гру",
      "fact.puzzle": "Просторові головоломки — у кожного намету є місце, двері та стежка.",
      "fact.seasons": "Весна, літо, осінь і зима — той самий пайплайн середовища рендерить усі сезони.",
      "fact.offline": "Гра спершу офлайн, без нав'язливої реклами: прототипні покупки вимкнені.",
      "fact.langs": "Мови інтерфейсу: Українська, English, Deutsch.",
      "footer.note": "Кадри знято пайплайном Unity-редактора 06.10.2026.",
      "footer.repo": "Репозиторій"
    },
    en: {
      "title": "Quiet Camp",
      "tagline": "A place to unwind",
      "lede": "A calm spatial puzzle: place every guest's tent, mind the shade and quiet, and watch the clearing settle into evening.",
      "gallery": "From the game",
      "cap.menu": "Main menu",
      "cap.map": "Campaign map",
      "cap.spring": "Spring clearing",
      "cap.summer": "Summer camp",
      "cap.autumn": "Autumn woods",
      "cap.winter": "Winter quiet",
      "cap.album": "Your camp album",
      "about": "About the game",
      "fact.puzzle": "Spatial logic puzzles — every tent has a footprint, a door and a path.",
      "fact.seasons": "Spring, summer, autumn and winter camps rendered by the same environment pipeline.",
      "fact.offline": "Offline-first, no ads pressure: prototype purchases stay disabled.",
      "fact.langs": "Interface languages: Українська, English, Deutsch.",
      "footer.note": "Renders captured from the Unity editor showcase pipeline on 2026-10-06.",
      "footer.repo": "Repository"
    },
    de: {
      "title": "Ruhiges Camp",
      "tagline": "Ein Ort zum Durchatmen",
      "lede": "Ein ruhiges Denkspiel: Stelle das Zelt jedes Gastes auf, achte auf Schatten und Stille — und sieh zu, wie die Lichtung in den Abend gleitet.",
      "gallery": "Aus dem Spiel",
      "cap.menu": "Hauptmenü",
      "cap.map": "Kampagnenkarte",
      "cap.spring": "Frühlingslichtung",
      "cap.summer": "Sommercamp",
      "cap.autumn": "Herbstwald",
      "cap.winter": "Winterstille",
      "cap.album": "Dein Camp-Album",
      "about": "Über das Spiel",
      "fact.puzzle": "Rätsel im Raum — jedes Zelt hat eine Grundfläche, einen Eingang und einen Weg.",
      "fact.seasons": "Frühling, Sommer, Herbst und Winter — dieselbe Umgebungs-Pipeline rendert alle Jahreszeiten.",
      "fact.offline": "Offline zuerst, ohne Werbedruck: Prototyp-Käufe bleiben deaktiviert.",
      "fact.langs": "Sprachen der Oberfläche: Українська, English, Deutsch.",
      "footer.note": "Renderings aus der Unity-Editor-Showcase-Pipeline vom 06.10.2026.",
      "footer.repo": "Repository"
    }
  };

  var SUPPORTED = ["uk", "en", "de"];
  var STORE_KEY = "qc-lang";

  function detect() {
    try {
      var saved = window.localStorage.getItem(STORE_KEY);
      if (saved && I18N[saved]) return saved;
    } catch (e) { /* storage unavailable — fall through */ }
    var langs = navigator.languages || [navigator.language || "en"];
    for (var i = 0; i < langs.length; i++) {
      var code = String(langs[i] || "").toLowerCase().split("-")[0];
      if (SUPPORTED.indexOf(code) !== -1) return code;
    }
    return "en";
  }

  function apply(lang) {
    var dict = I18N[lang] || I18N.en;
    document.documentElement.lang = lang;
    document.title = dict["title"];
    document.querySelectorAll("[data-i18n]").forEach(function (el) {
      var key = el.getAttribute("data-i18n");
      if (dict[key] !== undefined) el.textContent = dict[key];
    });
    document.querySelectorAll(".lang [data-lang]").forEach(function (btn) {
      btn.setAttribute("aria-pressed", btn.getAttribute("data-lang") === lang ? "true" : "false");
    });
    try { window.localStorage.setItem(STORE_KEY, lang); } catch (e) { /* ignore */ }
  }

  document.addEventListener("DOMContentLoaded", function () {
    document.querySelectorAll(".lang [data-lang]").forEach(function (btn) {
      btn.addEventListener("click", function () { apply(btn.getAttribute("data-lang")); });
    });
    apply(detect());
  });
})();
