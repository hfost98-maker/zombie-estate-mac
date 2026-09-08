using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SDL3;

namespace ZombieEstate
{
    /// <summary>
    /// Multi-window co-op when dual-screen is on (set ZOMBIE_ESTATE_DUAL_SCREEN=0 to disable):
    ///   Main window — Player 1 (human)
    ///   Extra windows — one per logged-in human player (P2–P4), not AI
    /// Extra windows are plain SDL surfaces; frames are blitted on the main FNA
    /// device and presented via GraphicsDevice.Present(overrideWindowHandle).
    /// </summary>
    public static class DualScreen
    {
        const int WindowWidth = 1280;
        const int WindowHeight = 720;
        const int MaxExtraWindows = 3;

        static bool? enabledSetting;
        static bool sdlReady;
        static bool windowReady;
        static bool initAttempted;
        static IntPtr[] extraWindowHandles;
        static IntPtr[] extraWindowGlContexts;
        static int[] extraWindowPlayerIndices;
        static Vector3[] extraCameraPositions;
        static Vector3[] extraCameraLookAts;
        static bool[] extraCameraInitialized;
        static Player[] extraCameraPlayers;
        static int extraWindowCount;
        static IntPtr mainGlContext = IntPtr.Zero;
        static IntPtr mainWindow = IntPtr.Zero;
        static bool mainWindowUsesOpenGl = true;
        static RenderTarget2D renderTarget;
        static int targetWidth = WindowWidth;
        static int targetHeight = WindowHeight;
        static SurfaceFormat lastRenderFormat = SurfaceFormat.Color;
        static int lastMainWidth = -1;
        static int lastMainHeight = -1;
        static SDL.SDL_WindowFlags lastMainFlags;

        static Vector3 savedCameraPosition;
        static Vector3 savedCameraLookAt;
        static Matrix savedView;
        static Matrix savedProjection;

        static MethodInfo itemScreenDraw;
        static FieldInfo itemScreenActiveField;
        static FieldInfo itemScreenParentField;
        static FieldInfo itemScreenStoreField;
        static FieldInfo storeOpenField;
        static FieldInfo storeScreensField;
        static MethodInfo storeDrawMethod;
        static MethodInfo storeToggleMethod;
        static MethodInfo inventoryToggleMethod;
        static MethodInfo playerDrawInventoryMethod;
        static MethodInfo compatBeginStoreMethod;
        static MethodInfo areYouSureDrawMethod;
        static MethodInfo gameOverDrawMethod;
        static bool continueReflectionReady;

        static int? spectatePlayerPreference;
        static bool spectatePreferenceResolved;

        static GameState lastState = (GameState)(-1);
        static Player shopInitiator;
        static bool suppressShopSync;
        static bool[] humanShopSession;
        static bool[] hadShopOpen;
        static bool primaryStoreWasOpen;

        static void EnsureShopSessionArrays()
        {
            if (humanShopSession == null)
                humanShopSession = new bool[4];
            if (hadShopOpen == null)
                hadShopOpen = new bool[4];
        }

        const int OverlayNone = 0;
        const int OverlayPlayer = 1;

        static int pendingOverlay;
        static object pendingOverlayGame1;
        static Player pendingOverlayPlayer;
        static bool pendingOverlaySpectating;

        public static bool IsEnabled
        {
            get
            {
                if (enabledSetting.HasValue)
                    return enabledSetting.Value;

                string env = Environment.GetEnvironmentVariable("ZOMBIE_ESTATE_DUAL_SCREEN");
                if (env == "0" || env == "false")
                {
                    enabledSetting = false;
                    return false;
                }
                if (env == "1" || env == "true")
                {
                    enabledSetting = true;
                    return true;
                }

                enabledSetting = true;
                return true;
            }
        }

        public static bool WindowReady
        {
            get { return windowReady && extraWindowCount > 0; }
        }

        /// <summary>True when dual-screen is on but no extra windows (e.g. only P2 joined).</summary>
        public static bool MainWindowOnly
        {
            get { return IsEnabled && ShouldSkipExtraWindows(); }
        }

        public static Player GetMainWindowPlayer()
        {
            if (Global.PlayerOne != null)
                return Global.PlayerOne;

            for (int idx = 1; idx <= 3; idx++)
            {
                Player player = AiTeammate.GetLoggedInPlayer((PlayerIndex)idx);
                if (player != null)
                    return player;
            }

            return null;
        }

        static bool ShouldSkipExtraWindows()
        {
            if (!IsEnabled)
                return true;

            if (Global.PlayerOne != null)
                return false;

            return CountLoggedInHumans() <= 1;
        }

        static int CountLoggedInHumans()
        {
            int count = 0;
            for (int idx = 0; idx <= 3; idx++)
            {
                if (AiTeammate.GetLoggedInPlayer((PlayerIndex)idx) != null)
                    count++;
            }
            return count;
        }

        public static bool ShouldShowHudOnMainScreen(Player player)
        {
            if (!IsEnabled || player == null)
                return true;

            Player main = GetMainWindowPlayer();
            if (ShouldSkipExtraWindows() || !WindowReady)
                return player == main;

            return player == Global.PlayerOne;
        }

        public static void DrawMainScreenShopUi(object game1, SpriteBatch spriteBatch)
        {
            if (spriteBatch == null)
                return;

            game1 = ResolveGame1(game1);

            if (!IsEnabled)
            {
                DrawStoreUnfiltered(GetTestStore(game1), spriteBatch);
                return;
            }

            Player main = GetMainWindowPlayer();
            if (main != null && IsShopSessionActive())
                DrawPlayerShopUi(spriteBatch, main, game1);
        }

        public static bool IsMouseInPlayerWindow(PlayerIndex index)
        {
            if (!IsEnabled)
                return false;

            if (ShouldSkipExtraWindows())
            {
                Player main = GetMainWindowPlayer();
                if (main != null && (int)main.myIndex == (int)index)
                    return IsMouseInWindow(mainWindow);
                return index == PlayerIndex.One;
            }

            if (!windowReady)
                return false;

            if (index == PlayerIndex.One)
                return IsMouseInWindow(mainWindow);

            IntPtr window = GetWindowHandleForPlayer(index);
            return window != IntPtr.Zero && IsMouseInWindow(window);
        }

        public static bool IsPlayerWindowFocused(PlayerIndex index)
        {
            if (!IsEnabled)
                return index == PlayerIndex.One;

            if (ShouldSkipExtraWindows())
            {
                Player main = GetMainWindowPlayer();
                if (main != null && (int)main.myIndex == (int)index)
                {
                    IntPtr focus = SDL.SDL_GetMouseFocus();
                    return focus == IntPtr.Zero || focus == mainWindow;
                }
                return index == PlayerIndex.One && main == null;
            }

            if (!windowReady)
                return index == PlayerIndex.One;

            if (index == PlayerIndex.One)
            {
                IntPtr focus = SDL.SDL_GetMouseFocus();
                return focus == IntPtr.Zero || focus == mainWindow;
            }

            IntPtr window = GetWindowHandleForPlayer(index);
            if (window == IntPtr.Zero)
                return false;

            return SDL.SDL_GetMouseFocus() == window || IsMouseInWindow(window);
        }

