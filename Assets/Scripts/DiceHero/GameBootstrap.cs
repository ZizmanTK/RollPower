using UnityEngine;

namespace DiceHero
{
    /// <summary>Entry point: builds the whole game procedurally when the scene starts.</summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Tooltip("URP/Lit material every runtime colour is cloned from (keeps the shader in builds).")]
        public Material baseMaterial;
        [Tooltip("URP/Lit material with emission enabled, cloned for all glowing parts.")]
        public Material glowMaterial;

        public Palette Palette { get; private set; }
        public DiceModel Dice { get; private set; }
        public DiceController Controller { get; private set; }
        public GameLoop Loop { get; private set; }

        void Awake()
        {
            if (Application.isPlaying) Sound.Init();
            BuildWorld();
        }

        public void BuildWorld()
        {
            // The player's die build (campaign stages use Pip's campaign modules), before gun models are made.
            WeaponDef.Apply(Campaign.Pending != null ? Campaign.FacesFor(Campaign.Pending) : Loadout.Faces);
            DiceController.ButtonMode = Settings.RollButton;
            Art.Theme = Campaign.Pending != null ? Campaign.DeckOf(Campaign.Pending).theme : Art.BaseTheme;
            Palette = new Palette(baseMaterial, glowMaterial);
            SetupLighting();
            World.Layout = Campaign.Pending?.layout; // a campaign stage brings its own arena
            World.HazardLayout = Campaign.Pending?.hazards;
            var world = World.Build(Palette);
            // Hazards animate, so they stay out of the static batch.
            Hazards.Build(Palette, new GameObject("Hazards").transform, World.HazardLayout);
            if (Application.isPlaying) StaticBatchingUtility.Combine(world.gameObject); // hundreds of static primitives → a few batches
            Dice = Art.Pip ? PipBuilder.Build(Palette, null, World.PlayerStart) : DiceModel.Build(Palette, null, World.PlayerStart);
            Controller = Dice.Root.gameObject.AddComponent<DiceController>();
            Controller.Init(Dice);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Art.Space;
                cam.farClipPlane = 250f;
                var follow = cam.GetComponent<CameraFollow>();
                if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
                follow.target = Dice.Root;
                follow.SnapToTarget();
            }

            Loop = gameObject.GetComponent<GameLoop>();
            if (Loop == null) Loop = gameObject.AddComponent<GameLoop>();
            Loop.Init(Controller, Palette);
        }

        static void SetupLighting()
        {
            var sun = Object.FindAnyObjectByType<Light>();
            if (sun == null) sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
            sun.color = new Color(0.9f, 0.93f, 1f);
            sun.intensity = 2.1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.8f;

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.36f, 0.48f);
            RenderSettings.ambientEquatorColor = new Color(0.2f, 0.22f, 0.3f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.08f);
            RenderSettings.fog = false; // open space: stars and the planet stay crisp
            if (Art.Theme != 0) Art.Light(sun);
        }
    }
}
