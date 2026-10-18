namespace FiveVibe
{
    public enum CharacterSwitchResult { Waiting, Confirmed, Unconfirmed }

    // The native transition flag can be false while the wheel/loading stage is
    // still pending. Only the actual protagonist and playable state confirm it.
    public sealed class CharacterSwitchMonitor
    {
        public const int StableMilliseconds=750;
        public const int TimeoutMilliseconds=120000;
        private int readySince=-1;

        public void Reset() { readySince=-1; }

        public CharacterSwitchResult Observe(int elapsed,bool targetActive,bool nativeTransition,bool controllable,bool sceneReady)
        {
            if(targetActive && !nativeTransition && controllable && sceneReady)
            {
                if(readySince<0) readySince=elapsed;
                if(elapsed-readySince>=StableMilliseconds) return CharacterSwitchResult.Confirmed;
            }
            else readySince=-1;

            // A late target gets the full short stability check, even if it first
            // appears immediately before the overall deadline.
            if(elapsed>=TimeoutMilliseconds && readySince<0) return CharacterSwitchResult.Unconfirmed;
            return CharacterSwitchResult.Waiting;
        }
    }
}
