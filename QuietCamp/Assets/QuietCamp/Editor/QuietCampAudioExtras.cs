using System.Collections.Generic;
using System.IO;
using Kruty1918.Audio;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Editor
{
    /// <summary>
    /// Generates additional sound effects procedurally (the kit ships only a
    /// few ambience/UI wavs) and registers them as extra catalog entries —
    /// audio_catalog.json stays untouched, so kit manifest hashes keep passing.
    /// Sounds: crickets loop, owl hoot, wind gust, leaf rustle, twig snap,
    /// soft chime. Idempotent: re-running overwrites the same files/entries.
    /// </summary>
    public static class QuietCampAudioExtras
    {
        const int Rate = 44100;
        const string Dir = "Assets/QuietCamp/Audio/Generated";
        const string CatalogPath = "Assets/QuietCamp/Resources/QuietCamp/AudioCatalog.asset";

        [MenuItem("QuietCamp/Generate Extra Sounds")]
        public static void Generate()
        {
            Directory.CreateDirectory(Path.Combine(Directory.GetCurrentDirectory(), Dir));

            WriteWav("crickets_loop.wav", Crickets());
            WriteWav("owl_hoot.wav", OwlHoot());
            WriteWav("wind_gust.wav", WindGust());
            WriteWav("leaf_rustle.wav", LeafRustle());
            WriteWav("twig_snap.wav", TwigSnap());
            WriteWav("chime_soft.wav", Chime());
            AssetDatabase.Refresh();

            var catalog = AssetDatabase.LoadAssetAtPath<QuietCampAudioCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[QuietCamp] AudioCatalog.asset missing — run MvpContentBuilder first.");
                return;
            }

            var extras = new List<AudioSoundDefinition>
            {
                Def("ambience.crickets", "crickets_loop.wav", AudioBus.Ambience, 0.22f, loop: true),
                Def("ambience.owl", "owl_hoot.wav", AudioBus.Ambience, 0.30f),
                Def("ambience.gust", "wind_gust.wav", AudioBus.Ambience, 0.26f),
                Def("sfx.rustle", "leaf_rustle.wav", AudioBus.Sfx, 0.32f),
                Def("sfx.twig", "twig_snap.wav", AudioBus.Sfx, 0.38f),
                Def("sfx.chime", "chime_soft.wav", AudioBus.Ui, 0.34f),
            };

            var so = new SerializedObject(catalog);
            var prop = so.FindProperty("_extraSounds");
            prop.arraySize = extras.Count;
            for (var i = 0; i < extras.Count; i++)
            {
                var e = prop.GetArrayElementAtIndex(i);
                var d = extras[i];
                e.FindPropertyRelative("Key").stringValue = d.Key;
                e.FindPropertyRelative("Clip").objectReferenceValue = d.Clip;
                e.FindPropertyRelative("Bus").enumValueIndex = (int)d.Bus;
                e.FindPropertyRelative("Volume").floatValue = d.Volume;
                e.FindPropertyRelative("Loop").boolValue = d.Loop;
                e.FindPropertyRelative("SpatialBlend").floatValue = 0f;
                e.FindPropertyRelative("Pitch").floatValue = 1f;
                e.FindPropertyRelative("Priority").intValue = 128;
                e.FindPropertyRelative("MaxSimultaneous").intValue = 8;
                e.FindPropertyRelative("PoolWarmup").intValue = 1;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[QuietCamp] {extras.Count} generated sounds registered in AudioCatalog.");
        }

        static AudioSoundDefinition Def(string key, string file, AudioBus bus, float volume, bool loop = false)
            => new AudioSoundDefinition
            {
                Key = key,
                Clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Dir}/{file}"),
                Bus = bus,
                Volume = volume,
                Loop = loop,
            };

        // ─── Synthesizers ───────────────────────────────────────────────────

        /// <summary>8s seamless cricket field: chirp trains around 4.2kHz.</summary>
        static float[] Crickets()
        {
            var n = Rate * 8;
            var s = new float[n];
            var rng = new System.Random(20240601);
            // Two overlapping "crickets" with different tempos — stereo feel on one channel.
            ChirpTrain(s, rng, 4200f, 4, 0.55f, 0.028f, 0.9f);
            ChirpTrain(s, rng, 5100f, 3, 0.73f, 0.020f, 0.6f);
            return s;
        }

        static void ChirpTrain(float[] s, System.Random rng, float freq,
            int pulses, float period, float pulseLen, float gain)
        {
            var t = (float)rng.NextDouble() * period;
            while (t < 8f + pulseLen * pulses)
            {
                var burst = 0.7f + 0.3f * (float)rng.NextDouble();
                for (var p = 0; p < pulses; p++)
                {
                    var start = t + p * pulseLen * 2f;
                    var i0 = (int)(start * Rate) % s.Length;
                    if (i0 < 0) i0 += s.Length;
                    var len = (int)(pulseLen * Rate);
                    for (var i = 0; i < len; i++)
                    {
                        var env = Mathf.Sin(Mathf.PI * i / len); // smooth on/off
                        s[(i0 + i) % s.Length] += env * env * burst * gain
                            * Mathf.Sin(2f * Mathf.PI * freq * (i / (float)Rate));
                    }
                }
                t += period * (0.85f + 0.3f * (float)rng.NextDouble());
            }
        }

        /// <summary>Two soft owl hoots, pitched down with vibrato.</summary>
        static float[] OwlHoot()
        {
            var n = (int)(Rate * 2.8f);
            var s = new float[n];
            Hoot(s, 0.10f, 340f, 295f, 0.42f, 0.55f);
            Hoot(s, 0.75f, 325f, 280f, 0.38f, 0.38f);
            return s;
        }

        static void Hoot(float[] s, float start, float f0, float f1, float len, float gain)
        {
            var i0 = (int)(start * Rate);
            var lenN = (int)(len * Rate);
            for (var i = 0; i < lenN && i0 + i < s.Length; i++)
            {
                var t = i / (float)lenN;
                var env = Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.15f)); // soft decay tail
                var vib = 1f + 0.006f * Mathf.Sin(2f * Mathf.PI * 11f * i / Rate);
                var f = Mathf.Lerp(f0, f1, t) * vib;
                s[i0 + i] += Mathf.Sin(2f * Mathf.PI * f * i / Rate) * env * env * gain
                    * Mathf.Exp(-2.2f * t);
            }
        }

        /// <summary>4s filtered-noise wind swell.</summary>
        static float[] WindGust()
        {
            var n = Rate * 4;
            var s = new float[n];
            var rng = new System.Random(7);
            float lp = 0f;
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)n;
                lp += (((float)rng.NextDouble() * 2f - 1f) - lp) * 0.02f; // heavy lowpass
                var swell = Mathf.Sin(Mathf.PI * t);                    // rise and fall
                s[i] = lp * swell * swell * 2.6f;
            }
            return s;
        }

        /// <summary>0.7s three crisp leaf-rustle bursts (highpassed noise).</summary>
        static float[] LeafRustle()
        {
            var n = (int)(Rate * 0.7f);
            var s = new float[n];
            var rng = new System.Random(21);
            float lp = 0f;
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)Rate;
                var white = (float)rng.NextDouble() * 2f - 1f;
                lp += (white - lp) * 0.35f;
                var hp = white - lp; // crisp high band
                var burst = (Mathf.Sin(2f * Mathf.PI * 4.5f * t) > 0.2f ? 1f : 0.25f);
                var fade = Mathf.Exp(-3.2f * t);
                s[i] = hp * burst * fade * 0.8f;
            }
            return s;
        }

        /// <summary>0.3s sharp twig snap: click + short noise burst.</summary>
        static float[] TwigSnap()
        {
            var n = (int)(Rate * 0.3f);
            var s = new float[n];
            var rng = new System.Random(33);
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)Rate;
                var noise = (float)rng.NextDouble() * 2f - 1f;
                var env = Mathf.Exp(-60f * t);                 // fast decay
                var click = t < 0.006f ? 1f - t / 0.006f : 0f; // initial crack
                s[i] = noise * env * 0.9f + click * 0.7f;
            }
            return s;
        }

        /// <summary>1.8s soft three-partial chime.</summary>
        static float[] Chime()
        {
            var n = (int)(Rate * 1.8f);
            var s = new float[n];
            var partials = new[] { 523.25f, 784f, 1046.5f };
            var gains = new[] { 0.5f, 0.3f, 0.18f };
            var decays = new[] { 1.6f, 2.4f, 3.2f };
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)Rate;
                for (var p = 0; p < partials.Length; p++)
                {
                    var env = Mathf.Exp(-decays[p] * t) * Mathf.Min(1f, t * 200f);
                    s[i] += Mathf.Sin(2f * Mathf.PI * partials[p] * t) * env * gains[p];
                }
            }
            return s;
        }

        // ─── WAV writer (PCM16 mono) ────────────────────────────────────────

        static void WriteWav(string name, float[] samples)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), Dir, name);
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                var byteCount = samples.Length * 2;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + byteCount);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(byteCount);
                foreach (var v in samples)
                    w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 30000f), -32768, 32767));
            }
        }
    }
}
