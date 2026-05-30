using Fusion.Menu;

namespace MultiClimb.MenuFlow
{
    public interface IMenuFlowLogger
    {
        void LogBeforeConnect(IFusionMenuConnectArgs args, string source);
        void LogAfterConnect(ConnectResult result, string source);
        void LogBeforeDisconnect(IFusionMenuConnection connection, int reason, string source);
    }
}