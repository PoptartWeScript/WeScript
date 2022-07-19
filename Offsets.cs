using System;
namespace DeadByDaylight
{

public class Offsets

{
   public static Int64 UWorld = 0xbe0a4e0;
   public static Int64 GNames = 0xbc2b8c0;

public class UE
{

   public class UWorld
{
       public static Int64 OwningGameInstance = 0x190;
       public static Int64 ULevel = 0x38;
}

   public class ULevel
{
       public static Int64 AActors = 0xA0;
       public static Int64 AActorsCount = 0xA8;
}

   public class UGameInstance
{
       public static Int64 LocalPlayers = 0x40;
}

   public class UPlayer
{
       public static Int64 PlayerController = 0x38;
}

   public class APlayerController
{
       public static Int64 APawn = 0x2b8;
       public static Int64 PlayerCameraManager = 0x2d0;
}

   public class APawn
{
       public static Int64 UPlayerInteractionHandler = 0x8f8;
}

   public class UPlayerInteractionHandler
{
       public static Int64 _skillCheck = 0x2e8;
}

   public class AActor
{
       public static Int64 ULocalRoot = 0x140;
       public static Int64 USceneComponent = 0x140;
       public static Int64 tempVec= 0x118; //but weirdly is  = 0x0;
       public static Int64 HealthPointer = 0x13F8;
       public static Int64 IsActorDead = 0x1E4;
       public static Int64 Healthy = 0x1E0;
       public static Int64 HasBeenSearched = 0x364;
       public static Int64 IsCleansed = 0x378;
       public static Int64 isRepaired = 0x339;
       public static Int64 HasBeenSet = 0x4b0;
       public static Int64 currentProgressPercent = 0x348;
}

   public class APlayerCameraManager
{
       public static Int64 CameraCachePrivate = 0x1B00;
}

}
}
}