        public static bool TryGetPlayerWindowMouseInput(
            PlayerIndex index,
            out float aimDx,
            out float aimDy,
            out bool fireHeld,
            out bool inPlayerWindow)
        {
            aimDx = aimDy = 0f;
            fireHeld = false;
            inPlayerWindow = false;

            if (!IsEnabled || !WindowReady)
                return false;

            IntPtr window = index == PlayerIndex.One
                ? mainWindow
                : GetWindowHandleForPlayer(index);
            if (window == IntPtr.Zero)
                return false;

            SDL.SDL_MouseButtonFlags globalButtons = SDL.SDL_GetGlobalMouseState(out float gx, out float gy);

            if (!GetWindowMouseAim(window, gx, gy, out aimDx, out aimDy, out inPlayerWindow))
                return true;

            if (!inPlayerWindow)
                return true;

            SDL.SDL_MouseButtonFlags localButtons = SDL.SDL_GetMouseState(out float lx, out float ly);
            fireHeld = HasMouseButton(localButtons, SDL.SDL_MouseButtonFlags.SDL_BUTTON_LMASK)
                || HasMouseButton(globalButtons, SDL.SDL_MouseButtonFlags.SDL_BUTTON_LMASK);

            return true;
        }

        public static bool TryUpdateMainCamera()
        {
            if (!IsEnabled || Global.State != GameState.INGAME)
                return false;

            EnsureExtraWindows();
            Player mainPlayer = GetMainWindowPlayer();
            Player viewPlayer = ResolveViewPlayer(mainPlayer);
            if (viewPlayer == null)
                return false;

            ApplyFollowCamera(viewPlayer, ref Global.CameraPosition, ref Global.CameraLookAt);
            return true;
        }

        public static void PrepareMainPresent(object game1)
        {
            if (!IsEnabled)
                return;

            GraphicsDevice device = Global.GraphicsDevice;
            if (device == null)
                return;

            EnsureGlBindings(game1);
            if (mainWindow == IntPtr.Zero || mainGlContext == IntPtr.Zero)
                return;

            if (!mainWindowUsesOpenGl || mainWindow == IntPtr.Zero || mainGlContext == IntPtr.Zero)
                return;

            if (!SDL.SDL_GL_MakeCurrent(mainWindow, mainGlContext))
            {
                mainWindowUsesOpenGl = false;
                return;
            }

            device.SetRenderTarget(null);
            RestoreMainViewport(game1);
        }

        public static void AfterMainDraw(object game1)
        {
            if (!IsEnabled || Global.State != GameState.INGAME || !sdlReady)
                return;
            if (Global.GraphicsDevice == null || Global.MasterCache == null)
                return;
            if (!EnsureRenderTarget())
                return;

            EnsureGlBindings(game1);
            SyncDisplayResources(game1);
            if (!EnsureRenderTarget())
                return;

            for (int i = 0; i < extraWindowCount; i++)
            {
                if (extraWindowHandles[i] == IntPtr.Zero)
                    continue;

                Player windowOwner = PlayerForIndex(extraWindowPlayerIndices[i]);
                if (windowOwner == null)
                    continue;

                try
                {
                    if (IsTeamWiped())
                    {
                        RenderContinueToExtraWindow(game1, extraWindowHandles[i], extraWindowGlContexts[i]);
                        continue;
                    }

                    Player viewPlayer = ResolveViewPlayer(windowOwner);
                    if (viewPlayer == null)
                        continue;

                    UpdateExtraWindowCamera(i, viewPlayer);
                    pendingOverlay = OverlayPlayer;
                    pendingOverlayGame1 = game1;
                    pendingOverlayPlayer = viewPlayer;
                    pendingOverlaySpectating = windowOwner.Dead && viewPlayer != windowOwner;
                    RenderCameraToTarget(game1, extraCameraPositions[i], extraCameraLookAts[i]);
                    PresentRenderTargetToWindow(game1, extraWindowHandles[i], extraWindowGlContexts[i]);
                }
                catch (Exception ex)
                {
                    Log("Player " + (extraWindowPlayerIndices[i] + 1) + " window render failed: " + ex.Message);
                }
            }

            RestoreMainGlContext();
        }

        public static void Tick()
        {
            if (!IsEnabled)
                return;

            object game1 = Global.Game;

            if (Global.State == GameState.INGAME && lastState != GameState.INGAME)
            {
                initAttempted = false;
                extraWindowCount = 0;
                mainWindow = IntPtr.Zero;
                mainGlContext = IntPtr.Zero;
                mainWindowUsesOpenGl = true;
                renderTarget = null;
                lastMainWidth = -1;
                lastMainHeight = -1;
                ClearHumanShopSession();
                Log("entered INGAME (multi-window enabled=" + IsEnabled + ")");
            }

            lastState = Global.State;
            if (Global.State == GameState.INGAME)
            {
                EnsureExtraWindows();
                SyncDisplayResources(game1);
                SyncShopSession(game1);

                for (int i = 0; i < extraWindowCount; i++)
                {
                    Player windowOwner = PlayerForIndex(extraWindowPlayerIndices[i]);
                    if (windowOwner == null || IsTeamWiped())
                        continue;

                    Player viewPlayer = ResolveViewPlayer(windowOwner);
                    if (viewPlayer != null)
                        UpdateExtraWindowCamera(i, viewPlayer);
                }
            }
        }

        static void EnsureExtraWindowArrays()
        {
            if (extraWindowHandles != null)
                return;

            extraWindowHandles = new IntPtr[MaxExtraWindows];
            extraWindowGlContexts = new IntPtr[MaxExtraWindows];
            extraWindowPlayerIndices = new int[MaxExtraWindows];
            extraCameraPositions = new Vector3[MaxExtraWindows];
            extraCameraLookAts = new Vector3[MaxExtraWindows];
            extraCameraInitialized = new bool[MaxExtraWindows];
            extraCameraPlayers = new Player[MaxExtraWindows];
        }

