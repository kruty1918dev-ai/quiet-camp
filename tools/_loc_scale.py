import json
UK={};EN={};DE={}
def add(k,uk,en,de): UK[k]=uk; EN[k]=en; DE[k]=de

add("district.farms.title","Робочі поля","Working fields","Bewirtschaftete Felder")
add("district.farms.intro","Поля й ферми, що знову годують дорогу. Люди повертаються туди, де є місце заночувати.","Fields and farms feeding the road again. People return where there is a place to sleep.","Felder und Höfe, die den Weg wieder versorgen. Menschen kehren zurück, wo es einen Schlafplatz gibt.")
add("district.mills.title","Млини на річці","Mills on the river","Mühlen am Fluss")
add("district.mills.intro","Старі млини чекають на нові руки. Береги тут вузькі — рахуйте кроки до води.","Old mills await new hands. The banks are narrow — mind your steps near the water.","Alte Mühlen warten auf neue Hände. Die Ufer sind schmal — zählt eure Schritte zum Wasser.")
add("district.villages.title","Села, що повстають","Villages rising again","Dörfer im Aufbruch")
add("district.villages.intro","Половина села стоїть, половина — руїни. Ваші табори вирішують, які двори знову житимуть.","Half the village stands, half is ruin. Your camps decide which yards will live again.","Das halbe Dorf steht, die andere Hälfte ist Ruine. Eure Lager entscheiden, welche Höfe wieder leben.")
add("district.passes.title","Гірські перевали","Mountain passes","Bergpässe")
add("district.passes.intro","Перевали пам'ятають кожного, хто йшов. У холоді й тумані вогонь важить удвічі більше.","The passes remember everyone who crossed. In cold and mist a fire counts double.","Die Pässe erinnern sich an jeden, der sie überquerte. In Kälte und Nebel zählt ein Feuer doppelt.")
add("district.resort.title","Покинутий курорт","The abandoned resort","Der verlassene Ferienort")
add("district.resort.intro","Курорт лишився в останнє літо. Дощові візерунки на альтанках — історії, які ще ніхто не дочитав.","The resort is frozen in its last summer. Rain patterns on the gazebos are stories nobody finished.","Der Ferienort blieb in seinem letzten Sommer stehen. Regenmuster auf den Pavillons sind Geschichten, die niemand zu Ende las.")
add("district.coast.title","Солоний берег","The salt shore","Die Salzküste")
add("district.coast.intro","Берег пахне сіллю й дьогтем. Звідси видно маяк — хтось нарешті вмикає світло.","The shore smells of salt and tar. The lighthouse is visible from here — someone finally lit it.","Das Ufer riecht nach Salz und Teer. Von hier sieht man den Leuchtturm — jemand hat das Licht endlich angezündet.")
add("district.highlands.title","Високогір'я","The highlands","Das Hochland")
add("district.highlands.intro","Високо стежки вузькі й небезпечні. Місця, які ви влаштуєте тут, врятують не одного мандрівника.","Up high the trails are narrow and dangerous. Places you arrange here will save more than one traveler.","Oben sind die Pfade schmal und gefährlich. Die Orte, die ihr hier herrichtet, retten mehr als einen Wanderer.")
add("district.frontier.title","Далекий кордон","The far frontier","Die ferne Grenze")
add("district.frontier.intro","Остання нанесена дорога. Далі — лише сліди тих, хто йшов до вас, і тих, хто піде після.","The last mapped road. Beyond it only tracks of those who walked before you — and those who will follow.","Die letzte eingezeichnete Straße. Dahinter nur Spuren derer, die vor euch gingen — und jener, die folgen werden.")
add("district.haven.title","Гавань","The haven","Der Hafen")
add("district.haven.intro","Кінець головної дороги — і початок усього іншого. Гавань приймає всіх, хто пройшов.","The end of the main road — and the start of everything else. The haven welcomes everyone who walked it.","Das Ende des Hauptwegs — und der Anfang von allem anderen. Der Hafen heißt alle willkommen, die ihn gingen.")

