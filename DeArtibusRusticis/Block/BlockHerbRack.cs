using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace DeArtibusRusticis;

public class BlockHerbRack : Block
{
    public override bool DoPartialSelection(IWorldAccessor world, BlockPos pos)
    {
        return true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        return GetBlockEntity<BlockEntityHerbRack>(blockSel.Position)?.OnInteract(byPlayer, blockSel)
            ?? base.OnBlockInteractStart(world, byPlayer, blockSel);
    }
}
