using System.Threading.Tasks;
using Fusion.Menu;

namespace MultiClimb.MenuFlow
{
    public interface IMenuFlowService
    {
        Task QuickPlayAsync(IFusionMenuConnectArgs args, IFusionMenuConnection connection, IFusionMenuUIController controller);
        Task ConnectWithCurrentArgsAsync(IFusionMenuConnectArgs args, IFusionMenuConnection connection, IFusionMenuUIController controller, string entryPoint);
        void OnBeforeConnect(IFusionMenuConnectArgs args, string source);
        void OnBeforeDisconnect(IFusionMenuConnection connection, int reason, string source);
    }
}