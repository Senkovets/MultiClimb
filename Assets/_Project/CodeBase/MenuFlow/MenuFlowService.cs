using System.Threading.Tasks;
using Fusion.Menu;

namespace MultiClimb.MenuFlow
{
    public class MenuFlowService : IMenuFlowService
    {
        private readonly IMenuFlowLogger _logger;

        public MenuFlowService(IMenuFlowLogger logger)
        {
            _logger = logger;
        }

        public async Task QuickPlayAsync(IFusionMenuConnectArgs args, IFusionMenuConnection connection, IFusionMenuUIController controller)
        {
            args.Session = null;
            args.Creating = false;
            args.Region = args.PreferredRegion;

            await ConnectWithCurrentArgsAsync(args, connection, controller, "QuickPlay");
        }

        public async Task ConnectWithCurrentArgsAsync(IFusionMenuConnectArgs args, IFusionMenuConnection connection, IFusionMenuUIController controller, string entryPoint)
        {
            _logger.LogBeforeConnect(args, entryPoint);

            controller.Show<FusionMenuUILoading>();

            var result = await connection.ConnectAsync(args);

            _logger.LogAfterConnect(result, entryPoint);

            await FusionMenuUIMain.HandleConnectionResult(result, controller);
        }

        public void OnBeforeConnect(IFusionMenuConnectArgs args, string source)
        {
            _logger.LogBeforeConnect(args, source);
        }

        public void OnBeforeDisconnect(IFusionMenuConnection connection, int reason, string source)
        {
            _logger.LogBeforeDisconnect(connection, reason, source);
        }
    }
}