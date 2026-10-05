#!/usr/bin/env python3
"""Prepare static legal pages. No deployment, SDK activation or Unity/player build.

Preview is deliberately marked DRAFT and noindex. Release mode fails before any
output if metadata or reviewed documents are incomplete. Python standard library.
"""
import argparse
import datetime as dt
import hashlib
import html
import ipaddress
import json
import re
import sys
from pathlib import Path
from urllib.parse import urlsplit, unquote

ROOT = Path(__file__).resolve().parents[1]
LEGAL = ROOT / "QuietCamp/Assets/QuietCamp/Resources/QuietCamp/legal.json"
MANIFEST = ROOT / "Design/Legal/site.json"
MARKERS = re.compile(r"\b(?:DRAFT|ENTWURF|VERIFY)\b|чернетк|\[(?:FILL|DECIDE|VERIFY|OPTIONAL|CURRENT RELEASE|FUTURE|ЗАПОВНИТИ|ОБРАТИ|ПЕРЕВІРИТИ|ЛИШЕ ЯКЩО|ПОТОЧНИЙ|МАЙБУТН|EINTRAGEN)|\{\{[^}]+\}\}", re.I)
LABELS = {
    "en": {"privacy": "Privacy policy", "data-request": "Data requests and deletion", "terms": "Terms of use", "support": "Support", "impressum": "Publisher details"},
    "uk": {"privacy": "Політика приватності", "data-request": "Запити й видалення даних", "terms": "Умови користування", "support": "Підтримка", "impressum": "Реквізити видавця"},
    "de": {"privacy": "Datenschutzerklärung", "data-request": "Datenanfragen und Löschung", "terms": "Nutzungsbedingungen", "support": "Support", "impressum": "Impressum"},
}
CSS = """html{color-scheme:light}body{margin:0;background:#f3efde;color:#233c31;font:1.05rem/1.65 system-ui,sans-serif}header,main,footer{max-width:48rem;margin:auto;padding:1.25rem}header{border-bottom:1px solid #bfcbb7}a{color:#285e47;overflow-wrap:anywhere}a:focus-visible{outline:3px solid #bb793d;outline-offset:4px}nav{display:flex;flex-wrap:wrap;gap:.5rem 1.1rem}h1{font-size:1.9rem;line-height:1.25}h2{font-size:1.3rem;margin-top:2rem}p,li{overflow-wrap:anywhere}.draft{border:2px solid #a66c33;background:#fff5d5;padding:1rem}footer{border-top:1px solid #bfcbb7;font-size:.9rem}code{font-size:.9em}@media(max-width:400px){header,main,footer{padding:1rem}}"""


def public_url(value):
    try:
        u = urlsplit(value)
        host = (u.hostname or "").lower()
        _ = u.port
        try:
            ipaddress.ip_address(host)
            return False
        except ValueError:
            pass
        return (u.scheme == "https" and "." in host and not u.username and not u.password
                and not u.path.lower().endswith(".pdf")
                and host not in {"localhost", "127.0.0.1", "example.com", "example.org", "example.net"}
                and not any(host.endswith(s) for s in (".example", ".invalid", ".test", ".example.com", ".example.org", ".example.net"))
                and "{{" not in unquote(value) and "}}" not in unquote(value))
    except (ValueError, TypeError):
        return False


def safe_link(value):
    """Never execute Markdown/HTML. Only HTTPS, mailto or local static pages."""
    try:
        u = urlsplit(value)
    except ValueError:
        return None
    if u.scheme == "https" and u.netloc and not u.username and not u.password:
        return value
    if u.scheme == "mailto" and re.fullmatch(r"[^\s@<>]+@[^\s@<>]+", u.path) and not u.query:
        return value
    if not u.scheme and not u.netloc and re.fullmatch(r"[a-z0-9-]+\.html(?:#[a-z0-9-]+)?", value):
        return value
    return None


def inline(text):
    result, at = [], 0
    for match in re.finditer(r"\[([^\]\n]+)\]\(([^\s)]+)\)|\*\*([^*\n]+)\*\*|`([^`\n]+)`", text):
        result.append(html.escape(text[at:match.start()]))
        if match.group(1):
            target = safe_link(match.group(2))
            label = html.escape(match.group(1))
            result.append(f'<a href="{html.escape(target, quote=True)}">{label}</a>' if target else label)
        elif match.group(3):
            result.append("<strong>" + html.escape(match.group(3)) + "</strong>")
        else:
            result.append("<code>" + html.escape(match.group(4)) + "</code>")
        at = match.end()
    result.append(html.escape(text[at:]))
    return "".join(result)


