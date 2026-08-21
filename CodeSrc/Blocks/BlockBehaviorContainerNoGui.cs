using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace DeArtibusRusticis.Blocks;

public class BlockBehaviorContainerNoGui : BlockBehavior {
    
    private readonly TagSet _allowedCollectibleTags;
    private int _maxTransferPerInteraction;
    // private string 
    
    public BlockBehaviorContainerNoGui(Block block) : base(block) {
        var attribs = block.Attributes;
        _allowedCollectibleTags = attribs["allowedCollectibleTags"].AsObject<TagSet>();
        _maxTransferPerInteraction = 1; //attribs["a"].AsInt();
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling) {
        var playerSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        var stack = playerSlot.Itemstack;
        if (stack == null || _allowedCollectibleTags.Overlaps(stack.Collectible.Tags)) {
            if (world.Side == EnumAppSide.Server) {
                var be = world.BlockAccessor.GetBlockEntity<BlockEntityContainer>(blockSel.Position);
                var containerSlot = be.Inventory[blockSel.SelectionBoxIndex];
                if (containerSlot == null) {
                    world.Logger.Error("[De Artibus Rusticis] Slot out of range");
                    return false;
                }
                if (stack != null) {
                    playerSlot.TryPutInto(world, containerSlot, _maxTransferPerInteraction);
                }
                else {
                    containerSlot.TryPutInto(world, playerSlot, _maxTransferPerInteraction);
                }
                
            }
            handling = EnumHandling.Handled;
            return true;
        }
        return false;
    }

    public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer) {
        var be = world.BlockAccessor.GetBlockEntity<BlockEntityContainer>(pos);
        var inv = be.Inventory;
        var desc = new StringBuilder();
        for (int i = 0; i < inv.Count; i++) {
            var stack = inv[i].Itemstack;
            if (stack != null) {
                desc.AppendLine(stack.ToString());
            }
            else {
                desc.AppendLine("Empty");   
            }
        }
        return desc.ToString();
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling) {
        return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer, ref handling);
    }
    
}