        static void EnsureExtraWindows()
        {
            if (sdlReady)
                return;

            if (Global.State != GameState.INGAME)
                return;

            if (initAttempted)
                return;

            initAttempted = true;
            EnsureExtraWindowArrays();

            if (ShouldSkipExtraWindows())
            {
                sdlReady = true;
                extraWindowCount = 0;
                windowReady = false;
                Log("main window only — no extra windows (P1 absent or solo human)");
                return;
            }

            try
            {
                if (Global.GraphicsDevice != null)
                {
                    Viewport vp = Global.GraphicsDevice.Viewport;
                    if (vp.Width > 0 && vp.Height > 0)
                    {
                        targetWidth = vp.Width;
                        targetHeight = vp.Height;
                    }
                }

                EnsureGlBindings(Global.Game);
                if (mainGlContext == IntPtr.Zero)
                    mainGlContext = SDL.SDL_GL_GetCurrentContext();

                extraWindowCount = 0;
                int posY = 80;
                for (int idx = 1; idx <= 3; idx++)
                {
                    if (!AiTeammate.IsHumanCoopPlayer((PlayerIndex)idx))
                        continue;

                    Player player = PlayerForIndex(idx);
                    if (player == null)
                        continue;

                    string title = "Zombie Estate - Player " + (idx + 1);
                    IntPtr window = CreateGlWindow(
                        title,
                        targetWidth + 40,
                        posY,
                        out IntPtr glContext);
                    if (window == IntPtr.Zero)
                    {
                        Log("extra window create failed for P" + (idx + 1) + ": " + SDL.SDL_GetError());
                        CleanupSdl();
                        initAttempted = false;
                        return;
                    }

                    extraWindowHandles[extraWindowCount] = window;
                    extraWindowGlContexts[extraWindowCount] = glContext;
                    extraWindowPlayerIndices[extraWindowCount] = idx;
                    extraCameraPlayers[extraWindowCount] = player;
                    extraCameraInitialized[extraWindowCount] = false;
                    extraWindowCount++;
                    posY += targetHeight + 40;
                }

                sdlReady = true;
                windowReady = EnsureRenderTarget();
                CaptureMainDisplayState();
                RestoreMainGlContext();
                Log("extra windows opened count=" + extraWindowCount + " renderReady=" + windowReady);
            }
            catch (Exception ex)
            {
                Log("init failed: " + ex.Message + " " + ex.GetType().Name);
                CleanupSdl();
                initAttempted = false;
            }
        }

        static IntPtr CreateGlWindow(string title, int posX, int posY, out IntPtr glContext)
        {
            glContext = IntPtr.Zero;

            IntPtr window = SDL.SDL_CreateWindow(
                title,
                targetWidth,
                targetHeight,
                SDL.SDL_WindowFlags.SDL_WINDOW_RESIZABLE);
            if (window == IntPtr.Zero)
                return IntPtr.Zero;

            SDL.SDL_SetWindowPosition(window, posX, posY);
            SDL.SDL_ShowWindow(window);
            return window;
        }

        static bool EnsureRenderTarget()
        {
            if (Global.GraphicsDevice == null)
                return false;

            GraphicsDevice device = Global.GraphicsDevice;
            SurfaceFormat format = device.PresentationParameters.BackBufferFormat;

            if (renderTarget != null
                && (renderTarget.Width != targetWidth
                    || renderTarget.Height != targetHeight
                    || lastRenderFormat != format))
            {
                ReleaseRenderTarget();
            }

            if (renderTarget != null)
            {
                windowReady = true;
                return true;
            }

            renderTarget = new RenderTarget2D(
                device,
                targetWidth,
                targetHeight,
                false,
                format,
                DepthFormat.Depth24);
            lastRenderFormat = format;
            windowReady = true;
            return true;
        }

        static void ReleaseRenderTarget()
        {
            if (renderTarget == null)
                return;

            renderTarget.Dispose();
            renderTarget = null;
            windowReady = extraWindowCount > 0;
        }

        static void SyncDisplayResources(object game1)
        {
            if (!sdlReady || Global.GraphicsDevice == null)
                return;

            EnsureGlBindings(game1);
            if (mainWindow == IntPtr.Zero)
                return;

            SDL.SDL_GetWindowSize(mainWindow, out int mainW, out int mainH);
            SDL.SDL_WindowFlags mainFlags = SDL.SDL_GetWindowFlags(mainWindow);
            bool mainFullscreen = (mainFlags & SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN) != 0;
            bool lastFullscreen = (lastMainFlags & SDL.SDL_WindowFlags.SDL_WINDOW_FULLSCREEN) != 0;

            if (mainW < 1)
                mainW = targetWidth;
            if (mainH < 1)
                mainH = targetHeight;

            // Ignore focus/input flags — only recreate when size or fullscreen actually changes.
            bool displayChanged = mainW != lastMainWidth
                || mainH != lastMainHeight
                || mainFullscreen != lastFullscreen;

            if (!displayChanged)
                return;

            lastMainWidth = mainW;
            lastMainHeight = mainH;
            lastMainFlags = mainFlags;

            if (mainW > 0 && mainH > 0)
            {
                targetWidth = mainW;
                targetHeight = mainH;
            }

            Log("main display changed " + targetWidth + "x" + targetHeight
                + " fullscreen=" + mainFullscreen + " — recreating P2 render target");

            ReleaseRenderTarget();
            lastRenderFormat = Global.GraphicsDevice.PresentationParameters.BackBufferFormat;
        }

        static void CaptureMainDisplayState()
        {
            if (mainWindow == IntPtr.Zero || Global.GraphicsDevice == null)
                return;

            SDL.SDL_GetWindowSize(mainWindow, out lastMainWidth, out lastMainHeight);
            lastMainFlags = SDL.SDL_GetWindowFlags(mainWindow);
            lastRenderFormat = Global.GraphicsDevice.PresentationParameters.BackBufferFormat;
        }

        static void EnsureGlBindings(object game1)
        {
            if (mainGlContext == IntPtr.Zero)
                mainGlContext = SDL.SDL_GL_GetCurrentContext();
            if (mainWindow == IntPtr.Zero)
                mainWindow = GetMainWindowHandle(game1);
        }

        static void RestoreMainGlContext()
        {
            if (!mainWindowUsesOpenGl || mainWindow == IntPtr.Zero || mainGlContext == IntPtr.Zero)
                return;

            if (!SDL.SDL_GL_MakeCurrent(mainWindow, mainGlContext))
                mainWindowUsesOpenGl = false;
        }

        static IntPtr GetWindowHandleForPlayer(PlayerIndex index)
        {
            EnsureExtraWindowArrays();
            int idx = (int)index;
            for (int i = 0; i < extraWindowCount; i++)
            {
                if (extraWindowPlayerIndices[i] == idx)
                    return extraWindowHandles[i];
            }
            return IntPtr.Zero;
        }

        static bool IsMouseInWindow(IntPtr window)
        {
            if (window == IntPtr.Zero)
                return false;

            SDL.SDL_MouseButtonFlags flags = SDL.SDL_GetGlobalMouseState(out float gx, out float gy);
            GetWindowMouseAim(window, gx, gy, out float dx, out float dy, out bool inside);
            return inside;
        }

        static void SyncShopSession(object game1)
        {
            if (suppressShopSync)
                return;

            EnsureShopSessionArrays();

            bool storeOpenNow = IsPrimaryStoreOpen(game1);
            bool globalStoreJustClosed = primaryStoreWasOpen && !storeOpenNow;

            Player opener = FindFirstHumanWithShopOpen();
            if (opener != null && shopInitiator == null)
            {
                shopInitiator = opener;
                MarkHumanShopSessionActive();
                suppressShopSync = true;
                try
                {
                    OpenShopForAllHumanPlayers(opener, game1);
                }
                finally
                {
                    suppressShopSync = false;
                }
                primaryStoreWasOpen = IsPrimaryStoreOpen(game1);
                return;
            }

            if (shopInitiator != null)
            {
                suppressShopSync = true;
                try
                {
                    Player closeInitiator = DetectShopCloseInitiator(game1);
                    if (closeInitiator != null)
                        ClearHumanShopSessionForPlayer(closeInitiator);

                    if (globalStoreJustClosed || closeInitiator != null)
                        RepairShopForSessionPlayers(game1, closeInitiator);

                    UpdateHumanShopSessionEndings();
                }
                finally
                {
                    suppressShopSync = false;
                }

                if (!AnyHumanInShopSession())
                    shopInitiator = null;
            }

            UpdateHadShopOpen();
            primaryStoreWasOpen = storeOpenNow;
        }