def markdown(source):
    """Small, escaped Markdown subset used by these documents, not a browser parser."""
    out, paragraph, list_kind = [], [], None

    def flush():
        if paragraph:
            out.append("<p>" + inline(" ".join(paragraph)) + "</p>")
            paragraph.clear()

    def end_list():
        nonlocal list_kind
        if list_kind:
            out.append("</" + list_kind + ">")
            list_kind = None

    for raw in source.splitlines():
        line = raw.strip()
        if not line:
            flush(); end_list(); continue
        heading = re.match(r"^(#{1,6})\s+(.+)$", line)
        item = re.match(r"^(?:[-*]\s+|\d+\.\s+)(.+)$", line)
        if heading:
            flush(); end_list()
            level = len(heading.group(1))
            out.append(f"<h{level}>" + inline(heading.group(2)) + f"</h{level}>")
        elif item:
            flush()
            kind = "ol" if line[0].isdigit() else "ul"
            if list_kind != kind:
                end_list(); list_kind = kind; out.append("<" + kind + ">")
            out.append("<li>" + inline(item.group(1)) + "</li>")
        elif line == "---":
            flush(); end_list(); out.append("<hr>")
        else:
            end_list(); paragraph.append(line)
    flush(); end_list()
    return "\n".join(out)


def filename(doc):
    return doc["id"] + "-" + doc["language"] + ".html"


def prepare(config, manifest, source_dir, release=False):
    issues, docs = [], []
    names = set()
    for entry in manifest["documents"]:
        if entry.get("id") not in LABELS["en"] or entry.get("language") not in LABELS:
            raise ValueError("Unsupported document or language in site manifest")
        name = filename(entry)
        if name in names:
            raise ValueError("Duplicate page: " + name)
        names.add(name)
        relative = Path(entry["source"])
        if relative.is_absolute() or ".." in relative.parts:
            raise ValueError("Document source must remain inside source directory")
        path = source_dir / relative
        if not path.resolve().is_relative_to(source_dir.resolve()):
            raise ValueError("Document source must remain inside source directory (including symlinks)")
        if not path.is_file():
            issues.append("Missing document: " + entry["source"]); continue
        text = path.read_text(encoding="utf-8")
        # Values come only from explicit configuration, never git identity or personal files.
        for key in ("appName", "publisherName", "supportEmail", "revision", "publishedOn"):
            text = text.replace("{{" + key + "}}", str(config.get(key, "")) or "[FILL: " + key + "]")
        if MARKERS.search(text):
            issues.append("Unresolved draft instructions: " + entry["source"])
        for target in re.findall(r"\[[^\]\n]+\]\(([^\s)]+)\)", text):
            if not safe_link(target):
                issues.append("Unsupported/unsafe document link: " + entry["source"])
        if not text.strip() or not text.startswith("# "):
            issues.append("Missing document title: " + entry["source"])
        if entry["id"] in {"privacy", "data-request", "support"}:
            for key in ("appName", "publisherName", "supportEmail"):
                if not config.get(key) or config[key] not in text:
                    issues.append("Document identity/contact mismatch (" + key + "): " + entry["source"])
        docs.append({**entry, "text": text, "file": name, "sha256": hashlib.sha256(text.encode()).hexdigest()})
    for lang in manifest["languages"]:
        for kind in ("privacy", "data-request", "terms", "support"):
            if not any(d["id"] == kind and d["language"] == lang for d in docs):
                issues.append("Missing localized " + kind + ": " + lang)
    if not config.get("appName") or not config.get("publisherName"):
        issues.append("Fill appName and publisherName in legal.json")
    email = config.get("supportEmail", "")
    if not re.fullmatch(r"[^\s@<>]+@[^\s@<>]+", email) or not public_url("https://" + email.split("@")[-1]):
        issues.append("Supply a monitored, non-placeholder supportEmail")
    if not config.get("revision"):
        issues.append("Missing policy revision")
    try:
        if not re.fullmatch(r"\d{4}-\d{2}-\d{2}", config.get("publishedOn", "")):
            raise ValueError()
        dt.date.fromisoformat(config["publishedOn"])
    except (ValueError, KeyError):
        issues.append("publishedOn must be a valid yyyy-mm-dd date")
    for field, page in manifest["urlFields"].items():
        value = config.get(field, "")
        if field == "accountDeletionUrl" and not config.get("accountsEnabled"):
            continue
        if field == "termsUrl" and not config.get("purchasesEnabled") and not value:
            continue
        if not public_url(value):
            issues.append("Publish a valid HTTPS " + field)
        elif Path(urlsplit(value).path).name != page or page not in names:
            issues.append(field + " must point to the generated " + page)
    if release and issues:
        raise ValueError("Release preparation blocked:\n- " + "\n- ".join(issues))
    return docs, issues