add("journey.level.named","{0} — {1}","{0} — {1}","{0} — {1}")
add("map.branch.sideRoute","Сюжетна гілка","Story branch","Nebenhandlung")
add("journey.garden.title","Теплиця","The greenhouse","Das Gewächshaus")
add("journey.garden.description","Стара теплиця між селами. Двадцять вісім галявин про те, як мертве місце знову стає живим. Відкривається за жаринки.","An old greenhouse between villages. Twenty-eight glades about a dead place turning alive again. Unlocks with embers.","Ein altes Gewächshaus zwischen den Dörfern. Achtundzwanzig Lichtungen darüber, wie ein toter Ort wieder lebendig wird. Schaltet mit Glut frei.")
add("journey.station.title","Стара станція","The old station","Der alte Bahnhof")
add("journey.station.description","Перон, де потяги не ходять вже десять років — але люди знову чекають тут на зустрічі. Вісімнадцять зупинок, відкривається за жаринки.","A platform where no train has stopped in ten years — yet people wait here for reunions again. Eighteen stops, unlocks with embers.","Ein Bahnsteig, auf dem seit zehn Jahren kein Zug hält — doch Menschen warten hier wieder auf Wiedersehen. Achtzehn Haltestellen, mit Glut freischaltbar.")
add("journey.mountain.title","Гірський маршрут","The mountain route","Die Bergroute")
add("journey.mountain.description","Двадцять чотири табори вгору через перевали. Той самий шарф, що знайшовся біля маяка, колись пройшов цим шляхом.","Twenty-four camps upward through the passes. The scarf found near the lighthouse once walked this road.","Vierundzwanzig Lager hinauf durch die Pässe. Der Schal, der am Leuchtturm gefunden wurde, ging einst diesen Weg.")
add("journey.resort.title","Покинутий курорт","The abandoned resort","Der verlassene Ferienort")
add("journey.resort.description","Тридцять галявин у курорті, що лишився в останнє літо. Ремонтуйте, читайте чужі записки й вирішуйте, що лишиться позаду.","Thirty glades in a resort stuck in its last summer. Repair, read strangers' notes, and decide what stays behind.","Dreißig Lichtungen in einem Ferienort, der in seinem letzten Sommer steckenblieb. Repariert, lest fremde Notizen und entscheidet, was zurückbleibt.")
add("journey.coast.title","Узбережжя","The coastline","Die Küstenlinie")
add("journey.coast.description","Двадцять прибережних таборів уздовж рибальських стежок. Маяк світить — але хто тримає його вночі?","Twenty shoreline camps along fishing paths. The lighthouse shines — but who keeps it at night?","Zwanzig Küstenlager entlang der Fischerpfade. Der Leuchtturm leuchtet — doch wer hütet ihn nachts?")

def beat(jid,nums,uk_texts,en_texts,de_texts):
    for n,u,e,d in zip(nums,uk_texts,en_texts,de_texts): add(f"journey.{jid}.story.{n}",u,e,d)

beat("lighthouse",[10,15,20],
 ["Сходи вежі знають ваші кроки. На півдорозі — малюнок: маяк і три намети, намальовані дитячою рукою багато років тому.",
  "У журналі доглядача — сторінка про шторм, коли світло врятувало цілий човен. Підписана вона ім'ям, яке ви вже чули на станції.",
  "Ліхтар запалено вперше за дванадцять років. На пляжі внизу хтось помахав у відповідь — гілка історії маяка закінчена, берег лишається."],
 ["The tower stairs know your steps. Halfway up, a child's drawing: the lighthouse and three tents, made many years ago.",
  "The keeper's log holds a page about a storm when the light saved an entire boat. It is signed by a name you heard at the station.",
  "The lamp is lit for the first time in twelve years. On the beach below, someone waves back — the lighthouse arc is complete, the shore remains."],
 ["Die Turmtreppe kennt eure Schritte. Auf halber Höhe eine Kinderzeichnung: der Leuchtturm und drei Zelte, vor vielen Jahren gemalt.",
  "Das Wärterbuch hält eine Seite über einen Sturm bereit, als das Licht ein ganzes Boot rettete. Signiert mit einem Namen, den ihr am Bahnhof gehört habt.",
  "Zum ersten Mal seit zwölf Jahren brennt die Lampe. Am Strand winkt jemand zurück — die Leuchtturm-Geschichte ist abgeschlossen, die Küste bleibt."])
