using UnityEngine;

namespace DiceHero
{
    /// <summary>
    /// Keyboard + gamepad input in one place (legacy Input Manager, works on Windows and WebGL).
    /// Gamepad: left stick / d-pad move, A roll (or dash) + confirm, B back, Start pause.
    /// </summary>
    public static class Controls
    {
        static float navRepeat;
        static Vector2Int lastNav;
        static bool dpadAxes = true;

        public static Vector2 Move
        {
            get
            {
                var v = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
                v += DPad();
                return Vector2.ClampMagnitude(v, 1f);
            }
        }

        static Vector2 DPad()
        {
            // XInput d-pad on Windows is joystick axes 6/7; WebGL "standard" gamepads report buttons 12-15.
            float x = 0f, y = 0f;
            if (dpadAxes)
            {
                try
                {
                    x = Input.GetAxisRaw("DPadX");
                    y = Input.GetAxisRaw("DPadY");
                }
                catch (System.ArgumentException) { dpadAxes = false; } // axes added by the editor setup; older projects lack them
            }
            if (Input.GetKey(KeyCode.JoystickButton14)) x -= 1f;
            if (Input.GetKey(KeyCode.JoystickButton15)) x += 1f;
            if (Input.GetKey(KeyCode.JoystickButton12)) y += 1f;
            if (Input.GetKey(KeyCode.JoystickButton13)) y -= 1f;
            return new Vector2(Mathf.Clamp(x, -1f, 1f), Mathf.Clamp(y, -1f, 1f));
        }

        public static bool Dash =>
            Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)
            || Input.GetKeyDown(KeyCode.JoystickButton0) || Input.GetKeyDown(KeyCode.JoystickButton5);

        public static bool Pause =>
            Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)
            || Input.GetKeyDown(KeyCode.JoystickButton7) || Input.GetKeyDown(KeyCode.JoystickButton9);

        public static bool Confirm =>
            Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.JoystickButton0);

        public static bool Back =>
            Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace) || Input.GetKeyDown(KeyCode.JoystickButton1);

        public static bool Restart => Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.JoystickButton3);

        /// <summary>Menu navigation with key repeat: arrows / WASD / stick / d-pad. Call once per frame.</summary>
        public static Vector2Int Nav(float unscaledDt)
        {
            var raw = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")) + DPad();
            var dir = new Vector2Int(Mathf.Abs(raw.x) > 0.5f ? (int)Mathf.Sign(raw.x) : 0, Mathf.Abs(raw.y) > 0.5f ? (int)Mathf.Sign(raw.y) : 0);
            if (dir.x != 0 && dir.y != 0) dir.x = 0; // one axis at a time
            if (dir == Vector2Int.zero) { lastNav = dir; return dir; }
            if (dir != lastNav) { lastNav = dir; navRepeat = 0.38f; return dir; }
            navRepeat -= unscaledDt;
            if (navRepeat > 0f) return Vector2Int.zero;
            navRepeat = 0.11f;
            return dir;
        }

        /// <summary>Number keys 1-9 (for picking upgrade cards).</summary>
        public static int NumberKey()
        {
            for (int i = 1; i <= 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha0 + i) || Input.GetKeyDown(KeyCode.Keypad0 + i)) return i;
            return 0;
        }
    }

    /// <summary>Player preferences, saved with PlayerPrefs (IndexedDB on WebGL).</summary>
    public static class Settings
    {
        public static float Music { get => PlayerPrefs.GetFloat("rp.music", 0.6f); set { PlayerPrefs.SetFloat("rp.music", value); Sound.ApplyVolumes(); } }
        public static float Sfx { get => PlayerPrefs.GetFloat("rp.sfx", 0.8f); set { PlayerPrefs.SetFloat("rp.sfx", value); Sound.ApplyVolumes(); } }
        public static float ShakeAmount { get => PlayerPrefs.GetFloat("rp.shake", 1f); set => PlayerPrefs.SetFloat("rp.shake", value); }
        /// <summary>Roll with the roll button (on) or by bumping into obstacles (off, the 2.0 controls).</summary>
        public static bool RollButton { get => PlayerPrefs.GetInt("rp.rollbutton", 1) == 1; set { PlayerPrefs.SetInt("rp.rollbutton", value ? 1 : 0); DiceController.ButtonMode = value; } }
        public static bool ShowTutorial { get => PlayerPrefs.GetInt("rp.tutorial", 1) == 1; set => PlayerPrefs.SetInt("rp.tutorial", value ? 1 : 0); }
        public static int BestScore { get => PlayerPrefs.GetInt("rp.best", 0); set => PlayerPrefs.SetInt("rp.best", value); }
        public static int BestWave { get => PlayerPrefs.GetInt("rp.bestWave", 0); set => PlayerPrefs.SetInt("rp.bestWave", value); }

        public static void Save() => PlayerPrefs.Save();
    }
}