def page(title, language, body, docs, current, config, release):
    def link(d):
        label = LABELS[language][d["id"]]
        return '<a href="' + d["file"] + '">' + html.escape(label) + "</a>"
    nav = " ".join(link(d) for d in docs if d["language"] == language)
    languages = " ".join('<a lang="' + d["language"] + '" hreflang="' + d["language"] + '" href="' + d["file"] + '">' + d["language"].upper() + "</a>"
                         for d in docs if d["id"] == current)
    if current != "impressum":
        nav += " " + " ".join(link(d) for d in docs if d["id"] == "impressum" and d["language"] != language)
    notice = "" if release else '<p class="draft" role="note">DRAFT / ЧЕРНЕТКА / ENTWURF — local preview, not a published policy.</p>'
    robots = "index,follow" if release else "noindex,nofollow"
    stamp = html.escape((config.get("revision") or "[FILL revision]") + " · " + (config.get("publishedOn") or "[FILL date]"))
    return f'''<!doctype html>
<html lang="{language}"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><meta name="robots" content="{robots}"><meta name="referrer" content="no-referrer"><title>{html.escape(title)}</title><link rel="stylesheet" href="style.css"></head>
<body><header><a href="index.html">{html.escape(config.get('appName') or 'QuietCamp')}</a><nav aria-label="Documents">{nav}</nav><nav aria-label="Language">{languages}</nav></header><main>{notice}{body}</main><footer>{stamp}</footer></body></html>
'''


def generate(config, manifest, source_dir, output, release=False):
    docs, issues = prepare(config, manifest, source_dir, release)
    # All validation completes before touching output. Use a fresh directory so
    # old approved pages or hidden stale documents cannot survive a failed pass.
    if output.exists() and any(output.iterdir()):
        raise ValueError("Output directory must be new or empty; existing pages are never overwritten")
    output.mkdir(parents=True, exist_ok=True)
    (output / "style.css").write_text(CSS, encoding="utf-8")
    for d in docs:
        (output / d["file"]).write_text(page(LABELS[d["language"]][d["id"]], d["language"], markdown(d["text"]), docs, d["id"], config, release), encoding="utf-8")
    body = "<h1>" + html.escape(config.get("appName") or "QuietCamp") + "</h1>"
    for lang in manifest["languages"]:
        body += '<h2 lang="' + lang + '">' + lang.upper() + '</h2><ul>'
        body += "".join('<li><a href="' + d["file"] + '">' + html.escape(LABELS[lang][d["id"]]) + "</a></li>" for d in docs if d["language"] == lang)
        body += "</ul>"
    (output / "index.html").write_text(page("QuietCamp documents", "en", body, docs, "privacy", config, release), encoding="utf-8")
    (output / "robots.txt").write_text("User-agent: *\n" + ("Allow: /\n" if release else "Disallow: /\n"), encoding="utf-8")
    (output / ".nojekyll").touch()
    report = {"mode": "release-candidate" if release else "draft-preview", "deployed": False,
              "revision": config.get("revision"), "issues": issues,
              "pages": [{k: d[k] for k in ("id", "language", "file", "sha256")} for d in docs]}
    (output / "preparation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--config", type=Path, default=LEGAL)
    parser.add_argument("--manifest", type=Path, default=MANIFEST)
    parser.add_argument("--source", type=Path, default=ROOT / "Design/Legal/templates")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--release", action="store_true", help="Reject all drafts; prepare pages locally, never publish them")
    args = parser.parse_args()
    try:
        report = generate(json.loads(args.config.read_text()), json.loads(args.manifest.read_text()), args.source, args.output, args.release)
        print(f"Prepared {len(report['pages'])} pages ({report['mode']}); {len(report['issues'])} unresolved items. No deployment.")
        return 0
    except (ValueError, OSError, KeyError) as error:
        print(str(error), file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
