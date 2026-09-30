using System;
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
            var wind = WindLoop();
            WriteWavStereo("wind_loop.wav", wind[0], wind[1]);
            AssetDatabase.Refresh();

            // Import intent for generated files: one-shots decompress on load
            // (cheap, snappy); the 24 s wind bed streams Vorbis like the fire loop.
            foreach (var f in new[] { "crickets_loop.wav", "owl_hoot.wav", "wind_gust.wav",
                         "leaf_rustle.wav", "twig_snap.wav", "chime_soft.wav" })
                MvpContentBuilder.ApplyAudioImportSettings($"{Dir}/{f}", "DecompressOnLoad", "PCM");
            MvpContentBuilder.ApplyAudioImportSettings($"{Dir}/wind_loop.wav", "Streaming", "Vorbis");
            UpdateWindCatalogEntry();

            var catalog = AssetDatabase.LoadAssetAtPath<QuietCampAudioCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[QuietCamp] AudioCatalog.asset missing — run MvpContentBuilder first.");
                return;
            }

            // Unity priority is inverted: lower number wins the voice. UI/feedback
            // (96–128) must outrank ambience beds (160–176) under load.
            var extras = new List<AudioSoundDefinition>
            {
                Def("ambience.crickets", "crickets_loop.wav", AudioBus.Ambience, 0.14f, loop: true,
                    maxSimultaneous: 1, priority: 168),
                Def("ambience.owl", "owl_hoot.wav", AudioBus.Ambience, 0.18f,
                    spatial: 1f, minDist: 3f, maxDist: 30f, maxSimultaneous: 1,
                    priority: 176,
                    // Outdoor echo only: a faint single slapback, never a room.
                    echo: new AudioEffectSettings
                    {
                        EnableEcho = true, EchoDelay = 180f, EchoDecayRatio = .20f,
                        EchoWetMix = .08f, EchoDryMix = 1f
                    }),
                Def("ambience.gust", "wind_gust.wav", AudioBus.Ambience, 0.22f,
                    spatial: 1f, minDist: 4f, maxDist: 26f, maxSimultaneous: 1,
                    priority: 168),
                Def("sfx.rustle", "leaf_rustle.wav", AudioBus.Sfx, 0.30f,
                    minDist: 1.5f, maxDist: 8f, maxSimultaneous: 2,
                    priority: 128, cooldown: .3f),
                Def("sfx.twig", "twig_snap.wav", AudioBus.Sfx, 0.26f,
                    maxSimultaneous: 2, priority: 128, cooldown: .3f),
                Def("sfx.chime", "chime_soft.wav", AudioBus.Ui, 0.12f,
                    maxSimultaneous: 1, priority: 104),
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
                e.FindPropertyRelative("SpatialBlend").floatValue = d.SpatialBlend;
                e.FindPropertyRelative("MinDistance").floatValue = d.MinDistance;
                e.FindPropertyRelative("MaxDistance").floatValue = d.MaxDistance;
                e.FindPropertyRelative("Pitch").floatValue = 1f;
                e.FindPropertyRelative("Priority").intValue = d.Priority;
                e.FindPropertyRelative("MaxSimultaneous").intValue = d.MaxSimultaneous;
                e.FindPropertyRelative("Cooldown").floatValue = d.Cooldown;
                e.FindPropertyRelative("PoolWarmup").intValue = 1;
                var fx = e.FindPropertyRelative("Effects");
                fx.FindPropertyRelative("EnableEcho").boolValue = d.Effects != null && d.Effects.EnableEcho;
                if (d.Effects != null && d.Effects.EnableEcho)
                {
                    fx.FindPropertyRelative("EchoDelay").floatValue = d.Effects.EchoDelay;
                    fx.FindPropertyRelative("EchoDecayRatio").floatValue = d.Effects.EchoDecayRatio;
                    fx.FindPropertyRelative("EchoWetMix").floatValue = d.Effects.EchoWetMix;
                    fx.FindPropertyRelative("EchoDryMix").floatValue = d.Effects.EchoDryMix;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Debug.Log($"[QuietCamp] {extras.Count} generated sounds registered in AudioCatalog.");
        }

        static AudioSoundDefinition Def(string key, string file, AudioBus bus, float volume,
            bool loop = false, float spatial = 0f, float minDist = 0f, float maxDist = 0f,
            int maxSimultaneous = 8, int priority = 128, float cooldown = 0f,
            AudioEffectSettings echo = null)
            => new AudioSoundDefinition
            {
                Key = key,
                Clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{Dir}/{file}"),
                Bus = bus,
                Volume = volume,
                Loop = loop,
                SpatialBlend = spatial,
                MinDistance = minDist,
                MaxDistance = maxDist,
                MaxSimultaneous = maxSimultaneous,
                Priority = priority,
                Cooldown = cooldown,
                Effects = echo,
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
            // A modulo-written pulse crossing the wrap continues at the head,
            // but independent trains still leave a boundary step: fold the last
            // 120 ms into the head and truncate — the seam joins two originally
            // adjacent samples instead of two unrelated phases.
            s = CyclicCrossfade(s, .12f);
            // Overlapping trains can exceed full scale — normalize to -4.4 dBFS.
            NormalizeTo(s, 0.6f);
            return s;
        }

        /// <summary>
        /// Proper cyclic wrap for loops: the last `seconds` are folded into the
        /// head with a linear crossfade and the tail is truncated, so the loop
        /// boundary joins two originally-adjacent samples — no energy dip, no
        /// click. Returns a buffer `fade` shorter than the input.
        /// </summary>
        static float[] CyclicCrossfade(float[] s, float seconds)
        {
            var f = Mathf.Min((int)(Rate * seconds), s.Length / 4);
            var len = s.Length - f;
            var o = new float[len];
            for (var i = 0; i < len; i++)
                o[i] = i < f
                    ? Mathf.Lerp(s[len + i], s[i], i / (float)f)
                    : s[i];
            return o;
        }

        /// <summary>Short smooth fade-out at the buffer tail — kills the residual
        /// endpoint step on one-shots without touching the attack.</summary>
        static void TailFade(float[] s, float seconds)
        {
            var n = Mathf.Min((int)(Rate * seconds), s.Length);
            for (var i = 0; i < n; i++)
                s[s.Length - n + i] *= 1f - i / (float)n;
        }

        static void NormalizeTo(float[] s, float target)
        {
            var peak = 0f;
            foreach (var v in s) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak <= 0f) return;
            var g = target / peak;
            for (var i = 0; i < s.Length; i++) s[i] *= g;
        }

        /// <summary>Subtracts the mean so noise-asymmetry cannot leave a DC
        /// component that thumps when the clip is cut abruptly.</summary>
        static void RemoveDc(float[] s)
        {
            var mean = 0f;
            foreach (var v in s) mean += v;
            mean /= s.Length;
            for (var i = 0; i < s.Length; i++) s[i] -= mean;
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
            TailFade(s, .005f);
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
            RemoveDc(s);
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
            TailFade(s, .004f);
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
                // Crack keeps its attack, but eases over ~0.8 ms instead of
                // starting at full level — removes the t=0 discontinuity.
                var click = t < 0.006f ? (1f - t / 0.006f) * Mathf.Min(1f, t / 0.0008f) : 0f;
                // ~0.5 ms micro-attack on the noise too — a raw noise sample at
                // t=0 is a digital click regardless of the crack's own ramp.
                var onset = Mathf.Min(1f, t / 0.0005f);
                s[i] = (noise * env * 0.55f + click * 0.4f) * onset;
            }
            RemoveDc(s);
            TailFade(s, .004f);
            NormalizeTo(s, .65f);
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
            TailFade(s, .005f);
            return s;
        }

        /// <summary>Points ambience.wind in audio_catalog.json at the owned
        /// generated bed and refreshes its integrity hash so the manifest
        /// stays truthful after every regeneration.</summary>
        static void UpdateWindCatalogEntry()
        {
            const string jsonPath = "Assets/QuietCamp/Resources/QuietCamp/audio_catalog.json";
            var derivative = $"{Dir}/wind_loop.wav";
            var full = Path.Combine(Directory.GetCurrentDirectory(), derivative);
            if (!File.Exists(full)) return;
            var doc = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(jsonPath));
            foreach (var s in doc["sounds"])
            {
                if ((string)s["key"] != "ambience.wind") continue;
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    var hash = sha.ComputeHash(File.ReadAllBytes(full));
                    s["sha256"] = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
                s["assetPath"] = derivative;
                s["durationSeconds"] = Math.Round(
                    (File.ReadAllBytes(full).Length - 44) / 4.0 / Rate, 4);
                // The licensed Moyva "Wind Soft Loop" turned out to be a mostly
                // sub-audible DC/VLF drift (AC content ~-40 dB RMS) — unusable
                // as an audible bed. We ship an owned generated loop instead;
                // the Moyva file remains untouched on disk.
                s["derivedFrom"] = "Assets/Moyva/Audio/Ambience/Wind Soft Loop.wav";
                s["licenseStatus"] = "Procedurally generated for QuietCamp; supersedes the Moyva wind file which measured as mostly sub-audible DC drift";
                File.WriteAllText(jsonPath, doc.ToString(Newtonsoft.Json.Formatting.Indented));
                AssetDatabase.ImportAsset(jsonPath);
                return;
            }
        }

        /// <summary>
        /// 24 s stereo wind bed: two decorrelated slow-swell noise fields.
        /// Replaces the licensed Moyva wind file that measured as a mostly
        /// sub-audible DC drift (diff-RMS ~0.0001). Ends fold cyclically into
        /// the head — a true seamless loop, no per-cycle dip.
        /// </summary>
        static float[][] WindLoop()
        {
            var n = Rate * 24;
            var l = new float[n];
            var r = new float[n];
            var rng = new System.Random(77);
            float lpL = 0f, lpR = 0f, lp2L = 0f, lp2R = 0f;
            // Two slow LFO swells per channel, decorrelated phases.
            for (var i = 0; i < n; i++)
            {
                var t = i / (float)Rate;
                var wL = (float)rng.NextDouble() * 2f - 1f;
                var wR = (float)rng.NextDouble() * 2f - 1f;
                lpL += (wL - lpL) * 0.04f; lp2L += (lpL - lp2L) * 0.35f;
                lpR += (wR - lpR) * 0.04f; lp2R += (lpR - lp2R) * 0.35f;
                var swirlL = .55f + .30f * Mathf.Sin(2f * Mathf.PI * t / 9.7f + 1.1f)
                    + .15f * Mathf.Sin(2f * Mathf.PI * t / 3.9f);
                var swirlR = .55f + .30f * Mathf.Sin(2f * Mathf.PI * t / 11.3f + 2.6f)
                    + .15f * Mathf.Sin(2f * Mathf.PI * t / 4.7f + .8f);
                l[i] = lp2L * swirlL * 1.6f;
                r[i] = lp2R * swirlR * 1.6f;
            }
            // Fold the wrap: 200 ms of tail crossfades into the head per channel.
            l = CyclicCrossfade(l, .2f);
            r = CyclicCrossfade(r, .2f);
            NormalizeTo(l, .55f); NormalizeTo(r, .55f);
            return new[] { l, r };
        }

        // ─── WAV writer (PCM16) ─────────────────────────────────────────────

        static void WriteWavStereo(string name, float[] left, float[] right)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), Dir, name);
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new BinaryWriter(fs))
            {
                var byteCount = left.Length * 4;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + byteCount);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                w.Write(16); w.Write((short)1); w.Write((short)2);
                w.Write(Rate); w.Write(Rate * 4); w.Write((short)4); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(byteCount);
                for (var i = 0; i < left.Length; i++)
                {
                    w.Write((short)Mathf.Clamp(Mathf.RoundToInt(left[i] * 30000f), -32768, 32767));
                    w.Write((short)Mathf.Clamp(Mathf.RoundToInt(right[i] * 30000f), -32768, 32767));
                }
            }
        }


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
