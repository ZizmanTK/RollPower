using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    public enum Track { None, Menu, Gauntlet, Scrap, Hydro, Cryo, Foundry, Core, Boss }

    /// <summary>
    /// Music: one generated theme per place, so each deck sounds like itself.
    ///   Menu     slow, spacious pads and a soft bell arpeggio (A minor, no drums)
    ///   Scrap    industrial: distorted bass, metal clanks on the off-beats (E minor)
    ///   Hydro    organic: marimba plucks on a pentatonic line, a shaker, warm pad (D dorian)
    ///   Cryo     icy: glassy FM bells through a long echo, sparse drums (F# minor)
    ///   Foundry  heavy: pounding kick, anvil strikes, a pulsing square bass (C harmonic minor)
    ///   Core     the House: driving synthwave (A minor)
    ///   Boss     fast and tense, 16th-note bass and alarm leads (D minor)
    ///   Gauntlet the original loop (or Resources/Music/RollPower if present)
    /// Each is composed the first time it's needed (22 kHz, 8 bars) and crossfades in.
    /// </summary>
    public static partial class Sound
    {
        const int MRate = 22050;
        static readonly Dictionary<Track, AudioClip> tracks = new Dictionary<Track, AudioClip>();
        static AudioSource musicB;
        static Track current = Track.None;
        static float fade = 1f; // 0 → 1: crossfade from the old source to the new one
        public static Track CurrentTrack => current;

        /// <summary>Switches the music (crossfade). No-op if it's already playing, or before Init.</summary>
        public static void PlayTrack(Track t)
        {
            if (music == null || t == current || t == Track.None) return;
            current = t;
            if (!tracks.TryGetValue(t, out var clip))
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                clip = t == Track.Gauntlet ? (Resources.Load<AudioClip>("Music/RollPower") ?? BuildMusic()) : Compose(t);
                Debug.Log($"[RollPower] music {t}: {clip.length:0.0}s composed in {sw.ElapsedMilliseconds} ms");
                tracks[t] = clip;
            }
            // A deck theme starts with the stage: compose the boss theme now too, so a boss arriving never hitches.
            if (t != Track.Menu && t != Track.Boss && !tracks.ContainsKey(Track.Boss)) tracks[Track.Boss] = Compose(Track.Boss);
            if (musicB == null) { musicB = host.AddComponent<AudioSource>(); musicB.loop = true; musicB.volume = 0f; }
            // Swap: the old music fades out on B while the new one comes in on A.
            (music, musicB) = (musicB, music);
            music.clip = clip; music.loop = true; music.volume = 0f; music.Play();
            fade = 0f;
        }

        /// <summary>Tools: a theme's samples (mono) and rate, for exporting a preview.</summary>
        public static float[] Samples(Track t, out int rate)
        {
            rate = MRate;
            var clip = t == Track.Gauntlet ? BuildMusic() : Compose(t);
            if (t == Track.Gauntlet) rate = Rate;
            var d = new float[clip.samples * clip.channels];
            clip.GetData(d, 0);
            return d;
        }

        static void TickMusic(float dt)
        {
            if (music == null) return;
            fade = Mathf.MoveTowards(fade, 1f, dt / 1.2f);
            music.volume = MusicVolume * duck * fade;
            if (musicB != null) { musicB.volume = MusicVolume * duck * (1f - fade); if (fade >= 1f && musicB.isPlaying) musicB.Stop(); }
        }

        class Style
        {
            public float bpm; public int root; public int[][] chords; public int[] scale;
            public string kick = "", snare = "", hat = "", perc = ""; // 16-step patterns, 'x' = hit
            public int bass;      // 0 sustained, 1 eighths, 2 offbeat, 3 sixteenths
            public int lead;      // 0 none, 1 square, 2 bell, 3 marimba, 4 saw
            public int arp;       // 0 none, 1 slow bell eighths, 2 square sixteenths
            public int percKind;  // 0 none, 1 metal clank, 2 shaker, 3 anvil
            public float padCut = 900f, delay = 0.2f, drive = 0f, padGain = 0.06f, bassGain = 1f;
            public int seed;
        }

        static Style StyleFor(Track t)
        {
            // Chords are semitone offsets from the root; minor triads unless given otherwise.
            int[] m(int r) => new[] { r, r + 3, r + 7 };
            int[] M(int r) => new[] { r, r + 4, r + 7 };
            switch (t)
            {
                case Track.Menu: return new Style { bpm = 84, root = 57, chords = new[] { m(0), M(-4), M(3), M(-2) }, scale = new[] { 0, 2, 3, 5, 7, 8, 10 },
                    hat = "", bass = 0, lead = 0, arp = 1, padCut = 1100f, delay = 0.4f, padGain = 0.06f, bassGain = 0.22f, seed = 1 };
                case Track.Scrap: return new Style { bpm = 112, root = 52, chords = new[] { m(0), M(-4), M(-2), m(0) }, scale = new[] { 0, 3, 5, 7, 10 },
                    kick = "x...x...x...x...", snare = "....x.......x...", hat = "..x...x...x...x.", perc = "..x.....x.x.....", percKind = 1,
                    bass = 1, lead = 1, drive = 0.6f, padCut = 600f, delay = 0.15f, seed = 4 };
                case Track.Hydro: return new Style { bpm = 96, root = 50, chords = new[] { m(0), M(5), m(0), M(-2) }, scale = new[] { 0, 2, 5, 7, 9 },
                    kick = "x.......x.......", snare = "........x.......", perc = "xxxxxxxxxxxxxxxx", percKind = 2,
                    bass = 2, lead = 3, padCut = 1000f, delay = 0.25f, padGain = 0.07f, seed = 5 };
                case Track.Cryo: return new Style { bpm = 90, root = 54, chords = new[] { m(0), M(-4), M(3), M(-2) }, scale = new[] { 0, 2, 3, 7, 8 },
                    kick = "x...............", snare = "............x...", hat = "......x.......x.",
                    bass = 0, lead = 2, arp = 1, padCut = 2400f, delay = 0.5f, padGain = 0.07f, bassGain = 0.4f, seed = 6 };
                case Track.Foundry: return new Style { bpm = 126, root = 48, chords = new[] { m(0), M(-4), m(5), M(7) }, scale = new[] { 0, 2, 3, 5, 7, 8, 11 },
                    kick = "x...x...x.x.x...", snare = "....x.......x...", hat = "x.x.x.x.x.x.x.x.", perc = "x...............", percKind = 3,
                    bass = 3, lead = 4, drive = 0.8f, padCut = 500f, delay = 0.12f, seed = 7 };
                case Track.Core: return new Style { bpm = 132, root = 57, chords = new[] { m(0), M(-4), M(-2), m(7) }, scale = new[] { 0, 2, 3, 5, 7, 8, 10 },
                    kick = "x...x...x...x...", snare = "....x.......x...", hat = "..x...x...x...x.",
                    bass = 1, lead = 1, arp = 2, padCut = 900f, delay = 0.2f, seed = 3 };
                default: return new Style { bpm = 144, root = 50, chords = new[] { m(0), M(-2), M(-4), M(7) }, scale = new[] { 0, 1, 3, 5, 7, 8, 10 },
                    kick = "x...x..xx...x...", snare = "....x.......x.xx", hat = "xxxxxxxxxxxxxxxx",
                    bass = 3, lead = 1, arp = 2, drive = 0.5f, padCut = 700f, delay = 0.15f, seed = 9 };
            }
        }

        static AudioClip Compose(Track t)
        {
            var s = StyleFor(t);
            float beat = 60f / s.bpm, step = beat / 4f;
            const int bars = 8;
            int n = Mathf.CeilToInt(bars * 4 * beat * MRate);
            var data = new float[n];
            var rnd = new System.Random(s.seed);
            Func<int, float> hz = mm => 440f * Mathf.Pow(2f, (mm - 69) / 12f);

            // A two-bar melody from the scale, repeated with a variation in bars 5-8.
            int[] motif = new int[32];
            for (int i = 0; i < 32; i++) motif[i] = rnd.NextDouble() < (s.lead == 2 ? 0.25 : 0.45) ? s.scale[rnd.Next(s.scale.Length)] + (rnd.NextDouble() < 0.3 ? 12 : 0) : -100;
            double padPh0 = 0, padPh1 = 0, padPh2 = 0, padPh3 = 0, padPh4 = 0, padPh5 = 0, bassPh = 0, leadPh = 0, arpPh = 0;
            float padY = 0f, bassY = 0f, percY = 0f, percBp = 0f;
            var noise = new System.Random(s.seed * 13);
            float N() => (float)noise.NextDouble() * 2f - 1f;

            for (int i = 0; i < n; i++)
            {
                float time = i / (float)MRate;
                float beatPos = time / beat;
                int bar = (int)(beatPos / 4f);
                int st = (int)(time / step);            // 16th index
                int s16 = st % 16;
                float inStep = (time - st * step);
                var chord = s.chords[(bar / 2) % s.chords.Length];
                float v = 0f;

                // Drums
                if (s.kick.Length == 16 && s.kick[s16] == 'x')
                    v += Mathf.Sin(2f * Mathf.PI * (48f * inStep + 55f * (1f - Mathf.Exp(-inStep * 28f)) / 28f * 1.8f)) * Mathf.Exp(-inStep * 9f) * 0.9f;
                if (s.snare.Length == 16 && s.snare[s16] == 'x')
                    v += (N() * Mathf.Exp(-inStep * 16f) * 0.45f + Mathf.Sin(2f * Mathf.PI * 185f * inStep) * Mathf.Exp(-inStep * 22f) * 0.25f);
                if (s.hat.Length == 16 && s.hat[s16] == 'x') v += N() * Mathf.Exp(-inStep * 70f) * 0.12f;
                if (s.perc.Length == 16 && s.perc[s16] == 'x')
                {
                    float x = N();
                    percBp += (x - percBp) * 0.35f;          // crude band-pass for metal / shaker
                    float metal = (x - percBp);
                    if (s.percKind == 1) v += (metal * 0.5f + Mathf.Sin(2f * Mathf.PI * 1430f * inStep) * 0.25f) * Mathf.Exp(-inStep * 25f) * 0.35f;
                    else if (s.percKind == 2) v += metal * Mathf.Exp(-inStep * 45f) * 0.12f;
                    else if (s.percKind == 3 && bar % 2 == 0)
                        v += (Mathf.Sin(2f * Mathf.PI * 820f * inStep) + Mathf.Sin(2f * Mathf.PI * 1310f * inStep) * 0.6f + metal * 0.3f) * Mathf.Exp(-inStep * 7f) * 0.3f;
                }

                // Bass
                int bassNote = chord[0] + s.root - 24;
                float bassEnv = 1f;
                if (s.bass == 1) { bassEnv = Mathf.Exp(-((time % (beat / 2f))) * 7f); if (st % 8 == 6) bassNote += 12; }
                else if (s.bass == 2) bassEnv = (st % 4 >= 2) ? Mathf.Exp(-((time % (beat / 2f))) * 5f) : 0f;
                else if (s.bass == 3) bassEnv = Mathf.Exp(-inStep * 14f);
                bassPh += hz(bassNote) / MRate;
                float bp = (float)(bassPh - Math.Floor(bassPh));
                // A held bass (the calm themes) is a rounded sine under a low cutoff: a held saw at 55 Hz just buzzes.
                float braw = s.bass == 0 ? Mathf.Sin(bp * 2f * Mathf.PI) + 0.2f * Mathf.Sin(bp * 4f * Mathf.PI)
                    : s.bass == 2 ? Mathf.Sin(bp * 2f * Mathf.PI) : s.bass == 3 ? (bp < 0.5f ? 1f : -1f) : 2f * bp - 1f;
                float bassCut = s.bass == 0 ? 220f : 300f + 700f * bassEnv;
                bassY += (braw - bassY) * (1f - Mathf.Exp(-2f * Mathf.PI * bassCut / MRate));
                float bass = bassY * (0.4f + 0.6f * bassEnv) * 0.45f * s.bassGain;
                if (s.drive > 0f) bass = (float)Math.Tanh(bass * (1f + s.drive * 4f)) * 0.5f;
                v += bass;

                // Pad: detuned saws on the chord, filtered (tremolo on the icy one)
                double f0 = hz(chord[0] + s.root) / MRate, f1 = hz(chord[1] + s.root) / MRate, f2 = hz(chord[2] + s.root) / MRate;
                padPh0 += f0 * 1.003; padPh1 += f0 * 0.997; padPh2 += f1 * 1.003; padPh3 += f1 * 0.997; padPh4 += f2 * 1.003; padPh5 += f2 * 0.997;
                float saw(double p) => (float)(2.0 * (p - Math.Floor(p)) - 1.0);
                float pad = saw(padPh0) + saw(padPh1) + saw(padPh2) + saw(padPh3) + saw(padPh4) + saw(padPh5);
                padY += (pad - padY) * (1f - Mathf.Exp(-2f * Mathf.PI * s.padCut / MRate));
                float trem = t == Track.Cryo ? 0.75f + 0.25f * Mathf.Sin(time * 5f) : 1f;
                v += padY * s.padGain * trem;

                // Lead: the motif on eighths
                if (s.lead > 0)
                {
                    int e8 = (int)(beatPos * 2f);
                    int note = motif[(e8 % 32 + (bar >= 4 && e8 % 4 == 3 ? 1 : 0)) % 32];
                    float inE = time - e8 * beat / 2f;
                    if (note > -50)
                    {
                        int mid = s.root + note + (s.lead == 2 ? 12 : 0);
                        leadPh += hz(mid) / MRate;
                        float lp = (float)(leadPh - Math.Floor(leadPh));
                        float lv;
                        if (s.lead == 1) lv = (lp < 0.25f ? 1f : -1f) * Mathf.Exp(-inE * 6f) * 0.12f;
                        else if (s.lead == 2) lv = Mathf.Sin(2f * Mathf.PI * lp + 2.2f * Mathf.Sin(2f * Mathf.PI * lp * 3.5f) * Mathf.Exp(-inE * 4f)) * Mathf.Exp(-inE * 3f) * 0.16f;
                        else if (s.lead == 3) lv = (Mathf.Sin(2f * Mathf.PI * lp) + 0.4f * Mathf.Sin(4f * Mathf.PI * lp)) * Mathf.Exp(-inE * 11f) * 0.2f;
                        else lv = (float)Math.Tanh((2f * lp - 1f) * 3f) * Mathf.Exp(-inE * 4f) * 0.09f;
                        v += lv;
                    }
                }

                // Arpeggio: chord tones going up
                if (s.arp > 0)
                {
                    int idx = s.arp == 1 ? (int)(beatPos * 2f) % 4 : st % 6;
                    int an = chord[idx % 3] + s.root + 12 + (idx >= 3 ? 12 : 0);
                    arpPh += hz(an) / MRate;
                    float ap = (float)(arpPh - Math.Floor(arpPh));
                    float inA = s.arp == 1 ? time % (beat / 2f) : inStep;
                    v += s.arp == 1 ? Mathf.Sin(2f * Mathf.PI * ap) * Mathf.Exp(-inA * 3.5f) * 0.11f : (ap < 0.25f ? 1f : -1f) * Mathf.Exp(-inA * 18f) * 0.07f;
                }

                // Kick pump on everything but the drums, like a sidechain.
                data[i] = v;
            }
            // Echo: a dotted-eighth delay with feedback.
            int dl = Mathf.RoundToInt(beat * 0.75f * MRate);
            for (int i = dl; i < n; i++) data[i] += data[i - dl] * s.delay;
            float peak = 0.0001f;
            for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            for (int i = 0; i < n; i++) data[i] = data[i] / peak * 0.8f;
            // Seam: blend the tail into the start so the loop doesn't click.
            int seam = Mathf.Min(n / 4, MRate / 10);
            for (int i = 0; i < seam; i++) { float k = i / (float)seam; data[i] = data[i] * k + data[n - seam + i] * (1f - k); }
            var clip = AudioClip.Create("Music" + t, n - seam, 1, MRate, false);
            var trimmed = new float[n - seam];
            Array.Copy(data, trimmed, n - seam);
            clip.SetData(trimmed, 0);
            return clip;
        }
    }
}
