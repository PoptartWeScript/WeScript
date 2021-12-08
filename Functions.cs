using System;
using WeScriptWrapper;


namespace DeadByDaylight
{
    public class Functions
    {       
        public static void Ppc()
        {
            if (Program.GWorldPtr != IntPtr.Zero)
            {



                var UGameInstance = Memory.ZwReadPointer(Program.processHandle, (IntPtr)(Program.GWorldPtr.ToInt64() + Offsets.UE.UWorld.OwningGameInstance), true);
                if (UGameInstance != IntPtr.Zero)
                {
                    var localPlayerArray = Memory.ZwReadPointer(Program.processHandle, (IntPtr)(UGameInstance.ToInt64() + Offsets.UE.UGameInstance.LocalPlayers), true);
                    if (localPlayerArray != IntPtr.Zero)
                    {
                        var ULocalPlayer = Memory.ZwReadPointer(Program.processHandle, localPlayerArray, true);
                        if (ULocalPlayer != IntPtr.Zero)
                        {
                            var ULocalPlayerControler = Memory.ZwReadPointer(Program.processHandle, (IntPtr)(ULocalPlayer.ToInt64() + Offsets.UE.UPlayer.PlayerController), true);

                            if (ULocalPlayerControler != IntPtr.Zero)
                            {
                                var Upawn = Memory.ZwReadPointer(Program.processHandle, (IntPtr)(ULocalPlayerControler.ToInt64() + Offsets.UE.APlayerController.AcknowledgedPawn), true);
                                var UplayerState = Memory.ZwReadPointer(Program.processHandle, (IntPtr)(Upawn.ToInt64() + Offsets.UE.APawn.PlayerState), true);



                                //ControllerRotation = Memory.ZwReadPointer(Program.processHandle, (IntPtr)(ULocalPlayerControler.ToInt64() + Offsets.UE.AController.ControlRotation), true);
                                //var ULocalPlayerPawn = Memory.ZwReadPointer(processHandle, (IntPtr)(ULocalPlayerControler.ToInt64() + Offsets.UE.AController.Character), true);



                                var APlayerCameraManager = Memory.ZwReadPointer(Program.processHandle, (IntPtr)ULocalPlayerControler.ToInt64() + 0x2D0, true);
                                if (APlayerCameraManager != IntPtr.Zero)
                                {
                                    Program.FMinimalViewInfo_Location = Memory.ZwReadVector3(Program.processHandle, (IntPtr)APlayerCameraManager.ToInt64() + 0x1A80 + 0x0000);
                                    //Console.WriteLine(Program.FMinimalViewInfo_Location);

                                    Program.FMinimalViewInfo_Rotation = Memory.ZwReadVector3(Program.processHandle, (IntPtr)APlayerCameraManager.ToInt64() + 0x1A80 + 0x000C);
                                    // Console.WriteLine(Program.FMinimalViewInfo_Rotation);
                                    Program.FMinimalViewInfo_FOV = Memory.ZwReadFloat(Program.processHandle, (IntPtr)APlayerCameraManager.ToInt64() + 0x1A80 + 0x0018);
                                    //Console.WriteLine(Program.FMinimalViewInfo_FOV);

                                }

                            }

                        }
                    }


                }

            }
        }
    }
}
