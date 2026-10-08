"use strict";
(function () {
  var data, frames = [], selected = [], requestId = 0;
  var dataset = document.getElementById("dataset"), search = document.getElementById("scenario-search");
  var trace = document.getElementById("trace-select"), scale = document.getElementById("trace-scale");
  var canvas = document.getElementById("timeline"), info = document.getElementById("trace-info");
  var status = document.getElementById("load-status"), view = {};
  function cell(row, value) { var td = document.createElement("td"); td.textContent = value; row.appendChild(td); }
  function ms(value) { return value === null || value === undefined ? "—" : value.toFixed(2); }
  function renderTable() {
    var body = document.getElementById("scenario-rows"); body.replaceChildren();
    var query = search.value.trim().toLocaleLowerCase("uk");
    data.scenarios.filter(function (s) { return s.stage.toLocaleLowerCase("uk").includes(query); }).forEach(function (s) {
      var row = document.createElement("tr");
      [s.stage, s.frames, ms(s.wallMs.p50), ms(s.wallMs.p95), ms(s.wallMs.max), ms(s["Main Thread"] && s["Main Thread"].p95), s.wallOver100Ms].forEach(function (v) { cell(row, v); });
      body.appendChild(row);
    });
    if (!body.children.length) { var row = document.createElement("tr"); cell(row, "Немає сценаріїв за цим запитом."); row.firstChild.colSpan = 7; body.appendChild(row); }
  }
  function renderMethods() {
    var body = document.getElementById("method-rows"); body.replaceChildren();
    data.methods.slice(0, 18).forEach(function (m) {
      var row = document.createElement("tr"); [m.name, m.samples, ms(m.p50), ms(m.p95), ms(m.max), m.worst.stage].forEach(function (v) { cell(row, v); }); body.appendChild(row);
    });
    document.getElementById("counter-warning").textContent = data.counterHealth.filter(function (c) { return !c.usable; }).map(function (c) { return c.name; }).join(", ") + ". Значення лише нульові: їхня вартість не встановлена. Render Thread у цьому run недоступний.";
  }
  function selectTrace() {
    var snapshotFrames = new Set(data.excludedSnapshotFrames);
    if (trace.value.startsWith("op:")) {
      var op = data.operations[Number(trace.value.slice(3))];
      selected = frames.filter(function (f) { return f.timestamp >= op.startMs && f.timestamp <= op.endMs && !snapshotFrames.has(f.frame); });
    } else selected = frames.filter(function (f) { return f.stage === trace.value.slice(6) && !snapshotFrames.has(f.frame); });
    draw();
  }
  function draw() {
    var width = canvas.clientWidth, height = canvas.clientHeight, dpr = window.devicePixelRatio || 1;
    canvas.width = width * dpr; canvas.height = height * dpr;
    var ctx = canvas.getContext("2d"); ctx.scale(dpr, dpr); ctx.clearRect(0, 0, width, height);
    if (!selected.length) { info.textContent = "Для цього вікна немає кадрів."; return; }
    var first = selected[0].timestamp, last = selected[selected.length - 1].timestamp;
    var maximum = Math.max.apply(null, selected.map(function (f) { return f.wall; }));
    var ceiling = scale.value === "100" ? 100 : Math.max(100, Math.ceil(maximum / 100) * 100);
    view = { first: first, last: last, width: width, height: height, left: 53, top: 18, right: width - 15, bottom: height - 38 };
    function x(t) { return view.left + (t - first) / Math.max(1, last - first) * (view.right - view.left); }
    function y(t) { return view.bottom - Math.min(t, ceiling) / ceiling * (view.bottom - view.top); }
    ctx.font = "12px system-ui"; ctx.fillStyle = "#637168"; ctx.strokeStyle = "#d7ded3";
    for (var i = 0; i <= 4; i++) {
      var value = ceiling * i / 4; ctx.beginPath(); ctx.moveTo(view.left, y(value)); ctx.lineTo(view.right, y(value)); ctx.stroke(); ctx.fillText(value.toFixed(0) + " ms", 2, y(value) + 4);
      ctx.fillText(((last - first) * i / 4000).toFixed(1) + " s", view.left + (view.right - view.left) * i / 4 - (i === 4 ? 25 : 0), height - 10);
    }
    ctx.setLineDash([4, 4]); ctx.strokeStyle = "#ad6746"; ctx.beginPath(); ctx.moveTo(view.left, y(33.33)); ctx.lineTo(view.right, y(33.33)); ctx.stroke(); ctx.setLineDash([]);
    selected.forEach(function (f) { ctx.strokeStyle = f.wall > ceiling ? "#b64032" : "#3f6954"; ctx.beginPath(); ctx.moveTo(x(f.timestamp), view.bottom); ctx.lineTo(x(f.timestamp), y(f.wall)); ctx.stroke(); });
    info.textContent = selected.length + " кадрів · max " + ms(maximum) + " мс · " + selected.filter(function (f) { return f.wall > 100; }).length + " кадрів >100 мс. Wall включає Editor та ОС.";
  }
  canvas.addEventListener("pointermove", function (event) {
    if (!selected.length) return;
    var relative = event.clientX - canvas.getBoundingClientRect().left;
    var at = view.first + (relative - view.left) / (view.right - view.left) * (view.last - view.first);
    var nearest = selected.reduce(function (a, b) { return Math.abs(a.timestamp - at) < Math.abs(b.timestamp - at) ? a : b; });
    info.textContent = "Frame " + nearest.frame + " · " + ms(nearest.wall) + " мс · " + nearest.stage + " · " + nearest.transition;
  });
  canvas.addEventListener("pointerleave", draw);
  async function load() {
    var current = ++requestId, base = "performance/2026-10-09/" + (dataset.value === "roadmap" ? "roadmap/" : "");
    status.textContent = "Завантажую дані…"; trace.disabled = true;
    try {
      var values = await Promise.all([fetch(base + "summary.json").then(function (r) { if (!r.ok) throw new Error("Summary HTTP " + r.status); return r.json(); }), fetch(base + "frames.csv").then(function (r) { if (!r.ok) throw new Error("Frames HTTP " + r.status); return r.text(); })]);
      if (current !== requestId) return;
      data = values[0]; frames = values[1].trim().split(/\r?\n/).slice(1).map(function (line) { var cells = line.split(","); return { frame: Number(cells[0]), timestamp: Number(cells[1]), wall: Number(cells[2]), stage: cells[3], transition: cells[4] }; });
      document.getElementById("summary-download").href = base + "summary.json";
      document.getElementById("frames-download").href = base + "frames.csv";
      document.getElementById("raw-download").href = base + "editor-audit.json.gz";
      status.textContent = data.frameCount + " кадрів · " + data.spanCount + " scoped вимірів · " + data.capturedUtc + " · terminal stage: " + data.terminalStage;
      renderTable(); renderMethods(); trace.replaceChildren();
      data.operations.forEach(function (op, index) { var option = new Option(op.label + " · " + (op.elapsedMs / 1000).toFixed(2) + " с", "op:" + index); trace.add(option); });
      data.scenarios.filter(function (s) { return s.frames >= 20; }).forEach(function (s) { trace.add(new Option(s.stage + " · " + s.frames + " кадрів", "stage:" + s.stage)); });
      var target = data.operations.findIndex(function (op) { return op.label === "route.high.normal.r2.menu-camp"; });
      trace.value = target >= 0 ? "op:" + target : "stage:ui.map.sweep"; trace.disabled = false; selectTrace();
    } catch (error) { if (current === requestId) status.textContent = "Не вдалося завантажити інтерактивні дані: " + error.message + ". Нижче доступні статичні графіки й посилання на звіт."; }
  }
  search.addEventListener("input", function () { if (data) renderTable(); });
  dataset.addEventListener("change", load); trace.addEventListener("change", selectTrace); scale.addEventListener("change", draw);
  window.addEventListener("resize", function () { if (selected.length) draw(); }); load();
}());
