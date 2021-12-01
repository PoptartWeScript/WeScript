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

namespace DeadByDaylight
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
        public static DateTime LastSpacePressedDT = DateTime.Now;
        public static IntPtr GWorldPtr = IntPtr.Zero;
        public static IntPtr GNamesPtr = IntPtr.Zero;
        public static uint Health = 0;

        public static Vector3 FMinimalViewInfo_Location = new Vector3(0, 0, 0);
        public static Vector3 FMinimalViewInfo_Rotation = new Vector3(0, 0, 0);
        public static float FMinimalViewInfo_FOV = 0;

        public static uint survivorID = 0;
        public static uint killerID = 0;
        public static uint escapeID = 0;
        public static uint hatchID = 0;
        public static uint generatorID = 0;
        public static uint TrapID = 0;
        public static uint ChestID = 0;
        public static uint totemID = 0;


        public static Menu RootMenu { get; private set; }
        public static Menu VisualsMenu { get; private set; }
        public static Menu MiscMenu { get; private set; }

        class Components
        {
            public static readonly MenuKeyBind MainAssemblyToggle = new MenuKeyBind("mainassemblytoggle", "Toggle the whole assembly effect by pressing key:", VirtualKeyCode.Delete, KeybindType.Toggle, true);
            public static class VisualsComponent
            {
                public static readonly MenuBool DrawTheVisuals = new MenuBool("drawthevisuals", "Enable all of the Visuals", true);
                public static readonly MenuColor SurvColor = new MenuColor("srvcolor", "Survivors Color", new SharpDX.Color(0, 255, 0, 60));
                public static readonly MenuBool DrawSurvivorBox = new MenuBool("srvbox", "Draw Survivors Box", true);
                public static readonly MenuColor KillerColor = new MenuColor("kilcolor", "Killers Color", new SharpDX.Color(255, 0, 0, 100));
                public static readonly MenuBool DrawKillerBox = new MenuBool("drawbox", "Draw Box ESP", true);
                public static readonly MenuSlider DrawBoxThic = new MenuSlider("boxthickness", "Draw Box Thickness", 0, 0, 10);
                public static readonly MenuBool DrawBoxBorder = new MenuBool("drawboxborder", "Draw Border around Box and Text?", true);
                public static readonly MenuBool DrawMiscInfo = new MenuBool("drawmiscinfos", "Draw hatch and escape positions", true);
                public static readonly MenuColor MiscColor = new MenuColor("misccolor", "Draw Text Color", new SharpDX.Color(255, 255, 255, 100));
                public static readonly MenuBool DrawGenerators = new MenuBool("drawgenerators", "Draw Generators positions", true);
                public static readonly MenuBool DrawTrap = new MenuBool("DrawTrap", "Draw Trap positions", true);
                public static readonly MenuBool DrawChest = new MenuBool("DrawChest", "Draw Chest positions", true);
                public static readonly MenuBool DrawTotems = new MenuBool("DrawTotems", "Draw Totems positions", true);
                //public static readonly MenuSlider OffsetGuesser = new MenuSlider("ofsgues", "Guess the offset", 10, 1, 250);
            }

            public static class MiscComponent
            {
                public static readonly MenuBool AutoSkillCheck = new MenuBool("autoskillcheck", "Auto Skill Check (+Bonus)", true);
            }
        }

        public static void InitializeMenu()
        {
            VisualsMenu = new Menu("visualsmenu", "Visuals Menu")
            {
                Components.VisualsComponent.DrawTheVisuals,
                Components.VisualsComponent.SurvColor,
                Components.VisualsComponent.DrawSurvivorBox,
                Components.VisualsComponent.KillerColor,
                Components.VisualsComponent.DrawKillerBox,
                Components.VisualsComponent.DrawBoxThic.SetToolTip("Setting thickness to 0 will let the assembly auto-adjust itself depending on model distance"),
                Components.VisualsComponent.DrawBoxBorder.SetToolTip("Drawing borders may take extra performance (FPS) on low-end computers"),
                Components.VisualsComponent.DrawMiscInfo,
                Components.VisualsComponent.MiscColor,
                Components.VisualsComponent.DrawGenerators,
                Components.VisualsComponent.DrawTrap,
                Components.VisualsComponent.DrawChest,
                Components.VisualsComponent.DrawTotems,
                //Components.VisualsComponent.OffsetGuesser,
            };

            MiscMenu = new Menu("miscmenu", "Misc Menu")
            {
                Components.MiscComponent.AutoSkillCheck
            };


            RootMenu = new Menu("dbdexample", "WeScript.app DeadByDaylight Example Assembly", true)
            {
                Components.MainAssemblyToggle.SetToolTip("The magical boolean which completely disables/enables the assembly!"),
                VisualsMenu,
                MiscMenu,
            };
            RootMenu.Attach();
        }


        public static string GetNameFromID(uint ID) //really bad implementation - probably needs fixing, plus it's better to use it as a dumper once at startup and cache ids
        {
            if (processHandle != IntPtr.Zero)
            {
                if (GameBase != IntPtr.Zero)
                {
                    uint BlockIndex = ID >> 16;
                    var Address = Memory.ZwReadPointer(processHandle, (IntPtr)(GNamesPtr.ToInt64() + 0x10 + BlockIndex * 8), isWow64Process);
                    if (Address != IntPtr.Zero)
                    {
                        var Offset = ID & 65535;
                        var NameAddress = (IntPtr)(Address.ToInt64() + Offset * 4);
                        var tempID = Memory.ZwReadDWORD(processHandle, NameAddress);
                        if (tempID == ID)
                        {
                            var charLen = Memory.ZwReadWORD(processHandle, (IntPtr)(NameAddress.ToInt64() + 4));
                            if (charLen > 0)
                            {
                                var name = Memory.ZwReadString(processHandle, (IntPtr)(NameAddress.ToInt64() + 6), false, charLen);
                                if (name.Length > 0) return name;
                            }
                        }
                    }
                }
            }
            return "NULL";
        }

        static void Main(string[] args)
        {
            Console.WriteLine("WeScript.app experimental DBD assembly for patch 4.0.2 with Driver bypass");
            InitializeMenu();
            if (!Memory.InitDriver(DriverName.nsiproxy))
            {
                Console.WriteLine("[ERROR] Failed to initialize driver for some reason...");
            }

            if (!Memory.HWIDSpoofer(HWDrvName.btbd_hwid))
            {
                Console.WriteLine("[ERROR] Failed to initialize HWID Spoofer for some reason...");
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
                var wndHnd = Memory.FindWindowName("DeadByDaylight  "); //why the devs added spaces after the name?!
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
                var wndHnd = Memory.FindWindowName("DeadByDaylight  "); //why the devs added spaces after the name?!
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
                        Console.WriteLine("Got GAMEBASE of DBD!");
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
                                GWorldPtr = Memory.ZwReadPointer(processHandle, GameBase + 0x9E24E60, isWow64Process);
                            }
                            if (GNamesPtr == IntPtr.Zero)
                            {
                                //GNamesPtr = Memory.ZwFindSignature(processHandle, GameBase, GameSize, "48 8B 05 ? ? ? ? 48 85 C0 75 5F", 0x3); //4.1 patch
                                GNamesPtr = GameBase + 0x9C558C0;
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
;
            var myPos = new Vector3();
            var USkillCheck = IntPtr.Zero;
            if (GWorldPtr != IntPtr.Zero)
            {
                var UGameInstance = Memory.ZwReadPointer(processHandle, GWorldPtr + 0x198, isWow64Process);
                if (UGameInstance != IntPtr.Zero)
                {
                    var localPlayerArray = Memory.ZwReadPointer(processHandle, (IntPtr)UGameInstance.ToInt64() + 0x40,
                        isWow64Process);
                    if (localPlayerArray != IntPtr.Zero)
                    {
                        var ULocalPlayer = Memory.ZwReadPointer(processHandle, localPlayerArray, isWow64Process);
                        if (ULocalPlayer != IntPtr.Zero)
                        {
                            var ULocalPlayerControler = Memory.ZwReadPointer(processHandle,
                                (IntPtr)ULocalPlayer.ToInt64() + 0x0038, isWow64Process);
                            if (ULocalPlayerControler != IntPtr.Zero)
                            {
                                var ULocalPlayerPawn = Memory.ZwReadPointer(processHandle,
                                    (IntPtr)ULocalPlayerControler.ToInt64() + 0x0268, isWow64Process);
                                if (ULocalPlayerPawn != IntPtr.Zero)
                                {
                                    var UInteractionHandler = Memory.ZwReadPointer(processHandle,
                                        (IntPtr)ULocalPlayerPawn.ToInt64() + 0x08E8, isWow64Process);

                                    if (UInteractionHandler != IntPtr.Zero)
                                    {
                                        USkillCheck = Memory.ZwReadPointer(processHandle,
                                            (IntPtr)UInteractionHandler.ToInt64() + 0x02D8, isWow64Process);
                                    }

                                    var ULocalRoot = Memory.ZwReadPointer(processHandle,
                                        (IntPtr)ULocalPlayerPawn.ToInt64() + 0x0140, isWow64Process);
                                    if (ULocalRoot != IntPtr.Zero)
                                    {
                                        myPos = Memory.ZwReadVector3(processHandle,
                                            (IntPtr)ULocalRoot.ToInt64() + 0x0118);
                                    }
                                }

                                var APlayerCameraManager = Memory.ZwReadPointer(processHandle,
                                    (IntPtr)ULocalPlayerControler.ToInt64() + 0x2D0, isWow64Process);
                                if (APlayerCameraManager != IntPtr.Zero)
                                {
                                    FMinimalViewInfo_Location = Memory.ZwReadVector3(processHandle,
                                        (IntPtr)APlayerCameraManager.ToInt64() + 0x1A80 + 0x0000);
                                    FMinimalViewInfo_Rotation = Memory.ZwReadVector3(processHandle,
                                        (IntPtr)APlayerCameraManager.ToInt64() + 0x1A80 + 0x000C);
                                    FMinimalViewInfo_FOV = Memory.ZwReadFloat(processHandle,
                                        (IntPtr)APlayerCameraManager.ToInt64() + 0x1A80 + 0x0018);
                                }
                            }

                            //var CameraPtr = Memory.ZwReadPointer(processHandle, (IntPtr)ULocalPlayer.ToInt64() + 0xB8, isWow64Process);
                            //if (CameraPtr != IntPtr.Zero)
                            //{
                            //    viewProj = Memory.ZwReadMatrix(processHandle, (IntPtr)(CameraPtr.ToInt64() + 0x1FC));
                            //}
                        }
                    }
                }

                if (Components.MiscComponent.AutoSkillCheck.Enabled)
                {
                    if (USkillCheck != IntPtr.Zero)
                    {
                        //Console.WriteLine($"USkillCheck: {USkillCheck.ToString("X")}");
                        var isDisplayed = Memory.ZwReadBool(processHandle,
                            (IntPtr)USkillCheck.ToInt64() + 0x148);

                        Console.WriteLine(isDisplayed);
                        if (isDisplayed && LastSpacePressedDT.AddMilliseconds(200) <
                            DateTime.Now)
                        {
                            var currentProgress = Memory.ZwReadFloat(processHandle,
                                (IntPtr)USkillCheck.ToInt64() + 0x14C); //0x02A0
                            var startSuccessZone = Memory.ZwReadFloat(processHandle,
                                (IntPtr)USkillCheck.ToInt64() + 0x19C);

                            if (currentProgress > startSuccessZone)
                            {
                                LastSpacePressedDT = DateTime.Now;
                                Input.KeyPress(VirtualKeyCode.Space);
                            }
                        }
                    }
                }

                var ULevel = Memory.ZwReadPointer(processHandle,GWorldPtr + 0x38, isWow64Process);
                if (ULevel != IntPtr.Zero)
                {
                    var AActors = Memory.ZwReadPointer(processHandle, (IntPtr)ULevel.ToInt64() + 0xA0, isWow64Process);
                    var ActorCnt = Memory.ZwReadUInt32(processHandle, (IntPtr)ULevel.ToInt64() + 0xA8);
                    if ((AActors != IntPtr.Zero) && (ActorCnt > 0))
                    {
                        for (uint i = 0; i <= ActorCnt; i++)
                        {
                            var AActor = Memory.ZwReadPointer(processHandle, (IntPtr)(AActors.ToInt64() + i * 8),
                                isWow64Process);
                            if (AActor != IntPtr.Zero)
                            {
                                var USceneComponent = Memory.ZwReadPointer(processHandle,
                                    (IntPtr)AActor.ToInt64() + 0x140, isWow64Process);
                                if (USceneComponent != IntPtr.Zero)
                                {
                                    var tempVec = Memory.ZwReadVector3(processHandle,
                                        (IntPtr)USceneComponent.ToInt64() + 0x118);
                                    var AActorID = Memory.ZwReadUInt32(processHandle,
                                        (IntPtr)AActor.ToInt64() + 0x18);

                                    var HealthPointer = Memory.ZwReadPointer(processHandle, (IntPtr)AActor.ToInt64() + 0x1390, isWow64Process);
                                    bool IsActorDead = Memory.ZwReadBool(processHandle, (IntPtr)HealthPointer.ToInt64() + 0x01E4);
                                    var Healthy = Memory.ZwReadByte(processHandle, (IntPtr)HealthPointer.ToInt64() + 0x01E0);
                                    var HasBeenSearched = Memory.ZwReadBool(processHandle, (IntPtr)AActor.ToInt64() + 0x0364);
                                    var IsCleansed = Memory.ZwReadByte(processHandle, (IntPtr)AActor.ToInt64() + 0x0368);
                                    var HasBeenSet = Memory.ZwReadBool(processHandle, (IntPtr)AActor.ToInt64() + 0x04C8);

                                    string retname = "";
                                    if ((AActorID > 0)) //&& (AActorID < 700000)
                                    {
                                        if ((survivorID == 0) || (killerID == 0) || (escapeID == 0) ||
                                            (hatchID == 0) || (generatorID == 0) || (TrapID == 0) || (totemID == 0) || (ChestID == 0))
                                        {

                                            retname = GetNameFromID(AActorID);
                                            if (retname.Contains("BP_CamperFemale")) survivorID = AActorID;
                                            if (retname.Contains("BP_CamperMale")) survivorID = AActorID;
                                            if (retname.Contains("SlasherInteractable_")) killerID = AActorID;
                                            if (retname.Contains("BP_Escape01"))escapeID = AActorID;
                                            if (retname.Contains("BP_Hatch"))hatchID = AActorID;                                           
                                            if (retname.StartsWith("Generator"))generatorID = AActorID;
                                            if (retname.Contains("BearTrap") || retname.Contains("PhantomTrap") || retname.Contains("DreamSnare")) TrapID = AActorID;
                                            if (retname.ToLower().Contains("hexspawner") || retname.ToLower().Contains("totem")) totemID = AActorID;
                                            if (retname.ToLower().Contains("chest")) ChestID = AActorID;

                                        }

                                    }

                                    if (Components.VisualsComponent.DrawTheVisuals.Enabled) //this should have been placed earlier?
                                    {

                                        int dist = (int)(GetDistance3D(myPos, tempVec) - 5);
                                        if (AActorID == survivorID)
                                        {

                                            Vector2 vScreen_h3ad = new Vector2(0, 0);
                                            Vector2 vScreen_f33t = new Vector2(0, 0);
                                            if (Renderer.WorldToScreenUE4(new Vector3(tempVec.X, tempVec.Y, tempVec.Z + 60.0f), out vScreen_h3ad, FMinimalViewInfo_Location, FMinimalViewInfo_Rotation, FMinimalViewInfo_FOV, wndMargins, wndSize))
                                            {
                                                string survivorname = "SURVIVOR";
                                                if (retname.Contains("BP_CamperFemale01_Character")) survivorname = "Meg";
                                                if (retname.Contains("BP_CamperFemale02_Character")) survivorname = "Claudette";
                                                if (retname.Contains("BP_CamperFemale03_Character")) survivorname = "Nea";
                                                if (retname.Contains("BP_CamperFemale04_Character")) survivorname = "Laurie";
                                                if (retname.Contains("BP_CamperFemale05_Character")) survivorname = "Feng";
                                                if (retname.Contains("BP_CamperFemale06_Character")) survivorname = "Kate";
                                                if (retname.Contains("BP_CamperFemale07_Character")) survivorname = "Jane";
                                                if (retname.Contains("BP_CamperFemale08_Character")) survivorname = "Nancy";
                                                if (retname.Contains("BP_CamperFemale09_Character")) survivorname = "Yui";
                                                if (retname.Contains("BP_CamperFemale10_Character")) survivorname = "Zarina";
                                                if (retname.Contains("BP_CamperFemale11_Character")) survivorname = "Cheryl";
                                                if (retname.Contains("BP_CamperFemale12_Character")) survivorname = "Elodie";
                                                if (retname.Contains("BP_CamperMale01")) survivorname = "Dwight";
                                                if (retname.Contains("BP_CamperMale02_Character")) survivorname = "Jake";
                                                if (retname.Contains("BP_CamperMale03_Character")) survivorname = "Ace";
                                                if (retname.Contains("BP_CamperMale04_Character")) survivorname = "Bill";
                                                if (retname.Contains("BP_CamperMale05_Character")) survivorname = "David";
                                                if (retname.Contains("BP_CamperMale06_Character")) survivorname = "Quentin";
                                                if (retname.Contains("BP_CamperMale07_Character")) survivorname = "Tapp";
                                                if (retname.Contains("BP_CamperMale08_Character")) survivorname = "Adam";
                                                if (retname.Contains("BP_CamperMale09_Character")) survivorname = "Jeff";
                                                if (retname.Contains("BP_CamperMale10_Character")) survivorname = "Ashley";
                                                if (retname.Contains("BP_CamperMale11_Character")) survivorname = "Steve";
                                                if (retname.Contains("BP_CamperMale12_Character")) survivorname = "Felix";
                                                Renderer.WorldToScreenUE4(new Vector3(tempVec.X, tempVec.Y, tempVec.Z - 130.0f), out vScreen_f33t, FMinimalViewInfo_Location, FMinimalViewInfo_Rotation, FMinimalViewInfo_FOV, wndMargins, wndSize);
                                                if (Components.VisualsComponent.DrawSurvivorBox.Enabled && !IsActorDead && dist > 5)
                                                {
                                                    if (Healthy == 2) Renderer.DrawFPSBox(vScreen_h3ad, vScreen_f33t, Color.Green, BoxStance.standing, Components.VisualsComponent.DrawBoxThic.Value, Components.VisualsComponent.DrawBoxBorder.Enabled);
                                                    if (Healthy == 1) Renderer.DrawFPSBox(vScreen_h3ad, vScreen_f33t, Color.Yellow, BoxStance.standing, Components.VisualsComponent.DrawBoxThic.Value, Components.VisualsComponent.DrawBoxBorder.Enabled);
                                                    if (Healthy == 0) Renderer.DrawFPSBox(vScreen_h3ad, vScreen_f33t, Color.Red, BoxStance.standing, Components.VisualsComponent.DrawBoxThic.Value, Components.VisualsComponent.DrawBoxBorder.Enabled);
                                                    Renderer.DrawText(survivorname + "[" + dist + "m]", vScreen_f33t.X, vScreen_f33t.Y + 5, Components.VisualsComponent.SurvColor.Color, 12, TextAlignment.centered, false);
                                                }
                                            }
                                        }

                                        if (AActorID == killerID)
                                        {
                                            Vector2 vScreen_h3ad = new Vector2(0, 0);
                                            Vector2 vScreen_f33t = new Vector2(0, 0);
                                            if (Renderer.WorldToScreenUE4(
                                                new Vector3(tempVec.X, tempVec.Y, tempVec.Z + 80.0f),
                                                out vScreen_h3ad, FMinimalViewInfo_Location, FMinimalViewInfo_Rotation,
                                                FMinimalViewInfo_FOV, wndMargins, wndSize))
                                            {
                                                Renderer.WorldToScreenUE4(
                                                    new Vector3(tempVec.X, tempVec.Y, tempVec.Z - 150.0f),
                                                    out vScreen_f33t, FMinimalViewInfo_Location,
                                                    FMinimalViewInfo_Rotation, FMinimalViewInfo_FOV, wndMargins,
                                                    wndSize);

                                                string killername = "KILLER";
                                                if (retname.Contains("BP_Slasher_Character_01_C")) killername = "Trapper";
                                                if (retname.Contains("BP_Slasher_Character_02_C")) killername = "Wraith";
                                                if (retname.Contains("BP_Slasher_Character_03_C")) killername = "Hillbilly";
                                                if (retname.Contains("BP_Slasher_Character_04_C")) killername = "Nurse";
                                                if (retname.Contains("BP_Slasher_Character_05_C")) killername = "Hag";
                                                if (retname.Contains("BP_Slasher_Character_06_C")) killername = "Myers";
                                                if (retname.Contains("BP_Slasher_Character_07_C")) killername = "Doctor";
                                                if (retname.Contains("BP_Slasher_Character_08_C")) killername = "Huntress";
                                                if (retname.Contains("BP_Slasher_Character_09_C")) killername = "Leatherface";
                                                if (retname.Contains("BP_Slasher_Character_10_C")) killername = "Freddy";
                                                if (retname.Contains("BP_Slasher_Character_11_C")) killername = "Pig";
                                                if (retname.Contains("BP_Slasher_Character_12_C")) killername = "Clown";
                                                if (retname.Contains("BP_Slasher_Character_13_C")) killername = "Spirit";
                                                if (retname.Contains("BP_Slasher_Character_14_C")) killername = "Legion";
                                                if (retname.Contains("BP_Slasher_Character_15_C")) killername = "Plague";
                                                if (retname.Contains("BP_Slasher_Character_16_C")) killername = "Ghost Face";
                                                if (retname.Contains("BP_Slasher_Character_17_C")) killername = "Demogorgon";
                                                if (retname.Contains("BP_Slasher_Character_18_C")) killername = "Oni";
                                                if (retname.Contains("BP_Slasher_Character_19_C")) killername = "Deathslinger";
                                                if (retname.Contains("BP_Slasher_Character_20_C")) killername = "Pyramid Head";
                                                if (retname.Contains("BP_Slasher_Character_21_C")) killername = "Blight";
                                                if (retname.Contains("BP_Slasher_Character_22_C")) killername = "Twins";
                                                if (retname.Contains("Twin")) killername = "Victor";
                                                if (Components.VisualsComponent.DrawKillerBox.Enabled)
                                                {
                                                    Renderer.DrawFPSBox(vScreen_h3ad, vScreen_f33t,
                                                        Components.VisualsComponent.KillerColor.Color,
                                                        BoxStance.standing,
                                                        Components.VisualsComponent.DrawBoxThic.Value,
                                                        Components.VisualsComponent.DrawBoxBorder.Enabled);
                                                    Renderer.DrawText(killername + "[" + dist + "m]", vScreen_f33t.X,
                                                        vScreen_f33t.Y + 5,
                                                        Components.VisualsComponent.KillerColor.Color, 12,
                                                        TextAlignment.centered, false);
                                                }
                                            }
                                        }

                                        if (AActorID == TrapID)
                                        {
                                            if (Components.VisualsComponent.DrawTrap.Enabled)
                                            {
                                                Vector2 vScreen_d3d11 = new Vector2(0, 0);
                                                if (Renderer.WorldToScreenUE4(tempVec, out vScreen_d3d11,
                                                    FMinimalViewInfo_Location, FMinimalViewInfo_Rotation,
                                                    FMinimalViewInfo_FOV,
                                                    wndMargins, wndSize))
                                                {
                                                    if (HasBeenSet) Renderer.DrawText("Trap [" + dist + "m]", vScreen_d3d11, Color.OrangeRed, 12, TextAlignment.centered, false);
                                                    if (!HasBeenSet) Renderer.DrawText("Trap [" + dist + "m]", vScreen_d3d11, Color.WhiteSmoke, 12, TextAlignment.centered, false);

                                                }
                                            }
                                        }

                                        if (AActorID == ChestID)
                                        {
                                            if (Components.VisualsComponent.DrawChest.Enabled)
                                            {
                                                Vector2 vScreen_d3d11 = new Vector2(0, 0);
                                                if (Renderer.WorldToScreenUE4(tempVec, out vScreen_d3d11,
                                                    FMinimalViewInfo_Location, FMinimalViewInfo_Rotation,
                                                    FMinimalViewInfo_FOV,
                                                    wndMargins, wndSize))
                                                {
                                                    if (!HasBeenSearched) Renderer.DrawText("Chest [" + dist + "m]", vScreen_d3d11, Color.HotPink, 12, TextAlignment.centered, false);
                                                }
                                            }
                                            }

                                            if (AActorID == totemID)
                                        {
                                            if (Components.VisualsComponent.DrawTotems.Enabled && IsCleansed > 0)
                                            {
                                                Vector2 vScreen_d3d11 = new Vector2(0, 0);
                                                if (Renderer.WorldToScreenUE4(tempVec, out vScreen_d3d11,
                                                    FMinimalViewInfo_Location, FMinimalViewInfo_Rotation,
                                                    FMinimalViewInfo_FOV,
                                                    wndMargins, wndSize))
                                                {
                                                    if (IsCleansed == 1) Renderer.DrawText("Dull [" + dist + "m]", vScreen_d3d11, Color.Tan, 12, TextAlignment.centered, false);
                                                    if (IsCleansed == 2) Renderer.DrawText("Hex [" + dist + "m]", vScreen_d3d11, Color.OrangeRed, 12, TextAlignment.centered, false);
                                                    if (IsCleansed == 3) Renderer.DrawText("Boon [" + dist + "m]", vScreen_d3d11, Color.DeepSkyBlue, 12, TextAlignment.centered, false);
                                                }
                                            }
                                        }

                                        if (Components.VisualsComponent.DrawMiscInfo.Enabled)
                                        {
                                            if (AActorID == escapeID)
                                            {
                                                Vector2 vScreen_d3d11 = new Vector2(0, 0);
                                                if (Renderer.WorldToScreenUE4(tempVec, out vScreen_d3d11,
                                                    FMinimalViewInfo_Location, FMinimalViewInfo_Rotation,
                                                    FMinimalViewInfo_FOV,
                                                    wndMargins, wndSize))
                                                {
                                                    Renderer.DrawText("ESCAPE [" + dist + "m]", vScreen_d3d11,
                                                        Components.VisualsComponent.MiscColor.Color, 12,
                                                        TextAlignment.centered, false);
                                                }
                                            }
                                            else if (AActorID == hatchID)
                                            {
                                                Vector2 vScreen_d3d11 = new Vector2(0, 0);
                                                if (Renderer.WorldToScreenUE4(tempVec, out vScreen_d3d11,
                                                    FMinimalViewInfo_Location, FMinimalViewInfo_Rotation,
                                                    FMinimalViewInfo_FOV,
                                                    wndMargins, wndSize))
                                                {
                                                    Renderer.DrawText("HATCH [" + dist + "m]", vScreen_d3d11,
                                                        Components.VisualsComponent.MiscColor.Color, 12,
                                                        TextAlignment.centered, false);
                                                }
                                            }
                                        }

                                        if (Components.VisualsComponent.DrawGenerators.Enabled)
                                        {
                                            if (AActorID != generatorID)
                                                continue;
                                            var isRepaired =
                                                Memory.ZwReadBool(processHandle, (IntPtr)AActor.ToInt64() + 0x0339);
                                            var isBlocked = Memory.ZwReadBool(processHandle,
                                                (IntPtr)AActor.ToInt64() + 0x0464);

                                            var currentProgressPercent =
                                                Memory.ZwReadFloat(processHandle, (IntPtr)AActor.ToInt64() + 0x0348) *
                                                100;
                                            Color selectedColor;
                                            if (isBlocked)
                                                selectedColor = Color.Red;
                                            else if (isRepaired)
                                                selectedColor = Color.Green;
                                            else
                                                selectedColor = Color.Yellow;
                                            Vector2 vScreen_d3d11 = new Vector2(0, 0);
                                            if (Renderer.WorldToScreenUE4(tempVec, out vScreen_d3d11,
                                                FMinimalViewInfo_Location, FMinimalViewInfo_Rotation,
                                                FMinimalViewInfo_FOV,
                                                wndMargins, wndSize))
                                            {
                                                if (!isRepaired)
                                                    Renderer.DrawText($"Generator [{dist}m] ({currentProgressPercent:##0}%)",
                                                        vScreen_d3d11,
                                                        selectedColor, 15, TextAlignment.centered, false);
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
