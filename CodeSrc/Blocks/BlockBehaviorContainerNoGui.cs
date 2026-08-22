using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace DeArtibusRusticis.Blocks;

public class BlockBehaviorContainerNoGui : BlockBehavior {
    
    private TagSet _allowedCollectibleTags = TagSet.Empty;
    private int _maxTransferPerInteraction;

    private string[] _allowedCollectibleTagsStr = [];
    
    public BlockBehaviorContainerNoGui(Block block) : base(block) { }

    public override void Initialize(JsonObject properties) {
        base.Initialize(properties);
        _allowedCollectibleTagsStr = properties["allowedCollectibleTags"].AsArray<string>([])!; //Just don't put null strings in the array
        _maxTransferPerInteraction = properties["maxTransferAmount"].AsInt(1);
    }

    public override void OnLoaded(ICoreAPI api) {
        base.OnLoaded(api);
        var collectibleTagRegistry = api.CollectibleTagRegistry;
        var result = collectibleTagRegistry.TryCreateTagSetAndLogIssues(out _allowedCollectibleTags, _allowedCollectibleTagsStr);
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
                world.BlockAccessor.MarkBlockEntityDirty(blockSel.Position);
            }
            handling = EnumHandling.Handled;
            return true;
        }
        return false;
    }

    public override string GetPlacedBlockInfo(IWorldAccessor world, BlockPos pos, IPlayer forPlayer) {
        var be = world.BlockAccessor.GetBlockEntity<BlockEntityContainer>(pos);
        if (be == null) {
            return "!!!No Block Entity!!!";
        }
        
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
        return base.GetPlacedBlockInfo(world, pos, forPlayer) + desc;
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer, ref EnumHandling handling) {
        return base.GetPlacedBlockInteractionHelp(world, selection, forPlayer, ref handling); //TODO
    }
    
}