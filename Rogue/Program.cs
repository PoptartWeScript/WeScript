using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpDX;
using SharpDX.Direct3D9;
using SharpDX.Mathematics;
using SharpDX.XInput;
using WeScriptWrapper;
using WeScript.SDK.UI;
using WeScript.SDK.UI.Components;
using System.Diagnostics;

namespace RogueCompany
{
    class Program
    {
        public static IntPtr processHandle = IntPtr.Zero; //processHandle variable used by OpenProcess (once)
        public static bool gameProcessExists = false; //avoid drawing if the game process is dead, or not existent
        public static bool isWow64Process = false; //we all know the game is 32bit, but anyway...
        public static bool isGameOnTop = false; //we should avoid drawing while the game is not set on top
        public static bool isOverlayOnTop = false; //we might allow drawing visuals, while the user is working with the "menu"
        public static uint PROCESS_ALL_ACCESS = 0x1FFFFF; //hardcoded access right to OpenProcess (even EAC strips some of the access flags)
        public static Vector2 wndMargins = new Vector2(0, 0); //if the game window is smaller than your desktop resolution, you should avoid drawing outside of it
        public static Vector2 wndSize = new Vector2(0, 0); //get the size of the game window ... to know where to draw
        public static IntPtr GameBase = IntPtr.Zero;
        public static IntPtr GameSize = IntPtr.Zero;
        public static IntPtr FindWindow = IntPtr.Zero;
        public static Vector2 GameCenterPos = new Vector2(0, 0);
        public static Vector2 GameCenterPos2 = new Vector2(0, 0);
        public static DateTime LastSpacePressedDT = DateTime.Now;
        public static IntPtr GWorldPtr = IntPtr.Zero;
        public static IntPtr GNamesPtr = IntPtr.Zero;
        public static IntPtr FNamePool = IntPtr.Zero;
        public static uint Health = 0;
        public static Vector3 FMinimalViewInfo_Location = new Vector3(0, 0, 0);
        public static Vector3 FMinimalViewInfo_Rotation = new Vector3(0, 0, 0);
        public static float FMinimalViewInfo_FOV = 0;
        public static IntPtr UplayerState = IntPtr.Zero;
        public static uint EnemyID = 0;
        public static IntPtr LTeamNum = IntPtr.Zero;
        public static IntPtr TeamNum = IntPtr.Zero;
        public static IntPtr LAKSTeamState = IntPtr.Zero;
        public static float CurrentActorHP = 0;
        public static Vector2 AimTarg2D = new Vector2(0, 0); //for aimbot
        public static Vector3 AimTarg3D = new Vector3(0, 0, 0);
        public static Dictionary<UInt32, string> CachedID = new Dictionary<UInt32, string>();

        public static WeScript.SDK.UI.Menu RootMenu { get; private set; }
        public static WeScript.SDK.UI.Menu VisualsMenu { get; private set; }
        public static Menu AimbotMenu { get; private set; }
        class Components
        {
            public static readonly MenuKeyBind MainAssemblyToggle = new MenuKeyBind("mainassemblytoggle", "Toggle the whole assembly effect by pressing key:", VirtualKeyCode.Delete, KeybindType.Toggle, true);
            public static class VisualsComponent
            {
                public static readonly MenuBool DrawTheVisuals = new MenuBool("drawthevisuals", "Enable all of the Visuals", true);
                public static readonly MenuColor EnemyColor = new MenuColor("enecolor", "Enemy Color", new SharpDX.Color(0, 255, 0, 60));
                public static readonly MenuBool DrawBox = new MenuBool("box", "DrawBox", true);
                public static readonly MenuSlider DrawBoxThic = new MenuSlider("boxthickness", "Draw Box Thickness", 0, 0, 10);
                public static readonly MenuBool DrawBoxBorder = new MenuBool("drawboxborder", "Draw Border around Box and Text?", true);                
            }

