using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    public enum Sfx
    {
        Gun1, Gun2, Gun3, Gun4, Gun5, Gun6,
        Explosion, BigExplosion, Hit, Deflect, Slam, MegaSlam, Dash, Hurt, WaveStart, EnemyShot, Roll,
    }

    /// <summary>
    /// All audio is synthesized at startup (no audio files): one clip per sound effect plus a looping
    /// synthwave track. Play() is a no-op until Init() has run, so batch tests stay silent.
    /// </summary>
    public static class Sound
    {
        const int Rate = 44100;
        static readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        static readonly Dictionary<Sfx, float> lastPlayed = new Dictionary<Sfx, float>();
        static AudioSource[] pool;
        static AudioSource music;
        static int next;
        static System.Random rng = new System.Random(1);

        public static float SfxVolume = 0.8f;
        public static float MusicVolume = 0.4f;

        public static void Init(GameObject host)
        {
            if (clips.Count == 0) Build();
            pool = new AudioSource[12];
            for (int i = 0; i < pool.Length; i++)
            {
                pool[i] = host.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
            }
            music = host.AddComponent<AudioSource>();
            music.clip = BuildMusic();
            music.loop = true;
            music.volume = MusicVolume;
            music.Play();
        }

        public static void Play(Sfx s, float volume = 1f, float pitchJitter = 0.06f)
        {
            if (pool == null || !clips.TryGetValue(s, out var clip)) return;
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(s, out float t) && now - t < 0.035f) return; // avoid stacking the same sound
            lastPlayed[s] = now;
            var src = pool[next];
            next = (next + 1) % pool.Length;
            src.pitch = 1f + ((float)rng.NextDouble() * 2f - 1f) * pitchJitter;
            src.PlayOneShot(clip, volume * SfxVolume);
        }

        public static Sfx GunSound(int number) => (Sfx)(number - 1);

        // ------------------------------------------------------------------ synthesis helpers

        static float Noise() => (float)rng.NextDouble() * 2f - 1f;
        static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase + 0.5f));
        static float Square(float phase, float duty = 0.5f) => (phase - Mathf.Floor(phase)) < duty ? 1f : -1f;
        static float Tri(float phase) => 1f - 4f * Mathf.Abs(phase - Mathf.Floor(phase + 0.5f));
        static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);

        /// <summary>Renders f(t, dt) into a clip; f returns the sample for time t (phase accumulation is up to the caller).</summary>
        static AudioClip Make(string name, float seconds, Func<float, float> f, float gain = 0.8f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            float peak = 0.0001f;
            for (int i = 0; i < n; i++) { data[i] = f(i / (float)Rate); peak = Mathf.Max(peak, Mathf.Abs(data[i])); }
            float norm = gain / peak;
            for (int i = 0; i < n; i++) data[i] *= norm;
            // Short fade-out to avoid clicks.
            int fade = Mathf.Min(n, 200);
            for (int i = 0; i < fade; i++) data[n - 1 - i] *= i / (float)fade;
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Pitch sweep oscillator: integrates frequency so the sweep has no discontinuities.</summary>
        class Osc
        {
            double phase;
            public float Next(float freq) { phase += freq / Rate; return (float)(phase - Math.Floor(phase)); }
        }

        class LowPass
        {
            float y;
            public float Next(float x, float cutoff) { float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate); y += a * (x - y); return y; }
        }

        static void Build()
        {
            {   // 1 Railgun: charge whine into a sharp crack
                var o = new Osc(); var lp = new LowPass();
                clips[Sfx.Gun1] = Make("gun1", 0.45f, t =>
                {
                    float whine = Mathf.Sin(2f * Mathf.PI * o.Next(Mathf.Lerp(900f, 2600f, Mathf.Clamp01(t / 0.06f)))) * Env(t, 0.005f, 0.05f);
                    float crack = lp.Next(Noise(), 5000f - t * 9000f) * Env(t, 0.002f, 0.12f);
                    return whine * 0.5f + crack;
                });
            }
            {   // 2 Twin blasters: short pulse blips
                var o = new Osc();
                clips[Sfx.Gun2] = Make("gun2", 0.1f, t => Square(o.Next(Mathf.Lerp(1100f, 420f, t / 0.1f)), 0.3f) * Env(t, 0.002f, 0.035f), 0.5f);
            }
            {   // 3 Tri-shot: bright zap
                var o = new Osc();
                clips[Sfx.Gun3] = Make("gun3", 0.16f, t => (Tri(o.Next(Mathf.Lerp(1500f, 300f, t / 0.16f))) + Noise() * 0.15f) * Env(t, 0.002f, 0.05f), 0.6f);
            }
            {   // 4 Plasma cannon: deep wobbling thump
                var o = new Osc(); var lp = new LowPass();
                clips[Sfx.Gun4] = Make("gun4", 0.6f, t =>
                {
                    float f = Mathf.Lerp(220f, 55f, Mathf.Clamp01(t / 0.35f)) * (1f + 0.08f * Mathf.Sin(t * 60f));
                    float body = Saw(o.Next(f));
                    return lp.Next(body + Noise() * 0.3f, 1400f) * Env(t, 0.004f, 0.18f);
                });
            }
            {   // 5 Scatter gun: filtered noise blast
                var lp = new LowPass();
                clips[Sfx.Gun5] = Make("gun5", 0.3f, t => lp.Next(Noise(), 3200f - t * 7000f) * Env(t, 0.002f, 0.07f));
            }
            {   // 6 Missile pod: launch whoosh
                var lp = new LowPass(); var o = new Osc();
                clips[Sfx.Gun6] = Make("gun6", 0.5f, t =>
                    lp.Next(Noise(), 600f + t * 4000f) * Env(t, 0.03f, 0.2f) + Mathf.Sin(2f * Mathf.PI * o.Next(180f + t * 400f)) * 0.2f * Env(t, 0.01f, 0.1f), 0.6f);
            }
            {   // Explosions: low rumble + crackle
                var lp = new LowPass(); var o = new Osc();
                clips[Sfx.Explosion] = Make("boom", 0.7f, t =>
                    lp.Next(Noise(), 900f - t * 900f) * Env(t, 0.003f, 0.18f) + Mathf.Sin(2f * Mathf.PI * o.Next(Mathf.Lerp(110f, 40f, t / 0.7f))) * Env(t, 0.003f, 0.15f));
                var lp2 = new LowPass(); var o2 = new Osc();
                clips[Sfx.BigExplosion] = Make("bigboom", 1.3f, t =>
                    lp2.Next(Noise(), 700f - t * 450f) * Env(t, 0.005f, 0.35f) + Mathf.Sin(2f * Mathf.PI * o2.Next(Mathf.Lerp(80f, 30f, t / 1.3f))) * 1.2f * Env(t, 0.005f, 0.3f));
            }
            {   // Hit tick
                var o = new Osc();
                clips[Sfx.Hit] = Make("hit", 0.06f, t => (Square(o.Next(1800f - t * 12000f)) * 0.5f + Noise() * 0.5f) * Env(t, 0.001f, 0.015f), 0.4f);
            }
            {   // Deflect: metallic ping (inharmonic partials)
                clips[Sfx.Deflect] = Make("deflect", 0.4f, t =>
                    (Mathf.Sin(2f * Mathf.PI * 1760f * t) + 0.6f * Mathf.Sin(2f * Mathf.PI * 2703f * t) + 0.4f * Mathf.Sin(2f * Mathf.PI * 4150f * t)) * Env(t, 0.001f, 0.09f), 0.45f);
            }
            {   // Slams: heavy thump
                var o = new Osc(); var lp = new LowPass();
                clips[Sfx.Slam] = Make("slam", 0.45f, t =>
                    Mathf.Sin(2f * Mathf.PI * o.Next(Mathf.Lerp(140f, 38f, Mathf.Clamp01(t / 0.2f)))) * Env(t, 0.002f, 0.12f) + lp.Next(Noise(), 1500f) * Env(t, 0.001f, 0.03f));
                var o2 = new Osc(); var lp2 = new LowPass();
                clips[Sfx.MegaSlam] = Make("megaslam", 0.9f, t =>
                    Mathf.Sin(2f * Mathf.PI * o2.Next(Mathf.Lerp(120f, 30f, Mathf.Clamp01(t / 0.35f)))) * Env(t, 0.002f, 0.25f) + lp2.Next(Noise(), 1100f) * Env(t, 0.001f, 0.12f));
            }
            {   // Dash whoosh
                var lp = new LowPass();
                clips[Sfx.Dash] = Make("dash", 0.3f, t => lp.Next(Noise(), 300f + 5000f * Mathf.Sin(Mathf.PI * t / 0.3f)) * Mathf.Sin(Mathf.PI * t / 0.3f), 0.5f);
            }
            {   // Hurt: descending buzz
                var o = new Osc();
                clips[Sfx.Hurt] = Make("hurt", 0.35f, t => Saw(o.Next(Mathf.Lerp(320f, 70f, t / 0.35f))) * Env(t, 0.002f, 0.12f), 0.6f);
            }
            {   // Roll tumble
                var lp = new LowPass();
                clips[Sfx.Roll] = Make("roll", 0.25f, t => lp.Next(Noise(), 800f) * (0.6f + 0.4f * Mathf.Sin(t * 90f)) * Env(t, 0.01f, 0.08f), 0.35f);
            }
            {   // Wave start: rising arpeggio chime
                float[] notes = { 440f, 554.37f, 659.25f, 880f };
                clips[Sfx.WaveStart] = Make("wave", 0.9f, t =>
                {
                    float s = 0f;
                    for (int i = 0; i < notes.Length; i++)
                    {
                        float st = t - i * 0.09f;
                        if (st > 0f) s += (Tri(notes[i] * st) + 0.3f * Mathf.Sin(2f * Mathf.PI * notes[i] * 2f * st)) * Env(st, 0.005f, 0.25f);
                    }
                    return s;
                }, 0.5f);
            }
            {   // Enemy shot
                var o = new Osc();
                clips[Sfx.EnemyShot] = Make("eshot", 0.14f, t => Square(o.Next(Mathf.Lerp(520f, 260f, t / 0.14f)), 0.25f) * Env(t, 0.002f, 0.05f), 0.3f);
            }
        }

        /// <summary>Writes every sound effect and the music loop to 16-bit WAV files (for auditioning outside the game).</summary>
        public static string ExportWavs(string dir)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (clips.Count == 0) Build();
            long sfxMs = sw.ElapsedMilliseconds;
            var musicClip = BuildMusic();
            long musicMs = sw.ElapsedMilliseconds - sfxMs;
            System.IO.Directory.CreateDirectory(dir);
            var report = new System.Text.StringBuilder($"synth time: sfx {sfxMs} ms, music {musicMs} ms\n");
            foreach (var kv in clips) report.AppendLine(WriteWav(kv.Value, System.IO.Path.Combine(dir, kv.Key + ".wav")));
            report.AppendLine(WriteWav(musicClip, System.IO.Path.Combine(dir, "Music.wav")));
            return report.ToString();
        }

        static string WriteWav(AudioClip clip, string path)
        {
            var data = new float[clip.samples];
            clip.GetData(data, 0);
            float peak = 0f; int bad = 0;
            foreach (var v in data) { if (float.IsNaN(v) || float.IsInfinity(v)) bad++; else peak = Mathf.Max(peak, Mathf.Abs(v)); }
            using (var w = new System.IO.BinaryWriter(System.IO.File.Create(path)))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + data.Length * 2);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
                w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(data.Length * 2);
                foreach (var v in data) w.Write((short)(Mathf.Clamp(float.IsNaN(v) ? 0f : v, -1f, 1f) * 32767f));
            }
            return $"{System.IO.Path.GetFileName(path)}: {clip.length:0.00}s peak {peak:0.00}{(bad > 0 ? $" BAD SAMPLES {bad}" : "")}";
        }

        // ------------------------------------------------------------------ music

        /// <summary>8-bar synthwave loop at 120 BPM in A minor: Am – F – C – G, two bars each.</summary>
        static AudioClip BuildMusic()
        {
            const float bpm = 120f;
            float beat = 60f / bpm;
            int bars = 8;
            float length = bars * 4 * beat;
            int n = Mathf.CeilToInt(length * Rate);
            var data = new float[n];

            // Chord roots (MIDI) and triads per 2-bar block.
            int[][] chords = { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 48, 52, 55 }, new[] { 55, 59, 62 } };
            Func<int, float> hz = m => 440f * Mathf.Pow(2f, (m - 69) / 12f);

            var bassLp = new LowPass();
            var padLp = new LowPass();
            var noiseRng = new System.Random(7);
            float bassPhase = 0f, arpPhase = 0f;
            float[] padPhase = new float[6];

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float beatPos = t / beat;                 // beats since start
                int bar = (int)(beatPos / 4f);
                var chord = chords[(bar / 2) % 4];
                float inBeat = beatPos - Mathf.Floor(beatPos);
                float sixteenth = beatPos * 4f;
                float in16 = sixteenth - Mathf.Floor(sixteenth);
                float in8 = beatPos * 2f - Mathf.Floor(beatPos * 2f);

                // Kick on every beat
                float kt = inBeat * beat;
                float kick = Mathf.Sin(2f * Mathf.PI * (45f * kt + 60f * (1f - Mathf.Exp(-kt * 30f)) / 30f * 1.8f)) * Mathf.Exp(-kt * 9f);

                // Snare on beats 2 and 4
                int beatInBar = (int)beatPos % 4;
                float snare = 0f;
                if (beatInBar == 1 || beatInBar == 3)
                {
                    float st = inBeat * beat;
                    snare = ((float)noiseRng.NextDouble() * 2f - 1f) * Mathf.Exp(-st * 14f) * 0.55f
                          + Mathf.Sin(2f * Mathf.PI * 190f * st) * Mathf.Exp(-st * 20f) * 0.3f;
                }

                // Closed hi-hat on off-beat 8ths
                float hat = 0f;
                if (((int)(beatPos * 2f)) % 2 == 1)
                    hat = ((float)noiseRng.NextDouble() * 2f - 1f) * Mathf.Exp(-in8 * beat * 0.5f * 60f) * 0.18f;

                // Bass: driving 8th notes on the root, octave jump on every 4th 8th
                int eighth = (int)(beatPos * 2f);
                float bassHz = hz(chord[0] - 24 + (eighth % 4 == 3 ? 12 : 0));
                bassPhase += bassHz / Rate;
                float bassEnv = Mathf.Exp(-in8 * beat * 0.5f * 6f);
                float bass = bassLp.Next(Saw(bassPhase), 380f + 900f * bassEnv) * (0.55f + 0.45f * bassEnv);

                // Arpeggio: 16ths up the chord over two octaves
                int step = (int)sixteenth % 6;
                int arpNote = chord[step % 3] + 12 + (step >= 3 ? 12 : 0);
                arpPhase += hz(arpNote) / Rate;
                float arp = Square(arpPhase, 0.25f) * Mathf.Exp(-in16 * beat * 0.25f * 18f) * 0.16f;

                // Pad: detuned saws on the triad, filtered
                float pad = 0f;
                for (int v = 0; v < 3; v++)
                {
                    float f = hz(chord[v]);
                    padPhase[v * 2] += f * 1.003f / Rate;
                    padPhase[v * 2 + 1] += f * 0.997f / Rate;
                    pad += Saw(padPhase[v * 2]) + Saw(padPhase[v * 2 + 1]);
                }
                pad = padLp.Next(pad, 900f) * 0.06f;

                // Sidechain-style pump from the kick
                float pump = 0.55f + 0.45f * Mathf.Clamp01(kt / 0.18f);
                data[i] = kick * 0.9f + snare + hat + (bass * 0.5f + arp + pad) * pump;
            }

            float peak = 0.0001f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.85f;
            var clip = AudioClip.Create("SynthwaveLoop", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
