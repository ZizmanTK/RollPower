using UnityEngine;

namespace DiceHero
{
    /// <summary>Entry point: builds the whole prototype procedurally when the scene starts.</summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Tooltip("Standard-shader material every runtime colour is cloned from (keeps the shader in builds).")]
        public Material baseMaterial;
        [Tooltip("Standard-shader material with emission enabled, cloned for all glowing parts.")]
        public Material glowMaterial;

        public Palette Palette { get; private set; }
        public DiceModel Dice { get; private set; }
        public DiceController Controller { get; private set; }
        public GameLoop Loop { get; private set; }

        void Awake()
        {
            BuildWorld();
        }

        public void BuildWorld()
        {
            Palette = new Palette(baseMaterial, glowMaterial);
            SetupLighting();
            World.Build(Palette);
            Dice = DiceModel.Build(Palette, null, World.PlayerStart);
            Controller = Dice.Root.gameObject.AddComponent<DiceController>();
            Controller.Init(Dice);
            Loop = gameObject.GetComponent<GameLoop>();
            if (Loop == null) Loop = gameObject.AddComponent<GameLoop>();
            Loop.Init(Controller, Palette);
            if (Application.isPlaying) Sound.Init(gameObject);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.02f, 0.03f, 0.06f);
                var follow = cam.GetComponent<CameraFollow>();
                if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
                follow.target = Dice.Root;
                follow.SnapToTarget();
            }
        }

        static void SetupLighting()
        {
            var sun = Object.FindFirstObjectByType<Light>();
            if (sun == null) sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(58f, -30f, 0f);
            sun.color = new Color(0.72f, 0.82f, 1f);
            sun.intensity = 2.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.3f, 0.38f, 0.55f);
            RenderSettings.ambientEquatorColor = new Color(0.2f, 0.24f, 0.34f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.05f, 0.08f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.02f, 0.03f, 0.06f);
            RenderSettings.fogStartDistance = 22f;
            RenderSettings.fogEndDistance = 55f;
        }
    }
}
