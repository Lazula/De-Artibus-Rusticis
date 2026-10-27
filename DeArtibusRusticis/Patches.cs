using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace DeArtibusRusticis;

[HarmonyPatch(typeof(BlockLinen))]
public static class BlockLinenPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(BlockLinen.OnHeldInteractStart))]
    public static bool BlockLinenOnHeldInteractStartPatch(BlockLinen __instance, ref ICoreAPI ___api, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handHandling)
    {
        Dictionary<string, string> cottageCheeseToBundle = new()
        {
            { "spicecottagecheeseportion", "deartibusrusticis:spicecurdbundle" },
            { "herbcottagecheeseportion", "deartibusrusticis:herbcurdbundle" }
        };

        if (blockSel != null)
        {
            BlockEntityBarrel? beba = ___api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BlockEntityBarrel;

            var liqslot = beba?.Inventory[1];

            if (beba != null && liqslot?.Empty == false)
            {
                if (cottageCheeseToBundle.TryGetValue(liqslot.Itemstack.Item?.Code?.Path ?? "", out string? bundle) && bundle != null)
                {
                    var props = BlockLiquidContainerBase.GetContainableProps(liqslot.Itemstack);
                    float itemsPerLitre = props?.ItemsPerLitre ?? 1;
                    float curLitres = liqslot.Itemstack.StackSize / itemsPerLitre;

                    if (curLitres < 25)
                    {
                        return true;
                    }

                    if (___api.World.Side == EnumAppSide.Server)
                    {
                        ItemStack ccStack = beba.Inventory[1].TakeOut((int)(25 * itemsPerLitre));

                        BlockCheeseCurdsBundle? block = ___api.World.GetBlock(new AssetLocation(bundle)) as BlockCheeseCurdsBundle;
                        ItemStack bundleStack = new(block);
                        block?.SetContents(bundleStack, ccStack);

                        slot.TakeOut(1);
                        slot.MarkDirty();

                        beba.MarkDirty(true);

                        if (!byEntity.TryGiveItemStack(bundleStack))
                        {
                            ___api.World.SpawnItemEntity(bundleStack, byEntity.Pos.XYZ.AddCopy(0, 0.5, 0));
                        }
                        ___api.World.Logger.Audit("{0} Took 1x{1} from Barrel at {2}.",
                            byEntity.GetName(),
                            bundleStack.Collectible.Code,
                            blockSel.Position
                        );
                    }

                    handHandling = EnumHandHandling.PreventDefault;
                    return false;
                }
            }
        }

        return true;
    }
}

[HarmonyPatch(typeof(BlockCheeseCurdsBundle))]
public static class BlockCheeseCurdsBundlePatch
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "StartSqueeze")]
    extern static void BECCBStartSqueeze(BECheeseCurdsBundle instance, IPlayer byPlayer);

    [HarmonyPrefix]
    [HarmonyPatch(nameof(BlockCheeseCurdsBundle.OnBlockInteractStart))]
    public static bool BlockCheeseCurdsBundleOnBlockInteractStartPatch(BlockCheeseCurdsBundle __instance, ref bool __result, ref ICoreAPI ___api, ref BlockSounds ___Sounds, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        Dictionary<string, string> bundleToRawCheese = new()
        {
            { "spicecurdbundle", "deartibusrusticis:spicerawcheese-salted" },
            { "herbcurdbundle", "deartibusrusticis:herbrawcheese-salted" }
        };

        BECheeseCurdsBundle? beccb = ___api.World.BlockAccessor.GetBlockEntity(blockSel.Position) as BECheeseCurdsBundle;

        if (beccb == null) return true;
        if (beccb.State != EnumCurdsBundleState.OpenedSalted) return true;

        if (!bundleToRawCheese.TryGetValue(__instance.Code.Path, out string? rawCheese) || rawCheese == null) return true;
        ItemStack cheeseRoll = new(___api.World.GetItem(new AssetLocation(rawCheese)));

        if (!byPlayer.InventoryManager.TryGiveItemstack(cheeseRoll, true))
        {
            ___api.World.SpawnItemEntity(cheeseRoll, byPlayer.Entity.Pos.XYZ.Add(0, 0.5, 0));
        }
        ___api.World.Logger.Audit("{0} Took 1x{1} at {2}.",
            byPlayer.PlayerName,
            cheeseRoll.Collectible.Code,
            blockSel.Position
        );

        ___api.World.BlockAccessor.SetBlock(___api.World.GetBlock(new AssetLocation("linen-normal-down"))!.Id, blockSel.Position);
        __result = true;
        return false;
    }
}
