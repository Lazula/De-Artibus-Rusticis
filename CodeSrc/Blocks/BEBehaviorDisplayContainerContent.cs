using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace DeArtibusRusticis.Blocks;

public class BEBehaviorDisplayContainerContent : BlockEntityBehavior {

    public BEBehaviorDisplayContainerContent(BlockEntity blockentity) : base(blockentity) { }

    public override void Initialize(ICoreAPI api, JsonObject properties) {
        base.Initialize(api, properties);
        if (api is ICoreClientAPI capi) {
            // capi.
        }
        //TODO
    }

}