beat("garden",[5,10,15,20,25],
 ["Скло витримало зиму. Під ламкою знайшлися сухі пакетики насіння — хтось лишив їх спеціально для тих, хто повернеться.",
  "Перші паростки пробились крізь мох. Анна записує дати в старий садовий журнал — сторінки починають заповнюватися знову.",
  "Чужий ключ від комори підійшов. Всередині — інструменти й записка: доглядайте, поки ми не повернемося. Записці п'ять років.",
  "Теплиця знову зелена. Сусідні ферми приносять перші замовлення — сад офіційно ожив.",
  "Остання рама встановлена. У журналі Богдан пише фінальний рядок: сад більше не потребує дозволу, щоб рости."],
 ["The glass survived winter. Under a shard: dry seed packets, left on purpose for whoever returned.",
  "First sprouts break through the moss. Anna writes dates in the old garden journal — its pages are filling again.",
  "A stranger's key fits the shed. Inside: tools and a note — tend it until we return. The note is five years old.",
  "The greenhouse is green again. Neighbouring farms bring their first orders — the garden is officially alive.",
  "The last frame is set. Bohdan writes the final journal line: the garden no longer needs permission to grow."],
 ["Das Glas hat den Winter überstanden. Unter einer Scherbe: trockene Samentüten — absichtlich für die Zurückkehrenden hinterlassen.",
  "Erste Sprossen durchbrechen das Moos. Anna schreibt Daten ins alte Gartenjournal — die Seiten füllen sich wieder.",
  "Ein fremder Schlüssel passt zum Schuppen. Darin: Werkzeug und eine Notiz — pflegt es, bis wir zurückkehren. Die Notiz ist fünf Jahre alt.",
  "Das Gewächshaus ist wieder grün. Nachbarhöfe bringen erste Bestellungen — der Garten lebt offiziell.",
  "Der letzte Rahmen ist gesetzt. Bohdan schreibt die letzte Zeile ins Journal: Der Garten braucht keine Erlaubnis mehr, um zu wachsen."])
beat("station",[5,10,15],
 ["Розклад на стіні датується десятиліттям тому. Під ним — свіжий напис крейдою: о 18:00 зустрічаємося тут.",
  "Знайдено торбу квитків із незаповненими датами. Люди почали лишати на них імена тих, кого чекають.",
  "Перший справжній прибуток: не потяг — стара дрезина, що привезла дахівника. Він каже, що станція йому вдячна."],
 ["The wall timetable is a decade old. Beneath it, fresh chalk: we meet here at 18:00.",
  "A pouch of tickets with blank dates turns up. People have started writing the names they are waiting for.",
  "The first real arrival: not a train — an old handcar carrying a roofer. He says the station has earned it."],
 ["Der Fahrplan an der Wand ist zehn Jahre alt. Darunter in frischer Kreide: Wir treffen uns um 18:00 hier.",
  "Eine Tasche mit Fahrkarten ohne Datum taucht auf. Menschen schreiben die Namen derer darauf, auf die sie warten.",
  "Die erste echte Ankunft: kein Zug — eine alte Draisine mit einem Dachdecker. Er sagt, der Bahnhof hat es verdient."])
beat("mountain",[5,10,15,20],
 ["Перевал позначений кам'яною пірамидою — кожен, хто долав цю дорогу, лишив камінь. Ви додаєте свій.",
  "У притулку — сторінки щоденника лісника. Він спостерігав за маяком звідси: світло гасло тієї ж ночі, коли зник його брат.",
  "Зсув змінив стежку, але ваші табори позначили обхід. Мандрівники вже користуються ним, не знаючи, хто його зробив.",
  "Вершина. У кам'яній ніші — остання сторінка щоденника: лісник спустився до маяка. Дві гілки історії нарешті сходяться."],
 ["The pass is marked by a cairn — everyone who crossed left a stone. You add yours.",
  "In the shelter: pages of a ranger's journal. He watched the lighthouse from up here — its light died the same night his brother vanished.",
  "A slide rerouted the trail, but your camps marked the detour. Travelers already use it without knowing who made it.",
  "The summit. In a stone niche, the ranger's final page: he descended to the lighthouse. Two story threads finally meet."],
 ["Der Pass ist mit einem Steinmann markiert — jeder, der überquerte, ließ einen Stein. Ihr legt euren dazu.",
  "In der Schutzhütte: Seiten eines Försterjournals. Er beobachtete den Leuchtturm von hier — sein Licht erlosch in derselben Nacht, in der sein Bruder verschwand.",
  "Ein Steinschlag legte den Pfad um, doch eure Lager markierten den Umweg. Wanderer nutzen ihn bereits, ohne zu wissen, wer ihn schuf.",
  "Der Gipfel. In einer Steinnische die letzte Journal-Seite: Der Förster stieg zum Leuchtturm hinab. Zwei Geschichtsstränge treffen sich endlich."])