        static void UpdateHadShopOpen()
        {
            EnsureShopSessionArrays();
            for (int idx = 0; idx <= 3; idx++)
            {
                Player player = PlayerForIndex(idx);
                hadShopOpen[idx] = player != null
                    && !player.Dead
                    && IsShopOpenForPlayer(player);
            }
        }

        static void ClearHumanShopSessionForPlayer(Player player)
        {
            EnsureShopSessionArrays();
            if (player == null)
                return;

            int idx = (int)player.myIndex;
            if (idx >= 0 && idx < humanShopSession.Length)
                humanShopSession[idx] = false;
        }

        static bool IsShopSessionActive()
        {
            if (AnyHumanInShopSession())
                return true;
            return FindFirstHumanWithShopOpen() != null;
        }

        static void MarkHumanShopSessionActive()
        {
            EnsureShopSessionArrays();
            for (int idx = 0; idx <= 3; idx++)
            {
                if (AiTeammate.IsHumanCoopPlayer((PlayerIndex)idx))
                    humanShopSession[idx] = true;
            }
        }

        static void ClearHumanShopSession()
        {
            EnsureShopSessionArrays();
            shopInitiator = null;
            for (int i = 0; i < humanShopSession.Length; i++)
            {
                humanShopSession[i] = false;
                hadShopOpen[i] = false;
            }
            primaryStoreWasOpen = false;
        }

        static bool AnyHumanInShopSession()
        {
            EnsureShopSessionArrays();
            for (int idx = 0; idx <= 3; idx++)
            {
                if (humanShopSession[idx] && AiTeammate.IsHumanCoopPlayer((PlayerIndex)idx))
                    return true;
            }
            return false;
        }

        static void UpdateHumanShopSessionEndings()
        {
            EnsureShopSessionArrays();
            for (int idx = 0; idx <= 3; idx++)
            {
                if (!humanShopSession[idx] || !AiTeammate.IsHumanCoopPlayer((PlayerIndex)idx))
                    continue;

                Player player = PlayerForIndex(idx);
                if (player == null || player.Dead)
                {
                    humanShopSession[idx] = false;
                    continue;
                }

                if (!IsShopOpenForPlayer(player))
                    humanShopSession[idx] = false;
            }
        }

        static void RepairShopForSessionPlayers(object game1, Player exceptPlayer)
        {
            EnsureShopSessionArrays();
            for (int idx = 0; idx <= 3; idx++)
            {
                if (!humanShopSession[idx] || !AiTeammate.IsHumanCoopPlayer((PlayerIndex)idx))
                    continue;

                Player player = PlayerForIndex(idx);
                if (player == null || player.Dead || player == exceptPlayer)
                    continue;

                if (IsShopOpenForPlayer(player))
                    continue;

                ReactivatePlayerStoreScreen(player, GetTestStore(game1));
                ReactivatePlayerStoreScreen(player, GameMods.GetSecondaryStore());
            }
        }

        static void ReactivatePlayerStoreScreen(Player player, object storeObj)
        {
            if (storeObj == null || player == null)
                return;

            EnsureStoreReflection();
            if (storeScreensField == null)
                return;

            IList screens = storeScreensField.GetValue(storeObj) as IList;
            if (screens == null)
                return;

            for (int i = 0; i < screens.Count; i++)
            {
                object screen = screens[i];
                if (screen == null)
                    continue;

                if (itemScreenParentField != null)
                {
                    object parent = itemScreenParentField.GetValue(screen);
                    if (parent != player)
                        continue;
                }

                if (itemScreenActiveField != null)
                    itemScreenActiveField.SetValue(screen, true);
                if (itemScreenStoreField != null)
                    itemScreenStoreField.SetValue(screen, true);
                if (storeOpenField != null)
                    storeOpenField.SetValue(storeObj, true);
                return;
            }
        }

        static bool IsPrimaryStoreOpen(object game1)
        {
            object primaryStore = GetTestStore(game1);
            if (primaryStore == null || storeOpenField == null)
                return false;

            EnsureStoreReflection();
            object openVal = storeOpenField.GetValue(primaryStore);
            return openVal is bool && (bool)openVal;
        }

        static Player DetectShopCloseInitiator(object game1)
        {
            EnsureShopSessionArrays();
            InputManager input = Global.Input;
            if (input == null)
                return null;

            object primaryStore = GetTestStore(game1);
            object secondaryStore = GameMods.GetSecondaryStore();

            for (int idx = 0; idx <= 3; idx++)
            {
                if (!AiTeammate.IsHumanCoopPlayer((PlayerIndex)idx))
                    continue;

                Player player = PlayerForIndex(idx);
                if (player == null || player.Dead)
                    continue;

                PlayerIndex playerIndex = (PlayerIndex)idx;

                // A at the store building toggles the shared store (controller A / keyboard G).
                if (input.APressed(playerIndex)
                    && (PlayerCollidesStore(player, primaryStore)
                        || PlayerCollidesStore(player, secondaryStore)))
                {
                    return player;
                }

                // B closes shop/inventory UI (controller B / keyboard H).
                if (input.BPressed(playerIndex) && hadShopOpen[idx])
                    return player;
            }

            return null;
        }

        static bool PlayerCollidesStore(Player player, object storeObj)
        {
            GameObject store = storeObj as GameObject;
            if (player == null || store == null)
                return false;

            return store.CollidedRectangle(player.CollisionRectangle);
        }

        static Player FindFirstHumanWithShopOpen()
        {
            for (int idx = 0; idx <= 3; idx++)
            {
                if (!AiTeammate.IsHumanCoopPlayer((PlayerIndex)idx))
                    continue;

                Player player = PlayerForIndex(idx);
                if (player != null && !player.Dead && IsShopOpenForPlayer(player))
                    return player;
            }
            return null;
        }

        static bool IsShopOpenForPlayer(Player player)
        {
            if (player == null)
                return false;

            EnsureStoreReflection();
            object primaryStore = GetTestStore(Global.Game);
            object secondaryStore = GameMods.GetSecondaryStore();

            if (IsPlayerStoreScreenOpen(player, primaryStore))
                return true;
            if (IsPlayerStoreScreenOpen(player, secondaryStore))
                return true;

            return player.inventory != null && player.inventory.Active && !player.inventory.Store;
        }

