#!/usr/bin/env python3
"""Local native-photo gallery and explicit visual-review ledger (commercial art stays local)."""
import argparse
import hashlib
import json
import csv
import re
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
CAT = ROOT / "Design/Roadmap/ModelCatalogue"
PHOTOS = CAT / "LocalPreviews~"
LEDGER = CAT / "visual-review.json"
FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"


def entries():
    return [json.loads(p.read_text()) for p in sorted(PHOTOS.glob("M*.json"))]


def sheets():
    rows = entries()
    font = ImageFont.truetype(FONT, 12)
    title = ImageFont.truetype(FONT, 19)
    output = PHOTOS / "sheets"
    output.mkdir(exist_ok=True)
    for start in range(0, len(rows), 20):
        group = rows[start:start + 20]
        if len(group) < 20 and len(rows) != 1210:
            break
        target = output / f"sheet-{start // 20 + 1:02d}.jpg"
        if target.exists():
            continue
        canvas = Image.new("RGB", (1760, 1240), "#f0f0e9")
        draw = ImageDraw.Draw(canvas)
        draw.text((14, 8), f"Native Unity / two views / {group[0]['displayId']} – {group[-1]['displayId']}", font=title, fill="#24352c")
        for i, row in enumerate(group):
            x, y = (i % 4) * 440, 42 + (i // 4) * 238
            for view, photo in enumerate(row.get("images") or []):
                with Image.open(PHOTOS / photo) as im:
                    canvas.paste(im.convert("RGB").resize((212, 196)), (x + 5 + view * 218, y))
            draw.text((x + 5, y + 198), row["displayId"] + "  " + row["name"][:49], font=font, fill="#12291d")
            draw.text((x + 5, y + 214), f"{row['triangles']:,} tris · " + row["materialBinding"], font=font, fill="#44554b")
        canvas.save(target, quality=90)
    print(f"{len(rows)} native receipts; {len(list(output.glob('*.jpg')))} contact sheets")


def accept(number, note):
    sheet = PHOTOS / "sheets" / f"sheet-{number:02d}.jpg"
    if not sheet.exists():
        raise SystemExit("Render sheet first, inspect its actual image, then explicitly record the review")
    rows = entries()[(number - 1) * 20:number * 20]
    state = json.loads(LEDGER.read_text()) if LEDGER.exists() else {"date": "2026-10-10", "method": "Native OpenGL photographs, two views per source; agent visual inspection of contact sheets and enlarged exceptions", "sheets": {}}
    state["sheets"][str(number)] = {"imageSha256": hashlib.sha256(sheet.read_bytes()).hexdigest(), "models": [r["id"] for r in rows], "displayIds": [r["displayId"] for r in rows], "observationUk": note}
    CAT.mkdir(exist_ok=True)
    LEDGER.write_text(json.dumps(state, ensure_ascii=False, indent=2) + "\n")
    print(f"Recorded explicit visual review of {len(rows)} models on sheet {number}")


NOUNS = {
    "whitewashed_house": "Побілена українська хата", "abandoned_house": "Стара хата",
    "barn": "Комора", "beehive": "Вулик", "bus_shelter_mosaic": "Сільська зупинка — основа під мозаїку",
    "bus_shelter": "Сільська зупинка", "chicken_coop": "Курник", "chicken": "Курка",
    "dam_breached": "Пошкоджена гребля", "field_weeds": "Польове різнотрав’я",
    "forester_hut": "Хатина лісника", "hydro_service_building": "Службова будівля греблі",
    "mooring_post": "Швартовий стовпчик", "picket_fence_damaged": "Пошкоджений штахетник",
    "picket_fence": "Штахетник", "plank_bridge": "Дощана переправа", "poplar": "Тополя",
    "power_pylon_fallen": "Повалена електроопора", "power_pylon_rusted": "Іржава електроопора",
    "power_pylon": "Висока електроопора", "reed_clump": "Пучок очерету",
    "well_sweep": "Криниця-журавель", "young_willow": "Молода верба", "concrete_fence": "Бетонна огорожа",
    "orchard_tree": "Садове дерево", "rural_pole": "Сільський електростовп",
    "sunflower_patch": "Група соняшників", "wattle_fence": "Плетений тин",
    "wheat_patch": "Пшеничний край", "gate": "Ворота", "well": "Криниця",
    "aircraft_fore": "Передня частина вигаданого літака", "aircraft_tail": "Хвостова частина вигаданого літака",
    "cargo_ship": "Вигадане вантажне судно",
}

# Specific forms precede broad families; the original unambiguous model name is always retained.
FORMS = [
    ("Tree_Fruit.*Fruit", "Окремі плоди для садового дерева"),
    ("MushroomHouse", "Казковий будиночок-гриб"), ("Mushroom_Group|Mushroom_Sparse", "Група лісових грибів"),
    ("Mushroom", "Лісовий гриб"), ("Grass_Tall", "Висока трава"), ("Grass_Short", "Низька трава"),
    ("Grass_Med", "Трава середньої висоти"), ("Grass_Large", "Густий трав’яний килим"),
    ("Wildflowers_Patch", "Велике поле польових квітів"), ("Wildflowers", "Польові квіти"),
    ("Sunflower", "Соняшник"), ("Rapeseed|CropField", "Жовті польові рослини"),
    ("LillyPads|Lillies", "Латаття"), ("Reeds", "Прибережний рогіз"), ("Toetoe", "Високі пухнасті злаки"),
    ("Tree_Birch", "Береза"), ("Tree_Fruit", "Плодове дерево"), ("Tree_Meadow", "Лугове листяне дерево"),
    ("Tree_Mangrove", "Мангрове дерево"), ("Tree_Dead|TreeDead", "Сухе дерево"), ("TreeStump|Stump", "Пень із корінням"),
    ("Tree_Root|Tree_Roots", "Корені або окремий стовбур"), ("Tree_Beard", "Плаский мох на дереві"),
    ("Background_Trees|Background_Tree", "Плаский дальній силует дерев"), ("Tree", "Дерево"),
    ("Bush_Bramble", "Голий терновий кущ"), ("Bush", "Кущ"), ("Grass|Ground_Cover", "Трав’яні пучки"),
    ("Rock_Cliff", "Вертикальна скеля"), ("Rock_Pile|Small_Rocks|Rock_Small_Pile|Rubble_Pebbles", "Група каміння"),
    ("Rock|Rubble_Stone", "Гранчастий валун"), ("Leaves_Pile", "Опале листя"), ("Leaves_Branch", "Осіння гілка"),
    ("Swamp_Log|Log", "Колода або гілка"), ("MossMound", "Моховий горбок"), ("Ground_Cliff", "Земляний острів"),
    ("Ground_Mound|Background_Hill|Mountains_Soft", "М’який пагорб"), ("Mountains", "Дальній гірський силует"),
    ("Water_Plane|Ocean_Tile", "Водна поверхня"), ("Swamp_Scum", "Пласка поверхнева рослинність"),
    ("Beach", "Піщаний берег"), ("DirtRoad", "Ґрунтова колія"), ("GrassPath|Path", "Доріжка"),
    ("Road|Sidewalk|Street_Divider", "Модуль дороги або тротуару"), ("Apartment", "Модуль цегляного житла"),
    ("Office", "Модуль офісної будівлі"), ("CityHall", "Адміністративна будівля"), ("Shop", "Модуль магазину"),
    ("Station", "Станційна будівля"), ("Stone_Cabin|WoodenShack|Shack", "Невелика хатина"),
    ("Outhouse|Portaloo", "Господарський туалет"), ("Warehouse", "Склад"), ("House|SmallBuilding", "Будинок"),
    ("Tent", "Намет"), ("Bridge", "Міст"), ("Jetty|Dock", "Дерев’яний настил або його деталь"),
    ("Camp_Fire|Fireplace", "Кострище або казанок"), ("Camp_Crate|Crate|Slatbox", "Дерев’яний ящик"),
    ("Camp_Bucket", "Відро"), ("Camp_Can", "Посудина"), ("Camp_Pouch", "Мішечок"),
    ("Camp_Tanning", "Рама зі шкурою"), ("Well", "Криниця"), ("WindChime", "Підвісний дерев’яний дзвіночок"),
    ("Windmill", "Дерев’яний млин або його деталь"), ("WaterWheel", "Колесо водяного млина"),
    ("Birdhouse", "Шпаківня"), ("HandCart|Wagon", "Візок"), ("HorseHitch", "Конов’язь"),
    ("ScareCrow", "Опудало"), ("Washingline", "Мотузка для білизни"), ("PicnicTable", "Стіл із лавами"),
    ("ParkBench|Couch", "Лавка"), ("Deckchair", "Шезлонг"), ("Wooden_Table|Table", "Дерев’яний стіл"),
    ("Wooden_Broom", "Граблі"), ("Wooden_Ladder", "Драбина"), ("Wooden_Bath", "Дерев’яна діжка"),
    ("Planter|PotPlant|Flower", "Декоративна рослина або квіткова скринька"),
    ("CemeteryFence|WireFence|BaseWall|Range_Wall", "Огорожа спеціального призначення"),
    ("Fence|Gate", "Дерев’яна огорожа або ворітця"), ("StoneWall|Stonewall", "Кам’яна огорожа"),
    ("StoneRunes|Stone_Arch|Stone_Stack|Sword_Stone", "Фантазійний кам’яний мотив"),
    ("StonePile", "Кам’яна пірамідка"), ("BusStop|Bustop", "Зупинка або знак зупинки"),
    ("Warpgate|SciFi|Steam_Car", "Фантазійна споруда або реквізит"), ("Characters", "Збірка персонажів"),
    ("Bedroll", "Скручений каремат"), ("Bag", "Ранець"), ("Armor", "Бронежилет"),
    ("Helmet", "Шолом"), ("Hair|Female_[0-9]", "Зачіска"), ("Beard", "Борода"),
    ("Hat", "Головний убір"), ("Patch", "Шеврон"), ("Pouch", "Тактична кишеня"),
    ("Scarf", "Шарф"), ("Facemask|GasMask", "Маска"), ("Glasses", "Окуляри"),
    ("Earmuffs|Eyepatch", "Аксесуар персонажа"), ("Tombstone", "Могильний камінь"),
    ("Bone|Skull|Gore|Shrunken|Effigy|Ritual", "Моторошний реквізит"), ("Canoe", "Дерев’яне каное"),
    ("Boat|Airboat", "Човен"), ("Tank", "Танк"), ("Armored", "Військова машина"),
    ("Car|Truck|Bike|Buggy|Van", "Транспортний засіб"), ("Wep|Ammo|Missile|C4|Bomb|Weapon|TankTrap|Target", "Зброя або бойовий реквізит"),
    ("FX_|Particle|SunShaft|DamageZone", "Геометрія спецефекту"), ("Cloud|Skydome|Fog", "Хмарний або атмосферний елемент"),
    ("Power|Transformer|Generator", "Електрообладнання"), ("Pipe|Vents|Aircon", "Труба або технічне обладнання"),
    ("LightPole|TrafficLight|Spotlight", "Ліхтар або світлофор"), ("Billboard|LargeSign|Poster|Sign", "Вивіска або реклама"),
    ("Trash|Rubbish|Skip", "Сміття або контейнер"), ("Pallet", "Дерев’яний піддон"),
    ("Wirespool", "Дерев’яна котушка"), ("WoodPlank|Rubble_Plank|Rubble_Pile", "Деревина або уламки"),
    ("Barrel|Propane", "Бочка або балон"), ("Cardboard|Carboard", "Картонна коробка"),
    ("Medical|Bandage|Gauze|Pills", "Медичний реквізит"), ("Kite", "Повітряний змій"),
    ("Key|Padlock", "Ключ або замок"), ("DreamCatch", "Ловець снів"), ("Window|Door|Cover|FireEscape|Spire", "Архітектурна деталь"),
    ("Concrete|Cinderblock|Port", "Бетонний будівельний модуль"), ("Tire", "Шина"),
    ("Wreckage|Junk", "Уламок або металобрухт"), ("Paper|Newspaper", "Папір або газети"),
    ("Manhole", "Кришка люка"), ("Mailbox", "Поштова скринька"), ("SecurityCamera|SatDish|ATM|ParkingMeter", "Міське технічне обладнання"),
    ("Iron_Sheet", "Іржавий хвилястий лист"), ("Chair|BunkBed|Benchpress", "Меблі або обладнання"),
    ("Soda|Hotdog|Burger|Buger", "Міський харчовий реквізит"), ("Umbrella", "Парасолька"),
    ("Barricade|Barrier|Cone", "Дорожній бар’єр або огородження"), ("Container", "Вантажний контейнер"),
    ("EmergencyDrop|Parachute", "Парашутний вантаж або купол"), ("GuardTower|RadioTower", "Висока вежа"),
    ("HeadBase", "Голова-манекен"), ("Needle", "Шприц"), ("Range_Trable", "Стіл із навісом"),
    ("Plane", "Літак"), ("Roof_Access", "Вихід на дах"), ("Skyline", "Дальній міський силует"),
    ("SubwayEntrance", "Вхід до метро"), ("WaterEdge", "Бетонний береговий модуль"), ("Hydrant", "Пожежний гідрант"),
    ("Light_Attachment", "Кронштейн ліхтаря"), ("Phones", "Телефонний апарат"), ("Police_Baton", "Поліцейський кийок"),
    ("Skylight", "Мансардне вікно"), ("SmartPhone", "Телефон"), ("Water_Tower", "Дерев’яна водонапірна вежа"),
    ("Steering_Wheel", "Кермо"), ("StoneArch|StoneStack|Stone_Hole", "Фантазійний кам’яний мотив"),
    ("Sword", "Меч"), ("Background_Land", "Плаский дальній ландшафт"), ("Swamp_Leaves", "Широке болотне листя"),
    ("Swamp_Lantern", "Моторошне ліхтарне дерево"), ("Swamp_Trunk", "Пень із корінням"),
    ("Flame_FX|LightRay", "Геометрія полум’я або світлового FX"),
]


def title(row):
    name = row["name"]
    if name.startswith(("ua_", "staging_")):
        clean = re.sub(r"_(lod|coarse|silhouette)$", "", re.sub(r"^(ua|staging)_", "", name))
        label = NOUNS.get(clean, clean)
    else:
        label = next((label for pattern, label in FORMS if re.search(pattern, name, re.I)), "Модульний реквізит")
    if "Collision" in row["path"]:
        label = "Колізійна оболонка: " + label.lower()
    match = re.search(r"_([0-9]+)$", name)
    if match:
        label += " · варіант " + match.group(1)
    if name.endswith(("_lod", "_coarse", "_silhouette")):
        label += " · спрощений варіант"
    return label


def decision(row):
    n, path = row["name"], row["path"]
    number = int(row["displayId"][1:])
    if "Collision" in path:
        return "службова геометрія", "службове", "Колізії; не показувати як декоративний об’єкт."
    if number in (34, 696, 820):
        return "помилка матеріалу", "блоковано", "Рожевий native render; виправити shader і повторити огляд перед використанням."
    if number in (1, 361, 366):
        return "персонажі", "потребує адаптації", "Збірка має накладені фігури; для окремого персонажа вибрати prefab."
    if re.search(r"FX_|Particle|SunShaft|DamageZone|Cloud|Skydome|Fog", n, re.I):
        return "FX / атмосфера", "контекст FX", "Оглянуто лише геометрію. Анімація, прозорість і повний ефект потребують окремої сцени."
    if re.search(r"MushroomHouse|Warpgate|SciFi|Steam_Car|Runes|Stone_Arch|Stone_Stack|Sword", n, re.I):
        return "фантазійне", "резерв", "Не відповідає першому побутовому українському набору."
    if re.search(r"Bone|Skull|Gore|Tombstone|Shrunken|Effigy|Ritual|Cemetery|DreamCatch|Swamp_Lantern", n, re.I):
        return "моторошний реквізит", "виключено зі стилю", "Не використовувати в затишних діорамах."
    if re.search(r"Wep|Ammo|Missile|C4|Bomb|Weapon|Armor|Armored|Helmet|Tank|Grenade|Parachute|EmergencyDrop|GuardTower|Target|Range_|BaseWall|WireFence|Barrier|Barricade", n, re.I):
        return "бойовий реквізит", "виключено зі стилю", "Зберегти в бібліотеці, не включати в перші 12 затишних діорам."
    if re.search(r"Char|Chr|PickUp", n, re.I):
        return "персонажний реквізит", "резерв", "Цивільні каремати/ранці можливі після адаптації кольорів; не основа ландшафту."
    if n.startswith("staging_") or re.search(r"power_pylon|dam_breached|hydro_service|rural_pole", n):
        return "велика інфраструктура", "резерв", "Окремий цілісний сюжетний ансамбль після затвердження стилю; не центр перших галявин."
    if re.search(r"Tree_Mangrove|Toetoe", n):
        return "географічний резерв", "резерв", "Мангри та екзотичні злаки не застосовувати до українських ставків."
    if number in (786, 787, 900, 901, 977, 978, 979, 1067, 1068):
        return "неоформлена геометрія", "потребує матеріалу", "У вихідній фотографії embedded material без потрібної палітри/текстури. Спершу прив’язати матеріал."
    if row["package"] == "Assets/PolygonCity" and number not in (497, 498, 499, 571, 575, 585, 586, 587, 588, 657, 676, 677, 678):
        return "місто / інфраструктура", "резерв", "Майбутня околиця або містечко; англомовні написи адаптувати. Не використовувати випадкові модулі."
    if re.search(r"Tree|Bush|Grass|Flowers|Sunflower|Rapeseed|CropField|Lill|Reeds|Moss|Ground|Beach|Rock|Mountains|Leaves|Log|Stump|Mushroom|field_weeds|poplar|willow|orchard|wheat|reed", n, re.I):
        if row["triangles"] > 25000:
            return "природа", "потребує LOD", "Source root містить густу геометрію та/або всі LOD. Для roadmap вибрати одну легку деталізацію, перевірити силует у малому масштабі."
        return "природа", "кандидат", "Природний край, тло або малий акцент. Зберегти відкритий центр і прохід до галявини."
    if n.startswith("ua_"):
        return "українська садиба", "кандидат", "Цілісний двір із власною огорожею, воротами та підходом; зв’язати ролі одного власника."
    if re.search(r"Well|Camp|Birdhouse|Fence|Gate|HandCart|ScareCrow|Washingline|Canoe|WindChime|Wooden_|Picnic|ParkBench|Table|Dock|Bridge|Jetty|Crate|Pallet|Wirespool", n, re.I):
        return "побут / туризм", "кандидат", "Один-два побутові акценти біля краю галявини; огорожі/настили мають власника й реальний підхід."
    return "додатковий реквізит", "резерв", "Зберегти для окремого контексту після затвердження основних 12 композицій."


def build():
    rows = entries()
    study_uses = {}
    study_root = ROOT / 'Design/Roadmap/DioramaStudies/2026-10-10'
    for receipt_path in sorted(study_root.glob('D*.json')):
        receipt = json.loads(receipt_path.read_text())
        for key in receipt.get('donorCatalogueIds', []) + receipt.get('usedAssets', []):
            study_uses.setdefault(key, []).append(receipt['id'])
    state = json.loads(LEDGER.read_text())
    reviewed = {asset: (int(sheet), note) for sheet, note in state["sheets"].items() for asset in note["models"]}
    if len(rows) != 1210 or set(reviewed) != {r["id"] for r in rows}:
        raise SystemExit("Every source requires an explicit visual review before catalogue publication")
    for number, record in state["sheets"].items():
        sheet = PHOTOS / "sheets" / f"sheet-{int(number):02d}.jpg"
        if hashlib.sha256(sheet.read_bytes()).hexdigest() != record["imageSha256"]:
            raise SystemExit(f"Sheet {number} changed after its review; review it again")
    for row in rows:
        if row["error"] or len(row["images"]) != 2:
            raise SystemExit("Missing native photos: " + row["displayId"])
        sheet, review = reviewed[row["id"]]
        category, fit, use = decision(row)
        row.update(titleUk=title(row), categoryUk=category, fitUk=fit, useUk=use,
                   descriptionUk=f"{title(row)}. Габарити джерела: {row['bounds'][0]:.2f} × {row['bounds'][1]:.2f} × {row['bounds'][2]:.2f} м (ширина × висота × глибина). {use}",
                   visualReview={"date": state["date"], "sheet": sheet, "observationUk": review["observationUk"],
                                 "photoHashes": [hashlib.sha256((PHOTOS / im).read_bytes()).hexdigest() for im in row["images"]]},
                   status="visually-reviewed",
                   dioramaStudyUses=sorted(set(study_uses.get(row['displayId'], []) + study_uses.get(row['name'], []))))
    catalogue = {"schemaVersion": 1, "date": state["date"], "models": rows, "coverage": {"sourceModels": len(rows), "nativePhotos": len(rows) * 2, "visuallyReviewed": len(reviewed)}, "scope": "Source model geometry + materials in two static native views, including LOD/collision; no particle-system animation or mobile-performance acceptance"}
    (CAT / "catalogue.json").write_text(json.dumps(catalogue, ensure_ascii=False, indent=2) + "\n")
    with (CAT / "catalogue.csv").open("w", newline="") as out:
        keys = ["displayId", "id", "titleUk", "categoryUk", "fitUk", "descriptionUk", "path", "triangles", "sourceHash", "dioramaStudyUses"]
        writer = csv.DictWriter(out, keys, extrasaction="ignore",lineterminator="\n"); writer.writeheader()
        writer.writerows(dict(row,dioramaStudyUses=';'.join(row['dioramaStudyUses'])) for row in rows)
    markup = (ROOT / "tools/model_catalogue.html").read_text()
    (CAT / "index.html").write_text(markup.replace("/*CATALOGUE_DATA*/", json.dumps(catalogue, ensure_ascii=False).replace("</", "<\\/")))
    print(f"Published descriptions and explicit review evidence for {len(rows)} models")


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=["sheets", "accept", "build"])
    parser.add_argument("--sheet", type=int)
    parser.add_argument("--note")
    args = parser.parse_args()
    if args.command == "sheets":
        sheets()
    elif args.command == "build":
        build()
    elif not args.sheet or not args.note:
        parser.error("Explicit review requires --sheet and --note")
    else:
        accept(args.sheet, args.note)