beat("resort",[5,10,15,20,25,30],
 ["У квитку відпочинку з 1998 року — фото басейну. Сьогодні там росте молодий сад — і це краще.",
  "Кухня пахне полином, не їжею — але в коморі знайшлося меню: вечірня вата для всіх. Деякі традиції варто повертати.",
  "Альтанка №7 — місце, де хтось залишив грамофон. Голос на платівці каже: поверніться наступного літа. Минуло дев'ять літ.",
  "Перші номери відкрито. Гості оформлюють чергу на ключі — історія, що спить десять років, нарешті прокидається.",
  "У фонтані знову вода. Постояльці занесли вчорашній вечір у книгу відгуків — перший новий запис за століття курорту.",
  "Курорт більше не покинутий. Заключний рядок адміністратора, датований 1998 роком, нарешті можна дочитати вголос."],
 ["A 1998 vacation ticket holds a poolside photo. Today a young garden grows there — and that is better.",
  "The kitchen smells of sage, not soup — but the pantry kept a menu: evening marshmallows for everyone. Some traditions deserve returning.",
  "Gazebo No.7 hides a gramophone. The record says: come back next summer. Nine years have passed.",
  "First rooms open. Guests form a key queue — a story asleep for ten years finally wakes.",
  "The fountain runs again. Guests entered last evening in the guest book — the first new entry in the resort's century.",
  "The resort is abandoned no more. The administrator's closing line from 1998 can finally be read aloud."],
 ["Ein Ferienticket von 1998 zeigt den Pool. Heute wächst dort ein junger Garten — und das ist besser.",
  "Die Küche riecht nach Beifuß, nicht nach Suppe — doch die Speisekammer hielt ein Menü: Abends Stockbrot für alle. Manche Traditionen verdienen eine Rückkehr.",
  "Pavillon Nr.7 birgt ein Grammophon. Die Platte sagt: Kommt nächsten Sommer zurück. Neun Jahre sind vergangen.",
  "Erste Zimmer öffnen. Gäste stehen Schlange nach Schlüsseln — eine Geschichte, die zehn Jahre schlief, wacht endlich auf.",
  "Der Brunnen läuft wieder. Gäste trugen gestern Abend ins Gästebuch ein — der erste neue Eintrag im Jahrhundert des Ferienorts.",
  "Der Ferienort ist nicht mehr verlassen. Der Schlussatz des Verwalters von 1998 kann endlich laut gelesen werden."])
beat("coast",[5,10,15,20],
 ["Рибальські мотузки досі міцні — хтось перевіряв їх недавно. Хтось, хто, можливо, ще чекає на човен.",
  "На скелі — карта відмілей з поміткою: тут спить стара бухта. Нижче видно сухий причал, якого немає на жодній схемі.",
  "Човен на якорі — не покинутий: у каюті свіжа олія на лампі й дві чашки. Господар десь близько.",
  "Остання пристань. На стовпі — гачок, де колись висів шарф. Ви знаєте, хто його колись носив."],
 ["The fishing ropes are still sound — someone checked them recently. Someone who may still be waiting for a boat.",
  "On the cliff, a shoal map marked: here sleeps the old bay. Below lies a dry pier missing from every chart.",
  "The anchored boat is not abandoned: fresh oil on the cabin lamp, two cups. The owner is close.",
  "The last pier. On the post, a hook where a scarf once hung. You know who used to wear it."],
 ["Die Fischerseile sind noch fest — jemand prüfte sie kürzlich. Jemand, der vielleicht noch auf ein Boot wartet.",
  "An der Klippe eine Untiefenkarte mit dem Vermerk: hier schläft die alte Bucht. Darunter ein trockener Anleger, der auf keinem Plan steht.",
  "Das Boot vor Anker ist nicht verlassen: frisches Öl an der Kabinenlampe, zwei Tassen. Der Eigentümer ist nah.",
  "Der letzte Anleger. Am Pfosten ein Haken, an dem einst ein Schal hing. Ihr wisst, wer ihn trug."])

def bonus(theme,uk_t,uk_d,en_t,en_d,de_t,de_d):
    add(f"map.bonus.{theme}.title",uk_t,en_t,de_t)
    add(f"map.bonus.{theme}.description",uk_d,en_d,de_d)