        static bool IsPlayerStoreScreenOpen(Player player, object storeObj)
        {
            if (storeObj == null || storeScreensField == null)
                return false;

            if (storeOpenField != null)
            {
                object openVal = storeOpenField.GetValue(storeObj);
                if (openVal is bool && !(bool)openVal)
                    return false;
            }

            IList screens = storeScreensField.GetValue(storeObj) as IList;
            if (screens == null)
                return false;

            for (int i = 0; i < screens.Count; i++)
            {
                object screen = screens[i];
                if (screen == null)
                    continue;

                if (itemScreenParentField != null)
                {
                    object parent = itemScreenParentField.GetValue(screen);
                    if (parent != player)
                        continue;
                }

                if (itemScreenActiveField != null)
                {
                    object active = itemScreenActiveField.GetValue(screen);
                    if (active is bool && (bool)active)
                        return true;
                }
            }

            return false;
        }

        static void OpenShopForAllHumanPlayers(Player initiator, object game1)
        {
            bool primaryOpen = IsPlayerStoreScreenOpen(initiator, GetTestStore(game1));
            bool secondaryOpen = IsPlayerStoreScreenOpen(initiator, GameMods.GetSecondaryStore());
            bool inventoryOpen = initiator.inventory != null
                && initiator.inventory.Active
                && !initiator.inventory.Store;

            if (primaryOpen)
                EnsureStoreOpen(GetTestStore(game1));
            if (secondaryOpen)
                EnsureStoreOpen(GameMods.GetSecondaryStore());

            if (inventoryOpen)
            {
                for (int idx = 0; idx <= 3; idx++)
                {
                    if (!AiTeammate.IsHumanCoopPlayer((PlayerIndex)idx))
                        continue;

                    Player player = PlayerForIndex(idx);
                    if (player == null || player.Dead || player == initiator)
                        continue;

                    EnsureInventoryOpen(player);
                }
            }
        }

        static void CloseShopForAllHumanPlayers(object game1)
        {
            EnsureStoreClosed(GetTestStore(game1));
            EnsureStoreClosed(GameMods.GetSecondaryStore());

            for (int idx = 0; idx <= 3; idx++)
            {
                if (!AiTeammate.IsHumanCoopPlayer((PlayerIndex)idx))
                    continue;

                Player player = PlayerForIndex(idx);
                if (player == null)
                    continue;

                EnsureInventoryClosed(player);
            }
        }

        static void EnsureStoreOpen(object storeObj)
        {
            if (storeObj == null)
                return;

            EnsureStoreReflection();
            if (storeOpenField == null || storeToggleMethod == null)
                return;

            object openVal = storeOpenField.GetValue(storeObj);
            if (openVal is bool && (bool)openVal)
                return;

            storeToggleMethod.Invoke(storeObj, null);
        }

        static void EnsureStoreClosed(object storeObj)
        {
            if (storeObj == null)
                return;

            EnsureStoreReflection();
            if (storeOpenField == null || storeToggleMethod == null)
                return;

            object openVal = storeOpenField.GetValue(storeObj);
            if (openVal is bool && !(bool)openVal)
                return;

            storeToggleMethod.Invoke(storeObj, null);
        }

        static void EnsureInventoryOpen(Player player)
        {
            if (player == null || player.inventory == null)
                return;

            EnsureStoreReflection();
            if (player.inventory.Active)
                return;

            if (inventoryToggleMethod != null)
                inventoryToggleMethod.Invoke(player.inventory, null);
        }

        static void EnsureInventoryClosed(Player player)
        {
            if (player == null || player.inventory == null)
                return;

            EnsureStoreReflection();
            if (!player.inventory.Active)
                return;

            if (inventoryToggleMethod != null)
                inventoryToggleMethod.Invoke(player.inventory, null);
        }

        static void RenderCameraToTarget(
            object game1,
            Vector3 camPos,
            Vector3 camLookAt)
        {
            GraphicsDevice device = Global.GraphicsDevice;
            if (device == null || renderTarget == null)
                return;

            RenderTarget2D prevTarget = device.GetRenderTargets().Length > 0
                ? device.GetRenderTargets()[0].RenderTarget as RenderTarget2D
                : null;

            int overlay = pendingOverlay;
            object overlayGame1 = pendingOverlayGame1 ?? game1;
            Player overlayPlayer = pendingOverlayPlayer;
            bool overlaySpectating = pendingOverlaySpectating;
            pendingOverlay = OverlayNone;
            pendingOverlayGame1 = null;
            pendingOverlayPlayer = null;
            pendingOverlaySpectating = false;

            savedCameraPosition = Global.CameraPosition;
            savedCameraLookAt = Global.CameraLookAt;
            savedView = Global.View;
            savedProjection = Global.Projection;

            try
            {
                Global.CameraPosition = camPos;
                Global.CameraLookAt = camLookAt;
                ApplyViewProjection();

                device.SetRenderTarget(renderTarget);
                device.Viewport = new Viewport(0, 0, targetWidth, targetHeight);
                device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.Black, 1f, 0);

                GameObjectCache cache = GetFieldValue(game1, "objCache") as GameObjectCache;
                if (cache != null)
                    cache.DrawObjects();
                else
                    Global.MasterCache.DrawObjects();

                GameWorld world = GetWorld(game1);
                if (world != null)
                    world.DRAW();

                if (overlay == OverlayPlayer && overlayPlayer != null)
                {
                    SpriteBatch spriteBatch = GetFieldValue(game1, "spriteBatch") as SpriteBatch;
                    if (spriteBatch != null)
                        DrawPlayerOverlays(overlayGame1, overlayPlayer, spriteBatch, overlaySpectating);
                }
            }
            finally
            {
                device.SetRenderTarget(prevTarget);
                Global.CameraPosition = savedCameraPosition;
                Global.CameraLookAt = savedCameraLookAt;
                Global.View = savedView;
                Global.Projection = savedProjection;
            }
        }

        static void PresentRenderTargetToWindow(object game1, IntPtr window, IntPtr glContext)
        {
            if (window == IntPtr.Zero || renderTarget == null)
                return;

            GraphicsDevice device = Global.GraphicsDevice;
            if (device == null)
                return;

            RenderTarget2D prevTarget = device.GetRenderTargets().Length > 0
                ? device.GetRenderTargets()[0].RenderTarget as RenderTarget2D
                : null;

            try
            {
                SDL.SDL_GetWindowSize(window, out int wx, out int wy);
                if (wx < 1)
                    wx = targetWidth;
                if (wy < 1)
                    wy = targetHeight;

                device.SetRenderTarget(null);
                device.Viewport = new Viewport(0, 0, wx, wy);
                device.Clear(ClearOptions.Target, Color.Black, 1f, 0);

                ApplyPointSampling(device);

                SpriteBatch spriteBatch = GetFieldValue(game1, "spriteBatch") as SpriteBatch;
                if (spriteBatch == null || !InvokeSpriteBatchBegin(spriteBatch))
                {
                    Log("PresentRenderTargetToWindow: SpriteBatch begin failed");
                    return;
                }

                spriteBatch.Draw(renderTarget, new Rectangle(0, 0, wx, wy), Color.White);
                spriteBatch.End();

                Rectangle source = new Rectangle(0, 0, targetWidth, targetHeight);
                Rectangle dest = new Rectangle(0, 0, wx, wy);
                device.Present(source, dest, window);
            }
            catch (Exception ex)
            {
                Log("PresentRenderTargetToWindow failed: " + ex.Message);
            }
            finally
            {
                device.SetRenderTarget(prevTarget);
                RestoreMainViewport(game1);
            }
        }

