namespace ReservationChecks
{
    internal sealed class Character : Obj { }
    internal interface IDoodadController { }
    internal sealed class GameCamera : Obj {internal static GameCamera instance=new GameCamera();}
    internal sealed class Localization {internal static Localization instance=new Localization();internal string Localize(string value)=>value;}
    internal sealed partial class Player
    {
        internal GameObject NativeRayHit=null;
        internal int NativeHoverQueries=0;
        internal bool InPlaceMode()=>BlueprintConstructionProjectRuntime.Hammer;
        private void FindHoverObject(out GameObject hover,out Character creature){NativeHoverQueries++;hover=NativeRayHit;creature=null;}
    }
}