bonus("lantern","Ліхтарний куточок","Вечірній ліхтар над тихою стежкою — місце, що світить тим, хто йде пізно.","Lantern nook","An evening lantern over a quiet path — a place that shines for late walkers.","Laternenwinkel","Eine Abendlaterne über einem stillen Pfad — ein Ort, der für späte Wanderer leuchtet.")
bonus("creek","Приструмкова галявина","Холодна вода, камені й вечірня пісня струмка між двома таборами.","Creekside","Cold water, stones and the evening song of a creek between two camps.","Am Bach","Kaltes Wasser, Steine und das Abendlied eines Bachs zwischen zwei Lagern.")
bonus("meadow","Польова галявина","Відкрите поле, де вітер несе насіння аж до наступного табору.","Open meadow","An open field where the wind carries seeds all the way to the next camp.","Offene Wiese","Ein offenes Feld, auf dem der Wind Samen bis zum nächsten Lager trägt.")
bonus("pinewood","Сосновий притулок","Високі сосни й смола в повітрі — природний дах для тихого ночлігу.","Pine shelter","Tall pines and resin in the air — a natural roof for a quiet night.","Kiefernunterstand","Hohe Kiefern und Harz in der Luft — ein natürliches Dach für eine ruhige Nacht.")
bonus("birch","Березовий гай","Молоді берези й весняне світло — місце, що пахне новим листям.","Birch copse","Young birches and spring light — a place that smells of new leaves.","Birkenhain","Junge Birken und Frühlingslicht — ein Ort, der nach neuen Blättern riecht.")
bonus("quarry","Старий кар'єр","Кам'яний амфітеатр, де відлуння ще пам'ятає робочі голоси.","Old quarry","A stone amphitheatre where echoes still remember working voices.","Alter Steinbruch","Ein steinernes Amphitheater, dessen Echo sich noch an Arbeitsstimmen erinnert.")
bonus("orchard","Фруктовий сад","Ряди дерев у кольорі — сезонний пиріг тут роблять спільно.","Orchard rows","Tree rows in colour — the seasonal pie here is always shared.","Obstgarten","Baumreihen in Farbe — der saisonale Kuchen wird hier stets geteilt.")
bonus("highmoor","Високий мох","Мохова пустеля над лісом, де тиша густа, як туман.","High moor","A moss wilderness above the forest where silence is thick as fog.","Hochmoor","Eine Mooswildnis über dem Wald, wo die Stille dicht wie Nebel ist.")
bonus("homestead","Стара садиба","Будинок без даху, але з садом — історія місця ще не дописана.","Old homestead","A roofless house with a garden — this place's story is not finished.","Alter Hof","Ein Haus ohne Dach, aber mit Garten — die Geschichte dieses Ortes ist noch nicht zu Ende.")
bonus("spring","Весняна галявина","Перші квіти й перша тепла ніч — відкривається тільки навесні.","Spring glade","First flowers and the first warm night — only opens in spring.","Frühlingslichtung","Erste Blumen und die erste warme Nacht — öffnet nur im Frühling.")
bonus("summer","Літня галявина","Довгі дні й теплий вечір біля води — відкривається тільки влітку.","Summer glade","Long days and a warm evening by the water — only opens in summer.","Sommerlichtung","Lange Tage und ein warmer Abend am Wasser — öffnet nur im Sommer.")
bonus("autumn","Осіння галявина","Золоте листя й прохолода перед першим снігом — відкривається тільки восени.","Autumn glade","Golden leaves and a chill before first snow — only opens in autumn.","Herbstlichtung","Goldene Blätter und Kühle vor dem ersten Schnee — öffnet nur im Herbst.")
bonus("overlook","Вид на долину","Край скелі з видом на всю вашу дорогу — преміум-галявина для тих, хто піднімався далеко.","Valley overlook","A cliff edge overlooking your whole road — a premium glade for those who climbed far.","Talblick","Ein Felsvorsprung mit Blick auf euren ganzen Weg — eine Premium-Lichtung für alle, die weit gestiegen sind.")
bonus("cliff","Скелястий виступ","Вузька площадка над урвищем — преміум-галявина, що вимагає точності.","Cliff ledge","A narrow shelf above the drop — a premium glade that demands precision.","Felsvorsprung","Ein schmaler Absatz über dem Abgrund — eine Premium-Lichtung, die Präzision verlangt.")
bonus("lagoon","Тиха лагуна","Нічне світло над водою — преміум-галявина для тих, хто любить тишу й глибину.","Quiet lagoon","Night light over the water — a premium glade for lovers of silence and depth.","Stille Lagune","Nachtlicht über dem Wasser — eine Premium-Lichtung für alle, die Stille und Tiefe lieben.")
bonus("marshal","Сторожова галявина","Випробування для досвідчених: тісне місце, строгі знаки, жодної вільної клітинки.","Marshal's glade","A trial for veterans: a tight site, strict signs, not one spare cell.","Wächterlichtung","Eine Prüfung für Veteranen: enger Platz, strenge Zeichen, keine freie Zelle.")
bonus("summit","Вершина","Останнє випробування дороги: зима, туман і місце, яке треба заслужити.","The summit","The road's final trial: winter, mist and a place you must earn.","Der Gipfel","Die letzte Prüfung des Wegs: Winter, Nebel und ein Ort, den man verdienen muss.")