            public static class AimbotComponent
            {
                public static readonly MenuBool AimGlobalBool = new MenuBool("enableaim", "Enable Aimbot Features", true);
                public static readonly MenuKeyBind AimKey = new MenuKeyBind("aimkey", "Aimbot HotKey (HOLD)", VirtualKeyCode.LeftMouse, KeybindType.Hold, false);
                public static readonly MenuSlider AimSpeed = new MenuSlider("aimspeed", "Aimbot Speed %", 12, 1, 100);
                public static readonly MenuSlider Distance = new MenuSlider("Distance Killer", "Distance kill%", 12, 1, 100);
                public static readonly MenuSlider AimFov = new MenuSlider("aimfov", "Aimbot FOV", 100, 4, 1000);
                public static readonly MenuBool DrawFov = new MenuBool("DrawFOV", "Enable FOV Circle Features Survivor", true);
                public static readonly MenuColor AimFovColor = new MenuColor("aimfovcolor", "FOV Color", new SharpDX.Color(255, 255, 255, 30));
            }
        }
        public static void InitializeMenu()
        {
            VisualsMenu = new WeScript.SDK.UI.Menu("visualsmenu", "Visuals Menu")
            {
                Components.VisualsComponent.DrawTheVisuals,
                Components.VisualsComponent.EnemyColor,
                Components.VisualsComponent.DrawBox,
                Components.VisualsComponent.DrawBoxThic.SetToolTip("Setting thickness to 0 will let the assembly auto-adjust itself depending on model distance"),
                Components.VisualsComponent.DrawBoxBorder.SetToolTip("Drawing borders may take extra performance (FPS) on low-end computers"),
            };

            AimbotMenu = new Menu("aimbotmenu", "Aimbot Killer Menu")
            {
                Components.AimbotComponent.AimGlobalBool,
                Components.AimbotComponent.AimKey,
                Components.AimbotComponent.AimSpeed,
                Components.AimbotComponent.AimFov,
                Components.AimbotComponent.DrawFov,

            };
            RootMenu = new WeScript.SDK.UI.Menu("Rogue", "WeScript.app Rogue Company --Poptart--", true)
            {
                Components.MainAssemblyToggle.SetToolTip("The magical boolean which completely disables/enables the assembly!"),
                VisualsMenu,
                AimbotMenu,
            };
            RootMenu.Attach();
        }

        private static double GetDistance2D(Vector2 pos1, Vector2 pos2)
        {
            Vector2 vector = new Vector2(pos1.X - pos2.X, pos1.Y - pos2.Y);
            return Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y);
        }

        
        private static string GetNameFromFName(uint key)
        {
            if (GNamesPtr == IntPtr.Zero)
                return "NULL";

            var chunkOffset = (uint)((int)(key) >> 16);
            var nameOffset = (ushort)key;
            ulong namePoolChunk = Memory.ZwReadUInt64(processHandle, (IntPtr)(GNamesPtr.ToInt64() + ((chunkOffset + 2) * 8)));
            ulong entryOffset = namePoolChunk + (ulong)(2 * nameOffset);
            short nameEntry = Memory.ZwReadInt16(processHandle, (IntPtr)entryOffset);
            int nameLength = nameEntry >> 6;
            string result = Memory.ZwReadString(processHandle, (IntPtr)entryOffset + 2, false);
            return result;
        }

