#!/usr/bin/env python3
"""Write reproducible manifests/assemblies/docs for original environment tools."""
from pathlib import Path
import json,hashlib
ROOT=Path(__file__).resolve().parents[1]
PACKAGES=ROOT/'QuietCamp/Packages'
TOOLS={
 'scene-placement':('ScenePlacement','PlacementSolver.Ensemble','Контакт, орієнтовані об’єми, просторовий індекс і атомарне розміщення ансамблів.','PlacementRequest[] + ISurfaceQuery + PlacementContext → PlacementResult','Кожен об’єкт має contacts і volumes. Preserve original models. Правила гри передаються через IPlacementRule. HeightFieldSurface — bilinear Y-up; власні поверхні реалізують ISurfaceQuery.'),
 'mesh-style':('MeshStyle','MeshHarmonizer.Harmonize','Гармонізація деталізації за масштабом, площею кадру та захищеними ознаками.','MeshData + StyleProfile + projectedArea → StyleResult','Edge collapse з link condition, контролем повороту граней і накопиченої похибки. UV/матеріальні межі, protected parts і custom channels захищені. Ціль може бути недосяжна: protected-detail-limit є чесним результатом. Не вигадує відсутню геометрію.'),
 'model-weathering':('ModelWeathering','Weathering.Apply','Кероване старіння матеріалів і конструктивних частин статичних моделей.','MeshData + WeatheringProfile → WeatheringResult','Старіння позиційне через UV/normal seams. Protected parts/joints лишаються на місці. Видалення дозволене тільки розміченим removableRoles. Без parts застосовується surface wear з діагностикою. GrowthRequest — дані для стороннього генератора рослинності.'),
 'fence-authoring':('FenceAuthoring','FenceGenerator.Generate','Огорожі за контурами: опори, секції, отвори й з’єднання.','FenceSpec + FenceKit + PlacementContext → FenceResult','Post asset має контакт із землею. Section asset центрований у нулі, span уздовж локального X; sectionLength описує номінальний проліт. railSocket задається в post-local units. Gate використовує ті самі socket/axis conventions. Нахили перевіряються повторно.'),
 'road-authoring':('RoadAuthoring','RoadGenerator.Generate','Зв’язні дороги на поверхні з переходами покриття та нерівними краями.','RoadSpec + ISurfaceQuery → RoadResult','Polyline або cubic Hermite, sampling до заданого step. Bands впорядковуються за arc distance. Potholes відкривають базову поверхню. Неприйнятний grade повертає TerrainAdjustment, поверхня не змінюється. Перехрестя задаються зв’язаними кінцями маршрутів.'),
 'power-lines':('PowerLines','PowerGenerator.Generate','Опори, sockets, статичне провисання й пошкоджені підвішені лінії.','PowerSpec + SupportKit + PlacementContext → PowerResult','Стоячі й fallen assets окремі. Fallen asset має вже покладену геометрію та ground contacts. CableState задає кріплення/обрив. Статична endpoint-preserving sag curve, без симуляції електрики/фізики. Несумісні типи й fallen standing sockets відхиляються.'),
 'rock-placement':('RockPlacement','RockGenerator.Generate','Геологічні групи каміння з опорою, напрямом і заглибленням.','RockSpec + PlacementContext → RockResult','Головний камінь групи обов’язковий; дрібні супутні можуть бути відхилені з діагностикою. Axis, radius, slope, масштаб і buriedFraction задає зовнішній геологічний профіль.'),
 'vegetation-placement':('VegetationPlacement','VegetationGenerator.Generate','Ярусна рослинність за екологічними полями, плямами й безпечними об’ємами.','VegetationSpec + PlacementContext + IEnvironmentalFields → VegetationResult','Source zone компілюється цілком; outputBounds лише розділяє готовий результат. Cell seeds не залежать від camera/chunk. Species задають scale/spacing/tiers/environment. Volumes для стовбура й крони розділяються автором; motionMargin враховує вітер.'),
}

