#!/usr/bin/env python3
"""Verify the interactive performance report locally or on GitHub Pages."""
import argparse
import json
import threading
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[2]


class QuietHandler(SimpleHTTPRequestHandler):
    def log_message(self, *_):
        pass


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--url")
    parser.add_argument("--output", type=Path, default=Path("/tmp/quietcamp-performance-browser"))
    args = parser.parse_args(); args.output.mkdir(parents=True, exist_ok=True)
    server = None
    if args.url:
        base = args.url
    else:
        server = ThreadingHTTPServer(("127.0.0.1", 0), partial(QuietHandler, directory=str(ROOT / "docs")))
        threading.Thread(target=server.serve_forever, daemon=True).start()
        base = f"http://127.0.0.1:{server.server_port}/performance.html"
    errors, scenarios = [], []
    try:
        with sync_playwright() as p:
            browser = p.chromium.launch(headless=True, args=["--disable-dev-shm-usage"])
            context = browser.new_context(viewport={"width": 1440, "height": 1000})
            page = context.new_page()
            page.on("pageerror", lambda e: errors.append(str(e)))
            page.on("response", lambda r: errors.append(f"HTTP {r.status}: {r.url}") if r.status >= 400 else None)
            page.goto(base, wait_until="networkidle")
            page.wait_for_function('document.querySelector("#load-status").textContent.includes("terminal stage: finished")')
            assert page.locator("#scenario-rows tr").count() > 20
            assert "max" in page.locator("#trace-info").inner_text()
            page.wait_for_function('document.querySelectorAll("#comparison-rows tr").length >= 12')
            assert page.locator("#dataset").input_value() == "optimized"
            assert "checkpoint" in page.locator("#comparison-source").inner_text()
            scenarios.append("Primary native dataset, table and default real-route timeline load")
            page.locator("#scenario-search").fill("winter")
            assert all("winter" in t.lower() for t in page.locator("#scenario-rows tr").all_inner_texts())
            page.locator("#scenario-search").fill("no-such-scenario")
            assert "Немає сценаріїв" in page.locator("#scenario-rows").inner_text()
            page.locator("#scenario-search").fill("")
            page.locator("#trace-scale").select_option("100")
            page.locator("#timeline").hover(position={"x": 300, "y": 100})
            assert "Frame" in page.locator("#trace-info").inner_text()
            scenarios.append("Scenario search, empty state, capped scale and frame hover")
            paths = {"optimized":"optimized/", "optimized-map":"optimized/roadmap/", "matrix":"", "roadmap":"roadmap/", "pass1":"optimized/pass1/", "pass1-map":"optimized/pass1/roadmap/"}
            for key, path in paths.items():
                summary_url = base.rsplit("/",1)[0]+"/performance/2026-10-09/"+path+"summary.json"
                summary = page.request.get(summary_url).json()
                page.locator("#dataset").select_option(key)
                page.wait_for_function('(n) => document.querySelector("#load-status").textContent.startsWith(n+" кадрів")', arg=summary["frameCount"])
                assert len(summary["scenarios"]) == page.locator("#scenario-rows tr").count()
                assert "2026-10-09/"+path+"summary.json" in page.locator("#summary-download").get_attribute("href")
                if key in ["roadmap","optimized-map","pass1-map"]:
                    page.locator("#trace-select").select_option("stage:ui.map.drag")
                    assert "120 кадрів" in page.locator("#trace-info").inner_text()
            scenarios.append("Six original/first-pass/final native datasets, timelines and matching downloads")
            for width in [320, 390, 768, 1440]:
                page.set_viewport_size({"width": width, "height": 1000})
                assert page.evaluate("document.documentElement.scrollWidth <= innerWidth"), f"Overflow at {width}"
                assert page.locator("#timeline").evaluate("c => c.width > 0 && c.height > 0")
            page.set_viewport_size({"width": 1440, "height": 1000})
            page.locator("#timeline").scroll_into_view_if_needed()
            page.screenshot(path=str(args.output / "performance-timeline.png"))
            page.evaluate('document.querySelectorAll("img").forEach(i => i.loading="eager")')
            page.wait_for_function('Array.from(document.images).every(i => i.complete && i.naturalWidth > 0)')
            scenarios.append("Responsive canvas/table at 320–1440 pixels and all scientific PNGs load")
            plain = browser.new_context(java_script_enabled=False)
            plain_page = plain.new_page(); plain_page.goto(base, wait_until="networkidle")
            assert "Інтерактивні таблиці потребують JavaScript" in plain_page.locator("noscript").inner_text()
            assert plain_page.locator("figure img").count() == 8
            scenarios.append("No-JavaScript report links and four static plots and four verified native UI captures")
            assert not errors, errors
            browser.close()
        receipt = {"result": "Passed", "url": base, "scenarios": scenarios, "pageErrors": errors}
        (args.output / "browser-results.json").write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n")
        print(json.dumps(receipt, ensure_ascii=False))
    finally:
        if server: server.shutdown(); server.server_close()


if __name__ == "__main__":
    main()
