using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ZombieEstate
{
    struct AiPlayerState
    {
        public int charMenuTimer;
        public int shopTimer;
        public int interWaveTimer;
        public int shopPhase;
        public string pendingEquipGun;

        public bool followP1;
        public Vector3 holdPosition;
        public bool holdPositionSet;

        public Vector3 lastPos;
        public int stuckFrames;
        public int unstuckBias;

        public bool moveForward;
        public bool moveBack;
        public bool moveLeft;
        public bool moveRight;
        public bool firePressed;
        public bool fireHeld;
        public bool aiming;
        public bool startPressed;
        public bool aPressed;
        public bool bPressed;
        public bool changeWepPressed;
        public bool spawnWavePressed;
        public bool dpadUp;
        public bool dpadDown;
        public bool dpadLeft;
        public bool dpadRight;
        public bool reloadHeld;
        public bool reloadPressed;
        public float aimAngle;

        public bool prevStart;
        public bool prevA;
        public bool prevB;
        public bool prevChangeWep;
        public bool prevSpawn;
        public bool prevDpadUp;
        public bool prevDpadDown;
        public bool prevDpadLeft;
        public bool prevDpadRight;
        public bool prevReload;
        public string pendingSellGun;

        public int retreatTimer;
    }

    /// <summary>
    /// AI teammates for couch co-op players 2–4 (PlayerIndex.One through Three).
    /// Injected into ZombieEstate.exe by scripts/patch-ai-player2.cs.
    /// </summary>
    public static class AiTeammate
    {
        const int FirstAiIndex = 1;
        const int LastAiIndex = 3;
        const int AiSlotCount = 3;
        const float FollowDistance = 4.5f;
        const float MaxCombatRangeFromP1 = 22f;
        const float MaxShootRange = 18f;
        const float FleeRadius = 6f;
        const float CriticalPersonalSpace = 1.8f;
        const int RetreatTriggerCount = 5;
        const int RetreatDurationFrames = 120;
        const float HuntMoveRange = 5f;
        const float MinEngageSpacing = 1.2f;

        static string[] GunNames;
        static int[] GunPrices;
        static string[] SecondaryGunNames;
        static int[] SecondaryGunPrices;

        const int ShopNone = 0;
        const int ShopGoToStore = 1;
        const int ShopOpenStore = 2;
        const int ShopBrowse = 3;
        const int ShopCloseStore = 4;
        const int ShopEquipOpen = 5;
        const int ShopEquipPick = 6;
        const int ShopEquipSlot = 7;
        const int ShopReady = 8;
        const int ShopGoToSecondary = 9;
        const int ShopOpenSecondary = 10;
        const int ShopSellOpen = 11;
        const int ShopSellPick = 12;
        const int ShopSellHold = 13;
        const int ShopRetryBuy = 14;
        const int OwnedGunLimit = 8;
        const int SellHoldFrames = 30;

        static bool? enabledSetting;
        static bool? keyboardP2Setting;
        static int frameCounter;
        static bool prevP1A;
        static bool remotePrevFire;
        static GamePadState[] prevGamePadStates;
        static bool wasStatsShowing;
        static AiPlayerState[] states;
        static AiPlayerState remoteP2State;
        static FieldInfo waveReadyField;
        static bool initialized;

        static void EnsureStatesInitialized()
        {
            if (initialized)
                return;

            states = new AiPlayerState[AiSlotCount];

            GunNames = new string[7];
            GunNames[0] = "Uzi";
            GunNames[1] = "Shotgun";
            GunNames[2] = "Light Assault Rifle";
            GunNames[3] = "Assault Rifle";
            GunNames[4] = "RM80";
            GunNames[5] = "Rocket Launcher";
            GunNames[6] = "MiniGun";

            GunPrices = new int[7];
            GunPrices[0] = 150;
            GunPrices[1] = 250;
            GunPrices[2] = 300;
            GunPrices[3] = 500;
            GunPrices[4] = 900;
            GunPrices[5] = 2000;
            GunPrices[6] = 4500;

            SecondaryGunNames = new string[5];
            SecondaryGunNames[0] = "Bubble Launcher";
            SecondaryGunNames[1] = "Card Shuffler";
            SecondaryGunNames[2] = "Laser Rifle";
            SecondaryGunNames[3] = "Grenade Launcher";
            SecondaryGunNames[4] = "Holy Squirt Gun";

            SecondaryGunPrices = new int[5];
            SecondaryGunPrices[0] = 800;
            SecondaryGunPrices[1] = 1200;
            SecondaryGunPrices[2] = 1500;
            SecondaryGunPrices[3] = 1800;
            SecondaryGunPrices[4] = 2200;

            initialized = true;
        }

        public static bool IsEnabled {
            get {
                if (!enabledSetting.HasValue)
                    enabledSetting = Environment.GetEnvironmentVariable("ZOMBIE_ESTATE_AI_P2") == "1";
                return enabledSetting.Value;
            }
        }

        public static bool KeyboardP2Active()
        {
            return CoopInputSplitActive();
        }

        /// <summary>True when keyboard/mouse should not use native all-player paths (co-op split on).</summary>
        public static bool CoopInputSplitActive()
        {
            if (Environment.GetEnvironmentVariable("ZOMBIE_ESTATE_AI_P2") == "1")
                return false;

            string kbEnv = Environment.GetEnvironmentVariable("ZOMBIE_ESTATE_KEYBOARD_P2");
            if (kbEnv == "0")
                return false;
            if (kbEnv == "1")
                return true;

            if (ReadPrefsFlag("KEYBOARD_P2", false))
                return false;
            if (ReadPrefsFlag("KEYBOARD_P2", true))
                return true;

            // Default on when AI teammate is off (co-op / keyboard friend).
            return true;
        }

        public static bool KeyboardP2BlocksCompatInput(PlayerIndex index)
        {
            return KeyboardP2UseGamepadOnly(index);
        }

        public static bool KeyboardP2UseGamepadOnly(PlayerIndex index)
        {
            if (!CoopInputSplitActive())
                return false;
            int slot = (int)index;
            if (slot == 0)
                return true;
            // P2 with a real controller never reads keyboard/mouse.
            if (slot == 1 && P2GamepadConnected())
                return true;
            return false;
        }

        /// <summary>P1 = controller 1; P2 = controller 2 when connected; keyboard/mouse = P3 or P2 fallback.</summary>
        static int KeyboardHumanPlayerIndex()
        {
            return (int)ResolveKeyboardHumanPlayerIndex();
        }

        static PlayerIndex ResolveKeyboardHumanPlayerIndex()
        {
            if (!P2GamepadConnected())
            {
                if (Global.PlayerTwo != null)
                    return PlayerIndex.Two;
            }
            else
            {
                if (Global.PlayerThree != null)
                    return PlayerIndex.Three;
                if (Global.PlayerTwo != null)
                    return PlayerIndex.Two;
            }

            for (int idx = 1; idx <= 3; idx++)
            {
                if (IsHumanCoopPlayer((PlayerIndex)idx))
                    return (PlayerIndex)idx;
            }

            return PlayerIndex.Two;
        }

        /// <summary>Which non-P1 player the dual-screen window should follow.</summary>
        public static Player GetDualScreenFollowPlayer()
        {
            if (!CoopInputSplitActive())
                return null;

            int kbIdx = KeyboardHumanPlayerIndex();
            Player keyboardPlayer = PlayerForHumanIndex(kbIdx);
            if (keyboardPlayer != null && !keyboardPlayer.Dead)
                return keyboardPlayer;

            if (Global.PlayerTwo != null && !Global.PlayerTwo.Dead)
                return Global.PlayerTwo;
            if (Global.PlayerThree != null && !Global.PlayerThree.Dead)
                return Global.PlayerThree;
            if (Global.PlayerFour != null && !Global.PlayerFour.Dead)
                return Global.PlayerFour;
            return null;
        }

        static Player PlayerForHumanIndex(int index)
        {
            return GetLoggedInPlayer((PlayerIndex)index);
        }

        /// <summary>Logged-in player for a slot (P1–P4), or null if that slot did not join.</summary>
        public static Player GetLoggedInPlayer(PlayerIndex index)
        {
            switch (index)
            {
            case PlayerIndex.One: return Global.PlayerOne;
            case PlayerIndex.Two: return Global.PlayerTwo;
            case PlayerIndex.Three: return Global.PlayerThree;
            case PlayerIndex.Four: return Global.PlayerFour;
            default: return null;
            }
        }

        /// <summary>True for logged-in human players (not AI-controlled slots).</summary>
        public static bool IsHumanCoopPlayer(PlayerIndex index)
        {
            if (IsEnabled)
                return index == PlayerIndex.One && Global.PlayerOne != null;

            return GetLoggedInPlayer(index) != null;
        }

        static bool IsKeyboardHumanPlayer(PlayerIndex index)
        {
            return CoopInputSplitActive() && index == ResolveKeyboardHumanPlayerIndex();
        }

        static string GetGameDirectory()
        {
            try
            {
                string loc = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(loc))
                {
                    string dir = Path.GetDirectoryName(loc);
                    if (!string.IsNullOrEmpty(dir))
                        return dir;
                }
            }
            catch
            {
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        static bool ReadPrefsFlag(string key, bool valueWhenTrue)
        {
            try
            {
                string prefs = Path.Combine(GetGameDirectory(), "zombie-estate.prefs");
                if (!File.Exists(prefs))
                    return false;
                string needle = key + "=" + (valueWhenTrue ? "1" : "0");
                foreach (string line in File.ReadAllLines(prefs))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("#") || trimmed.Length == 0)
                        continue;
                    if (trimmed == needle)
                        return true;
                }
            }
            catch
            {
            }
            return false;
        }

        static void EnsureKeyboardP2Setting()
        {
            if (keyboardP2Setting.HasValue)
                return;

            string env = Environment.GetEnvironmentVariable("ZOMBIE_ESTATE_KEYBOARD_P2");
            if (env == "1")
            {
                keyboardP2Setting = true;
                return;
            }
            if (env == "0")
            {
                keyboardP2Setting = false;
                return;
            }

            if (Environment.GetEnvironmentVariable("ZOMBIE_ESTATE_REMOTE_P2") == "1")
            {
                keyboardP2Setting = true;
                return;
            }

            if (ReadPrefsFlag("KEYBOARD_P2", true))
            {
                keyboardP2Setting = true;
                return;
            }
            if (ReadPrefsFlag("KEYBOARD_P2", false))
            {
                keyboardP2Setting = false;
                return;
            }

            // Co-op: P1 controller; P2 controller when present; keyboard/mouse on P3 (or P2 if no pad 2).
            keyboardP2Setting = Environment.GetEnvironmentVariable("ZOMBIE_ESTATE_AI_P2") != "1";
        }

        public static bool IsKeyboardP2Enabled {
            get {
                EnsureKeyboardP2Setting();
                return keyboardP2Setting.Value;
            }
        }

        static bool Enabled { get { return IsEnabled; } }

        static bool P2GamepadConnected()
        {
            GamePadState pad2 = NativeGamePad(PlayerIndex.Two);
            if (!pad2.IsConnected)
                return false;

            GamePadState pad1 = NativeGamePad(PlayerIndex.One);
            if (!pad1.IsConnected)
                return true;

            // One physical pad can appear on both PlayerIndex slots on some platforms.
            return !GamepadsMirrorEachOther(pad1, pad2);
        }

        static bool GamepadsMirrorEachOther(GamePadState a, GamePadState b)
        {
            return a.Buttons == b.Buttons
                && a.DPad == b.DPad
                && Math.Abs(a.ThumbSticks.Left.X - b.ThumbSticks.Left.X) < 0.001f
                && Math.Abs(a.ThumbSticks.Left.Y - b.ThumbSticks.Left.Y) < 0.001f
                && Math.Abs(a.ThumbSticks.Right.X - b.ThumbSticks.Right.X) < 0.001f
                && Math.Abs(a.ThumbSticks.Right.Y - b.ThumbSticks.Right.Y) < 0.001f
                && Math.Abs(a.Triggers.Left - b.Triggers.Left) < 0.001f
                && Math.Abs(a.Triggers.Right - b.Triggers.Right) < 0.001f;
        }

        static object nativeGamePadDelegate;
        static MethodInfo nativeGamePadInvoke;

        /// <summary>Physical controller only — bypasses FNA keyboard→P1 gamepad merge.</summary>
        static GamePadState NativeGamePad(PlayerIndex index)
        {
            if (nativeGamePadDelegate == null)
            {
                Type platform = typeof(GamePad).Assembly.GetType("Microsoft.Xna.Framework.FNAPlatform");
                FieldInfo field = platform?.GetField(
                    "GetGamePadState",
                    BindingFlags.Public | BindingFlags.Static);
                nativeGamePadDelegate = field?.GetValue(null);
                if (nativeGamePadDelegate != null)
                    nativeGamePadInvoke = nativeGamePadDelegate.GetType().GetMethod("Invoke");
            }

            if (nativeGamePadInvoke != null)
            {
                return (GamePadState)nativeGamePadInvoke.Invoke(
                    nativeGamePadDelegate,
                    new object[] { (int)index, GamePadDeadZone.IndependentAxes });
            }

            return GamePad.GetState(index);
        }

        public static bool QueryOverridesGamepad(PlayerIndex index)
        {
            return IsKeyboardHumanPlayer(index) || IsAiPlayer(index);
        }

        const float StickThreshold = 0.2f;
        const float TriggerThreshold = 0.02f;

        static GamePadState PrevGamePad(PlayerIndex index)
        {
            if (prevGamePadStates == null)
                prevGamePadStates = new GamePadState[4];
            int slot = (int)index;
            if (slot < 0 || slot >= prevGamePadStates.Length)
                return new GamePadState();
            return prevGamePadStates[slot];
        }

        static void SnapshotGamePadStates()
        {
            if (prevGamePadStates == null)
                prevGamePadStates = new GamePadState[4];
            for (int i = 0; i < prevGamePadStates.Length; i++)
                prevGamePadStates[i] = NativeGamePad((PlayerIndex)i);
        }

        static bool GamepadButtonPressed(ButtonState current, ButtonState previous)
        {
            return current == ButtonState.Pressed && previous != ButtonState.Pressed;
        }

        static bool GamepadDpadPressed(ButtonState current, ButtonState previous)
        {
            return current == ButtonState.Pressed && previous != ButtonState.Pressed;
        }

        public static bool GamepadLeftStickForward(PlayerIndex index)
        {
            return NativeGamePad(index).ThumbSticks.Left.Y > StickThreshold;
        }

        public static bool GamepadLeftStickBack(PlayerIndex index)
        {
            return NativeGamePad(index).ThumbSticks.Left.Y < -StickThreshold;
        }

        public static bool GamepadLeftStickLeft(PlayerIndex index)
        {
            return NativeGamePad(index).ThumbSticks.Left.X < -StickThreshold;
        }

        public static bool GamepadLeftStickRight(PlayerIndex index)
        {
            return NativeGamePad(index).ThumbSticks.Left.X > StickThreshold;
        }

        public static bool GamepadFirePressed(PlayerIndex index, InputManager input)
        {
            GamePadState pad = NativeGamePad(index);
            if (pad.Triggers.Right < TriggerThreshold)
                return false;
            GamePadState prev = PrevGamePad(index);
            return prev.Triggers.Right < TriggerThreshold;
        }

        public static bool GamepadFireHeld(PlayerIndex index)
        {
            return NativeGamePad(index).Triggers.Right >= TriggerThreshold;
        }

        public static bool GamepadAiming(PlayerIndex index)
        {
            Vector2 stick = NativeGamePad(index).ThumbSticks.Right;
            return stick.LengthSquared() > 0.0001f;
        }

        public static float GamepadAimAngle(PlayerIndex index)
        {
            Vector2 stick = NativeGamePad(index).ThumbSticks.Right;
            if (stick.LengthSquared() <= 0.0001f)
                return 0f;
            return (float)Math.Atan2(stick.Y, stick.X);
        }

        public static bool GamepadStartPressed(PlayerIndex index, InputManager input)
        {
            GamePadState pad = NativeGamePad(index);
            GamePadState prev = PrevGamePad(index);
            return GamepadButtonPressed(pad.Buttons.Start, prev.Buttons.Start);
        }

        public static bool GamepadAPressed(PlayerIndex index, InputManager input)
        {
            GamePadState pad = NativeGamePad(index);
            GamePadState prev = PrevGamePad(index);
            return GamepadButtonPressed(pad.Buttons.A, prev.Buttons.A);
        }

        public static bool GamepadBPressed(PlayerIndex index, InputManager input)
        {
            GamePadState pad = NativeGamePad(index);
            GamePadState prev = PrevGamePad(index);
            return GamepadButtonPressed(pad.Buttons.B, prev.Buttons.B);
        }

        public static bool GamepadChangeWepPressed(PlayerIndex index, InputManager input)
        {
            GamePadState pad = NativeGamePad(index);
            GamePadState prev = PrevGamePad(index);
            return GamepadButtonPressed(pad.Buttons.Y, prev.Buttons.Y);
        }

        public static bool GamepadSpawnWavePressed(PlayerIndex index, InputManager input)
        {
            GamePadState pad = NativeGamePad(index);
            GamePadState prev = PrevGamePad(index);
            return GamepadButtonPressed(pad.Buttons.Back, prev.Buttons.Back);
        }

        public static bool GamepadDPadUp(PlayerIndex index, InputManager input)
        {
            GamePadState pad = NativeGamePad(index);
            GamePadState prev = PrevGamePad(index);
            return GamepadDpadPressed(pad.DPad.Up, prev.DPad.Up);
        }

        public static bool GamepadDPadDown(PlayerIndex index, InputManager input)
        {
            GamePadState pad = NativeGamePad(index);
            GamePadState prev = PrevGamePad(index);
            return GamepadDpadPressed(pad.DPad.Down, prev.DPad.Down);
        }

        public static bool GamepadDPadLeft(PlayerIndex index, InputManager input)
        {
            GamePadState pad = NativeGamePad(index);
            GamePadState prev = PrevGamePad(index);
            return GamepadDpadPressed(pad.DPad.Left, prev.DPad.Left);
        }

        public static bool GamepadDPadRight(PlayerIndex index, InputManager input)
        {
            GamePadState pad = NativeGamePad(index);
            GamePadState prev = PrevGamePad(index);
            return GamepadDpadPressed(pad.DPad.Right, prev.DPad.Right);
        }

        public static bool GamepadReloadHeld(PlayerIndex index)
        {
            return NativeGamePad(index).Buttons.X == ButtonState.Pressed;
        }

        public static bool GamepadReloadPressed(PlayerIndex index, InputManager input)
        {
            GamePadState pad = NativeGamePad(index);
            GamePadState prev = PrevGamePad(index);
            return GamepadButtonPressed(pad.Buttons.X, prev.Buttons.X);
        }

        static void ClearRemoteKeyboardState()
        {
            remoteP2State.moveForward = false;
            remoteP2State.moveBack = false;
            remoteP2State.moveLeft = false;
            remoteP2State.moveRight = false;
            remoteP2State.firePressed = false;
            remoteP2State.fireHeld = false;
            remoteP2State.aiming = false;
            remoteP2State.startPressed = false;
            remoteP2State.aPressed = false;
            remoteP2State.bPressed = false;
            remoteP2State.spawnWavePressed = false;
            remoteP2State.reloadHeld = false;
            remoteP2State.reloadPressed = false;
            remoteP2State.changeWepPressed = false;
            remoteP2State.dpadUp = false;
            remoteP2State.dpadDown = false;
            remoteP2State.dpadLeft = false;
            remoteP2State.dpadRight = false;
            remotePrevFire = false;
        }

        static void UpdateKeyboardP2Input()
        {
            if (!CoopInputSplitActive())
                return;

            PlayerIndex kbPlayer = ResolveKeyboardHumanPlayerIndex();

            KeyboardState kb = Keyboard.GetState();
            remoteP2State.moveForward = kb.IsKeyDown(Keys.W);
            remoteP2State.moveBack = kb.IsKeyDown(Keys.S);
            remoteP2State.moveLeft = kb.IsKeyDown(Keys.A);
            remoteP2State.moveRight = kb.IsKeyDown(Keys.D);

            // Native PC keyboard layout (see InputManager disassembly).
            remoteP2State.startPressed = kb.IsKeyDown(Keys.M) || kb.IsKeyDown(Keys.Enter);
            remoteP2State.aPressed = kb.IsKeyDown(Keys.G);
            remoteP2State.bPressed = kb.IsKeyDown(Keys.H);
            remoteP2State.spawnWavePressed = kb.IsKeyDown(Keys.P);
            remoteP2State.reloadHeld = kb.IsKeyDown(Keys.R);
            remoteP2State.reloadPressed = kb.IsKeyDown(Keys.R);
            remoteP2State.changeWepPressed = kb.IsKeyDown(Keys.Q);
            remoteP2State.dpadUp = kb.IsKeyDown(Keys.Up);
            remoteP2State.dpadDown = kb.IsKeyDown(Keys.Down);
            remoteP2State.dpadLeft = kb.IsKeyDown(Keys.Left);
            remoteP2State.dpadRight = kb.IsKeyDown(Keys.Right);

            bool mouseAllowed = !DualScreen.IsEnabled || !DualScreen.WindowReady
                || DualScreen.IsPlayerWindowFocused(kbPlayer);

            if (!mouseAllowed)
            {
                remoteP2State.fireHeld = false;
                remoteP2State.firePressed = false;
                remoteP2State.aiming = false;
                remotePrevFire = false;
                return;
            }

            if (DualScreen.IsEnabled && DualScreen.WindowReady
                && DualScreen.TryGetPlayerWindowMouseInput(
                    kbPlayer,
                    out float aimDx,
                    out float aimDy,
                    out bool fireHeld,
                    out bool inPlayerWindow))
            {
                if (inPlayerWindow)
                {
                    remoteP2State.fireHeld = fireHeld;
                    remoteP2State.firePressed = fireHeld && !remotePrevFire;
                    remotePrevFire = fireHeld;

                    float aimDistSq = aimDx * aimDx + aimDy * aimDy;
                    if (fireHeld || aimDistSq > 4f)
                    {
                        remoteP2State.aiming = true;
                        if (aimDistSq > 0.25f)
                            remoteP2State.aimAngle = (float)Math.Atan2(aimDy, aimDx);
                    }
                    else
                    {
                        remoteP2State.aiming = false;
                    }
                    return;
                }
            }

            bool fire = Mouse.GetState().LeftButton == ButtonState.Pressed;
            remoteP2State.fireHeld = fire;
            remoteP2State.firePressed = fire && !remotePrevFire;
            remotePrevFire = fire;

            MouseState mouse = Mouse.GetState();
            if (Global.GraphicsDevice != null)
            {
                var vp = Global.GraphicsDevice.Viewport;
                float cx = vp.X + vp.Width * 0.5f;
                float cy = vp.Y + vp.Height * 0.5f;
                float dx = mouse.X - cx;
                float dy = cy - mouse.Y;
                if (dx * dx + dy * dy > 64f)
                {
                    remoteP2State.aiming = true;
                    remoteP2State.aimAngle = (float)Math.Atan2(dy, dx);
                }
                else
                {
                    remoteP2State.aiming = false;
                }
            }
        }

        static int IndexToSlot(int playerIndex)
        {
            if (playerIndex < FirstAiIndex || playerIndex > LastAiIndex)
                return -1;
            return playerIndex - FirstAiIndex;
        }


        static Player GetPlayerForSlot(int slot)
        {
            switch (slot)
            {
            case 0: return Global.PlayerTwo;
            case 1: return Global.PlayerThree;
            case 2: return Global.PlayerFour;
            default: return null;
            }
        }

        static bool HasAnyAiPlayer()
        {
            return Global.PlayerTwo != null || Global.PlayerThree != null || Global.PlayerFour != null;
        }

        static Vector3 FollowOffset(int numberIndex)
        {
            switch (numberIndex)
            {
            case 1: return new Vector3(-2.5f, 0f, -1.5f);
            case 2: return new Vector3(2.5f, 0f, -1.5f);
            case 3: return new Vector3(0f, 0f, 2.5f);
            default: return Vector3.Zero;
            }
        }

        public static bool UseP1OnlyCamera()
        {
            if (DualScreen.IsEnabled)
                return true;
            return Enabled && HasAnyAiPlayer();
        }

        public static bool TryUpdateCameraP1Only()
        {
            if (!UseP1OnlyCamera())
                return false;

            Player p1 = Global.PlayerOne;
            if (p1 == null || p1.Dead)
                return false;

            Vector3 center = p1.Position;
            const float distance = 10f;
            const float smooth = 0.1f;
            float camHeight = 4.2f * 4f * distance / 10f;
            float camZOffset = 4.8f * 4f * distance / 10f;

            Vector3 lookAt = Global.CameraLookAt;
            lookAt.X = MathHelper.SmoothStep(lookAt.X, center.X, smooth);
            lookAt.Z = MathHelper.SmoothStep(lookAt.Z, center.Z + 0.4f, smooth);
            Global.CameraLookAt = lookAt;

            Vector3 camPos = Global.CameraPosition;
            camPos.X = MathHelper.SmoothStep(camPos.X, center.X, smooth);
            camPos.Y = MathHelper.SmoothStep(camPos.Y, camHeight, smooth);
            camPos.Z = MathHelper.SmoothStep(camPos.Z, center.Z - camZOffset, smooth);
            if (camPos.Z < -120f)
                camPos.Z = -120f;
            Global.CameraPosition = camPos;

            return true;
        }

        public static bool SkipCameraBounds(Player player)
        {
            if (player == null)
                return false;
            if (DualScreen.IsEnabled && player != DualScreen.GetMainWindowPlayer())
                return true;
            if (!Enabled)
                return false;
            int idx = (int)player.myIndex;
            return idx >= FirstAiIndex && idx <= LastAiIndex;
        }

        public static void Tick()
        {
            DualScreen.Tick();
            if (CoopInputSplitActive())
                UpdateKeyboardP2Input();

            if (!Enabled)
            {
                if (CoopInputSplitActive())
                    SnapshotGamePadStates();
                return;
            }

            EnsureStatesInitialized();
            frameCounter++;
            UpdateFollowCommand();

            if (Global.State == GameState.CHARACTERSELECTION)
            {
                UpdateAllCharacterSelect();
                return;
            }

            if (Global.State != GameState.INGAME)
                return;

            bool statsShowing = Global.MasterWave != null && Global.MasterWave.StatsShowing;
            if (statsShowing)
            {
                AcknowledgeStatsForAllAi();
                wasStatsShowing = true;
                return;
            }

            if (wasStatsShowing)
            {
                wasStatsShowing = false;
                ForceAllAiShopReady();
            }

            for (int slot = 0; slot < AiSlotCount; slot++)
            {
                Player player = GetPlayerForSlot(slot);
                if (player == null)
                    continue;

                if (player.Dead)
                {
                    if (BetweenWaves())
                        TryReadyUp(player, ref states[slot]);
                    continue;
                }

                ClearFrameInput(ref states[slot]);

                if (player.Frozen)
                    UpdateInterWave(player, ref states[slot], false);
                else
                    UpdateGameplay(player, ref states[slot]);
            }

            EnsureAllAiReady();
            if (CoopInputSplitActive())
                SnapshotGamePadStates();
        }

        static void ForceAllAiShopReady()
        {
            for (int slot = 0; slot < AiSlotCount; slot++)
            {
                states[slot].shopPhase = ShopReady;
                states[slot].shopTimer = 0;
            }
        }

        static void UpdateFollowCommand()
        {
            if (Global.State != GameState.INGAME)
                return;
            if (Global.MasterWave == null || !Global.MasterWave.InWave)
                return;

            GamePadState pad = NativeGamePad((PlayerIndex)0);
            if (pad.IsConnected)
            {
                bool aNow = pad.Buttons.A == ButtonState.Pressed;
                if (aNow && !prevP1A)
                {
                    for (int slot = 0; slot < AiSlotCount; slot++)
                        states[slot].followP1 = !states[slot].followP1;
                }
                prevP1A = aNow;
            }
        }

        static void ClearFrameInput(ref AiPlayerState state)
        {
            state.moveForward = state.moveBack = state.moveLeft = state.moveRight = false;
            state.firePressed = state.fireHeld = false;
            state.aiming = false;
            state.startPressed = state.aPressed = state.bPressed = false;
            state.changeWepPressed = state.spawnWavePressed = false;
            state.dpadUp = state.dpadDown = state.dpadLeft = state.dpadRight = false;
            state.reloadHeld = false;
        }

        static void UpdateAllCharacterSelect()
        {
            for (int slot = 0; slot < AiSlotCount; slot++)
            {
                Player player = GetPlayerForSlot(slot);
                ref AiPlayerState state = ref states[slot];
                state.charMenuTimer++;

                if (player == null)
                {
                    if (slot > 0 && GetPlayerForSlot(slot - 1) == null)
                        continue;

                    if (state.charMenuTimer > 45 && state.charMenuTimer % 30 == 0)
                        state.startPressed = true;
                    continue;
                }

                if (state.charMenuTimer > 30 && state.charMenuTimer % 25 == 0)
                    state.aPressed = true;
            }
        }

        static void UpdateInterWave(Player player, ref AiPlayerState state, bool allowMovement)
        {
            if (BetweenWaves())
                state.interWaveTimer++;
            else
                state.interWaveTimer = 0;

            if (state.interWaveTimer > 300)
            {
                state.shopPhase = ShopReady;
                TryReadyUp(player, ref state);
                return;
            }

            ItemScreen primaryScreen = GetPlayerStoreScreen(player, FindPrimaryStore());
            bool primaryOpen = primaryScreen != null && primaryScreen.Active && primaryScreen.Store;

            ItemScreen secondaryScreen = GetPlayerStoreScreen(player, FindSecondaryStore());
            bool secondaryOpen = secondaryScreen != null && secondaryScreen.Active && secondaryScreen.Store;

            ItemScreen inv = player.inventory;
            bool invOpen = inv != null && inv.Active && !inv.Store;

            if (primaryOpen)
            {
                state.shopPhase = ShopBrowse;
                state.shopTimer++;
                UpdateStoreShopping(player, ref state, primaryScreen, false);
                return;
            }

            if (secondaryOpen)
            {
                state.shopPhase = ShopBrowse;
                state.shopTimer++;
                UpdateStoreShopping(player, ref state, secondaryScreen, true);
                return;
            }

            if (invOpen)
            {
                state.shopTimer++;
                if (state.shopPhase == ShopSellPick || state.shopPhase == ShopSellHold)
                    UpdateInventorySell(player, ref state, inv);
                else
                    UpdateInventoryEquip(player, ref state, inv);
                return;
            }

            if (state.shopPhase == ShopSellHold)
            {
                state.shopPhase = ShopSellOpen;
                state.shopTimer = 0;
            }

            if (state.shopPhase == ShopRetryBuy)
            {
                state.shopTimer++;
                if (state.shopTimer > 10)
                {
                    string target = state.pendingEquipGun;
                    bool secondary = !string.IsNullOrEmpty(target) && IsSecondaryGunName(target);
                    state.shopPhase = secondary ? ShopGoToSecondary : ShopGoToStore;
                    state.shopTimer = 0;
                }
                TryReadyUp(player, ref state);
                return;
            }

            TryReadyUp(player, ref state);

            if (state.shopPhase == ShopCloseStore || state.shopPhase == ShopBrowse)
            {
                state.shopPhase = ShopEquipOpen;
                state.shopTimer = 0;
            }

            if (state.shopPhase == ShopEquipOpen)
            {
                state.shopTimer++;
                if (state.shopTimer > 15)
                {
                    if (string.IsNullOrEmpty(state.pendingEquipGun))
                        state.pendingEquipGun = GetBestOwnedGunName(player);
                    if (string.IsNullOrEmpty(state.pendingEquipGun))
                    {
                        state.shopPhase = ShopReady;
                        state.shopTimer = 0;
                    }
                    else
                    {
                        state.changeWepPressed = true;
                        state.shopPhase = ShopEquipPick;
                        state.shopTimer = 0;
                    }
                }
                TryReadyUp(player, ref state);
                return;
            }

            if (state.shopPhase == ShopSellOpen)
            {
                state.shopTimer++;
                if (state.shopTimer > 15)
                {
                    state.pendingSellGun = GetWorstOwnedGunName(player);
                    if (string.IsNullOrEmpty(state.pendingSellGun))
                    {
                        state.shopPhase = ShopReady;
                        state.shopTimer = 0;
                    }
                    else
                    {
                        state.changeWepPressed = true;
                        state.shopPhase = ShopSellPick;
                        state.shopTimer = 0;
                    }
                }
                TryReadyUp(player, ref state);
                return;
            }

            if (allowMovement && state.shopPhase == ShopOpenStore)
            {
                state.shopTimer++;
                GameObject store = FindPrimaryStore();
                if (store != null && Collides(store, player) && state.shopTimer % 15 == 0)
                    state.aPressed = true;
                TryReadyUp(player, ref state);
                return;
            }

            if (allowMovement && state.shopPhase == ShopGoToStore)
            {
                state.shopTimer++;
                GameObject store = FindPrimaryStore();
                Player p1 = Global.PlayerOne;
                if (store == null || p1 == null || DistanceXZ(player.Position, store.Position) > 18f)
                {
                    state.shopPhase = ShopGoToSecondary;
                    state.shopTimer = 0;
                    TryReadyUp(player, ref state);
                    return;
                }

                SmartMove(player, ref state, store.Position, 1.5f);
                if (Collides(store, player))
                {
                    state.shopPhase = ShopOpenStore;
                    state.shopTimer = 0;
                }
                else if (state.shopTimer > 120)
                {
                    state.shopPhase = ShopGoToSecondary;
                    state.shopTimer = 0;
                }
                TryReadyUp(player, ref state);
                return;
            }

            if (allowMovement && state.shopPhase == ShopOpenSecondary)
            {
                state.shopTimer++;
                GameObject store = FindSecondaryStore();
                if (store != null && Collides(store, player) && state.shopTimer % 15 == 0)
                    state.aPressed = true;
                TryReadyUp(player, ref state);
                return;
            }

            if (allowMovement && state.shopPhase == ShopGoToSecondary)
            {
                state.shopTimer++;
                GameObject store = FindSecondaryStore();
                if (store == null)
                {
                    state.shopPhase = ShopReady;
                    state.shopTimer = 0;
                    TryReadyUp(player, ref state);
                    return;
                }

                SmartMove(player, ref state, store.Position, 1.5f);
                if (Collides(store, player))
                {
                    state.shopPhase = ShopOpenSecondary;
                    state.shopTimer = 0;
                }
                else if (state.shopTimer > 120)
                {
                    state.shopPhase = ShopReady;
                    state.shopTimer = 0;
                }
                TryReadyUp(player, ref state);
                return;
            }

            TryReadyUp(player, ref state);
        }

        static FieldInfo GetWaveReadyField()
        {
            if (waveReadyField == null)
                waveReadyField = typeof(WaveMaster).GetField("Ready", BindingFlags.Instance | BindingFlags.NonPublic);
            return waveReadyField;
        }

        static void AcknowledgeStatsForAllAi()
        {
            if (Global.MasterWave == null || !Global.MasterWave.StatsShowing)
                return;

            FieldInfo readyField = GetWaveReadyField();
            bool[] ready = readyField != null
                ? readyField.GetValue(Global.MasterWave) as bool[]
                : null;

            for (int slot = 0; slot < AiSlotCount; slot++)
            {
                Player player = GetPlayerForSlot(slot);
                if (player == null)
                    continue;

                if (ready != null && player.numberIndex >= 0 && player.numberIndex < ready.Length)
                    ready[player.numberIndex] = true;

                if (frameCounter % 18 == slot * 3)
                    states[slot].aPressed = true;
            }
        }

        static bool IsStoreOrInventoryOpen(Player player)
        {
            ItemScreen primary = GetPlayerStoreScreen(player, FindPrimaryStore());
            if (primary != null && primary.Active && primary.Store)
                return true;

            ItemScreen secondary = GetPlayerStoreScreen(player, FindSecondaryStore());
            if (secondary != null && secondary.Active && secondary.Store)
                return true;

            ItemScreen inv = player.inventory;
            return inv != null && inv.Active && !inv.Store;
        }

        static void TryReadyUp(Player player, ref AiPlayerState state)
        {
            if (!BetweenWaves())
                return;

            if (Global.MasterWave != null && Global.MasterWave.StatsShowing)
                return;

            if (state.shopPhase != ShopReady && IsStoreOrInventoryOpen(player))
                return;

            if (state.shopPhase == ShopEquipOpen
                || state.shopPhase == ShopEquipPick
                || state.shopPhase == ShopEquipSlot
                || state.shopPhase == ShopSellOpen
                || state.shopPhase == ShopSellPick
                || state.shopPhase == ShopSellHold
                || state.shopPhase == ShopRetryBuy)
                return;

            state.shopPhase = ShopReady;
            player.ReadyForWave = true;
        }

        static void EnsureAllAiReady()
        {
            if (Global.MasterWave == null || !BetweenWaves())
                return;

            if (Global.MasterWave.StatsShowing)
                return;

            for (int slot = 0; slot < AiSlotCount; slot++)
            {
                Player player = GetPlayerForSlot(slot);
                if (player == null)
                    continue;

                player.ReadyForWave = true;
                if (states[slot].shopPhase != ShopGoToStore
                    && states[slot].shopPhase != ShopOpenStore
                    && states[slot].shopPhase != ShopGoToSecondary
                    && states[slot].shopPhase != ShopOpenSecondary
                    && states[slot].shopPhase != ShopBrowse
                    && states[slot].shopPhase != ShopEquipOpen
                    && states[slot].shopPhase != ShopEquipPick
                    && states[slot].shopPhase != ShopEquipSlot
                    && states[slot].shopPhase != ShopSellOpen
                    && states[slot].shopPhase != ShopSellPick
                    && states[slot].shopPhase != ShopSellHold
                    && states[slot].shopPhase != ShopRetryBuy)
                {
                    states[slot].shopPhase = ShopReady;
                }

                if (states[slot].interWaveTimer > 300)
                    states[slot].shopPhase = ShopReady;
            }
        }

        static void UpdateGameplay(Player player, ref AiPlayerState state)
        {
            if (BetweenWaves())
            {
                if (state.shopPhase == ShopNone)
                {
                    state.shopPhase = ShopGoToStore;
                    state.shopTimer = 0;
                    state.interWaveTimer = 0;
                }
                UpdateInterWave(player, ref state, true);
                return;
            }

            state.shopPhase = ShopNone;
            state.shopTimer = 0;
            state.interWaveTimer = 0;

            Player p1 = Global.PlayerOne;
            CombatAndFollow(player, ref state, p1);
        }

        static void CombatAndFollow(Player player, ref AiPlayerState state, Player p1)
        {
            float nearestThreat = NearestEnemyDistance(player.Position);
            int nearbyCount = CountNearbyEnemies(player.Position, FleeRadius);

            if (state.retreatTimer > 0)
                state.retreatTimer--;

            bool overwhelmed = nearbyCount >= RetreatTriggerCount
                || (nearestThreat < CriticalPersonalSpace && nearbyCount >= 2);

            if (overwhelmed && state.retreatTimer <= 0)
                state.retreatTimer = RetreatDurationFrames;

            if (state.retreatTimer > 0 && nearbyCount <= 1 && nearestThreat > FleeRadius + 1f)
                state.retreatTimer = 0;

            GameObject target = FindBestTarget(player.Position, p1);

            if (state.retreatTimer > 0)
            {
                TacticalRetreat(player, ref state, p1, target);
                return;
            }

            if (target != null)
            {
                HuntTarget(player, ref state, p1, target);
                return;
            }

            state.holdPositionSet = false;
            if (state.followP1 && p1 != null && !p1.Dead)
            {
                TightFollow(player, ref state, p1);
                return;
            }

            if (!state.holdPositionSet)
            {
                state.holdPosition = player.Position;
                state.holdPositionSet = true;
            }
        }

        static void HuntTarget(Player player, ref AiPlayerState state, Player p1, GameObject target)
        {
            state.holdPositionSet = false;
            float dist = DistanceXZ(player.Position, target.Position);

            if (dist > HuntMoveRange)
                SmartMove(player, ref state, target.Position, HuntMoveRange - 1f);
            else if (dist < MinEngageSpacing)
                MaintainSpacing(player, ref state, target);

            AimAndShoot(player, ref state, target.Position);
        }

        static void MaintainSpacing(Player player, ref AiPlayerState state, GameObject threat)
        {
            float dx = player.Position.X - threat.Position.X;
            float dz = player.Position.Z - threat.Position.Z;
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 0.01f)
                return;

            Vector3 goal = player.Position;
            goal.X += (dx / len) * 2f;
            goal.Z += (dz / len) * 2f;
            SmartMove(player, ref state, goal, 0.4f);
        }

        static void AimAndShoot(Player player, ref AiPlayerState state, Vector3 aimPoint)
        {
            state.aimAngle = AngleTo(player.Position, aimPoint);
            state.aiming = true;
            state.fireHeld = true;
            if (frameCounter % 4 == 0)
                state.firePressed = true;
        }

        static void TacticalRetreat(Player player, ref AiPlayerState state, Player p1, GameObject target)
        {
            state.holdPositionSet = false;
            Vector3 fleeFrom = FindThreatCentroid(player.Position, FleeRadius + 2f);
            Vector3 goal = player.Position;

            if (DistanceXZ(fleeFrom, player.Position) > 0.01f)
            {
                float dx = player.Position.X - fleeFrom.X;
                float dz = player.Position.Z - fleeFrom.Z;
                float len = (float)Math.Sqrt(dx * dx + dz * dz);
                if (len > 0.01f)
                {
                    goal.X += (dx / len) * 4f;
                    goal.Z += (dz / len) * 4f;
                }
            }

            if (p1 != null && !p1.Dead && DistanceXZ(player.Position, p1.Position) > MaxCombatRangeFromP1)
            {
                Vector3 towardP1 = GetFollowPosition(p1, player);
                goal.X = MathHelper.Lerp(goal.X, towardP1.X, 0.35f);
                goal.Z = MathHelper.Lerp(goal.Z, towardP1.Z, 0.35f);
            }

            SmartMove(player, ref state, goal, 1f);

            GameObject shootAt = target ?? FindNearestEnemy(player.Position, MaxShootRange);
            if (shootAt != null)
                AimAndShoot(player, ref state, shootAt.Position);
        }

        static GameObject FindNearestEnemy(Vector3 from, float maxRange)
        {
            if (Global.MasterCache == null || Global.MasterCache.gameObjects == null)
                return null;

            GameObject best = null;
            float bestDist = maxRange;
            foreach (GameObject obj in Global.MasterCache.gameObjects)
            {
                if (obj == null || obj.Dead || !obj.Active || !obj.Enemy)
                    continue;
                float dist = DistanceXZ(from, obj.Position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = obj;
                }
            }
            return best;
        }

        static Vector3 FindThreatCentroid(Vector3 from, float radius)
        {
            if (Global.MasterCache == null || Global.MasterCache.gameObjects == null)
                return from;

            float sumX = 0f;
            float sumZ = 0f;
            int count = 0;

            foreach (GameObject obj in Global.MasterCache.gameObjects)
            {
                if (obj == null || obj.Dead || !obj.Active || !obj.Enemy)
                    continue;
                float dist = DistanceXZ(from, obj.Position);
                if (dist > radius)
                    continue;
                sumX += obj.Position.X;
                sumZ += obj.Position.Z;
                count++;
            }

            if (count == 0)
                return from;

            return new Vector3(sumX / count, from.Y, sumZ / count);
        }

        static float NearestEnemyDistance(Vector3 from)
        {
            if (Global.MasterCache == null || Global.MasterCache.gameObjects == null)
                return float.MaxValue;

            float best = float.MaxValue;
            foreach (GameObject obj in Global.MasterCache.gameObjects)
            {
                if (obj == null || obj.Dead || !obj.Active || !obj.Enemy)
                    continue;
                float dist = DistanceXZ(from, obj.Position);
                if (dist < best)
                    best = dist;
            }
            return best;
        }

        static int CountNearbyEnemies(Vector3 from, float radius)
        {
            if (Global.MasterCache == null || Global.MasterCache.gameObjects == null)
                return 0;

            int count = 0;
            foreach (GameObject obj in Global.MasterCache.gameObjects)
            {
                if (obj == null || obj.Dead || !obj.Active || !obj.Enemy)
                    continue;
                if (DistanceXZ(from, obj.Position) <= radius)
                    count++;
            }
            return count;
        }

        static Vector3 GetFollowPosition(Player p1, Player follower)
        {
            Vector3 offset = FollowOffset(follower.numberIndex);
            return new Vector3(p1.Position.X + offset.X, p1.Position.Y, p1.Position.Z + offset.Z);
        }

        static void TightFollow(Player player, ref AiPlayerState state, Player p1)
        {
            state.holdPositionSet = false;
            Vector3 goal = GetFollowPosition(p1, player);
            float dist = DistanceXZ(player.Position, goal);
            if (dist > FollowDistance)
                SmartMove(player, ref state, goal, FollowDistance - 0.5f);

            GameObject target = FindBestTarget(player.Position, p1);
            if (target != null)
                AimAndShoot(player, ref state, target.Position);
            else
            {
                state.aimAngle = AngleTo(player.Position, p1.Position);
                state.aiming = true;
            }
        }

        static void EngageTarget(Player player, ref AiPlayerState state, Player p1, GameObject target)
        {
            HuntTarget(player, ref state, p1, target);
        }

        static void UpdateStoreShopping(Player player, ref AiPlayerState state, ItemScreen screen, bool secondaryShop)
        {
            state.shopTimer++;

            if (!string.IsNullOrEmpty(state.pendingEquipGun) && OwnsGun(player, state.pendingEquipGun))
            {
                if (state.shopTimer > 8 && state.shopTimer % 12 == 0)
                    state.bPressed = true;
                return;
            }

            string buyTarget = GetNextGunToBuy(player, secondaryShop);
            if (buyTarget == null)
            {
                if (state.shopTimer > 20 && state.shopTimer % 15 == 0)
                    state.bPressed = true;
                if (secondaryShop)
                    state.shopPhase = ShopReady;
                else
                    state.shopPhase = ShopGoToSecondary;
                state.shopTimer = 0;
                pendingEquipBest(player, ref state, secondaryShop);
                return;
            }

            int price = GetGunPrice(buyTarget, secondaryShop);
            if (player.Money < price || IsInventoryFull(player))
            {
                string sellGun = GetWorstOwnedGunName(player);
                if (sellGun != null && (IsInventoryFull(player) || player.Money < price))
                {
                    state.pendingEquipGun = buyTarget;
                    state.pendingSellGun = sellGun;
                    if (state.shopTimer > 10 && state.shopTimer % 15 == 0)
                        state.bPressed = true;
                    state.shopPhase = ShopSellOpen;
                    state.shopTimer = 0;
                    return;
                }

                if (state.shopTimer > 20 && state.shopTimer % 15 == 0)
                    state.bPressed = true;
                if (secondaryShop)
                    state.shopPhase = ShopReady;
                else
                    state.shopPhase = ShopGoToSecondary;
                state.shopTimer = 0;
                pendingEquipBest(player, ref state, secondaryShop);
                return;
            }

            Point? itemPos = FindItemPosition(screen, buyTarget);
            if (!itemPos.HasValue)
            {
                if (state.shopTimer > 15 && state.shopTimer % 12 == 0)
                    state.moveBack = true;
                return;
            }

            if (screen.selected.X == itemPos.Value.X && screen.selected.Y == itemPos.Value.Y)
            {
                if (state.shopTimer % 20 == 0)
                    state.aPressed = true;
                state.pendingEquipGun = buyTarget;
                return;
            }

            NavigateSelection(screen, ref state, itemPos.Value);
        }

        static void pendingEquipBest(Player player, ref AiPlayerState state, bool secondaryShop)
        {
            string best = GetBestOwnedGunName(player, secondaryShop);
            if (!string.IsNullOrEmpty(best))
                state.pendingEquipGun = best;
        }

        static void UpdateInventoryEquip(Player player, ref AiPlayerState state, ItemScreen inv)
        {
            state.shopTimer++;
            string gunName = state.pendingEquipGun ?? GetBestOwnedGunName(player);
            if (string.IsNullOrEmpty(gunName))
            {
                if (state.shopTimer % 15 == 0)
                    state.bPressed = true;
                state.shopPhase = ShopReady;
                state.shopTimer = 0;
                return;
            }

            if (state.shopPhase == ShopEquipPick)
            {
                Point? pos = FindItemPosition(inv, gunName);
                if (!pos.HasValue)
                {
                    gunName = GetBestOwnedGunName(player);
                    pos = string.IsNullOrEmpty(gunName) ? null : FindItemPosition(inv, gunName);
                    if (!pos.HasValue)
                    {
                        if (state.shopTimer % 15 == 0)
                            state.bPressed = true;
                        state.shopPhase = ShopReady;
                        state.shopTimer = 0;
                        return;
                    }
                    state.pendingEquipGun = gunName;
                }

                if (inv.selected.X == pos.Value.X && inv.selected.Y == pos.Value.Y)
                {
                    if (state.shopTimer % 15 == 0)
                        state.aPressed = true;
                    state.shopPhase = ShopEquipSlot;
                    state.shopTimer = 0;
                    return;
                }

                NavigateSelection(inv, ref state, pos.Value);
                return;
            }

            if (state.shopPhase == ShopEquipSlot)
            {
                if (state.shopTimer == 8)
                    state.dpadRight = true;
                if (state.shopTimer == 20)
                    state.bPressed = true;
                if (state.shopTimer > 35)
                {
                    state.shopPhase = ShopReady;
                    state.shopTimer = 0;
                    state.pendingEquipGun = null;
                }
            }
        }

        static void UpdateInventorySell(Player player, ref AiPlayerState state, ItemScreen inv)
        {
            state.shopTimer++;
            string gunName = state.pendingSellGun ?? GetWorstOwnedGunName(player);
            if (string.IsNullOrEmpty(gunName))
            {
                if (state.shopTimer % 15 == 0)
                    state.bPressed = true;
                state.shopPhase = ShopReady;
                state.shopTimer = 0;
                return;
            }

            if (state.shopPhase == ShopSellPick)
            {
                Point? pos = FindItemPosition(inv, gunName);
                if (!pos.HasValue)
                {
                    gunName = GetWorstOwnedGunName(player);
                    pos = string.IsNullOrEmpty(gunName) ? null : FindItemPosition(inv, gunName);
                    if (!pos.HasValue)
                    {
                        if (state.shopTimer % 15 == 0)
                            state.bPressed = true;
                        state.shopPhase = ShopReady;
                        state.shopTimer = 0;
                        return;
                    }
                    state.pendingSellGun = gunName;
                }

                if (inv.selected.X == pos.Value.X && inv.selected.Y == pos.Value.Y)
                {
                    state.shopPhase = ShopSellHold;
                    state.shopTimer = 0;
                    return;
                }

                NavigateSelection(inv, ref state, pos.Value);
                return;
            }

            if (state.shopPhase == ShopSellHold)
            {
                state.reloadHeld = true;
                if (state.shopTimer >= SellHoldFrames)
                {
                    state.reloadHeld = false;
                    state.bPressed = true;
                    state.pendingSellGun = null;
                    state.shopPhase = ShopRetryBuy;
                    state.shopTimer = 0;
                }
            }
        }

        static void SmartMove(Player player, ref AiPlayerState state, Vector3 goal, float stopDistance)
        {
            TrackStuck(player, ref state);
            if (player.stuck || state.stuckFrames > 12)
            {
                UnstuckMove(ref state, player, goal);
                return;
            }

            MoveToward(player.Position, goal, stopDistance, ref state);
        }

        static void UnstuckMove(ref AiPlayerState state, Player player, Vector3 goal)
        {
            float dx = goal.X - player.Position.X;
            float dz = goal.Z - player.Position.Z;
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 0.01f)
            {
                SetMoveDirection(ref state, state.unstuckBias == 0 ? 1f : -1f, state.unstuckBias == 0 ? 0f : 1f);
            }
            else
            {
                dx /= len;
                dz /= len;
                if (state.unstuckBias == 0)
                    SetMoveDirection(ref state, -dz, dx);
                else
                    SetMoveDirection(ref state, dz, -dx);
            }

            state.unstuckBias = 1 - state.unstuckBias;
            state.stuckFrames = 0;
        }

        static void TrackStuck(Player player, ref AiPlayerState state)
        {
            float moved = DistanceXZ(player.Position, state.lastPos);
            if (moved < 0.04f)
                state.stuckFrames++;
            else
                state.stuckFrames = 0;
            state.lastPos = player.Position;
        }

        static GameObject FindBestTarget(Vector3 from, Player p1)
        {
            if (Global.MasterCache == null || Global.MasterCache.gameObjects == null)
                return null;

            GameObject best = null;
            float bestScore = float.MaxValue;

            foreach (GameObject obj in Global.MasterCache.gameObjects)
            {
                if (obj == null || obj.Dead || !obj.Active || !obj.Enemy)
                    continue;

                float distSelf = DistanceXZ(from, obj.Position);
                if (distSelf > MaxShootRange)
                    continue;

                float score = distSelf;
                Zombie z = obj as Zombie;
                if (z != null && z.Engaged)
                    score *= 0.55f;

                if (p1 != null && !p1.Dead)
                {
                    float distP1 = DistanceXZ(p1.Position, obj.Position);
                    if (distP1 > MaxCombatRangeFromP1 + 4f)
                        score *= 1.35f;
                    else if (distP1 < MaxCombatRangeFromP1)
                        score *= 0.92f;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    best = obj;
                }
            }

            return best;
        }

        static bool BetweenWaves()
        {
            return Global.MasterWave != null && !Global.MasterWave.InWave;
        }

        static string GetNextGunToBuy(Player p, bool secondaryShop)
        {
            string[] names = secondaryShop ? SecondaryGunNames : GunNames;
            if (names == null)
                return null;

            for (int i = 0; i < names.Length; i++)
            {
                if (!OwnsGun(p, names[i]))
                    return names[i];
            }
            return null;
        }

        static bool IsInventoryFull(Player p)
        {
            if (p.gunCache == null || p.gunCache.OwnedGuns == null)
                return false;
            return p.gunCache.OwnedGuns.Count > OwnedGunLimit;
        }

        static bool IsSecondaryGunName(string name)
        {
            if (SecondaryGunNames == null || string.IsNullOrEmpty(name))
                return false;
            for (int i = 0; i < SecondaryGunNames.Length; i++)
            {
                if (SecondaryGunNames[i] == name)
                    return true;
            }
            return false;
        }

        static int GetGunTierIndex(string name)
        {
            if (string.IsNullOrEmpty(name))
                return -1;

            if (GunNames != null)
            {
                for (int i = 0; i < GunNames.Length; i++)
                {
                    if (GunNames[i] == name)
                        return i;
                }
            }

            if (SecondaryGunNames != null)
            {
                for (int i = 0; i < SecondaryGunNames.Length; i++)
                {
                    if (SecondaryGunNames[i] == name)
                        return GunNames.Length + i;
                }
            }
            return -1;
        }

        static string GetWorstOwnedGunName(Player p)
        {
            if (p.gunCache == null || p.gunCache.OwnedGuns == null)
                return null;

            string worst = null;
            int worstTier = int.MaxValue;

            foreach (Gun gun in p.gunCache.OwnedGuns)
            {
                if (gun == null || string.IsNullOrEmpty(gun.Name))
                    continue;
                if (gun.Name == "Pistol")
                    continue;

                int tier = GetGunTierIndex(gun.Name);
                if (tier < 0)
                    tier = 0;

                if (tier < worstTier)
                {
                    worstTier = tier;
                    worst = gun.Name;
                }
            }
            return worst;
        }

        static string GetBestOwnedGunName(Player p, bool includeSecondary)
        {
            if (p.gunCache == null || p.gunCache.OwnedGuns == null)
                return null;

            if (includeSecondary && SecondaryGunNames != null)
            {
                for (int i = SecondaryGunNames.Length - 1; i >= 0; i--)
                {
                    if (OwnsGun(p, SecondaryGunNames[i]))
                        return SecondaryGunNames[i];
                }
            }

            if (GunNames == null)
                return null;

            for (int i = GunNames.Length - 1; i >= 0; i--)
            {
                if (OwnsGun(p, GunNames[i]))
                    return GunNames[i];
            }
            return null;
        }

        static string GetBestOwnedGunName(Player p)
        {
            return GetBestOwnedGunName(p, true);
        }

        static bool OwnsGun(Player p, string name)
        {
            if (p.gunCache == null || p.gunCache.OwnedGuns == null)
                return false;
            foreach (Gun gun in p.gunCache.OwnedGuns)
            {
                if (gun != null && gun.Name == name)
                    return true;
            }
            return false;
        }

        static int GetGunPrice(string name, bool secondaryShop)
        {
            string[] names = secondaryShop ? SecondaryGunNames : GunNames;
            int[] prices = secondaryShop ? SecondaryGunPrices : GunPrices;
            if (names == null || prices == null)
                return int.MaxValue;

            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == name)
                    return prices[i];
            }
            return int.MaxValue;
        }

        static Point? FindItemPosition(ItemScreen screen, string itemName)
        {
            if (screen.items == null)
                return null;

            int width = screen.items.GetLength(0);
            int height = screen.items.GetLength(1);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    CircleItem item = screen.items[x, y];
                    if (item != null && item.filled && item.Name == itemName)
                        return new Point(x, y);
                }
            }
            return null;
        }

        static void NavigateSelection(ItemScreen screen, ref AiPlayerState state, Point target)
        {
            if (target.Y > screen.selected.Y)
                state.moveBack = true;
            else if (target.Y < screen.selected.Y)
                state.moveForward = true;

            if (target.X > screen.selected.X)
                state.moveRight = true;
            else if (target.X < screen.selected.X)
                state.moveLeft = true;
        }

        static GameObject FindPrimaryStore()
        {
            if (Global.MasterCache == null || Global.MasterCache.gameObjects == null)
                return null;

            GameObject leftMost = null;
            foreach (GameObject obj in Global.MasterCache.gameObjects)
            {
                if (obj == null || !obj.Active || obj.GetType().Name != "Store")
                    continue;
                if (GameMods.IsSecondaryStore(obj))
                    continue;
                if (leftMost == null || obj.Position.X < leftMost.Position.X)
                    leftMost = obj;
            }
            return leftMost;
        }

        static GameObject FindSecondaryStore()
        {
            GameObject cached = GameMods.GetSecondaryStore();
            if (cached != null && cached.Active)
                return cached;

            if (Global.MasterCache == null || Global.MasterCache.gameObjects == null)
                return null;

            foreach (GameObject obj in Global.MasterCache.gameObjects)
            {
                if (obj != null && obj.Active && GameMods.IsSecondaryStore(obj))
                    return obj;
            }
            return null;
        }

        static ItemScreen GetPlayerStoreScreen(Player p, GameObject storeObj)
        {
            if (storeObj == null)
                return null;

            FieldInfo screensField = storeObj.GetType().GetField("screens", BindingFlags.Instance | BindingFlags.NonPublic);
            if (screensField == null)
                return null;

            IList screens = screensField.GetValue(storeObj) as IList;
            if (screens == null)
                return null;

            foreach (ItemScreen screen in screens)
            {
                if (screen != null && screen.parent == p)
                    return screen;
            }
            return null;
        }

        static bool Collides(GameObject a, Player b)
        {
            return a.CollidedRectangle(b.CollisionRectangle);
        }

        static void MoveToward(Vector3 from, Vector3 to, float desiredRange, ref AiPlayerState state)
        {
            float dx = to.X - from.X;
            float dz = to.Z - from.Z;
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);
            if (dist < 0.01f || dist <= desiredRange)
                return;
            SetMoveDirection(ref state, dx / dist, dz / dist);
        }

        static void SetMoveDirection(ref AiPlayerState state, float nx, float nz)
        {
            const float threshold = 0.35f;
            if (nz > threshold) state.moveForward = true;
            if (nz < -threshold) state.moveBack = true;
            if (nx < -threshold) state.moveLeft = true;
            if (nx > threshold) state.moveRight = true;
        }

        static float DistanceXZ(Vector3 a, Vector3 b)
        {
            float dx = a.X - b.X;
            float dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        static float AngleTo(Vector3 from, Vector3 to)
        {
            float dx = to.X - from.X;
            float dz = to.Z - from.Z;
            if (Math.Abs(dx) < 0.001f && Math.Abs(dz) < 0.001f)
                return 0f;
            return (float)Math.Atan2(dz, -dx);
        }

        static bool IsAiPlayer(PlayerIndex index)
        {
            if (!Enabled)
                return false;
            EnsureStatesInitialized();
            return IndexToSlot((int)index) >= 0;
        }

        static AiPlayerState invalidState;

        static ref AiPlayerState StateFor(PlayerIndex index)
        {
            EnsureStatesInitialized();
            int slot = IndexToSlot((int)index);
            if (slot < 0 || slot >= AiSlotCount)
                return ref invalidState;
            return ref states[slot];
        }

        public static bool ShouldSkipDisconnect(Player player)
        {
            if (player == null)
                return false;
            if (CoopInputSplitActive() && player.myIndex == ResolveKeyboardHumanPlayerIndex())
                return true;
            if (!Enabled)
                return false;
            int idx = (int)player.myIndex;
            return idx >= FirstAiIndex && idx <= LastAiIndex;
        }

        public static bool QueryLeftStickForward(PlayerIndex index)
        {
            if (IsKeyboardHumanPlayer(index))
                return remoteP2State.moveForward;
            return IsAiPlayer(index) && StateFor(index).moveForward;
        }

        public static bool QueryLeftStickBack(PlayerIndex index)
        {
            if (IsKeyboardHumanPlayer(index))
                return remoteP2State.moveBack;
            return IsAiPlayer(index) && StateFor(index).moveBack;
        }

        public static bool QueryLeftStickLeft(PlayerIndex index)
        {
            if (IsKeyboardHumanPlayer(index))
                return remoteP2State.moveLeft;
            return IsAiPlayer(index) && StateFor(index).moveLeft;
        }

        public static bool QueryLeftStickRight(PlayerIndex index)
        {
            if (IsKeyboardHumanPlayer(index))
                return remoteP2State.moveRight;
            return IsAiPlayer(index) && StateFor(index).moveRight;
        }

        public static bool QueryFirePressed(PlayerIndex index)
        {
            if (IsKeyboardHumanPlayer(index))
                return remoteP2State.firePressed;
            return IsAiPlayer(index) && StateFor(index).firePressed;
        }

        public static bool QueryFireHeld(PlayerIndex index)
        {
            if (IsKeyboardHumanPlayer(index))
                return remoteP2State.fireHeld;
            return IsAiPlayer(index) && StateFor(index).fireHeld;
        }

        public static bool QueryAiming(PlayerIndex index)
        {
            if (IsKeyboardHumanPlayer(index))
                return remoteP2State.aiming;
            return IsAiPlayer(index) && StateFor(index).aiming;
        }

        public static bool HasAimControl(PlayerIndex index)
        {
            if (IsKeyboardHumanPlayer(index))
                return remoteP2State.aiming;
            return IsAiPlayer(index) && StateFor(index).aiming;
        }

        public static float GetAimAngle(PlayerIndex index)
        {
            if (IsKeyboardHumanPlayer(index))
                return remoteP2State.aimAngle;
            EnsureStatesInitialized();
            int slot = IndexToSlot((int)index);
            return slot >= 0 ? states[slot].aimAngle : 0f;
        }

        public static bool QueryStartPressed(PlayerIndex index)
        {
            if (IsKeyboardHumanPlayer(index))
            {
                bool edge = remoteP2State.startPressed && !remoteP2State.prevStart;
                remoteP2State.prevStart = remoteP2State.startPressed;
                return edge;
            }
            if (!IsAiPlayer(index)) return false;
            ref AiPlayerState state = ref StateFor(index);
            bool aiEdge = state.startPressed && !state.prevStart;
            state.prevStart = state.startPressed;
            return aiEdge;
        }

        public static bool QueryAPressed(PlayerIndex index)
        {
            if (IsKeyboardHumanPlayer(index))
            {
                bool edge = remoteP2State.aPressed && !remoteP2State.prevA;
                remoteP2State.prevA = remoteP2State.aPressed;
                return edge;
            }
            if (!IsAiPlayer(index)) return false;
            ref AiPlayerState state = ref StateFor(index);
            bool aiEdge = state.aPressed && !state.prevA;
            state.prevA = state.aPressed;
            return aiEdge;
        }

        public static bool QueryBPressed(PlayerIndex index)
        {
            if (IsKeyboardHumanPlayer(index))
            {
                bool edge = remoteP2State.bPressed && !remoteP2State.prevB;
                remoteP2State.prevB = remoteP2State.bPressed;
                return edge;
            }
            if (!IsAiPlayer(index)) return false;
            ref AiPlayerState state = ref StateFor(index);
            bool aiEdge = state.bPressed && !state.prevB;
            state.prevB = state.bPressed;
            return aiEdge;
        }

        public static bool QueryChangeWepPressed(PlayerIndex index)
        {
            if (IsKeyboardHumanPlayer(index))
            {
                bool edge = remoteP2State.changeWepPressed && !remoteP2State.prevChangeWep;
                remoteP2State.prevChangeWep = remoteP2State.changeWepPressed;
                return edge;
            }
            if (!IsAiPlayer(index)) return false;
            ref AiPlayerState state = ref StateFor(index);
            bool aiEdge = state.changeWepPressed && !state.prevChangeWep;
            state.prevChangeWep = state.changeWepPressed;
            return aiEdge;
        }

        public static bool QuerySpawnWavePressed(PlayerIndex index)
        {
            if (IsKeyboardHumanPlayer(index))
            {
                bool edge = remoteP2State.spawnWavePressed && !remoteP2State.prevSpawn;
                remoteP2State.prevSpawn = remoteP2State.spawnWavePressed;
                return edge;
            }
            if (!IsAiPlayer(index)) return false;
            ref AiPlayerState state = ref StateFor(index);
            bool aiEdge = state.spawnWavePressed && !state.prevSpawn;
            state.prevSpawn = state.spawnWavePressed;
            return aiEdge;
        }

        public static bool QueryDPadUp(PlayerIndex index)
        {
            if (IsKeyboardHumanPlayer(index))
            {
                bool edge = remoteP2State.dpadUp && !remoteP2State.prevDpadUp;
                remoteP2State.prevDpadUp = remoteP2State.dpadUp;
                return edge;
            }
            if (!IsAiPlayer(index)) return false;
            ref AiPlayerState state = ref StateFor(index);
            bool aiEdge = state.dpadUp && !state.prevDpadUp;
            state.prevDpadUp = state.dpadUp;
            return aiEdge;
        }

        public static bool QueryDPadDown(PlayerIndex index)
        {
            if (IsKeyboardHumanPlayer(index))
            {
                bool edge = remoteP2State.dpadDown && !remoteP2State.prevDpadDown;
                remoteP2State.prevDpadDown = remoteP2State.dpadDown;
                return edge;
            }
            if (!IsAiPlayer(index)) return false;
            ref AiPlayerState state = ref StateFor(index);
            bool aiEdge = state.dpadDown && !state.prevDpadDown;
            state.prevDpadDown = state.dpadDown;
            return aiEdge;
        }

        public static bool QueryDPadLeft(PlayerIndex index)
        {
            if (IsKeyboardHumanPlayer(index))
            {
                bool edge = remoteP2State.dpadLeft && !remoteP2State.prevDpadLeft;
                remoteP2State.prevDpadLeft = remoteP2State.dpadLeft;
                return edge;
            }
            if (!IsAiPlayer(index)) return false;
            ref AiPlayerState state = ref StateFor(index);
            bool aiEdge = state.dpadLeft && !state.prevDpadLeft;
            state.prevDpadLeft = state.dpadLeft;
            return aiEdge;
        }

        public static bool QueryDPadRight(PlayerIndex index)
        {
            if (IsKeyboardHumanPlayer(index))
            {
                bool edge = remoteP2State.dpadRight && !remoteP2State.prevDpadRight;
                remoteP2State.prevDpadRight = remoteP2State.dpadRight;
                return edge;
            }
            if (!IsAiPlayer(index)) return false;
            ref AiPlayerState state = ref StateFor(index);
            bool aiEdge = state.dpadRight && !state.prevDpadRight;
            state.prevDpadRight = state.dpadRight;
            return aiEdge;
        }

        public static bool QueryReloadHeld(PlayerIndex index)
        {
            if (IsKeyboardHumanPlayer(index))
                return remoteP2State.reloadHeld;
            return IsAiPlayer(index) && StateFor(index).reloadHeld;
        }

        public static bool QueryReloadPressed(PlayerIndex index)
        {
            if (IsKeyboardHumanPlayer(index))
            {
                bool edge = remoteP2State.reloadPressed && !remoteP2State.prevReload;
                remoteP2State.prevReload = remoteP2State.reloadPressed;
                return edge;
            }
            return false;
        }
    }
}
