using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace DeArtibusRusticis;

public class BlockHerbRack : Block
{
    static WorldInteraction[] interactions = null!;
    static ItemStack[] herbrackable = null!;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);

        if (api.Side.IsClient() && interactions == null)
        {
            List<ItemStack> rackstacks = [];
            foreach (CollectibleObject? obj in api.World.Collectibles)
            {
                if (obj?.Attributes?.IsTrue("herbrackable") == true)
                {
                    rackstacks.Add(new(obj));
                }
            }

            herbrackable = [.. rackstacks];

            interactions = [
                new()
                {
                    ActionLangCode = "deartibusrusticis:blockhelp-herbrack-add-one",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = herbrackable,
                    GetMatchingStacks = (wi, bs, es) =>
                    {
                        if (api.World.BlockAccessor.GetBlockEntity<BlockEntityHerbRack>(bs.Position) is not BlockEntityHerbRack be) return null;

                        ItemSlot slot = be.Inventory[bs.SelectionBoxIndex];
                        if (slot.Empty) return herbrackable;

                        int maxStackSize = slot.Itemstack?.ItemAttributes["herbRackStackSize"].AsInt(slot.MaxSlotStackSize) ?? slot.MaxSlotStackSize;

                        return slot.Itemstack?.StackSize < maxStackSize ? [new(slot.Itemstack.Collectible)] : null;
                    }
                },
                new()
                {
                    ActionLangCode = "deartibusrusticis:blockhelp-herbrack-add-bulk",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = herbrackable,
                    HotKeyCode = "ctrl",
                    GetMatchingStacks = (wi, bs, es) =>
                    {
                        if (api.World.BlockAccessor.GetBlockEntity<BlockEntityHerbRack>(bs.Position) is not BlockEntityHerbRack be) return null;

                        ItemSlot slot = be.Inventory[bs.SelectionBoxIndex];
                        if (slot.Empty) return herbrackable;

                        int maxStackSize = slot.Itemstack?.ItemAttributes["herbRackStackSize"].AsInt(slot.MaxSlotStackSize) ?? slot.MaxSlotStackSize;

                        return slot.Itemstack?.StackSize < maxStackSize ? [new(slot.Itemstack.Collectible)] : null;
                    }
                },
                new()
                {
                    ActionLangCode = "deartibusrusticis:blockhelp-herbrack-remove-one",
                    MouseButton = EnumMouseButton.Right,
                    RequireFreeHand = true,
                    ShouldApply = (wi, bs, es) => {
                        if (api.World.BlockAccessor.GetBlockEntity<BlockEntityHerbRack>(bs.Position) is not BlockEntityHerbRack be) return false;
                        return !be.Inventory[bs.SelectionBoxIndex].Empty;
                    }
                },
                new()
                {
                    ActionLangCode = "deartibusrusticis:blockhelp-herbrack-remove-bulk",
                    MouseButton = EnumMouseButton.Right,
                    RequireFreeHand = true,
                    HotKeyCode = "ctrl",
                    ShouldApply = (wi, bs, es) => {
                        if (api.World.BlockAccessor.GetBlockEntity<BlockEntityHerbRack>(bs.Position) is not BlockEntityHerbRack be) return false;
                        return !be.Inventory[bs.SelectionBoxIndex].Empty;
                    }
                }
            ];
        }
    }

    public override bool DoPartialSelection(IWorldAccessor world, BlockPos pos)
    {
        return true;
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        return GetBlockEntity<BlockEntityHerbRack>(blockSel.Position)?.OnInteract(byPlayer, blockSel)
            ?? base.OnBlockInteractStart(world, byPlayer, blockSel);
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
    {
        return interactions;
    }
}
