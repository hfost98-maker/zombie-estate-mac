using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ZombieEstate
{
    /// <summary>
    /// Map selection and secondary premium weapon shop. Injected by patch-ai-player2.cs.
    /// </summary>
    public static class GameMods
    {
        static bool initialized;
        static int selectedMapIndex;
        static bool prevMapLeft;
        static bool prevMapRight;
        static GameObject secondaryStoreRef;

        // Carnival-only ambience (music + starry sky).
        static bool ambienceReady;
        static bool carnivalAmbienceActive;
        static SoundEffectInstance[] defaultSongInstances;
        static SoundEffectInstance[] carnivalSongInstances;
        static SoundEffectInstance[] carnivalRotateTracks;
        static string[] carnivalRotateLabels;
        static int carnivalRotateIndex;
        static bool carnivalRotateActive;
        static bool carnivalRotateTrackPlaying;
        static float carnivalMusicVolume;
        static Texture2D defaultBarBg;
        static Texture2D starryBarBg;
        static FieldInfo barBackgroundField;
        static FieldInfo songInstancesField;

        static string[] mapLevel;
        static string[] mapPath;
        static string[] mapSpawn;
        static string[] mapLabel;
        static int mapCount;

        static void EnsureInitialized()
        {
            if (initialized)
                return;

            EnsureGameWorkingDirectory();

            const int maxCandidates = 8;
            string[] candLevel = new string[maxCandidates];
            string[] candPath = new string[maxCandidates];
            string[] candSpawn = new string[maxCandidates];
            string[] candLabel = new string[maxCandidates];
            bool[] candRequire = new bool[maxCandidates];
            int candidates = 0;

            candLevel[candidates] = "Manor.txt";
            candPath[candidates] = "PathMap_Manor.txt";
            candSpawn[candidates] = "ManorSpawns.txt";
            candLabel[candidates] = "Manor";
            candRequire[candidates] = false;
            candidates++;

            candLevel[candidates] = "TestMap_NEW.txt";
            candPath[candidates] = "PathMap_New.txt";
            candSpawn[candidates] = "TestMapSpawns_NEW.txt";
            candLabel[candidates] = "TestMap NEW";
            candRequire[candidates] = false;
            candidates++;

            candLevel[candidates] = "HedgeMaze.txt";
            candPath[candidates] = "PathMap_HedgeMaze.txt";
            candSpawn[candidates] = "HedgeMazeSpawns.txt";
            candLabel[candidates] = "Hedge Maze";
            candRequire[candidates] = false;
            candidates++;

            candLevel[candidates] = "OpenField.txt";
            candPath[candidates] = "PathMap_OpenField.txt";
            candSpawn[candidates] = "OpenFieldSpawns.txt";
            candLabel[candidates] = "Open Field";
            candRequire[candidates] = false;
            candidates++;

            candLevel[candidates] = "Carnival.txt";
            candPath[candidates] = "PathMap_Carnival.txt";
            candSpawn[candidates] = "CarnivalSpawns.txt";
            candLabel[candidates] = "Carnival";
            candRequire[candidates] = false;
            candidates++;

            candLevel[candidates] = "Maps/Graveyard.txt";
            candPath[candidates] = "Maps/PathMap_Graveyard.txt";
            candSpawn[candidates] = "Maps/GraveyardSpawns.txt";
            candLabel[candidates] = "Graveyard (ZE2)";
            candRequire[candidates] = true;
            candidates++;

            candLevel[candidates] = "Maps/Church.txt";
            candPath[candidates] = "Maps/PathMap_Church.txt";
            candSpawn[candidates] = "Maps/ChurchSpawns.txt";
            candLabel[candidates] = "Church (ZE2)";
            candRequire[candidates] = true;
            candidates++;

            candLevel[candidates] = "Maps/Mall.txt";
            candPath[candidates] = "Maps/PathMap_Mall.txt";
            candSpawn[candidates] = "Maps/MallSpawns.txt";
            candLabel[candidates] = "Mall (ZE2)";
            candRequire[candidates] = true;
            candidates++;

            mapLevel = new string[maxCandidates];
            mapPath = new string[maxCandidates];
            mapSpawn = new string[maxCandidates];
            mapLabel = new string[maxCandidates];
            mapCount = 0;

            for (int i = 0; i < candidates; i++)
            {
                if (candRequire[i] && !MapFilesExist(candLevel[i], candPath[i], candSpawn[i]))
                    continue;

                mapLevel[mapCount] = candLevel[i];
                mapPath[mapCount] = candPath[i];
                mapSpawn[mapCount] = candSpawn[i];
                mapLabel[mapCount] = candLabel[i];
                mapCount++;
            }

            if (mapCount == 0)
            {
                mapLevel[0] = "Manor.txt";
                mapPath[0] = "PathMap_Manor.txt";
                mapSpawn[0] = "ManorSpawns.txt";
                mapLabel[0] = "Manor";
                mapCount = 1;
            }

            if (selectedMapIndex >= mapCount)
                selectedMapIndex = 0;

            string defaultMap = Environment.GetEnvironmentVariable("ZOMBIE_ESTATE_DEFAULT_MAP");
            if (!string.IsNullOrEmpty(defaultMap))
            {
                defaultMap = defaultMap.Trim();
                for (int i = 0; i < mapCount; i++)
                {
                    if (string.Equals(mapLabel[i], defaultMap, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(mapLevel[i], defaultMap, StringComparison.OrdinalIgnoreCase)
                        || mapLevel[i].StartsWith(defaultMap, StringComparison.OrdinalIgnoreCase))
                    {
                        selectedMapIndex = i;
                        Console.WriteLine("Default map: " + mapLabel[i]);
                        break;
                    }
                }
            }

            initialized = true;
        }

        static bool MapFilesExist(string level, string path, string spawn)
        {
            return File.Exists(ResolveGamePath(level))
                && File.Exists(ResolveGamePath(path))
                && File.Exists(ResolveGamePath(spawn));
        }

        public static string GetLevelFile()
        {
            EnsureInitialized();
            if (mapLevel == null || mapCount <= 0)
                return ResolveGamePath("Manor.txt");
            int idx = selectedMapIndex;
            if (idx < 0 || idx >= mapCount)
                idx = 0;
            return ResolveGamePath(mapLevel[idx]);
        }

        public static string GetPathMapFile()
        {
            EnsureInitialized();
            int idx = selectedMapIndex;
            if (idx < 0 || idx >= mapCount)
                idx = 0;
            return ResolveGamePath(mapPath[idx]);
        }

        public static string GetSpawnsFile()
        {
            EnsureInitialized();
            int idx = selectedMapIndex;
            if (idx < 0 || idx >= mapCount)
                idx = 0;
            return ResolveGamePath(mapSpawn[idx]);
        }

        public static string GetMapLabel()
        {
            EnsureInitialized();
            int idx = selectedMapIndex;
            if (idx < 0 || idx >= mapCount)
                idx = 0;
            return mapLabel[idx];
        }

        /// <summary>
        /// Rebuild GameWorld from the map chosen on character select. LoadContent
        /// builds Manor before the player picks a map; call this when starting a run.
        /// </summary>
        public static bool IsCarnivalMap()
        {
            EnsureInitialized();
            int idx = selectedMapIndex;
            if (idx < 0 || idx >= mapCount)
                return false;
            return string.Equals(mapLabel[idx], "Carnival", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mapLevel[idx], "Carnival.txt", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Remap standard zombie sprites to clown variants painted in MasterGrid row 4 cols 24–27.
        /// </summary>
        public static void UpdateCarnivalClowns()
        {
            UpdateCarnivalAmbience();
            UpdateCarnivalMusicRotation();
            if (!IsCarnivalMap() || Global.State != GameState.INGAME)
                return;
            if (Global.MasterCache == null || Global.MasterCache.gameObjects == null)
                return;

            FieldInfo texField = typeof(GameObject).GetField(
                "textureCoord",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (texField == null)
                return;

            foreach (GameObject obj in Global.MasterCache.gameObjects)
            {
                if (obj == null || !(obj is Zombie) || !obj.Active)
                    continue;
                Point tc = (Point)texField.GetValue(obj);
                Point clown = RemapClownTexCoord(tc);
                if (clown.X != tc.X || clown.Y != tc.Y)
                    texField.SetValue(obj, clown);
            }
        }

        static Point RemapClownTexCoord(Point tc)
        {
            if (tc.Y == 4 && tc.X >= 0 && tc.X <= 15)
                return new Point(tc.X + 24, tc.Y);
            if (tc.Y == 5 && tc.X <= 3)
                return new Point(27, 4);
            return tc;
        }

        public static void ApplySelectedMap()
        {
            EnsureInitialized();
            Game game = Global.Game;
            if (game == null)
                return;

            Type game1Type = game.GetType();
            FieldInfo wField = game1Type.GetField("levelW", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo hField = game1Type.GetField("levelH", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo worldField = game1Type.GetField("world", BindingFlags.Instance | BindingFlags.NonPublic);
            if (wField == null || hField == null || worldField == null)
                return;

            int w = (int)wField.GetValue(game);
            int h = (int)hField.GetValue(game);
            int idx = selectedMapIndex;
            if (idx < 0 || idx >= mapCount)
                idx = 0;

            string levelFile = ResolveGamePath(mapLevel[idx]);
            string pathFile = ResolveGamePath(mapPath[idx]);
            string spawnFile = ResolveGamePath(mapSpawn[idx]);
            if (!File.Exists(levelFile) || !File.Exists(pathFile) || !File.Exists(spawnFile))
            {
                Console.WriteLine("Map files missing for " + mapLabel[idx] + ", keeping current world.");
                Console.WriteLine("  Expected level: " + levelFile);
                Console.WriteLine("  Expected path:  " + pathFile);
                Console.WriteLine("  Expected spawn: " + spawnFile);
                return;
            }

            Console.WriteLine("Loading selected map: " + mapLabel[idx]);
            Console.WriteLine("  Level: " + levelFile);
            Console.WriteLine("  Path:  " + pathFile);
            Console.WriteLine("  Spawn: " + spawnFile);

            if (!IsCarnivalMap())
                RestoreDefaultAmbience();
            UpdateCarnivalAmbience();

            try
            {
                GameWorld world = new GameWorld(w, h, game);
                worldField.SetValue(game, world);
                Global.World = world;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Map load failed: " + ex.Message);
            }
        }

        public static void UpdateMapSelect()
        {
            EnsureInitialized();
            if (Global.State != GameState.CHARACTERSELECTION)
                return;

            GamePadState pad = GamePad.GetState((PlayerIndex)0);
            KeyboardState keys = Keyboard.GetState();

            bool left = keys.IsKeyDown(Keys.Left) || keys.IsKeyDown(Keys.A)
                || pad.DPad.Left == ButtonState.Pressed || pad.ThumbSticks.Left.X < -0.5f;
            bool right = keys.IsKeyDown(Keys.Right) || keys.IsKeyDown(Keys.D)
                || pad.DPad.Right == ButtonState.Pressed || pad.ThumbSticks.Left.X > 0.5f;

            if (left && !prevMapLeft)
            {
                selectedMapIndex--;
                if (selectedMapIndex < 0)
                    selectedMapIndex = mapCount - 1;
            }

            if (right && !prevMapRight)
            {
                selectedMapIndex++;
                if (selectedMapIndex >= mapCount)
                    selectedMapIndex = 0;
            }

            prevMapLeft = left;
            prevMapRight = right;
            UpdateCarnivalAmbience();
        }

        public static void DrawMapLabel(SpriteBatch spriteBatch)
        {
            EnsureInitialized();
            if (Global.State != GameState.CHARACTERSELECTION)
                return;
            if (spriteBatch == null || Global.Font == null)
                return;

            string text = "Map: " + GetMapLabel() + "  (< > to change)";
            Vector2 size = Global.Font.MeasureString(text);
            float x = 640f - size.X * 0.5f;
            spriteBatch.DrawString(Global.Font, text, new Vector2(x, 24f), Color.White);
        }

        public static void AfterLoadContent(object game1)
        {
            EnsureInitialized();
            try
            {
                SpawnSecondaryStore();
                EnsureCarnivalAmbienceAssets();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Secondary store setup failed: " + ex.Message);
            }
        }

        /// <summary>Carnival map only: spooky music stems + starry BarBG sky.</summary>
        public static void UpdateCarnivalAmbience()
        {
            if (Global.SoundManager == null)
                return;

            EnsureCarnivalAmbienceAssets();
            bool wantCarnival = IsCarnivalMap() && Global.State == GameState.INGAME;

            if (wantCarnival == carnivalAmbienceActive)
                return;

            if (wantCarnival)
                ActivateCarnivalAmbience();
            else
                RestoreDefaultAmbience();
        }

        static void EnsureCarnivalAmbienceAssets()
        {
            if (ambienceReady)
                return;

            SoundManager sm = Global.SoundManager;
            if (sm == null)
                return;

            Type smType = sm.GetType();
            songInstancesField = smType.GetField(
                "SongInstances",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (songInstancesField == null)
            {
                Console.WriteLine("Carnival music: SongInstances field not found.");
                return;
            }

            defaultSongInstances = songInstancesField.GetValue(sm) as SoundEffectInstance[];
            if (defaultSongInstances == null)
                return;

            defaultBarBg = Global.BarBGImg;

            Type barType = typeof(Bar);
            barBackgroundField = barType.GetField(
                "BackgroundImg",
                BindingFlags.Instance | BindingFlags.NonPublic);

            GraphicsDevice device = Global.GraphicsDevice;
            if (device != null)
            {
                string skyPath = Path.Combine(GetGameDirectory(), "CarnivalAssets", "BarBG_starry.png");
                if (File.Exists(skyPath))
                {
                    using (FileStream fs = File.OpenRead(skyPath))
                        starryBarBg = Texture2D.FromStream(device, fs);
                    Console.WriteLine("Loaded carnival starry sky: " + skyPath);
                }
                else
                {
                    Console.WriteLine("Carnival sky missing: " + skyPath);
                }
            }

            carnivalRotateTracks = LoadCarnivalRotateTracks();
            if (carnivalRotateTracks != null)
                Console.WriteLine("Loaded carnival rotating playlist (" + carnivalRotateTracks.Length + " tracks).");

            carnivalSongInstances = LoadCarnivalSongInstances();
            if (carnivalSongInstances != null && carnivalRotateTracks == null)
                Console.WriteLine("Loaded carnival music stems.");

            ambienceReady = carnivalRotateTracks != null
                || carnivalSongInstances != null
                || starryBarBg != null;
        }

        static SoundEffectInstance[] LoadCarnivalRotateTracks()
        {
            string dir = Path.Combine(GetGameDirectory(), "CarnivalMusic");
            string track1 = ResolveRotateTrackPath(dir, 1);
            string track2 = ResolveRotateTrackPath(dir, 2);
            if (track1 == null || track2 == null)
                return null;

            SoundEffectInstance inst1 = LoadWavInstance(track1, false);
            SoundEffectInstance inst2 = LoadWavInstance(track2, false);
            if (inst1 == null || inst2 == null)
                return null;

            carnivalRotateLabels = new string[] {
                Path.GetFileName(track1),
                Path.GetFileName(track2),
            };
            return new SoundEffectInstance[] { inst1, inst2 };
        }

        static string ResolveRotateTrackPath(string dir, int slot)
        {
            string[] candidates;
            if (slot == 1)
            {
                candidates = new string[] {
                    "carnival_track_1.wav",
                    "carnival_main.wav",
                    "magnifying_glass.wav",
                };
            }
            else
            {
                candidates = new string[] {
                    "carnival_track_2.wav",
                    "carnival_alt.wav",
                    "gargoyle.wav",
                };
            }

            foreach (string name in candidates)
            {
                string path = Path.Combine(dir, name);
                if (File.Exists(path))
                    return path;
            }
            return null;
        }

        static SoundEffectInstance[] LoadCarnivalSongInstances()
        {
            if (ResolveRotateTrackPath(Path.Combine(GetGameDirectory(), "CarnivalMusic"), 1) != null
                && ResolveRotateTrackPath(Path.Combine(GetGameDirectory(), "CarnivalMusic"), 2) != null)
            {
                return null;
            }

            string dir = Path.Combine(GetGameDirectory(), "CarnivalMusic");
            string mainPath = Path.Combine(dir, "carnival_main.wav");
            if (File.Exists(mainPath))
            {
                SoundEffectInstance main = LoadWavInstance(mainPath, true);
                if (main != null)
                {
                    Console.WriteLine("Loaded carnival custom track: " + mainPath);
                    return new SoundEffectInstance[] { main };
                }
            }

            string[] files = {
                "carnival_bass.wav",
                "carnival_calliope.wav",
                "carnival_chimes.wav",
            };
            SoundEffectInstance[] instances = new SoundEffectInstance[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                string path = Path.Combine(dir, files[i]);
                if (!File.Exists(path))
                {
                    Console.WriteLine("Carnival music missing: " + path);
                    return null;
                }
                instances[i] = LoadWavInstance(path, true);
                if (instances[i] == null)
                    return null;
            }
            return instances;
        }

        static SoundEffectInstance LoadWavInstance(string path, bool loop)
        {
            try
            {
                using (FileStream fs = File.OpenRead(path))
                {
                    SoundEffect sfx = SoundEffect.FromStream(fs);
                    SoundEffectInstance inst = sfx.CreateInstance();
                    inst.IsLooped = loop;
                    inst.Volume = 0f;
                    return inst;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Carnival music load failed (" + path + "): " + ex.Message);
                return null;
            }
        }

        static void UpdateCarnivalMusicRotation()
        {
            if (!carnivalRotateActive || !carnivalAmbienceActive)
                return;
            if (!IsCarnivalMap() || Global.State != GameState.INGAME)
                return;
            if (carnivalRotateTracks == null || carnivalRotateTracks.Length < 2)
                return;
            if (!carnivalRotateTrackPlaying)
                return;

            SoundEffectInstance current = carnivalRotateTracks[carnivalRotateIndex];
            if (current == null)
                return;
            if (current.State == SoundState.Playing)
                return;

            carnivalRotateTrackPlaying = false;
            carnivalRotateIndex++;
            if (carnivalRotateIndex >= carnivalRotateTracks.Length)
                carnivalRotateIndex = 0;
            PlayRotatingTrack(carnivalRotateIndex);
        }

        static void PlayRotatingTrack(int index)
        {
            if (carnivalRotateTracks == null || index < 0 || index >= carnivalRotateTracks.Length)
                return;

            for (int i = 0; i < carnivalRotateTracks.Length; i++)
            {
                if (carnivalRotateTracks[i] == null)
                    continue;
                carnivalRotateTracks[i].Stop();
                carnivalRotateTracks[i].Volume = 0f;
            }

            SoundEffectInstance inst = carnivalRotateTracks[index];
            inst.IsLooped = false;
            inst.Volume = carnivalMusicVolume;
            inst.Play();
            carnivalRotateTrackPlaying = true;
            string label = carnivalRotateLabels != null && index < carnivalRotateLabels.Length
                ? carnivalRotateLabels[index]
                : "track " + (index + 1);
            Console.WriteLine("Carnival music now playing: " + label);
        }

        static void StopRotatingTracks()
        {
            carnivalRotateActive = false;
            carnivalRotateTrackPlaying = false;
            if (carnivalRotateTracks == null)
                return;
            StopInstances(carnivalRotateTracks);
        }

        static void ActivateCarnivalAmbience()
        {
            carnivalAmbienceActive = true;
            SwapSky(true);
            SwapMusic(true);
            Console.WriteLine("Carnival ambience active (starry sky + carnival music).");
        }

        static void RestoreDefaultAmbience()
        {
            carnivalAmbienceActive = false;
            SwapMusic(false);
            SwapSky(false);
        }

        static void SwapMusic(bool carnival)
        {
            SoundManager sm = Global.SoundManager;
            if (sm == null || songInstancesField == null)
                return;

            if (carnival)
            {
                StopInstances(defaultSongInstances);
                carnivalMusicVolume = MathHelper.Clamp(
                    sm.StartMusicVolume * sm.MusicVolumeModifier, 0f, 1f);

                if (carnivalRotateTracks != null && carnivalRotateTracks.Length >= 2)
                {
                    StopInstances(carnivalSongInstances);
                    songInstancesField.SetValue(sm, new SoundEffectInstance[3]);
                    carnivalRotateActive = true;
                    carnivalRotateIndex = 0;
                    PlayRotatingTrack(0);
                    return;
                }

                if (carnivalSongInstances == null)
                    return;
                StopRotatingTracks();
                songInstancesField.SetValue(sm, carnivalSongInstances);
                StartInstances(carnivalSongInstances, carnivalMusicVolume);
            }
            else
            {
                StopRotatingTracks();
                if (carnivalSongInstances != null)
                    StopInstances(carnivalSongInstances);
                if (defaultSongInstances != null)
                {
                    songInstancesField.SetValue(sm, defaultSongInstances);
                    StartInstances(defaultSongInstances, sm.StartMusicVolume * sm.MusicVolumeModifier);
                }
            }
        }

        static void SwapSky(bool carnival)
        {
            Texture2D tex = carnival ? starryBarBg : defaultBarBg;
            if (tex == null)
                return;

            Global.BarBGImg = tex;
            if (Global.Bars == null || barBackgroundField == null)
                return;

            foreach (Bar bar in Global.Bars)
            {
                if (bar != null)
                    barBackgroundField.SetValue(bar, tex);
            }
        }

        static void StopInstances(SoundEffectInstance[] instances)
        {
            if (instances == null)
                return;
            for (int i = 0; i < instances.Length; i++)
            {
                if (instances[i] == null)
                    continue;
                instances[i].Stop();
                instances[i].Volume = 0f;
            }
        }

        static void StartInstances(SoundEffectInstance[] instances, float baseVolume)
        {
            if (instances == null)
                return;
            float v0 = MathHelper.Clamp(baseVolume, 0f, 1f);
            float v1 = MathHelper.Clamp(baseVolume * 0.65f, 0f, 1f);
            float v2 = MathHelper.Clamp(baseVolume * 0.45f, 0f, 1f);
            float[] levels = { v0, v1, v2 };
            for (int i = 0; i < instances.Length && i < levels.Length; i++)
            {
                if (instances[i] == null)
                    continue;
                instances[i].Volume = levels[i];
                if (instances[i].State != SoundState.Playing)
                    instances[i].Play();
            }
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

        static void EnsureGameWorkingDirectory()
        {
            try
            {
                string dir = GetGameDirectory();
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    Directory.SetCurrentDirectory(dir);
            }
            catch
            {
            }
        }

        static string ResolveGamePath(string relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return relativePath;
            if (Path.IsPathRooted(relativePath))
                return relativePath;
            return Path.Combine(GetGameDirectory(), relativePath);
        }

        public static bool IsSecondaryStore(GameObject obj)
        {
            if (obj == null)
                return false;
            if (secondaryStoreRef != null && ReferenceEquals(obj, secondaryStoreRef))
                return true;
            return obj.GetType().Name == "Store" && obj.Position.X > -34f;
        }

        public static GameObject GetSecondaryStore()
        {
            return secondaryStoreRef;
        }

        static void SpawnSecondaryStore()
        {
            if (Global.MasterCache == null || Global.MasterCache.gameObjects == null)
                return;

            Type storeType = typeof(Global).Assembly.GetType("ZombieEstate.Store");
            if (storeType == null)
                return;

            Vector3 pos = new Vector3(-28f, 0f, 7f);
            object storeObj = Activator.CreateInstance(storeType, pos);
            GameObject store = storeObj as GameObject;
            if (store == null)
                return;

            Global.MasterCache.gameObjects.Add(store);
            secondaryStoreRef = store;
            TrimStoreToPremiumWeapons(storeObj, storeType);
        }

        static void TrimStoreToPremiumWeapons(object storeObj, Type storeType)
        {
            FieldInfo gunsField = storeType.GetField("Guns", BindingFlags.Instance | BindingFlags.NonPublic);
            if (gunsField == null)
                return;

            IList gunList = gunsField.GetValue(storeObj) as IList;
            if (gunList == null)
                return;

            gunList.Clear();

            AddPremiumGun(gunList, 0, "Bubble Launcher", GetPrice("BubbleLauncher"));
            AddPremiumGun(gunList, 1, "Card Shuffler", GetPrice("ShuffleGun"));
            AddPremiumGun(gunList, 2, "Laser Rifle", GetPrice("LaserRifle"));
            AddPremiumGun(gunList, 3, "Grenade Launcher", GetPrice("GrenadeLauncher"));
            AddPremiumGun(gunList, 4, "Holy Squirt Gun", GetPrice("HolySquirtGun"));
        }

        static int GetPrice(string fieldName)
        {
            FieldInfo priceField = typeof(Prices).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            if (priceField == null)
                return 9999;
            return (int)priceField.GetValue(Global.Prices);
        }

        static void AddPremiumGun(IList gunList, int row, string name, int price)
        {
            Point tex = new Point(22, 1);
            CircleItem item = new CircleItem(new Point(0, row), name, "null", price, 1, -1, -1, Color.White);
            item.texCoord = tex;
            item.filled = true;
            gunList.Add(item);
        }
    }
}
