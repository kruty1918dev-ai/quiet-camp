#!/usr/bin/env python3
"""Reproducible catalog + raw-model inventory and Ukrainian requirement matrix."""
import argparse
import collections
import csv
import hashlib
import io
import json
from pathlib import Path
import struct

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/"Design/Roadmap/Assets"
BASE=ROOT/"QuietCamp/Assets/QuietCamp"
CATALOGS=[
    ("Resources/QuietCamp/roadmap_models.json","runtime-available","Kenney/project credits"),
    ("Resources/QuietCamp/roadmap_story_models.json","runtime-available","project procedural geometry"),
    ("Resources/QuietCamp/roadmap_culture_models.json","runtime-available","original-project-owned"),
    ("Authoring/Roadmap/Models/StagingLandmarks/models.json","staging-donor","see StagingLandmarks/ATTRIBUTION.md"),
    ("Authoring/Roadmap/Models/EnvironmentKit/models.json","staging-original","original-project-owned")]


def csv_text(rows):
    stream=io.StringIO(newline="");writer=csv.DictWriter(stream,fieldnames=list(rows[0]),lineterminator="\n");writer.writeheader();writer.writerows(rows);return stream.getvalue()


def files_for():
    rows=[];raw=[]
    for relative,status,license in CATALOGS:
        path=BASE/relative
        for model in json.loads(path.read_text()):
            rows.append(dict(id=model["id"],triangles=len(model["data"])//18,vertices=len(model["data"])//6,
                uvFloats=len(model.get("uvs",[])),packedStreamBytes=4*(len(model["data"])+len(model["colors"])+len(model.get("uvs",[]))),
                sourceHeight=model.get("sourceHeight"),catalog=str(path.relative_to(ROOT)),status=status,license=license))
    rows.sort(key=lambda r:r["id"])
    assert len({r["id"] for r in rows})==len(rows)
    for path in sorted((ROOT/"QuietCamp/Assets").rglob("*")):
        if path.suffix.lower() not in (".obj",".fbx",".glb",".gltf",".blend",".dae",".3ds"):continue
        data=path.read_bytes();triangles=None
        if path.suffix.lower()==".obj":
            triangles=sum(max(0,len(line.split())-3) for line in data.decode().splitlines() if line.startswith("f "))
        elif path.suffix.lower()==".glb":
            n=struct.unpack_from("<I",data,12)[0];g=json.loads(data[20:20+n])
            triangles=sum(g["accessors"][p["indices"]]["count"]//3 for mesh in g.get("meshes",[]) for p in mesh["primitives"] if p.get("mode",4)==4 and "indices" in p)
        raw.append(dict(path=str(path.relative_to(ROOT)),format=path.suffix.lower()[1:],bytes=len(data),triangles=triangles,sha256=hashlib.sha256(data).hexdigest()))
    candidates=json.loads((Path(__file__).parent/"sketchfab-candidates.json").read_text())
    needs=json.loads((Path(__file__).parent/"requirements.json").read_text())
    ids={r["id"] for r in rows}
    for need in needs:
        assert set(need["existing"]+need["added"])<=ids,need["role"]
    counts=collections.Counter(r["status"] for r in rows)
    summary=dict(schemaVersion=1,date="2026-10-08",scope="All five roadmap mesh streams plus raw model files under QuietCamp/Assets; FBX triangle counts require Unity import",
        catalogCounts=dict(counts),catalogModels=len(rows),rawModelFiles=len(raw),newSketchfabDownloads=0,
        nativeBakePerformed=False,models=rows,rawModels=raw)
    md=["# Матриця моделей Quiet Camp — 2026-10-08", "",
        f"У каталогах доступні **{counts['runtime-available']}** моделі; до цієї зміни вже було **9** неопублікованих donor-варіантів літака/корабля. Додано **24 власні** low poly моделі, включно з LOD: **4 208 трикутників**. Повний список: [model-inventory.csv](model-inventory.csv), [model-inventory.json](model-inventory.json).",
        f"Інвентаризовано **{len(raw)}** сирих файлів геометрії: [raw-model-files.csv](raw-model-files.csv). OBJ має точну кількість трикутників після fan-triangulation; GLB — суму triangle primitives; FBX зазначено без неперевіреної кількості. Процедурні story-моделі враховано через їхні mesh streams.","",
        "**Нових завантажень зі Sketchfab немає:** вхід зупинився на 2FA, Google-сеанс завершився. Знайдені кандидати наведені нижче. Власний EnvironmentKit не походить від цих моделей і не видається за імпорт. Наявний Sketchfab-корабель перероблено з уже збереженого атрибутованого GLB.","",
        "Набір доступний редактору та native baker; main/runtime catalog досі legacy. Native bake, новий рендер, Unity import і мобільна продуктивність не перевірені: AGENTS.md блокує новий запуск Editor поряд з активною ADB-сесією.","",
        "## Ціль та межі", "",
        "Камера залишається далекою, orthographic, pitch 55° / yaw 0° / roll 0°. Прості плоскі меші, наявна палітра, спокійні природні зелені й теплі нейтральні тони; насиченість створюється ансамблями. Занедбаність читається через порожні вікна, отвори в дахах, бур'яни й іржу, без урожаю та суцільної сірості.",
        "Українське прочитання має виникати з поєднання хати, двору, заднього городу, лісосмуг, інфраструктури й стриманого власного орнаменту. Окремий вулик або синя облямівка не гарантує впізнавання країни іноземцем. Прапорів, тризубів, текстових підказок і запозиченої російської атрибутики не додається.","",
        "## Наявне → потрібне", "",
        "| Роль / де | Було | Додано / підготовлено | Дія та логіка розміщення |",
        "|---|---|---|---|"]
    for n in needs:
        md.append("| "+n["role"]+" · "+n["scope"]+" | "+(", ".join("`"+s+"`" for s in n["existing"]) or "—")+" | "+(", ".join("`"+s+"`" for s in n["added"]) or "—")+" | "+n["action"]+" "+n["placement"]+" |")
    md += ["", "## Авторинг у цій зміні", "",
        "`ua.abandoned-homestead`: пошкоджена хата повернута фасадом до воріт, сарай/курник/вулик у власному дворі; видалені дерева, які займали місця господарських споруд, додано бур'яни позаду. У roadmap-полях регіонів 0 і 1 врожай замінено на `ua_field_weeds`; це охоплює перші 10 рівнів, але весь регіон 1 простягається до рівня 15. Висота покриву береться з метаданих, не зі значення для соняшника. Зупинка отримала власний мотив на стіні. Непідключений `stone_largeC` у двох шаблонах замінено на реально наявний `stone_largeA`; старий невикористаний metadata-запис залишено для окремого очищення.",
        "Ці зміни рецептури перевіряються inspect → patch з source hash → validate → compose --dry-run. Вони не пересувають вузли, не змінюють puzzle IDs, порядок, unlock rules або save-прогрес. Повне перепланування села, окремий стан обірваних проводів і вилучення старих наметів із roadmap-силуетів залишаються відкритими задачами, а не вигаданою готовністю моделей.","",
        "## Sketchfab: джерела та статус", "",
        "Точні license version, комплект файлів і право на redistribution перевірити після завантаження. CC-BY candidate не означає, що його геометрія вже у Git. Для кожного нового імпорту зберігати автора, URL, ліцензію, SHA-256 сирого файлу та перелік змін. CC-BY-NC не підходить; Free Standard не прирівнювати до CC-BY.","",
        "| Кандидат | Автор / ліцензія | Статус | Адаптація |", "|---|---|---|---|"]
    for c in candidates["candidates"]:
        md.append(f"| [{c['title']}]({c['url']}) | {c['author']} · {c['license']} | {c['status']} | {c['adaptation']} |")
    md += ["", "## Оптимізація й перевірка", "",
        "Нові власні моделі: flat normals, shared palette, без текстур/rig/анімаційних кліпів, 6–932 трикутники на меш. Деталі, невидимі з висоти, не збільшувати штучно. Дві LOD-версії рослинності та дамби; fallen pylon не використовується як жива опора мережі.",
        "Корабель: 2 384 → 1 100 трикутників, coarse 1 366 → 650. Silhouette 181 → 300 навмисно зберігає корпус і щогли; загалом усі ship LOD 3 931 → 2 050. Welded QEM не ріже сітку по кольорах; palette переноситься з найближчих donor faces. Спільні pivot/height запобігають стрибкам масштабу при LOD. UV/textures прибрано, оригінальний donor та CC BY атрибуцію збережено. Це вимір геометрії й payload, не обіцянка FPS.","",
        "[environment-kit-contact-sheet.png](environment-kit-contact-sheet.png) та [ship-lod-contact-sheet.png](ship-lod-contact-sheet.png) — raster inspection справжніх трикутників, не Unity render. Жодного generated concept image не використано як доказ готового меша.","",
        "У старих незмінених runtime streams виявлено 14 вироджених faces: flower_yellowA (1), sign (4), flower_yellowB (8), flower_purpleA (1). Вони зафіксовані як попередній стан, а не приховано виправлені через hand-edit generated Resources. Нові/перероблені меші перевіряються строго без вироджених faces.","",
        "Відтворення й перевірки описані в [tools/roadmap-assets/README.md](../../../tools/roadmap-assets/README.md). Native Bake Main і Bake Staging, сезонні ракурси та mobile metrics проводити лише після безпечного дозволеного запуску Unity. Літак/дамба/судно — окремі staging journeys; вони не додають сюжети в перші 10 main-рівнів.","",
        "## Усі roadmap-моделі", "", "| ID | Трикутники | Каталог / статус |", "|---|---:|---|"]
    for r in rows:md.append(f"| `{r['id']}` | {r['triangles']} | {Path(r['catalog']).name} · {r['status']} |")
    return {OUT/"model-inventory.json":json.dumps(summary,ensure_ascii=False,indent=2)+"\n",OUT/"model-inventory.csv":csv_text(rows),OUT/"raw-model-files.csv":csv_text(raw),OUT/"MODEL-MATRIX-UA.md":"\n".join(md)+"\n"}


def main():
    p=argparse.ArgumentParser();p.add_argument("--check",action="store_true");args=p.parse_args()
    for path,text in files_for().items():
        if args.check:
            if not path.exists() or path.read_text()!=text:raise AssertionError("Stale inventory: "+str(path))
        else:path.parent.mkdir(parents=True,exist_ok=True);path.write_text(text)
    print("PASS model matrix / catalog and raw-file inventory")


if __name__=="__main__":main()
