using DeArtibusRusticis.Blocks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace DeArtibusRusticis;

public class DeArtibusRusticisModSystem : ModSystem {

    public override void Start(ICoreAPI api) {
        api.RegisterBlockBehaviorClass("DAR.ContainerNoGui", typeof(BlockBehaviorContainerNoGui));
        api.RegisterBlockEntityClass("DAR.GenericContainerNoGui", typeof(BEGenericContainerNoGui));
        api.RegisterBlockClass("DAR.MultiSelection", typeof(BlockMultiSelection));
    }

    public override void StartServerSide(ICoreServerAPI api) { }

    public override void StartClientSide(ICoreClientAPI api) { }
}