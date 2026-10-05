using System;
using System.IO;
using Kruty1918.Audio;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Editor
{
    public static class SoundscapeAudioContent
    {
        const string Folder = "Assets/QuietCamp/Audio/Generated/Soundscape/";
        // Base/extras builders can rebuild legacy definitions. Reapply owned
        // overrides afterwards so regeneration cannot remove music or Foley.
        public static void ImportAvailableOverrides()
        {
            if (File.Exists("Assets/QuietCamp/Audio/Generated/Cozy/clearing_music.wav")) CozyAudioContent.Import();
            if (File.Exists(Folder + "rain_canopy.wav")) Import();
            if (File.Exists(BiomeAudioContent.Folder + "winter.wav")) BiomeAudioContent.Import();
        }
        [MenuItem("QuietCamp/Import Soundscape Audio")]
        public static void Import()
        {
            AssetDatabase.Refresh();
            var catalog = QuietCampAudioCatalog.Load();
            if (catalog == null) throw new InvalidOperationException("Audio catalog missing.");
            var so = new SerializedObject(catalog);
            Add(so, "ambience.rain", "rain_canopy", 1, true, false, .19f);
            Add(so, "ambience.rain.canvas", "rain_canvas", 1, true, true, .12f);
            Add(so, "sfx.tent.gust", "fabric_gust", 3, false, true, .14f);
            Add(so, "sfx.tent.drag", "fabric_gust", 3, false, true, .11f);
            Add(so, "sfx.fire.quench", "fire_quench", 2, false, true, .17f);
            Add(so, "sfx.rain.drip", "drip", 3, false, true, .075f);
            Add(so, "ambience.bird", "bird", 3, false, true, .11f);
            Add(so, "level.complete", "complete", 1, false, false, .20f);
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        }
        internal static void Add(SerializedObject catalog, string key, string name, int count, bool loop, bool spatial, float volume, string folder = Folder)
        {
            var clips = new AudioClip[count];
            for (int i = 0; i < count; i++)
            {
                var path = folder + name + (loop ? "" : "_" + i) + ".wav";
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) throw new InvalidOperationException("Generate soundscape audio first: " + path);
                importer.forceToMono = spatial; importer.loadInBackground = loop;
                var settings = importer.defaultSampleSettings;
                settings.preloadAudioData = !loop;
                settings.loadType = loop ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = loop ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
                settings.quality = .85f; settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                importer.defaultSampleSettings = settings; importer.SaveAndReimport();
                clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
            var array = catalog.FindProperty(key == "ambience.bird" || key == "level.complete" ? "_sounds" : "_extraSounds");
            int index = 0;
            while (index < array.arraySize && array.GetArrayElementAtIndex(index).FindPropertyRelative("Key").stringValue != key) index++;
            if (index == array.arraySize) array.arraySize++;
            var entry = array.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("Key").stringValue = key;
            entry.FindPropertyRelative("Clip").objectReferenceValue = clips[0];
            var variants = entry.FindPropertyRelative("Variants"); variants.arraySize = count > 1 ? count : 0;
            for (int i = 0; i < variants.arraySize; i++) variants.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            bool surface = key.StartsWith("sfx.surface.",StringComparison.Ordinal);
            bool feedback = key == "level.complete" || key == "sfx.tent.drag" || surface;
            entry.FindPropertyRelative("Bus").enumValueIndex = (int)(feedback ? AudioBus.Sfx : AudioBus.Ambience);
            entry.FindPropertyRelative("Volume").floatValue = volume;
            entry.FindPropertyRelative("VolumeRandom").floatValue = loop ? 0 : .006f;
            entry.FindPropertyRelative("Pitch").floatValue = 1;
            entry.FindPropertyRelative("PitchRandom").floatValue = loop ? 0 : .015f;
            entry.FindPropertyRelative("Loop").boolValue = loop;
            entry.FindPropertyRelative("SpatialBlend").floatValue = spatial ? 1 : 0;
            entry.FindPropertyRelative("MinDistance").floatValue = 3;
            entry.FindPropertyRelative("MaxDistance").floatValue = key == "ambience.bird" || key == "ambience.biome.water" ? 28 : 18;
            entry.FindPropertyRelative("DopplerLevel").floatValue = 0;
            entry.FindPropertyRelative("ReverbZoneMix").floatValue = 0;
            entry.FindPropertyRelative("Priority").intValue = feedback ? 80 : loop ? 168 : 196;
            entry.FindPropertyRelative("Cooldown").floatValue = loop ? 0 : surface ? .20f : key == "ambience.bird" ? 3 : key == "sfx.tent.drag" ? .75f : 1;
            entry.FindPropertyRelative("MaxSimultaneous").intValue = loop ? 1 : 2;
            entry.FindPropertyRelative("PoolWarmup").intValue = 0;
            foreach (var effect in new[] { "LowPass", "HighPass", "Echo", "Reverb", "Distortion", "Chorus" })
                entry.FindPropertyRelative("Effects").FindPropertyRelative("Enable" + effect).boolValue = false;
            entry.FindPropertyRelative("Duck").FindPropertyRelative("Enabled").boolValue = false;
        }
    }
}
