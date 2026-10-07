using System.Collections.Generic;
using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// The foremen of decks 2-4 (the Compactor, deck 1, lives in Game.cs). Each tests the deck's new idea:
    ///   Gardener (Hydroponics): a plant: shots pass through its leaves, fire burns it.
    ///   Driller (Cryo Mines): burrows toward Pip (a dust mound); underground only an explosion's shockwave reaches it.
    ///   It surfaces with a shockwave and mites, its drill iced over from the mine: fire melts through.
    ///   Smelter (Foundry): three steel plates (piercing or explosive), then a shielded core (shock), then the core.
    /// Mode fields on Enemy: Gardener 0 rooted, 1 sinking, 2 rising · Driller 0 burrowed, 1 surfacing, 2 surfaced ·
    /// Smelter mode = current plate (3 = core exposed).
    /// </summary>
    public partial class Game
    {
        static readonly Vector3[] GardenerSpots = { new Vector3(0f, 0f, 4f), new Vector3(-5.5f, 0f, 2f), new Vector3(5.5f, 0f, 2f), new Vector3(0f, 0f, -1f) };
        float spiral;

        void StepForeman(Enemy e, float dt, Vector3 dp)
        {
            if (e.spawnT < 1f) return;
            Vector3 to = dp - e.pos; to.y = 0f;
            float dist = to.magnitude;
            Vector3 dir = dist > 0.01f ? to / dist : Vector3.forward;
            bool angry = e.BossFraction < 0.5f;
            e.modeT -= dt;
            e.abilityTimer -= dt;
            e.fireTimer -= dt;
            switch (e.kind)
            {
                case EnemyKind.Gardener:
                {
                    float sink = e.mode == 1 ? Mathf.Clamp01(e.modeT / 0.8f) : e.mode == 2 ? 1f - Mathf.Clamp01(e.modeT / 0.8f) : 1f;
                    e.t.localScale = new Vector3(1f, Mathf.Max(0.05f, sink), 1f);
                    if (e.mode == 1 && e.modeT <= 0f) { e.pos = GardenerSpots[(System.Array.IndexOf(GardenerSpots, Nearest(GardenerSpots, e.pos)) + 1 + Random.Range(0, 2)) % GardenerSpots.Length]; e.mode = 2; e.modeT = 0.8f; }
                    else if (e.mode == 2 && e.modeT <= 0f) { e.mode = 0; e.modeT = 10f; }
                    else if (e.mode == 0 && angry && e.modeT <= 0f) { e.mode = 1; e.modeT = 0.8f; Sound.Play(Sfx.Thud, 0.8f); }
                    if (e.mode == 0 && e.fireTimer <= 0f)
                    {
                        // Spore spiral: slow bullets in a ring that turns a little every volley.
                        e.fireTimer = angry ? 2f : 2.6f;
                        int n = angry ? 12 : 8;
                        spiral += 17f;
                        for (int i = 0; i < n; i++)
                        {
                            var d = Quaternion.Euler(0f, spiral + i * 360f / n, 0f) * Vector3.forward;
                            FireBullet(e.pos + Vector3.up * 1.1f + d * 1.2f, d * 4.2f, 1, 0.3f);
                        }
                        Sound.Play(Sfx.EnemyShot, 0.6f);
                    }
                    if (e.mode == 0 && e.abilityTimer <= 0f)
                    {
                        e.abilityTimer = 7f;
                        for (int i = 0; i < 2; i++) Spawn(EnemyKind.Crawler, e.pos + Quaternion.Euler(0f, Random.value * 360f, 0f) * Vector3.forward * 2.2f);
                    }
                    e.t.position = e.pos;
                    e.t.rotation = Quaternion.Slerp(e.t.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-2f * dt));
                    break;
                }
                case EnemyKind.Driller:
                {
                    switch (e.mode)
                    {
                        case 0: // burrowed: the mound homes in on Pip
                            e.pos += dir * Mathf.Min(dist, (angry ? 5f : 4f) * dt);
                            if (e.modeT <= 0f || dist < 0.6f)
                            {
                                e.mode = 1; e.modeT = angry ? 0.55f : 0.75f;
                                e.lane.gameObject.SetActive(true);
                                e.lane.position = e.pos + Vector3.up * 0.02f;
                                Sound.Play(Sfx.Beep, 0.8f, 0f);
                            }
                            break;
                        case 1: // surfacing: a red ring where it will come up
                            e.lane.localScale = new Vector3(4.4f, 0.01f, 4.4f) * (0.85f + 0.15f * Mathf.Sin(Time.time * 30f));
                            if (e.modeT <= 0f)
                            {
                                e.mode = 2; e.modeT = angry ? 2.4f : 3f;
                                e.lane.gameObject.SetActive(false);
                                Juice(0.6f, 0.04f);
                                Fx.Shockwave(pal, e.pos, Palette.Hex("#CFEFFF"), 2.2f);
                                Fx.Debris(pal, e.pos + Vector3.up * 0.2f, Palette.Hex("#BFE6F5"), 12, 5f);
                                Sound.Play(Sfx.MegaSlam, 1f, 0f);
                                if (dist < 2.2f) { HurtPlayer(1, e.pos, "driller"); dice.Knock(dir * 8f); }
                                for (int i = 0; i < (angry ? 4 : 3); i++) Spawn(EnemyKind.Mite, e.pos + Quaternion.Euler(0f, i * 90f + 45f, 0f) * Vector3.forward * 2.6f);
                            }
                            break;
                        default: // surfaced: vulnerable
                            if (dist < e.radius + 0.5f) HurtPlayer(1, e.pos, "driller");
                            if (e.modeT <= 0f) { e.mode = 0; e.modeT = angry ? 2.6f : 3.4f; Fx.Debris(pal, e.pos + Vector3.up * 0.2f, Palette.Hex("#BFE6F5"), 8, 4f); Sound.Play(Sfx.Thud, 0.8f); }
                            break;
                    }
                    bool up = e.mode == 2;
                    e.plates[0].gameObject.SetActive(up);   // the machine
                    e.plates[1].gameObject.SetActive(!up);  // the dust mound
                    if (up) e.plates[0].Rotate(0f, 0f, 0f);
                    else e.plates[1].localRotation = Quaternion.Euler(0f, Time.time * 90f, 0f);
                    float lim = World.HalfSize - 1f;
                    e.pos.x = Mathf.Clamp(e.pos.x, -lim, lim); e.pos.z = Mathf.Clamp(e.pos.z, -lim, lim);
                    e.t.position = e.pos;
                    if (up) e.t.rotation = Quaternion.Slerp(e.t.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-6f * dt));
                    break;
                }
                case EnemyKind.Smelter:
                {
                    e.vel = Vector3.Lerp(e.vel, dist > 3f ? dir * e.speed * (e.mode >= 3 ? 1.5f : 1f) : Vector3.zero, 1f - Mathf.Exp(-3f * dt));
                    e.pos += e.vel * dt;
                    float lim = World.HalfSize - e.radius;
                    e.pos.x = Mathf.Clamp(e.pos.x, -lim, lim); e.pos.z = Mathf.Clamp(e.pos.z, -lim, lim);
                    e.t.position = e.pos;
                    e.t.rotation = Quaternion.Slerp(e.t.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-3f * dt));
                    if (e.fireTimer <= 0f)
                    {
                        e.fireTimer = e.mode >= 3 ? 1.8f : 2.6f;
                        int n = e.mode >= 3 ? 5 : 3;
                        for (int i = 0; i < n; i++)
                        {
                            var d = Quaternion.Euler(0f, (i - (n - 1) * 0.5f) * 16f, 0f) * dir;
                            FireBullet(e.pos + Vector3.up * 1.2f + d * 1.3f, d * 6f, 1, 0.42f);
                        }
                        Sound.Play(Sfx.EnemyShot, 0.7f);
                    }
                    // The plate that can be broken now pulses.
                    for (int i = 0; i < 3; i++)
                        if (e.plates[i] != null && e.plates[i].gameObject.activeSelf)
                            e.plates[i].localScale = Vector3.one * (i == e.mode ? 1f + 0.06f * Mathf.Sin(Time.time * 8f) : 1f);
                    if (dist < e.radius + 0.5f) { HurtPlayer(1, e.pos, "smelter"); dice.Knock(dir * 7f); }
                    break;
                }
            }
        }

        static Vector3 Nearest(Vector3[] spots, Vector3 p)
        {
            Vector3 best = spots[0];
            foreach (var s in spots) if ((s - p).sqrMagnitude < (best - p).sqrMagnitude) best = s;
            return best;
        }

        /// <summary>Smelter: a plate broke; the next one (or the core) is open.</summary>
        public void SmelterPlateBroken(Enemy e)
        {
            var p = e.plates[e.mode];
            Fx.Explosion(pal, p.position, Palette.Hex("#FF7A1A"), 1.6f);
            Fx.Debris(pal, p.position, Palette.Hex("#2A2624"), 10, 5f);
            p.gameObject.SetActive(false);
            Juice(0.5f, 0.05f);
            Sound.Play(Sfx.BigExplosion, 0.8f, 0f);
            e.mode++;
            e.plateHp = Enemy.SmelterPlateHp;
            if (e.mode >= 3) GiveCoat(e, Defence.Shield, Enemy.SmelterShieldHp);
            loop.ShowBanner(null, e.mode < 3 ? $"PLATE DOWN · {3 - e.mode} LEFT" : "CORE SHIELD: " + Enemy.Beaters(Defence.Shield), 2f, UiKit.Gold);
        }

    }

    public static class BossModels
    {
        static readonly Color Red = Palette.Hex("#FF2A3D");

        public static void Build(Enemy e, Palette pal, Transform root)
        {
            var dark = pal.Get("EnemyDark", Palette.Hex("#15171B"), 0.5f, 0.6f);
            var eye = pal.Glow("EnemyEye", Red, 2.6f, Red);
            switch (e.kind)
            {
                case EnemyKind.Gardener:
                {
                    e.hp = 200f; e.speed = 0f; e.radius = 1.2f; e.fireTimer = 2f; e.abilityTimer = 5f; e.modeT = 10f;
                    var gbm = BlenderModels.Spawn("Enemies/Gardener", root, pal);
                    if (gbm != null) { EnemyRig.Attach(root, gbm, EnemyKind.Gardener); break; }
                    var pot = pal.Get("GardPot", Palette.Hex("#3A4B52"), 0.5f, 0.6f);
                    var band = pal.Get("GardBand", Palette.Hex("#C8913E"), 0.7f, 0.9f);
                    var leaf = pal.Get("GardLeaf", Palette.Hex("#4FA34A"), 0.35f, 0f);
                    var leafDark = pal.Get("GardLeafDark", Palette.Hex("#2E6B33"), 0.3f, 0f);
                    Prim.Make(PrimitiveType.Cylinder, "Pot", root, new Vector3(0f, 0.3f, 0f), new Vector3(2.3f, 0.3f, 2.3f), pot);
                    foreach (float y in new[] { 0.18f, 0.48f }) Prim.Make(PrimitiveType.Cylinder, "Band", root, new Vector3(0f, y, 0f), new Vector3(2.36f, 0.04f, 2.36f), band);
                    Prim.Make(PrimitiveType.Sphere, "Bulb", root, new Vector3(0f, 1.05f, 0f), new Vector3(1.5f, 1.2f, 1.5f), leafDark);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60f;
                        var d = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                        Prim.Make(PrimitiveType.Cube, "Petal", root, d * 0.95f + Vector3.up * 0.75f, new Vector3(0.55f, 0.07f, 1.1f), leaf, Quaternion.Euler(-25f, a, 0f));
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        float a = 45f + i * 90f;
                        Prim.Make(PrimitiveType.Cylinder, "Vine", root, Quaternion.Euler(0f, a, 0f) * Vector3.forward * 1.25f + Vector3.up * 0.4f, new Vector3(0.12f, 0.6f, 0.12f), leafDark, Quaternion.Euler(0f, a, 55f));
                    }
                    Prim.Make(PrimitiveType.Sphere, "Core", root, new Vector3(0f, 1.55f, 0.35f), new Vector3(0.45f, 0.45f, 0.45f), eye);
                    break;
                }
                case EnemyKind.Driller:
                {
                    e.hp = 150f; e.speed = 0f; e.radius = 1.0f; e.mode = 0; e.modeT = 2f;
                    var dbm = BlenderModels.Spawn("Enemies/Driller", root, pal);
                    if (dbm != null)
                    {
                        e.plates = new[] { BlenderModels.Pivot(dbm, "Pivot_Machine", root), BlenderModels.Pivot(dbm, "Pivot_Mound", root) };
                        EnemyRig.Attach(root, dbm, EnemyKind.Driller, e.plates[0]);
                        e.lane = Prim.Make(PrimitiveType.Cylinder, "SurfaceRing", null, root.position, new Vector3(4.4f, 0.01f, 4.4f), pal.Glow("DrillWarn", Red, 1.1f, Red * 0.4f)).transform;
                        e.lane.gameObject.SetActive(false);
                        break;
                    }
                    var steel = pal.Get("DrillSteel", Palette.Hex("#5D6B78"), 0.6f, 0.8f);
                    var bit = pal.Get("DrillBit", Palette.Hex("#E8A33A"), 0.6f, 0.8f);
                    var machine = new GameObject("Machine").transform; machine.SetParent(root, false);
                    Prim.Make(PrimitiveType.Cylinder, "Body", machine, new Vector3(0f, 0.55f, -0.1f), new Vector3(1.5f, 0.55f, 1.5f), steel);
                    Prim.Make(PrimitiveType.Cylinder, "BitA", machine, new Vector3(0f, 0.6f, 0.95f), new Vector3(0.9f, 0.25f, 0.9f), bit, Quaternion.Euler(90f, 0f, 0f));
                    Prim.Make(PrimitiveType.Cylinder, "BitB", machine, new Vector3(0f, 0.6f, 1.35f), new Vector3(0.55f, 0.2f, 0.55f), bit, Quaternion.Euler(90f, 0f, 0f));
                    Prim.Make(PrimitiveType.Cylinder, "BitC", machine, new Vector3(0f, 0.6f, 1.65f), new Vector3(0.22f, 0.15f, 0.22f), bit, Quaternion.Euler(90f, 0f, 0f));
                    Prim.Make(PrimitiveType.Cube, "Eye", machine, new Vector3(0f, 1.05f, 0.45f), new Vector3(0.7f, 0.08f, 0.06f), eye);
                    foreach (float x in new[] { -0.75f, 0.75f }) Prim.Make(PrimitiveType.Cube, "Tread", machine, new Vector3(x, 0.22f, -0.1f), new Vector3(0.35f, 0.44f, 1.6f), dark);
                    var mound = new GameObject("Mound").transform; mound.SetParent(root, false);
                    var dirt = pal.Get("DrillDirt", Palette.Hex("#9FB4BF"), 0.2f, 0f);
                    for (int i = 0; i < 6; i++) Prim.Make(PrimitiveType.Sphere, "Dirt", mound, Quaternion.Euler(0f, i * 60f, 0f) * Vector3.forward * 0.45f + Vector3.up * 0.1f, new Vector3(0.7f, 0.3f, 0.7f), dirt);
                    CoatModels.Build(pal, machine, Defence.Ice, 0.95f, 0.75f, false);
                    e.plates = new[] { machine, mound };
                    e.lane = Prim.Make(PrimitiveType.Cylinder, "SurfaceRing", null, root.position, new Vector3(4.4f, 0.01f, 4.4f), pal.Glow("DrillWarn", Red, 1.1f, Red * 0.4f)).transform;
                    e.lane.gameObject.SetActive(false);
                    break;
                }
                case EnemyKind.Smelter:
                {
                    e.speed = 1.1f; e.radius = 1.25f; e.fireTimer = 2f; e.mode = 0;
                    e.hp = 90f; e.plateHp = Enemy.SmelterPlateHp;
                    var sbm = BlenderModels.Spawn("Enemies/Smelter", root, pal);
                    if (sbm != null)
                    {
                        e.plates = new Transform[3];
                        for (int i = 0; i < 3; i++) e.plates[i] = BlenderModels.Pivot(sbm, "Pivot_Plate" + i, root);
                        EnemyRig.Attach(root, sbm, EnemyKind.Smelter);
                        break;
                    }
                    var iron = pal.Get("SmeltIron", Palette.Hex("#2A2624"), 0.4f, 0.8f);
                    var mouth = pal.Glow("SmeltMouth", Palette.Hex("#FF7A1A"), 3f);
                    Prim.Make(PrimitiveType.Cube, "Furnace", root, new Vector3(0f, 0.85f, 0f), new Vector3(1.9f, 1.7f, 1.7f), iron);
                    Prim.Make(PrimitiveType.Cube, "Mouth", root, new Vector3(0f, 0.7f, 0.86f), new Vector3(1.0f, 0.5f, 0.04f), mouth);
                    Prim.Make(PrimitiveType.Cylinder, "Chimney", root, new Vector3(0.5f, 2.0f, -0.4f), new Vector3(0.4f, 0.5f, 0.4f), iron);
                    Prim.Make(PrimitiveType.Cylinder, "Glow", root, new Vector3(0.5f, 2.5f, -0.4f), new Vector3(0.34f, 0.02f, 0.34f), mouth);
                    Prim.Make(PrimitiveType.Cube, "Eye", root, new Vector3(0f, 1.4f, 0.86f), new Vector3(1.2f, 0.07f, 0.05f), eye);
                    foreach (float x in new[] { -0.7f, 0.7f }) Prim.Make(PrimitiveType.Cube, "Leg", root, new Vector3(x, 0.15f, 0f), new Vector3(0.4f, 0.3f, 1.8f), dark);
                    e.plates = new Transform[3];
                    var spots = new[] { (new Vector3(0f, 0.95f, 0.98f), Quaternion.identity), (new Vector3(-1.02f, 0.95f, 0f), Quaternion.Euler(0f, 90f, 0f)), (new Vector3(1.02f, 0.95f, 0f), Quaternion.Euler(0f, -90f, 0f)) };
                    for (int i = 0; i < 3; i++)
                    {
                        var p = new GameObject("Plate" + i).transform; p.SetParent(root, false);
                        p.localPosition = spots[i].Item1; p.localRotation = spots[i].Item2;
                        // Steel shutter, red-hot at the rim: piercing or explosive guns crack it.
                        Prim.Make(PrimitiveType.Cube, "Slab", p, Vector3.zero, new Vector3(1.5f, 1.3f, 0.14f), pal.Get("SmeltPlate", Palette.Hex("#6E7680"), 0.5f, 0.85f));
                        Prim.Make(PrimitiveType.Cube, "Rim", p, new Vector3(0f, -0.62f, 0.07f), new Vector3(1.56f, 0.08f, 0.04f), pal.Glow("SmeltRim", Palette.Hex("#FF7A1A"), 2.2f));
                        foreach (float x in new[] { -0.6f, 0.6f }) foreach (float y in new[] { -0.5f, 0.5f })
                            Prim.Make(PrimitiveType.Sphere, "Rivet", p, new Vector3(x, y, 0.08f), Vector3.one * 0.12f, pal.Get("CoatRivet", Palette.Hex("#D0D6DC"), 0.6f, 0.9f));
                        e.plates[i] = p;
                    }
                    break;
                }
            }
        }
    }
}