main_beats={
45:("Фермерська візня знову їздить між дворами — на ній місця для пасажирів, яких ще вчора не було.",
    "The farm cart runs between yards again — seats for passengers who were not here yesterday.",
    "Der Bauernwagen fährt wieder zwischen den Höfen — Sitzplätze für Passagiere, die gestern noch nicht da waren."),
50:("Останнє поле перед млинами зоране. Хтось повісив на стовпі мапу вашої мережі — її вже знають далі на північ.",
    "The last field before the mills is ploughed. Someone hung a map of your network on a post — it is known farther north already.",
    "Das letzte Feld vor den Mühlen ist gepflügt. Jemand hängte eine Karte eures Netzes an einen Pfosten — man kennt es schon weiter nördlich."),
55:("Млин обертається вперше за десять років. На мішках із борошном — позначки місць, які ви влаштували.",
    "The mill turns for the first time in a decade. Flour sacks bear marks of places you arranged.",
    "Die Mühle dreht sich zum ersten Mal seit zehn Jahren. Mehlsäcke tragen Zeichen der Orte, die ihr herrichtetet."),
58:("Ставок біля млина знову повний — вода, яку вміло відпустили, тепер несе два човни.",
    "The mill pond is full again — water wisely released now carries two boats.",
    "Der Mühlteich ist wieder voll — klug abgelassenes Wasser trägt nun zwei Boote."),
60:("Вулиці села чисті: половина будинків житиме, інші стоятимуть як музей дороги.",
    "The village streets are swept: half the houses will live, the others will stand as a museum of the road.",
    "Die Dorfstraßen sind gefegt: Die halben Häuser werden leben, die anderen bleiben als Museum des Weges."),
65:("Листи з села дійшли до станції — люди пишуть на адреси, які ще минулого року були пусткою.",
    "Village letters reached the station — people write to addresses that stood empty last year.",
    "Dorfbriefe erreichten den Bahnhof — Menschen schreiben an Adressen, die letztes Jahr noch leer standen."),
66:("Дзвін сільської церкви лунає вперше за п'ять років. Подорожні зупиняються й прислухаються.",
    "The village bell rings for the first time in five years. Travelers stop and listen.",
    "Die Dorfglocke läutet zum ersten Mal seit fünf Jahren. Reisende bleiben stehen und lauschen."),
70:("Перевал відкрито: ваші табори нижче стали хребтом дороги, що веде нагору.",
    "The pass opens: your camps below became the spine of the road climbing up.",
    "Der Pass ist offen: Eure Lager unten wurden zum Rückgrat des aufsteigenden Weges."),
74:("Гірський притулок повний — у журналі перших гостей записи тими ж руками, що й у вашому першому таборі.",
    "The mountain shelter is full — the first guests' journal has entries in hands you saw at your very first camp.",
    "Die Berghütte ist voll — im Gästebuch stehen Einträge von Händen, die ihr schon in eurem ersten Lager saht."),
75:("Курортна брама скрипить, але відчиняється. За нею — літо, якого хтось не дочекався.",
    "The resort gate creaks but opens. Behind it — a summer someone never got to finish.",
    "Das Tor zum Ferienort knarrt, öffnet sich aber. Dahinter — ein Sommer, den jemand nicht beendete."),
80:("Басейн відчищений і знову наповнюється. Кажуть, вода тут лікувала не тіло — самотність.",
    "The pool is cleaned and filling again. They say the water here healed not bodies — loneliness.",
    "Das Becken ist gesäubert und füllt sich wieder. Man sagt, das Wasser heilte hier nicht Körper — sondern Einsamkeit."),
82:("Остання галявина курорту зроблена. На рецепції дзвоник — дзвін, який десять років нікого не кликав, востаннє дзвонить вам.",
    "The resort's last glade is done. On the desk, a bell — silent for ten years — rings for you at last.",
    "Die letzte Lichtung des Ferienorts ist fertig. Auf der Rezeption läutet eine Glocke, die zehn Jahre schwieg, zum ersten Mal für euch."),
85:("Берегова дорога йде сіллю й туманом. Ваші вогні видно з води — рибалки вже називають їх маяком.",
    "The shore road runs through salt and mist. Your fires are visible from the water — fishers already call them a lighthouse.",
    "Die Küstenstraße führt durch Salz und Nebel. Eure Feuer sind vom Wasser aus sichtbar — Fischer nennen sie schon einen Leuchtturm."),
90:("Останній причал узбережжя приймає човен. Подорожні сходять на берег просто до вашого табору.",
    "The coast's last pier takes a boat. Travelers step ashore straight into your camp.",
    "Der letzte Anleger der Küste nimmt ein Boot an. Reisende gehen direkt in euer Lager an Land."),
95:("Сніг у високогір'ї чистий, як перший день. Стежки, які ви влаштували, вже носять нові сліди.",
    "Highland snow is clean as the first day. Trails you arranged already carry fresh tracks.",
    "Der Schnee im Hochland ist rein wie am ersten Tag. Die Pfade, die ihr herrichtetet, tragen schon neue Spuren."),
98:("Притулок над хмарами готовий. Учорашній шторм пройшов — місце встигло когось сховати.",
    "The shelter above the clouds is ready. Yesterday's storm passed — the place already gave someone cover.",
    "Die Hütte über den Wolken steht bereit. Der gestrige Sturm zog vorüber — der Ort hat schon jemanden geschützt."),
100:("Кордонна застава пуста, але карта на стіні актуальна: хтось наносить ваші місця.",
    "The frontier post stands empty, but its wall map is current: someone is charting your places.",
    "Der Grenzposten steht leer, doch die Wandkarte ist aktuell: Jemand zeichnet eure Orte ein."),
105:("Дорога звужується до стежки. Попереду — місця, які на карті позначені лише словом далі.",
    "The road narrows to a trail. Ahead lie places maps mark only with the word farther.",
    "Der Weg verengt sich zum Pfad. Voraus liegen Orte, die Karten nur mit weiter markieren."),
106:("Останній кам'яний стовп кордону. За ним — земля, де нікого немає, крім тих, хто ще дорогою.",
    "The last stone post of the frontier. Beyond lies land where nobody lives yet — except those still on their way.",
    "Der letzte Grenzstein. Dahinter liegt Land, in dem noch niemand lebt — außer jenen, die unterwegs sind."),
110:("Гавань. Тут закінчується ваша дорога — і починається місце, куди всі дороги світу приходять відпочити.",
    "The haven. Your road ends here — and a place begins where all the world's roads come to rest.",
    "Der Hafen. Hier endet euer Weg — und beginnt ein Ort, an dem alle Straßen der Welt rasten."),
}
for n,(u,e,d) in main_beats.items(): add(f"journey.main.story.{n}",u,e,d)

UK["journey.main.description"]="Основна історія — сто десять галявин у п'ятьох актах. Спочатку безпечні табори, далі — дорога, що з'єднує місця в живий світ."
EN["journey.main.description"]="The main story — one hundred ten glades across five acts. First safe camps, then a road that joins places into a living world."
DE["journey.main.description"]="Die Hauptgeschichte — einhundertzehn Lichtungen in fünf Akten. Erst sichere Lager, dann ein Weg, der Orte zu einer lebendigen Welt verbindet."

for name,d in (("uk",UK),("en",EN),("de",DE)):
    p=f'QuietCamp/Assets/QuietCamp/Resources/QuietCampLocales/{name}.json'
    data=json.load(open(p,encoding='utf-8'))
    for k,v in d.items(): data[k]=v
    json.dump(data,open(p,'w',encoding='utf-8'),ensure_ascii=False,indent=4)
print('keys added:',len(UK))
