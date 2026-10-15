"""Sync only season-atmosphere sources to the authorized open QA project."""
from pathlib import Path
import shutil
import uuid
ROOT=Path(__file__).resolve().parents[1]
PROJECT=ROOT/'QuietCamp';QA=Path(r'C:\Users\Public\qcui-20261006\QuietCamp')
FILES=['Assets/QuietCamp/Scripts/Infrastructure/CampArtDefinition.cs','Assets/QuietCamp/Scripts/Infrastructure/SeasonAtmosphereCatalog.cs','Assets/QuietCamp/Scripts/Presentation/World/CampArtView.cs','Assets/QuietCamp/Scripts/Presentation/World/CampAtmosphere.cs','Assets/QuietCamp/Scripts/Presentation/World/SeasonModelAppearance.cs','Assets/QuietCamp/Scripts/Presentation/World/SeasonAtmosphereView.cs','Assets/QuietCamp/Editor/LevelAuthoring/SeasonAtmosphereAuthoring.cs','Assets/QuietCamp/Editor/LevelAuthoring/LevelAuthoringWindow.Atmosphere.cs','Assets/QuietCamp/Editor/LevelAuthoring/LevelAuthoringWindow.cs','Assets/QuietCamp/Editor/LevelAuthoring/LevelAuthoringWorkspace.cs','Assets/QuietCamp/Editor/QC007ArtCapture.cs','Assets/QuietCamp/Resources/QuietCamp/season_atmosphere.json','Assets/QuietCamp/Tests/Editor/SeasonAtmosphereTests.cs']
FILES+=['Assets/QuietCamp/Scripts/Infrastructure/SunflowerGeometry.cs','Assets/QuietCamp/Scripts/Presentation/World/PlantWeatherResponse.cs','Assets/QuietCamp/Editor/LevelAuthoring/VegetationAuthoring.cs','Assets/QuietCamp/Resources/QuietCamp/vegetation_presets.json','Assets/QuietCamp/Editor/LevelAuthoring/CampaignWorkshopRegeneration.cs','Assets/QuietCamp/Editor/LevelAuthoring/LevelAuthoringDocument.cs','Assets/QuietCamp/Editor/LevelAuthoring/LevelAuthoringPalette.cs','Assets/QuietCamp/Editor/LevelAuthoring/LevelAuthoringCampaign.cs','Assets/QuietCamp/Editor/LevelAuthoring/WorldStudyFactory.cs','Assets/QuietCamp/Resources/QuietCamp/CampArtLit.shader']
FILES+=['Assets/QuietCamp/Scripts/Presentation/World/BoardRenderer.cs']
FILES+=['Assets/QuietCamp/Editor/LevelAuthoring/AuthoringFileTransaction.cs']
FILES+=['Assets/QuietCamp/Editor/LevelAuthoring/AuthoringVegetationSnapshot.cs','Assets/QuietCamp/Tests/Editor/CampArtCompositionTests.cs']
FILES+=['Assets/QuietCamp/Scripts/Presentation/World/CampSceneHost.cs']
FILES+=['Assets/QuietCamp/Scripts/Presentation/World/WeatherCoverage.cs','Assets/QuietCamp/Scripts/Presentation/World/SeasonVisibilityPass.cs','Assets/QuietCamp/Resources/QuietCamp/SeasonVisibility.shader','Assets/QuietCamp/Resources/QuietCamp/PlacementGrid.shader']
FILES+=['Assets/QuietCamp/Scripts/Presentation/World/RiverWeatherMaterial.cs']
FILES+=['Assets/QuietCamp/Scripts/Presentation/World/WeatherObjectMotion.cs']
FILES+=['Assets/QuietCamp/Editor/LevelAuthoring/AtmosphereDemoComposition.cs']
FILES+=['Assets/QuietCamp/Scripts/Presentation/World/WeatherSurfaceField.cs','Assets/QuietCamp/Scripts/Presentation/World/SeasonalTreeVisual.cs','Assets/QuietCamp/Scripts/Presentation/World/SeasonalPropVisual.cs']
FILES+=['Assets/QuietCamp/Scripts/Presentation/World/SeasonSurfaceAccents.cs']
FILES+=['Assets/QuietCamp/Scripts/Presentation/World/CampArtEdgeBlur.cs','Assets/QuietCamp/Resources/QuietCamp/CampArtEdgeBlur.shader']
FILES+=['Assets/QuietCamp/Editor/LevelAuthoring/WeatherMotionCapture.cs']
FILES+=['Assets/QuietCamp/Scripts/Presentation/World/TentCloth.cs','Assets/QuietCamp/Scripts/Presentation/World/CampRainShelter.cs']
FILES+=['Assets/QuietCamp/Editor/LevelAuthoring/WeatherModelRigAuthoring.cs']
FILES+=['Assets/QuietCamp/Resources/QuietCamp/TentCloth.shader']
FILES+=['Assets/QuietCamp/Editor/LevelAuthoring/SurfaceWeathering.cs','Assets/QuietCamp/Editor/LevelAuthoring/CrashMeshAuthoring.cs','Assets/QuietCamp/Editor/LevelAuthoring/AircraftCrashAuthoring.cs','Assets/QuietCamp/Editor/LevelAuthoring/ModelAuthoringWorkshop.cs','Assets/QuietCamp/Editor/LevelAuthoring/LevelAuthoringWindow.Models.cs','Assets/QuietCamp/EditorBridge/Editor/PwbLevelAuthoringAdapter.cs']
FILES+=['Assets/QuietCamp/Editor/LevelAuthoring/AuthoringWeatherExposure.cs']
FILES+=['Assets/QuietCamp/Tests/Editor/SurfaceWeatheringTests.cs','Assets/QuietCamp/Editor/LevelAuthoring/AuthoringPropCatalog.cs']
FILES+=['Assets/QuietCamp/Editor/LevelAuthoring/ForestRegrowthAuthoring.cs','Assets/QuietCamp/Scripts/Presentation/World/SeasonLightShafts.cs','Assets/QuietCamp/Resources/QuietCamp/SeasonLightShaft.shader','Assets/QuietCamp/Resources/QuietCamp/forest_regrowth.json']
FILES+=['Assets/QuietCamp/Tests/Editor/ForestAtmosphereTests.cs']
FILES+=['Assets/QuietCamp/Scripts/Presentation/World/CampSurfaceUseField.cs','Assets/QuietCamp/Scripts/Presentation/World/CampGroundDetails.cs','Assets/QuietCamp/Scripts/Presentation/World/MeadowSurface.cs','Assets/QuietCamp/Scripts/Presentation/World/SeasonProfile.cs','Assets/QuietCamp/Resources/QuietCamp/CampSurfaceUse.hlsl','Assets/QuietCamp/Resources/QuietCamp/Meadow.shader','Assets/QuietCamp/Resources/QuietCamp/camp_surface_use.json','Assets/QuietCamp/Editor/LevelAuthoring/ObjectOvergrowthAuthoring.cs','Assets/QuietCamp/Resources/QuietCamp/object_overgrowth.json','Assets/QuietCamp/Tests/Editor/CampSurfaceUseTests.cs']
FILES+=['Assets/QuietCamp/Tests/Editor/SeasonPresentationTests.cs','Assets/QuietCamp/Tests/Editor/BiomeSurfaceTests.cs']
FILES+=['Assets/QuietCamp/Editor/LevelAuthoring/OpeningCampaignAuthoring.cs','Assets/QuietCamp/Scripts/Infrastructure/OpeningCampaignStory.cs','Assets/QuietCamp/Resources/QuietCamp/opening_campaign_story.json','Assets/QuietCamp/Editor/LevelAuthoring/RiverTerrainAuthoringData.cs','Assets/QuietCamp/Scripts/Presentation/UI/CampHud.cs','Assets/QuietCamp/Tests/Editor/OpeningCampaignTests.cs']
FILES+=['Assets/QuietCamp/Editor/LevelAuthoring/OpeningCampaignGameplayProbe.cs','Assets/QuietCamp/Editor/LevelAuthoring/LevelAuthoringPlayDriver.cs','Assets/QuietCamp/Scripts/Infrastructure/EditorAuthoringPreview.cs']
FILES+=['Assets/QuietCamp/Editor/QuietCamp.Editor.asmdef','Assets/QuietCamp/EditorBridge/Editor/MicroVerseRiverTerrainAdapter.cs']
FILES+=['Assets/QuietCamp/Editor/LevelAuthoring/CampaignLandmarkCatalog.cs','Assets/QuietCamp/Authoring/CampaignLandmarks/catalog.json','Assets/QuietCamp/Authoring/CampaignLandmarks/models.json']
for name in FILES:
 source=PROJECT/name
 if not source.exists():continue
 meta=Path(str(source)+'.meta')
 if not meta.exists():
  importer='MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n' if source.suffix=='.cs' else 'TextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
  if source.suffix=='.hlsl':importer='ShaderIncludeImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
  meta.write_text('fileFormatVersion: 2\nguid: '+uuid.uuid4().hex+'\n'+importer,encoding='utf-8')
 target=QA/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target);shutil.copy2(meta,Path(str(target)+'.meta'))
print('Synced season atmosphere sources and GUIDs')
