using System;
using WeScriptWrapper;


namespace DeadByDaylight
{
    public class Functions
    {
        public static float Rad2Deg(float rad)
        {
            return (float)(rad * 180.0f / Math.PI);
        }

        public static float Deg2Rad(float deg)
        {
            return (float)(deg * Math.PI / 180.0f);
        }

        public static float atanf(float X)
        {
            return (float)Math.Atan(X);
        }

        public static float tanf(float X)
        {
            return (float)Math.Tan(X);
        }

        public static void Ppc()
        {


            if (Program.GWorldPtr != IntPtr.Zero)
            {
                Program.UGameInstance = Memory.ZwReadPointer(Program.processHandle, (IntPtr)(Program.GWorldPtr.ToInt64() + Offsets.UE.UWorld.OwningGameInstance), true);
                if (Program.UGameInstance != IntPtr.Zero)
                {
                    Program.localPlayerArray = Memory.ZwReadPointer(Program.processHandle, (IntPtr)(Program.UGameInstance.ToInt64() + Offsets.UE.UGameInstance.LocalPlayers),true);
                    if (Program.localPlayerArray != IntPtr.Zero)
                    {
                        Program.ULocalPlayer = Memory.ZwReadPointer(Program.processHandle, Program.localPlayerArray, true);
                        if (Program.ULocalPlayer != IntPtr.Zero)
                        {
                            Program.ULocalPlayerControler = Memory.ZwReadPointer(Program.processHandle,
                                (IntPtr)(Program.ULocalPlayer.ToInt64() + Offsets.UE.UPlayer.PlayerController), true);
                            if (Program.ULocalPlayerControler != IntPtr.Zero)
                            {
                                Program.ULocalPlayerPawn = Memory.ZwReadPointer(Program.processHandle,
                                    (IntPtr)(Program.ULocalPlayerControler.ToInt64() + Offsets.UE.APlayerController.APawn), true); //0x0268
                                if (Program.ULocalPlayerPawn != IntPtr.Zero)
                                {
                                    Program.UInteractionHandler = Memory.ZwReadPointer(Program.processHandle,
                                        (IntPtr)(Program.ULocalPlayerPawn.ToInt64() + Offsets.UE.APawn.UPlayerInteractionHandler), true);
                                    if (Program.UInteractionHandler != IntPtr.Zero)
                                    {
                                        Program.USkillCheck = Memory.ZwReadPointer(Program.processHandle,
                                            (IntPtr)(Program.UInteractionHandler.ToInt64() + Offsets.UE.UPlayerInteractionHandler._skillCheck), true);
                                    }
                                    Program.ULocalRoot = Memory.ZwReadPointer(Program.processHandle,
                                        (IntPtr)(Program.ULocalPlayerPawn.ToInt64() + Offsets.UE.AActor.ULocalRoot), true);
                                    if (Program.ULocalRoot != IntPtr.Zero)
                                    {
                                        Program.myPos = Memory.ZwReadVector3(Program.processHandle,
                                            (IntPtr)Program.ULocalRoot.ToInt64() + 0x0118);
                                    }
                                }
                                Program.APlayerCameraManager = Memory.ZwReadPointer(Program.processHandle,
                                    (IntPtr)(Program.ULocalPlayerControler.ToInt64() + Offsets.UE.APlayerController.PlayerCameraManager), true);
                                if (Program.APlayerCameraManager != IntPtr.Zero)
                                {
                                    Program.FMinimalViewInfo_Location = Memory.ZwReadVector3(Program.processHandle,(IntPtr)(Program.APlayerCameraManager.ToInt64() + Offsets.UE.APlayerCameraManager.CameraCachePrivate) + 0x0000);
                                    Program.FMinimalViewInfo_Rotation = Memory.ZwReadVector3(Program.processHandle, (IntPtr)(Program.APlayerCameraManager.ToInt64() + Offsets.UE.APlayerCameraManager.CameraCachePrivate) + 0x000C);
                                    Program.FMinimalViewInfo_FOV = Memory.ZwReadFloat(Program.processHandle, (IntPtr)(Program.APlayerCameraManager.ToInt64() + Offsets.UE.APlayerCameraManager.CameraCachePrivate) + 0x0018);
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

