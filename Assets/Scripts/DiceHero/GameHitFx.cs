using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// What a hit looks like, so the player sees why it worked or didn't:
    ///   fire sets the robot burning (flames, a little damage over time; vines blacken, ice steams),
    ///   shock crackles over it in arcs and stuns it for a moment, a piercing slug punches out the far side in
    ///   sparks and plate fragments, explosives blast debris, bolts spark.
    /// A blocked shot shows the reason too: it ricochets off steel, skids off ice in chips, is soaked by a shield
    /// (the dome flares) or passes through vines (leaves flutter; Projectiles lets the shot fly on).
    /// </summary>
    public partial class Game
    {
        static readonly Color FireCol = Palette.Hex("#FF7A1A"), FireHot = Palette.Hex("#FFD04A"), ShockCol = Palette.Hex("#FFE14D"),
            SteelCol = Palette.Hex("#AEB8C4"), IceCol = Palette.Hex("#DDF4FF"), LeafCol = Palette.Hex("#4FA34A");

        public void HitFx(Enemy e, WeaponDef w, Vector3 from, Defence was)
        {
            Vector3 p = e.Position;
            Vector3 dir = p - from; dir.y = 0f; dir = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.forward;
            switch (w.type)
            {
                case DamageType.Fire:
                    e.burnT = 1.6f;
                    if (was == Defence.Ice && Random.value < 0.5f) Fx.Puff(pal, p + Vector3.up * 0.3f, 0.35f, 0.6f); // steam
                    if (Random.value < 0.15f) Fx.Flame(pal, p + Random.insideUnitSphere * e.radius * 0.6f, FireCol, 0.16f, 0.35f);
                    break;
                case DamageType.Shock:
                    e.shockT = Mathf.Max(e.shockT, e.IsBig ? 0.25f : 0.55f);
                    for (int i = 0; i < 2; i++) Fx.Arc(pal, p + Random.onUnitSphere * e.radius * 0.8f, p + Random.onUnitSphere * e.radius * 0.8f, ShockCol, 0.12f);
                    if (was == Defence.Shield) Fx.Flash(pal, p, CoatModels.Tint(Defence.Shield), e.radius * 2.6f, 0.08f);
                    break;
                case DamageType.Pierce:
                    // Punches out the far side.
                    Fx.Spray(pal, p + dir * e.radius, dir, Color.white, 5, 9f);
                    if (was == Defence.Steel) Fx.Debris(pal, p, SteelCol, 3, 4f);
                    Fx.Flash(pal, p, w.color, 0.5f, 0.06f);
                    break;
                case DamageType.Blast:
                    Fx.Debris(pal, p, was == Defence.Steel ? SteelCol : Palette.Hex("#2E323C"), 4, 5f);
                    break;
                default:
                    Fx.Sparks(pal, p, w.color, 2);
                    break;
            }
        }

        public void DeflectFx(Enemy e, WeaponDef w, Vector3 from)
        {
            Vector3 p = e.Position;
            Vector3 n = from - p; n.y = 0f; n = n.sqrMagnitude > 0.001f ? n.normalized : Vector3.back;
            Vector3 inc = -n;
            Vector3 side = Vector3.Cross(Vector3.up, n);
            switch (e.Untouchable ? Defence.Bare : e.CurrentDefence)
            {
                case Defence.Steel:
                    // Ricochet: off the plate at an angle, with a spark and a "tink".
                    Fx.Spray(pal, p + n * e.radius, Vector3.Reflect(inc, n) + side * Random.Range(-0.8f, 0.8f) + Vector3.up * 0.4f, Palette.Hex("#FFD24A"), 3, 8f);
                    break;
                case Defence.Ice:
                    Fx.Spray(pal, p + n * e.radius, side * (Random.value < 0.5f ? -1f : 1f) + Vector3.up * 0.2f, IceCol, 2, 6f);
                    Fx.Debris(pal, p + n * e.radius, IceCol, 2, 2.5f);
                    break;
                case Defence.Shield:
                    Fx.Flash(pal, p, CoatModels.Tint(Defence.Shield), e.radius * 2.6f, 0.07f);
                    break;
                case Defence.Vines:
                    Fx.Debris(pal, p, LeafCol, 2, 2f);
                    break;
                case Defence.Burrowed:
                    Fx.Puff(pal, p, 0.4f, 0.5f);
                    break;
            }
        }

        /// <summary>Burning and shock on a robot. Returns true while shock stuns it (it doesn't move or bite).</summary>
        bool StepStatus(Enemy e, float dt)
        {
            if (e.burnT > 0f)
            {
                e.burnT -= dt;
                e.fxT -= dt;
                if (e.fxT <= 0f)
                {
                    e.fxT = 0.12f;
                    Vector3 at = e.pos + Vector3.up * (e.Flying ? 2.0f : e.radius * 0.9f) + Random.insideUnitSphere * e.radius * 0.7f;
                    Fx.Flame(pal, at, Random.value < 0.4f ? FireHot : FireCol, 0.1f + e.radius * 0.18f, 0.4f);
                    if (Random.value < 0.15f) Fx.Puff(pal, at + Vector3.up * 0.3f, 0.16f, 0.6f);
                }
                // Burning eats what fire gets through: vines, ice, or a bare robot.
                if (Enemy.Counters(DamageType.Fire, e.CurrentDefence) && !e.Untouchable)
                {
                    float d = 0.6f * dt;
                    if (e.Coated) { e.coatHp -= d; if (e.coatHp <= 0f) CoatBroken(e); }
                    else if (!(e.kind == EnemyKind.Smelter && e.mode < 3)) { e.hp -= d; if (e.hp <= 0f) { Killed(e); return true; } }
                }
            }
            if (e.shockT > 0f)
            {
                e.shockT -= dt;
                e.fxT -= dt;
                if (e.fxT <= 0f)
                {
                    e.fxT = 0.06f;
                    Vector3 c = e.Position;
                    Fx.Arc(pal, c + Random.onUnitSphere * e.radius, c + Random.onUnitSphere * e.radius, ShockCol, 0.09f);
                }
                return !e.IsBig;
            }
            return false;
        }
    }
}