def assembly(name,refs,core=False,editor=False):
 return dict(name=name,rootNamespace=name,references=refs,includePlatforms=['Editor'] if editor else [],excludePlatforms=[],allowUnsafeCode=False,autoReferenced=False,noEngineReferences=core)

def main():
 for slug,(ns,api,purpose,contract,notes) in TOOLS.items():
  folder=PACKAGES/f'com.kruty1918.{slug}';folder.mkdir(parents=True,exist_ok=True)
  dependencies={'com.unity.collections':'6.6.0','com.unity.modules.physics':'1.0.0','com.unity.modules.terrain':'1.0.0','com.unity.modules.jsonserialize':'1.0.0'} if slug=='scene-placement' else {'com.kruty1918.scene-placement':'0.1.0'}
  definition=dict(name='com.kruty1918.'+slug,version='0.1.0',displayName=slug.replace('-',' ').title(),description=purpose,unity='6000.6',author=dict(name='kruty1918dev-ai'),license='MIT',dependencies=dependencies)
  (folder/'package.json').write_text(json.dumps(definition,ensure_ascii=False,indent=2)+'\n')
  core=f'Kruty1918.{ns}.Core';unity=f'Kruty1918.{ns}.Unity';editor=f'Kruty1918.{ns}.Editor'
  for relative,definition in [
   (f'Runtime/Core/{core}.asmdef',assembly(core,[] if slug=='scene-placement' else ['Kruty1918.ScenePlacement.Core'],core=True)),
   (f'Runtime/Unity/{unity}.asmdef',assembly(unity,[core]+([] if slug=='scene-placement' else ['Kruty1918.ScenePlacement.Core','Kruty1918.ScenePlacement.Unity']))),
   (f'Editor/{editor}.asmdef',assembly(editor,[core,unity]+(['Unity.Collections'] if slug=='scene-placement' else ['Kruty1918.ScenePlacement.Core','Kruty1918.ScenePlacement.Unity','Kruty1918.ScenePlacement.Editor']),editor=True))]:
   target=folder/relative;target.parent.mkdir(parents=True,exist_ok=True);target.write_text(json.dumps(definition,indent=2)+'\n')
  readme=f'''# {slug}

{purpose}

Статус 0.1.0: Core-контракти перевірено; clean-project native import/tests та
візуальне приймання інтеграції гри ще Pending. Ця версія не є заявою production readiness.

Оригінальний reusable UPM-пакет. Алгоритми Core не знають Quiet Camp, сцен,
Resources, AssetDatabase чи шейдерів. API можна викликати з власного editor,
процедурного генератора або іншої системи. Static geometry v1; skinned meshes
не підтримуються. Core не має прихованих Update, файлових writes або глобального random.

## Встановлення

Приватний репозиторій: https://github.com/kruty1918dev-ai/{slug}
Потрібен Git-доступ до репозиторію. Unity Package Manager приймає Git URL
із pinned commit. Embedded package — повна копія; sibling checkout не потрібен.
Для спеціалізованого пакета встановіть scene-placement у кореневий manifest.
Залежності: `{json.dumps(dependencies)}`.
Unity adapter baseline — Unity 6000.6; інші версії ще не перевірені.
Core використовує .NET Standard-compatible C# і перевіряється окремим .NET host.

## API та принцип роботи

`{api}`: `{contract}`.

{notes}

Спеціалізовані генератори використовують Y-up / XZ ground-plane. Одиниця —
довільна узгоджена одиниця світу; адаптер гри задає її відповідність метрам.
scene-placement може використовувати власний Up та ISurfaceQuery. TriangleSurface,
HeightFieldSurface і TerrainSurface семплюють Y-up; це не довільний ray tracer.
Unity Terrain повинен бути без повороту/масштабування. Об’єм — фізична форма,
а не лише pivot. Зміна seed не змінює IDs ролей ансамблю.

Дані — звичайні C# об’єкти. Є CancellationToken, stable IDs і explicit seed.
Всі координати й одиниці вхідних даних повинні належати одному authoring space.
Матеріали, модельні IDs, правила й shader binding надає ваш адаптер.
Core result/diagnostics повертаються до створення GameObjects/asset writes.
Original meshes зберігаються; Unity outputs мають явне володіння й release.

## Три частини

- Runtime/Core: portable algorithms and data; assembly `{core}`.
- Runtime/Unity: Unity adapters; assembly `{unity}`.
- Editor: batch bake; assembly `{editor}`. Окремих вікон немає.

## Автоматичні команди

Editor method: `Kruty1918.{ns}.EditorTools.Batch.Run`
({ 'scene-placement використовує Kruty1918.ScenePlacement.EditorTools.PlacementBatch.Run' if slug=='scene-placement' else 'див. Recipe у Editor/Batch.cs' }).
Передайте `--authoring-request /absolute/recipe.json` та
`--authoring-output Assets/AuthoringOutput`. Native bake запускається вашим
Unity CLI через -executeMethod. Runtime API не вимагає JSON або CLI.
На хості Quiet Camp Unity запускається тільки через погоджений isolated wrapper.

## Перевірка й приклади

`Tests~/Contracts.csproj` — автономний контракт без Unity/game assets.
Для спеціалізованого пакета: `dotnet run --project Tests~/Contracts.csproj
-p:ScenePlacementRoot=/absolute/path/to/scene-placement`.
У embedded checkout default dependency лежить поруч у Packages.
`Samples~/Example.cs` — мінімальний виклик API на синтетичних даних.
`Tests/Editor/NativeContracts.cs` — native EditMode acceptance у чистому проєкті;
додайте package name у manifest.testables. Ці native tests ще не виконані.
Native Unity/import acceptance звітується окремо; .NET тест не доводить
візуальну якість або mobile FPS.

## Ліцензія

MIT, власний код. Комерційні моделі й їхні фотографії до пакета не входять.
'''
  (folder/'README.md').write_text(readme)
  (folder/'CHANGELOG.md').write_text('# Changelog\n\n## 0.1.0\n\nInitial deterministic static-environment algorithms; native acceptance tracked separately.\n')
  (folder/'LICENSE.md').write_text('MIT License\n\nCopyright (c) 2026 kruty1918dev-ai\n\nPermission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:\n\nThe above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.\n\nTHE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.\n')
  (folder/'.gitignore').write_text('bin/\nobj/\n.verification-core/\n*.log\n*.xml\n')
  tests=folder/'Tests~';tests.mkdir(exist_ok=True)
  includes='<Compile Include="../Runtime/Core/**/*.cs" /><Compile Include="Contracts.cs" /><Compile Include="../Samples~/Example.cs" />'
  if slug!='scene-placement':includes+='<Compile Include="$(ScenePlacementRoot)/Runtime/Core/**/*.cs" />'
  (tests/'Contracts.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><ScenePlacementRoot Condition="\'$(ScenePlacementRoot)\'==\'\'">../../com.kruty1918.scene-placement</ScenePlacementRoot></PropertyGroup><ItemGroup>'+includes+'</ItemGroup></Project>\n')
  for path in sorted(folder.rglob('*')):
   if path.name.endswith('.meta') or path.name.startswith('.') or any(part.endswith('~') for part in path.relative_to(folder).parts):continue
   relative=path.relative_to(folder).as_posix();meta=Path(str(path)+'.meta')
   if meta.exists():continue
   guid=hashlib.sha256(('kruty1918-original-environment-v1/'+slug+'/'+relative).encode()).hexdigest()[:32]
   text=f'fileFormatVersion: 2\nguid: {guid}\n'
   if path.is_dir():text+='folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
   elif path.suffix=='.cs':text+='MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n'
   elif path.suffix=='.asmdef':text+='AssemblyDefinitionImporter:\n  externalObjects: {}\n'
   else:text+='DefaultImporter:\n  externalObjects: {}\n'
   meta.write_text(text)
  parentmeta=Path(str(folder)+'.meta')
  if not parentmeta.exists():parentmeta.write_text('fileFormatVersion: 2\nguid: '+hashlib.sha256(('kruty1918-original-environment-v1/'+slug).encode()).hexdigest()[:32]+'\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n')
 print('Prepared metadata for eight original packages; native verification not claimed.')
if __name__=='__main__':main()