        static void RestoreMainViewport(object game1)
        {
            GraphicsDevice device = Global.GraphicsDevice;
            if (device == null)
                return;

            int wx = device.PresentationParameters.BackBufferWidth;
            int wy = device.PresentationParameters.BackBufferHeight;
            if (wx < 1 || wy < 1)
            {
                IntPtr window = GetMainWindowHandle(game1);
                if (window != IntPtr.Zero)
                    SDL.SDL_GetWindowSize(window, out wx, out wy);
            }

            if (wx < 1)
                wx = targetWidth;
            if (wy < 1)
                wy = targetHeight;

            device.Viewport = new Viewport(0, 0, wx, wy);
        }

        static void ApplyPointSampling(GraphicsDevice device)
        {
            if (device == null)
                return;

            SamplerState sampler = device.SamplerStates[0];
            sampler.MinFilter = TextureFilter.Point;
            sampler.MagFilter = TextureFilter.Point;
            sampler.MipFilter = TextureFilter.Point;
        }

        static bool InvokeSpriteBatchBegin(SpriteBatch spriteBatch)
        {
            Type compat = Type.GetType("Xblig.Compat.Xna3SpriteBatchCompat, FNA.NetStub");
            MethodInfo begin = compat?.GetMethod("Begin", new[] { typeof(SpriteBatch) });
            if (begin == null)
                return false;
            begin.Invoke(null, new object[] { spriteBatch });
            ApplyPointSampling(Global.GraphicsDevice);
            return true;
        }

        static void DrawPlayerOverlays(object game1, Player player, SpriteBatch spriteBatch, bool spectating)
        {
            Type compat = Type.GetType("Xblig.Compat.Xna3SpriteBatchCompat, FNA.NetStub");
            MethodInfo begin = compat?.GetMethod("Begin", new[] { typeof(SpriteBatch) });
            if (begin == null)
                return;

            begin.Invoke(null, new object[] { spriteBatch });

            if (IsTeamWiped())
            {
                DrawTeamWipeOverlay(game1, spriteBatch);
            }
            else
            {
                if (Global.MasterWave != null)
                {
                    Global.MasterWave.DrawCount(spriteBatch, Global.GraphicsDevice);
                    Global.MasterWave.DrawStats(spriteBatch, Global.GraphicsDevice);
                }

                if (player.gunCache != null)
                    player.gunCache.DrawHUD(spriteBatch);

                if (IsShopSessionActive())
                    DrawPlayerShopUi(spriteBatch, player, game1);

                Global.DrawFloaters(spriteBatch);

                if (spectating)
                    DrawSpectateBanner(spriteBatch, player);
            }

            spriteBatch.End();
        }

        static void DrawSpectateBanner(SpriteBatch spriteBatch, Player target)
        {
            if (spriteBatch == null || target == null || Global.Font == null)
                return;

            int playerNumber = (int)target.myIndex + 1;
            string label = "SPECTATING PLAYER " + playerNumber;
            Vector2 pos = new Vector2(24f, targetHeight - 48f);
            spriteBatch.DrawString(Global.Font, label, pos, Color.White * 0.85f);
        }

        static bool IsTeamWiped()
        {
            if (Global.ActivePlayers == null || Global.ActivePlayers.Count == 0)
                return false;
            return Global.AllDead();
        }

        static void EnsureContinueReflection()
        {
            if (continueReflectionReady)
                return;

            Type areYouSureType = typeof(Global).Assembly.GetType("ZombieEstate.AreYouSure");
            Type gameOverType = typeof(Global).Assembly.GetType("ZombieEstate.GameOver");
            if (areYouSureType != null)
            {
                areYouSureDrawMethod = areYouSureType.GetMethod(
                    "Draw",
                    new[] { typeof(SpriteBatch) });
            }
            if (gameOverType != null)
            {
                gameOverDrawMethod = gameOverType.GetMethod(
                    "Draw",
                    new[] { typeof(SpriteBatch) });
            }

            continueReflectionReady = true;
        }

        static void DrawTeamWipeOverlay(object game1, SpriteBatch spriteBatch)
        {
            if (game1 == null || spriteBatch == null)
                return;

            EnsureContinueReflection();
            object contScreen = GetFieldValue(game1, "ContScreen");
            object gameOverScreen = GetFieldValue(game1, "gameOverScreen");

            if (Global.Continues > 0 && contScreen != null && areYouSureDrawMethod != null)
                areYouSureDrawMethod.Invoke(contScreen, new object[] { spriteBatch });
            else if (gameOverScreen != null && gameOverDrawMethod != null)
                gameOverDrawMethod.Invoke(gameOverScreen, new object[] { spriteBatch });
        }

        static void RenderContinueToExtraWindow(object game1, IntPtr window, IntPtr glContext)
        {
            if (window == IntPtr.Zero)
                return;

            GraphicsDevice device = Global.GraphicsDevice;
            if (device == null)
                return;

            RenderTarget2D prevTarget = device.GetRenderTargets().Length > 0
                ? device.GetRenderTargets()[0].RenderTarget as RenderTarget2D
                : null;

            try
            {
                SDL.SDL_GetWindowSize(window, out int wx, out int wy);
                if (wx < 1)
                    wx = targetWidth;
                if (wy < 1)
                    wy = targetHeight;

                device.SetRenderTarget(null);
                device.Viewport = new Viewport(0, 0, wx, wy);
                device.Clear(ClearOptions.Target, Color.Black, 1f, 0);

                SpriteBatch spriteBatch = GetFieldValue(game1, "spriteBatch") as SpriteBatch;
                if (spriteBatch != null && InvokeSpriteBatchBegin(spriteBatch))
                {
                    DrawTeamWipeOverlay(game1, spriteBatch);
                    spriteBatch.End();
                }

                Rectangle dest = new Rectangle(0, 0, wx, wy);
                device.Present(null, dest, window);
            }
            catch (Exception ex)
            {
                Log("RenderContinueToExtraWindow failed: " + ex.Message);
            }
            finally
            {
                device.SetRenderTarget(prevTarget);
                RestoreMainViewport(game1);
            }
        }

        static Player ResolveViewPlayer(Player windowOwner)
        {
            if (windowOwner != null && !windowOwner.Dead)
                return windowOwner;

            return PickSpectateTarget(windowOwner);
        }

        static Player PickSpectateTarget(Player windowOwner)
        {
            EnsureSpectatePreference();

            if (spectatePlayerPreference.HasValue)
            {
                Player preferred = PlayerForIndex(spectatePlayerPreference.Value);
                if (preferred != null && !preferred.Dead)
                    return preferred;
            }

            for (int idx = 0; idx <= 3; idx++)
            {
                if (windowOwner != null && (int)windowOwner.myIndex == idx)
                    continue;

                if (!AiTeammate.IsHumanCoopPlayer((PlayerIndex)idx))
                    continue;

                Player candidate = PlayerForIndex(idx);
                if (candidate != null && !candidate.Dead)
                    return candidate;
            }

            if (Global.ActivePlayers != null)
            {
                for (int i = 0; i < Global.ActivePlayers.Count; i++)
                {
                    Player candidate = Global.ActivePlayers[i];
                    if (candidate == null || candidate.Dead)
                        continue;
                    if (windowOwner != null && candidate == windowOwner)
                        continue;
                    return candidate;
                }
            }

            return null;
        }

