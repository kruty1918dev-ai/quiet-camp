using System;
using Kruty1918.Audio;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Editor
{
    public static class CozyAudioContent
    {
        const string Folder = "Assets/QuietCamp/Audio/Generated/Cozy/";
        [MenuItem("QuietCamp/Import Cozy Audio")]
        public static void Import()
        {
            AssetDatabase.Refresh();
            var catalog = QuietCampAudioCatalog.Load();
            if (catalog == null) throw new InvalidOperationException("Audio catalog missing.");
            var so = new SerializedObject(catalog);
            foreach (var kind in new[] { "lift", "settle", "rotate", "remove", "page", "hint", "pause", "resume", "toggle", "check", "clearing_music" })
            {
                bool music = kind == "clearing_music";
                int count = kind == "lift" || kind == "settle" || kind == "rotate" || kind == "remove" || kind == "page" ? 3 : 1;
                var clips = new AudioClip[count];
                for (int i = 0; i < count; i++)
                {
                    string path = Folder + kind + (music ? "" : "_" + i) + ".wav";
                    var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                    importer.forceToMono = !music; importer.loadInBackground = music;
                    var settings = importer.defaultSampleSettings;
                    settings.preloadAudioData = !music;
                    settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                    settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
                    settings.quality = .75f; settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
                    importer.defaultSampleSettings = settings; importer.SaveAndReimport();
                    clips[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }
                string key = music ? "music.clearing" : kind == "rotate" ? "placement.rotate" : kind == "check" ? "rule.invalid" : "sfx." + (kind == "lift" || kind == "settle" || kind == "remove" ? "tent." : "") + kind;
                var array = so.FindProperty(kind == "rotate" || kind == "check" ? "_sounds" : "_extraSounds");
                int index = 0;
                while (index < array.arraySize && array.GetArrayElementAtIndex(index).FindPropertyRelative("Key").stringValue != key) index++;
                if (index == array.arraySize) array.arraySize++;
                var e = array.GetArrayElementAtIndex(index);
                e.FindPropertyRelative("Key").stringValue = key;
                e.FindPropertyRelative("Clip").objectReferenceValue = clips[0];
                var variants = e.FindPropertyRelative("Variants"); variants.arraySize = count > 1 ? count : 0;
                for (int i = 0; i < variants.arraySize; i++) variants.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
                e.FindPropertyRelative("Bus").enumValueIndex = (int)(music ? AudioBus.Music : kind == "lift" || kind == "settle" || kind == "remove" || kind == "rotate" ? AudioBus.Sfx : AudioBus.Ui);
                e.FindPropertyRelative("Volume").floatValue = music ? .075f : kind == "hint" ? .12f : .23f;
                e.FindPropertyRelative("VolumeRandom").floatValue = music ? 0 : .01f;
                e.FindPropertyRelative("Pitch").floatValue = 1;
                e.FindPropertyRelative("PitchRandom").floatValue = count > 1 ? .018f : 0;
                e.FindPropertyRelative("Loop").boolValue = music;
                e.FindPropertyRelative("SpatialBlend").floatValue = kind == "lift" || kind == "settle" || kind == "remove" ? .85f : 0;
                e.FindPropertyRelative("MinDistance").floatValue = 2;
                e.FindPropertyRelative("MaxDistance").floatValue = 16;
                e.FindPropertyRelative("DopplerLevel").floatValue = 0;
                e.FindPropertyRelative("ReverbZoneMix").floatValue = 0;
                e.FindPropertyRelative("Priority").intValue = music ? 176 : 100;
                e.FindPropertyRelative("Cooldown").floatValue = music ? 0 : kind == "hint" ? 1 : .12f;
                e.FindPropertyRelative("MaxSimultaneous").intValue = music ? 1 : 2;
                e.FindPropertyRelative("PoolWarmup").intValue = music ? 0 : 1;
                foreach (var effect in new[] { "LowPass", "HighPass", "Echo", "Reverb", "Distortion", "Chorus" })
                    e.FindPropertyRelative("Effects").FindPropertyRelative("Enable" + effect).boolValue = false;
                e.FindPropertyRelative("Duck").FindPropertyRelative("Enabled").boolValue = false;
            }
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        }
    }
}