        public static void SigScan()
        {
            //GWorldPtr = Memory.ZwFindSignature(processHandle, GameBase, GameSize, "48 89 05 ? ? ? ? 0F 28 D7", 0x3); //4.2
            GWorldPtr = Memory.ZwReadPointer(processHandle, GameBase + 0x689BE48, isWow64Process);
            GNamesPtr = GameBase + 0x672D200;
        }
        static void Main(string[] args)
        {
            Console.WriteLine("Rogue Company Assembly by Poptart");
            InitializeMenu();
            if (!Memory.InitDriver(DriverName.nsiproxy))
            {
                Console.WriteLine("[ERROR] Failed to initialize driver for some reason...");
            }
            Renderer.OnRenderer += OnRenderer;
            Memory.OnTick += OnTick;
        }
        public static double dims = 0.01905f;
        private static double GetDistance3D(Vector3 myPos, Vector3 enemyPos)
        {
            Vector3 vector = new Vector3(myPos.X - enemyPos.X, myPos.Y - enemyPos.Y, myPos.Z - enemyPos.Z);
            return Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y + vector.Z * vector.Z) * dims;
        }
        private static void OnTick(int counter, EventArgs args)
        {
            if (processHandle == IntPtr.Zero) //if we still don't have a handle to the process
            {
                var wndHnd = Memory.FindWindowName("Rogue Company  "); //why the devs added spaces after the name?!
                if (wndHnd != IntPtr.Zero) //if it exists
                {
                    //Console.WriteLine("weheree");
                    var calcPid = Memory.GetPIDFromHWND(wndHnd); //get the PID of that same process
                    if (calcPid > 0) //if we got the PID
                    {
                        processHandle = Memory.ZwOpenProcess(PROCESS_ALL_ACCESS, calcPid); //the driver will get a stripped handle, but doesn't matter, it's still OK
                        if (processHandle != IntPtr.Zero)
                        {
                            //if we got access to the game, check if it's x64 bit, this is needed when reading pointers, since their size is 4 for x86 and 8 for x64
                            isWow64Process = Memory.IsProcess64Bit(processHandle); //we know DBD is 64 bit but anyway...
                        }
                        else
                        {
                            Console.WriteLine("failed to get handle");
                        }
                    }
                }
            }
            else //else we have a handle, lets check if we should close it, or use it
            {
                var wndHnd = Memory.FindWindowName("Rogue Company  "); //why the devs added spaces after the name?!
                if (wndHnd != IntPtr.Zero) //window still exists, so handle should be valid? let's keep using it
                {
                    //the lines of code below execute every 33ms outside of the renderer thread, heavy code can be put here if it's not render dependant
                    gameProcessExists = true;
                    wndMargins = Renderer.GetWindowMargins(wndHnd);
                    wndSize = Renderer.GetWindowSize(wndHnd);
                    isGameOnTop = Renderer.IsGameOnTop(wndHnd);
                    isOverlayOnTop = Overlay.IsOnTop();
                    if (GameBase == IntPtr.Zero) //do we have access to Gamebase address?
                    {
                        GameBase = Memory.ZwGetModule(processHandle, null, isWow64Process); //if not, find it
                        //Console.WriteLine($"GameBase: {GameBase.ToString("X")}");
                        Console.WriteLine("Got GAMEBASE of RogueCompany!");
                    }
                    else
                    {
                        if (GameSize == IntPtr.Zero)
                        {
                            GameSize = Memory.ZwGetModuleSize(processHandle, null, isWow64Process);
                            //Console.WriteLine($"GameSize: {GameSize.ToString("X")}");
                        }
                        else
                        {
                            if (GWorldPtr == IntPtr.Zero)
                            {
                                //GWorldPtr = Memory.ZwFindSignature(processHandle, GameBase, GameSize, "48 8B 1D ? ? ? ? 48 85 DB 74 3B 41", 0x3); //4.1 patch
                                GWorldPtr = Memory.ZwReadPointer(processHandle, GameBase + 0x689BE48, isWow64Process);
                                // "Epic Games" GWorldPtr = Memory.ZwReadPointer(processHandle, GameBase + 0x97EA450, isWow64Process);
                            }

                            if (GNamesPtr == IntPtr.Zero)
                            {
                                //GNamesPtr = Memory.ZwFindSignature(processHandle, GameBase, GameSize, "48 8B 05 ? ? ? ? 48 85 C0 75 5F", 0x3); //4.1 patch
                                GNamesPtr = GameBase + 0x672D200;
                                //"Epic Games GNamesPtr = GameBase + 0x962D240;
                                //Console.WriteLine($"GNamesPtr: {GNamesPtr.ToString("X")}");
                            }
                        }
                    }
                }
                else //else most likely the process is dead, clean up
                {
                    Memory.CloseHandle(processHandle); //close the handle to avoid leaks
                    processHandle = IntPtr.Zero; //set it like this just in case for C# logic
                    gameProcessExists = false;
                    //clear your offsets, modules
                    GameBase = IntPtr.Zero;
                    GameSize = IntPtr.Zero;
                    GWorldPtr = IntPtr.Zero;
                    GNamesPtr = IntPtr.Zero;
                }
            }
        }        
        private static void OnRenderer(int fps, EventArgs args)
        {
            if (!gameProcessExists) return; //process is dead, don't bother drawing
            if ((!isGameOnTop) && (!isOverlayOnTop)) return; //if game and overlay are not on top, don't draw
            if (!Components.MainAssemblyToggle.Enabled) return; //main menu boolean to toggle the cheat on or off            

            double fClosestPos = 999999;
            GameCenterPos = new Vector2(wndSize.X / 2 + wndMargins.X, wndSize.Y / 2 + wndMargins.Y);
            GameCenterPos2 = new Vector2(wndSize.X / 2 + wndMargins.X, wndSize.Y / 2 + wndMargins.Y + 750.0f);//even if the game is windowed, calculate perfectly it's "center" for aim or crosshair

            if (GWorldPtr != IntPtr.Zero)
            {

                Functions.Ppc();
                var ULevel = Memory.ZwReadPointer(processHandle, GWorldPtr + 0x30, isWow64Process);
                if (GWorldPtr != IntPtr.Zero)
                {
                    var AActors = Memory.ZwReadPointer(processHandle, (IntPtr)ULevel.ToInt64() + 0x98, isWow64Process);
                    var ActorCnt = Memory.ZwReadUInt32(processHandle, (IntPtr)ULevel.ToInt64() + 0xA0);

                    if ((AActors != IntPtr.Zero) && (ActorCnt > 0))
                    {
                        for (uint i = 0; i <= ActorCnt; i++)
                        {
                            var AActor = Memory.ZwReadPointer(processHandle, (IntPtr)(AActors.ToInt64() + i * 8),
                                isWow64Process);
                            if (AActor != IntPtr.Zero)
                            {
                                                                    
                                var USceneComponent = Memory.ZwReadPointer(processHandle,
                                    (IntPtr)AActor.ToInt64() + 0x130, isWow64Process);
                                if (USceneComponent != IntPtr.Zero)
                                {
                                    var tempVec = Memory.ZwReadVector3(processHandle,
                                        (IntPtr)USceneComponent.ToInt64() + 0x11C);

                                    var AActorID = Memory.ZwReadUInt32(processHandle,
                                        (IntPtr)AActor.ToInt64() + 0x18);
                                    if (!CachedID.ContainsKey(AActorID))
                                    {
                                        var retname = GetNameFromFName(AActorID);
                                        CachedID.Add(AActorID, retname);
                                    }

                                    CurrentActorHP = Memory.ZwReadFloat(processHandle, (IntPtr)AActor.ToInt64() + 0x528);

                                    //string retname = "";
                                    if ((AActorID > 0)) //&& (AActorID < 700000)
                                    {
                                        var retname = CachedID[AActorID];
                                        retname = GetNameFromFName(AActorID);
                                        if (retname.Contains("MainCharacter_C") || retname.Contains("DefaultPVPBotCharacter_C") || retname.Contains("DefaultBotCharacter_C")) EnemyID = AActorID;
                                        var actor_pawn = Memory.ZwReadPointer(processHandle, (IntPtr)AActor.ToInt64() + 0x118, true);
                                        var Playerstate = Memory.ZwReadPointer(processHandle, (IntPtr)actor_pawn.ToInt64() + 0x240, true);
                                        var Offset_AKSTeamState = 0x398;
                                        var Offset_r_TeamNum = 0x220;
                                        var LAKSTeamState = Memory.ZwReadPointer(processHandle, (IntPtr)UplayerState.ToInt64() + Offset_AKSTeamState, true);
                                        LTeamNum = Memory.ZwReadPointer(processHandle, (IntPtr)LAKSTeamState.ToInt64() + Offset_r_TeamNum, true);
                                        var AKSTeamState = Memory.ZwReadPointer(processHandle, (IntPtr)Playerstate.ToInt64() + Offset_AKSTeamState, true);
                                        TeamNum = Memory.ZwReadPointer(processHandle, (IntPtr)AKSTeamState.ToInt64() + Offset_r_TeamNum, true);
                                    }
                                    if (Components.VisualsComponent.DrawTheVisuals.Enabled) //this should have been placed earlier?
                                    {
                                        //Console.WriteLine(team);
                                        //Console.WriteLine(team);

                                        int dist = (int)(GetDistance3D(FMinimalViewInfo_Location, tempVec));

                                        if (AActorID == EnemyID && CurrentActorHP > 0)
                                        {
                                            Vector2 vScreen_h3ad = new Vector2(0, 0);
                                            Vector2 vScreen_f33t = new Vector2(0, 0);
                                            if (Renderer.WorldToScreenUE4(new Vector3(tempVec.X, tempVec.Y, tempVec.Z + 50.0f), out vScreen_h3ad, FMinimalViewInfo_Location, FMinimalViewInfo_Rotation, FMinimalViewInfo_FOV, wndMargins, wndSize))
                                            {

                                                Renderer.WorldToScreenUE4(new Vector3(tempVec.X, tempVec.Y, tempVec.Z - 130.0f), out vScreen_f33t, FMinimalViewInfo_Location, FMinimalViewInfo_Rotation, FMinimalViewInfo_FOV, wndMargins, wndSize);
                                                if (Components.VisualsComponent.DrawBox.Enabled)
                                                {
                                                    if(LTeamNum != TeamNum)

                                                    Renderer.DrawFPSBox(vScreen_h3ad, vScreen_f33t, Components.VisualsComponent.EnemyColor.Color, BoxStance.standing, Components.VisualsComponent.DrawBoxThic.Value, Components.VisualsComponent.DrawBoxBorder.Enabled);
                                                    Renderer.DrawText("[" + dist + "m]", vScreen_f33t.X, vScreen_f33t.Y + 5, Components.VisualsComponent.EnemyColor.Color, 12, TextAlignment.centered, false);
                                                }
                                                else
                                                {                        
                                                    Renderer.DrawFPSBox(vScreen_h3ad, vScreen_f33t, Color.Blue, BoxStance.standing, Components.VisualsComponent.DrawBoxThic.Value, Components.VisualsComponent.DrawBoxBorder.Enabled);
                                                    Renderer.DrawText("[" + dist + "m]", vScreen_f33t.X, vScreen_f33t.Y + 5, Color.Black, 12, TextAlignment.centered, false);                                                   
                                                }   
                                                
                                            }

                                            if (Components.AimbotComponent.DrawFov.Enabled) //draw fov circle
                                            {
                                                Renderer.DrawCircle(GameCenterPos, Components.AimbotComponent.AimFov.Value, Components.AimbotComponent.AimFovColor.Color);
                                            }
                                            var AimDist2D = GetDistance2D(vScreen_h3ad, GameCenterPos);
                                            if (Components.AimbotComponent.AimFov.Value < AimDist2D) continue;

                                            if (AimDist2D < fClosestPos)
                                            {
                                                fClosestPos = AimDist2D;
                                                AimTarg2D = vScreen_h3ad;


                                                if (Components.AimbotComponent.AimKey.Enabled && Components.AimbotComponent.AimGlobalBool.Enabled && dist > 5 && LTeamNum != TeamNum)
                                                {

                                                    double DistX = AimTarg2D.X - GameCenterPos.X;
                                                    double DistY = (AimTarg2D.Y) - GameCenterPos.Y;

                                                    double slowDistX = DistX / (0.5f + (Math.Abs(DistX) / (1.0f + Components.AimbotComponent.AimSpeed.Value)));
                                                    double slowDistY = DistY / (0.5f + (Math.Abs(DistY) / (1.0f + Components.AimbotComponent.AimSpeed.Value)));
                                                    Input.mouse_eventWS(MouseEventFlags.MOVE, (int)slowDistX, (int)slowDistY, MouseEventDataXButtons.NONE, IntPtr.Zero);

                                                    //Vector3 Aimassist = new Vector3()

                                                }

                                            }
                                        }

                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}