        static void EnsureSpectatePreference()
        {
            if (spectatePreferenceResolved)
                return;

            spectatePreferenceResolved = true;
            spectatePlayerPreference = null;

            string env = Environment.GetEnvironmentVariable("ZOMBIE_ESTATE_SPECTATE_PLAYER");
            if (TryParseSpectatePreference(env, out int envPref))
            {
                spectatePlayerPreference = envPref;
                return;
            }

            string prefsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zombie-estate.prefs");
            try
            {
                if (File.Exists(prefsPath))
                {
                    foreach (string line in File.ReadAllLines(prefsPath))
                    {
                        string trimmed = line.Trim();
                        if (!trimmed.StartsWith("SPECTATE_PLAYER=", StringComparison.OrdinalIgnoreCase))
                            continue;
                        string value = trimmed.Substring("SPECTATE_PLAYER=".Length).Trim();
                        if (TryParseSpectatePreference(value, out int pref))
                            spectatePlayerPreference = pref;
                        break;
                    }
                }
            }
            catch
            {
            }
        }

        static bool TryParseSpectatePreference(string value, out int playerIndex)
        {
            playerIndex = -1;
            if (string.IsNullOrEmpty(value))
                return false;

            value = value.Trim();
            if (value.Equals("auto", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!int.TryParse(value, out int playerNumber))
                return false;

            if (playerNumber < 1 || playerNumber > 4)
                return false;

            playerIndex = playerNumber - 1;
            return true;
        }

        static void UpdateExtraWindowCamera(int slotIndex, Player player)
        {
            if (player == null)
                return;

            if (extraCameraPlayers[slotIndex] != player)
            {
                extraCameraPlayers[slotIndex] = player;
                extraCameraInitialized[slotIndex] = false;
            }

            ApplyFollowCamera(
                player,
                ref extraCameraPositions[slotIndex],
                ref extraCameraLookAts[slotIndex],
                ref extraCameraInitialized[slotIndex]);
        }

        static Player PlayerForIndex(int index)
        {
            switch (index)
            {
            case 0: return Global.PlayerOne;
            case 1: return Global.PlayerTwo;
            case 2: return Global.PlayerThree;
            case 3: return Global.PlayerFour;
            default: return null;
            }
        }

        static void ApplyFollowCameraAtPoint(
            Vector3 center,
            ref Vector3 camPos,
            ref Vector3 camLookAt,
            ref bool initialized)
        {
            const float distance = 10f;
            const float smooth = 0.1f;
            float camHeight = 4.2f * 4f * distance / 10f;
            float camZOffset = 4.8f * 4f * distance / 10f;

            if (!initialized)
            {
                camLookAt = new Vector3(center.X, center.Y, center.Z + 0.4f);
                camPos = new Vector3(center.X, camHeight, center.Z - camZOffset);
                if (camPos.Z < -120f)
                    camPos.Z = -120f;
                initialized = true;
                return;
            }

            camLookAt.X = MathHelper.SmoothStep(camLookAt.X, center.X, smooth);
            camLookAt.Z = MathHelper.SmoothStep(camLookAt.Z, center.Z + 0.4f, smooth);

            camPos.X = MathHelper.SmoothStep(camPos.X, center.X, smooth);
            camPos.Y = MathHelper.SmoothStep(camPos.Y, camHeight, smooth);
            camPos.Z = MathHelper.SmoothStep(camPos.Z, center.Z - camZOffset, smooth);
            if (camPos.Z < -120f)
                camPos.Z = -120f;
        }

        static void ApplyFollowCamera(Player player, ref Vector3 camPos, ref Vector3 camLookAt)
        {
            bool initialized = true;
            ApplyFollowCamera(player, ref camPos, ref camLookAt, ref initialized);
        }

        static void ApplyFollowCamera(
            Player player,
            ref Vector3 camPos,
            ref Vector3 camLookAt,
            ref bool initialized)
        {
            ApplyFollowCameraAtPoint(player.Position, ref camPos, ref camLookAt, ref initialized);
        }

        static void ApplyViewProjection()
        {
            Global.View = Matrix.CreateLookAt(
                Global.CameraPosition,
                Global.CameraLookAt,
                Vector3.Up);

            float aspect = Global.AspectRatio;
            if (aspect <= 0f)
                aspect = targetWidth / (float)targetHeight;

            Global.Projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(60f),
                aspect,
                1f,
                10000f);
        }

        static bool GetWindowMouseAim(
            IntPtr window,
            float globalX,
            float globalY,
            out float aimDx,
            out float aimDy,
            out bool inWindow)
        {
            aimDx = aimDy = 0f;
            inWindow = false;
            if (window == IntPtr.Zero)
                return false;

            if (!SDL.SDL_GetWindowSizeInPixels(window, out int pixW, out int pixH))
                return false;
            if (pixW < 1 || pixH < 1)
                return false;

            float localX;
            float localY;

            // Prefer window-local coords when this window owns the mouse.
            if (SDL.SDL_GetMouseFocus() == window)
            {
                SDL.SDL_GetMouseState(out localX, out localY);
                inWindow = localX >= 0f && localX < pixW && localY >= 0f && localY < pixH;
            }
            else
            {
                if (!SDL.SDL_GetWindowPosition(window, out int posX, out int posY))
                    return false;
                if (!SDL.SDL_GetWindowSize(window, out int logW, out int logH))
                    return false;

                float scaleX = logW > 0 ? pixW / (float)logW : 1f;
                float scaleY = logH > 0 ? pixH / (float)logH : 1f;

                localX = (globalX - posX) * scaleX;
                localY = (globalY - posY) * scaleY;
                inWindow = localX >= 0f && localX < pixW && localY >= 0f && localY < pixH;
            }

            if (!inWindow)
                return true;

            // Match FNA.NetStub TryGetMouseAim: (mouseX - centerX, centerY - mouseY)
            aimDx = localX - pixW * 0.5f;
            aimDy = pixH * 0.5f - localY;
            return true;
        }

        static bool HasMouseButton(SDL.SDL_MouseButtonFlags buttons, SDL.SDL_MouseButtonFlags mask)
        {
            return (buttons & mask) != 0;
        }

        static void DrawPlayerShopUi(SpriteBatch spriteBatch, Player player, object game1)
        {
            if (player == null || spriteBatch == null)
                return;

            EnsureStoreReflection();
            BeginStoreSpriteBatch(spriteBatch);
            try
            {
                DrawStoreScreensForPlayer(GetTestStore(game1), spriteBatch, player);
                DrawStoreScreensForPlayer(GameMods.GetSecondaryStore(), spriteBatch, player);

                if (player.inventory != null && player.inventory.Active && !player.inventory.Store
                    && playerDrawInventoryMethod != null)
                {
                    playerDrawInventoryMethod.Invoke(player, new object[] { spriteBatch });
                }
            }
            finally
            {
                EndStoreSpriteBatch(spriteBatch);
            }
        }

