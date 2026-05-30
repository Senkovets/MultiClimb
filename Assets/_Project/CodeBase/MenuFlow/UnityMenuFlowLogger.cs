using Fusion.Menu;
using UnityEngine;

namespace MultiClimb.MenuFlow
{
    public sealed class UnityMenuFlowLogger : IMenuFlowLogger
    {
        public void LogBeforeConnect(IFusionMenuConnectArgs args, string source)
        {
            Debug.Log($"[MenuFlow] Connect requested from '{source}'. Scene='{args.Scene.SceneName}', Session='{args.Session ?? "<quick-play>"}', Creating={args.Creating}, Region='{args.Region}'");
        }

        public void LogAfterConnect(ConnectResult result, string source)
        {
            Debug.Log($"[MenuFlow] Connect completed from '{source}'. Success={result.Success}, FailReason={result.FailReason}, CustomHandling={result.CustomResultHandling}");
        }

        public void LogBeforeDisconnect(IFusionMenuConnection connection, int reason, string source)
        {
            Debug.Log($"[MenuFlow] Disconnect requested from '{source}'. Session='{connection?.SessionName}', Reason={reason}");
        }
    }
}