        static void DrawStoreUnfiltered(object storeObj, SpriteBatch spriteBatch)
        {
            if (storeObj == null || spriteBatch == null)
                return;
            EnsureStoreReflection();
            if (storeDrawMethod != null)
                storeDrawMethod.Invoke(storeObj, new object[] { spriteBatch });
        }

        static object ResolveGame1(object game1)
        {
            if (game1 != null && GetTestStore(game1) != null)
                return game1;
            if (Global.Game != null)
                return Global.Game;
            return game1;
        }

        static object GetTestStore(object game1)
        {
            return GetFieldValue(game1, "testStore");
        }

        static void EnsureStoreReflection()
        {
            if (itemScreenDraw != null)
                return;

            Type screenType = typeof(Global).Assembly.GetType("ZombieEstate.ItemScreen");
            Type storeType = typeof(Global).Assembly.GetType("ZombieEstate.Store");
            if (screenType == null || storeType == null)
                return;

            itemScreenDraw = screenType.GetMethod("Draw", new[] { typeof(SpriteBatch) });
            itemScreenActiveField = screenType.GetField("Active", BindingFlags.Instance | BindingFlags.Public);
            itemScreenParentField = screenType.GetField("parent", BindingFlags.Instance | BindingFlags.Public);
            itemScreenStoreField = screenType.GetField("Store", BindingFlags.Instance | BindingFlags.Public);
            storeOpenField = storeType.GetField("storeOpen", BindingFlags.Instance | BindingFlags.NonPublic);
            storeScreensField = storeType.GetField("screens", BindingFlags.Instance | BindingFlags.NonPublic);
            storeDrawMethod = storeType.GetMethod("DrawStore", new[] { typeof(SpriteBatch) });
            storeToggleMethod = storeType.GetMethod("ToggleStore", Type.EmptyTypes);
            inventoryToggleMethod = screenType.GetMethod("ToggleInventory", Type.EmptyTypes);
            playerDrawInventoryMethod = typeof(Player).GetMethod(
                "DrawInventory",
                new[] { typeof(SpriteBatch) });

            Type compat = Type.GetType("Xblig.Compat.Xna3SpriteBatchCompat, FNA.NetStub");
            if (compat != null)
            {
                compatBeginStoreMethod = compat.GetMethod(
                    "Begin",
                    new[] { typeof(SpriteBatch), typeof(int), typeof(SpriteSortMode), typeof(int) });
            }
        }

        static void DrawStoreScreensForPlayer(object storeObj, SpriteBatch spriteBatch, Player player)
        {
            if (storeObj == null || player == null || storeScreensField == null)
                return;

            if (storeOpenField != null)
            {
                object openVal = storeOpenField.GetValue(storeObj);
                if (openVal is bool && !(bool)openVal)
                    return;
            }

            IList screens = storeScreensField.GetValue(storeObj) as IList;
            if (screens == null)
                return;

            for (int i = 0; i < screens.Count; i++)
            {
                object screen = screens[i];
                if (screen == null)
                    continue;

                if (itemScreenParentField != null)
                {
                    object parent = itemScreenParentField.GetValue(screen);
                    if (parent != player)
                        continue;
                }

                if (itemScreenActiveField != null)
                {
                    object active = itemScreenActiveField.GetValue(screen);
                    if (active is bool && !(bool)active)
                        continue;
                }

                itemScreenDraw.Invoke(screen, new object[] { spriteBatch });
            }
        }

        static void BeginStoreSpriteBatch(SpriteBatch spriteBatch)
        {
            spriteBatch.End();
            if (compatBeginStoreMethod != null)
            {
                compatBeginStoreMethod.Invoke(
                    null,
                    new object[] { spriteBatch, 1, SpriteSortMode.Deferred, 0 });
            }
            else if (InvokeSpriteBatchBegin(spriteBatch))
            {
            }

            GraphicsDevice device = Global.GraphicsDevice;
            if (device == null)
                return;

            SamplerState sampler = device.SamplerStates[0];
            sampler.MinFilter = TextureFilter.Point;
            sampler.MagFilter = TextureFilter.Point;
            sampler.MipFilter = TextureFilter.Point;
        }

        static void EndStoreSpriteBatch(SpriteBatch spriteBatch)
        {
            spriteBatch.End();
            InvokeSpriteBatchBegin(spriteBatch);
        }

        static IntPtr GetMainWindowHandle(object game1)
        {
            if (mainWindow != IntPtr.Zero)
                return mainWindow;

            if (game1 == null && Global.Game != null)
                game1 = Global.Game;
            if (game1 == null)
                return IntPtr.Zero;

            PropertyInfo windowProp = game1.GetType().GetProperty("Window");
            object window = windowProp?.GetValue(game1, null);
            PropertyInfo handleProp = window?.GetType().GetProperty("Handle");
            if (handleProp == null)
                return IntPtr.Zero;

            mainWindow = (IntPtr)handleProp.GetValue(window, null);
            return mainWindow;
        }

        static GameWorld GetWorld(object game1)
        {
            if (game1 == null)
                return Global.World;
            GameWorld world = GetFieldValue(game1, "world") as GameWorld;
            return world ?? Global.World;
        }

        static object GetFieldValue(object obj, string name)
        {
            if (obj == null)
                return null;
            FieldInfo field = obj.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(obj);
        }

        static void CleanupSdl()
        {
            RestoreMainGlContext();

            if (extraWindowHandles != null && extraWindowGlContexts != null)
            {
                for (int i = 0; i < MaxExtraWindows; i++)
                {
                    if (extraWindowGlContexts[i] != IntPtr.Zero)
                    {
                        SDL.SDL_GL_DestroyContext(extraWindowGlContexts[i]);
                        extraWindowGlContexts[i] = IntPtr.Zero;
                    }
                    if (extraWindowHandles[i] != IntPtr.Zero)
                    {
                        SDL.SDL_DestroyWindow(extraWindowHandles[i]);
                        extraWindowHandles[i] = IntPtr.Zero;
                    }
                    if (extraCameraPlayers != null)
                        extraCameraPlayers[i] = null;
                    if (extraCameraInitialized != null)
                        extraCameraInitialized[i] = false;
                }
            }

            extraWindowCount = 0;
            ReleaseRenderTarget();
            mainGlContext = IntPtr.Zero;
            mainWindow = IntPtr.Zero;
            mainWindowUsesOpenGl = true;
            ClearHumanShopSession();
            sdlReady = false;
            windowReady = false;
        }

        static void Log(string message)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + " DualScreen: " + message;
            Console.WriteLine(line);
            try
            {
                string dir = AppDomain.CurrentDomain.BaseDirectory;
                File.AppendAllText(Path.Combine(dir, "dual-screen.log"), line + Environment.NewLine);
            }
            catch
            {
            }
        }
    }
}
