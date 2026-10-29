using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace DeArtibusRusticis;

// TODO
// - sound
// - seasoningPortionSize[Litres]: take this many items from the stack or litres from the ILiquidSource
// - seasoningRecipeCodes: use only these recipe codes
// - errors on failure

// BlockMeal doesn't have its own overrides for these, so
// we just match on the class instead.
[HarmonyPatch(typeof(CollectibleObject))]
public static class CollectibleObjectPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(CollectibleObject.GetMergableQuantity))]
    public static bool AddMealSeasoningInventoryQuantity(CollectibleObject __instance, ref int __result, ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority)
    {
        if (__instance is BlockMeal
            && priority != EnumMergePriority.AutoMerge
            && sourceStack?.ItemAttributes.IsTrue("isSeasoning") == true
            && sinkStack?.Attributes.HasAttribute("seasoning") == false
            && (float)(sinkStack?.Attributes.GetDecimal("quantityServings") ?? 0) > 0
            && sinkStack?.Attributes.HasAttribute("recipeCode") == true)
        {
            int servings = (int)Math.Ceiling(sinkStack!.Attributes.GetDecimal("quantityServings"));
            __result = sourceStack.StackSize >= servings ? servings : 0;
            return false;
        }
        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(CollectibleObject.TryMergeStacks))]
    public static bool AddMealSeasoningInventory(CollectibleObject __instance, ItemStackMergeOperation op)
    {
        ItemStack sourceStack = op.SourceSlot.Itemstack!;
        ItemStack sinkStack = op.SinkSlot.Itemstack!;

        if (__instance is BlockMeal
            && op.CurrentPriority == EnumMergePriority.DirectMerge
            && sourceStack.ItemAttributes.IsTrue("isSeasoning")
            && !sinkStack.Attributes.HasAttribute("seasoning")
            && (float)sinkStack.Attributes.GetDecimal("quantityServings") > 0)
        {
            // Don't season sealed crocks
            if (sinkStack.Collectible is BlockCrock && sinkStack.Attributes.GetBool("sealed")) return true;

            int servings = (int)Math.Ceiling(sinkStack.Attributes.GetDecimal("quantityServings") * sinkStack.StackSize);
            if (sourceStack.StackSize < servings) return true;

            string seasoning = sourceStack.Collectible.Code;
            op.SourceSlot.TakeOut(servings);
            op.SinkSlot.Itemstack!.Attributes.SetString("seasoning", seasoning);

            op.World.Logger.Audit(
                "{0} added {1}x{2} seasoning to meal with recipe {3} in inventory",
                op.ActingPlayer.PlayerName,
                servings,
                sinkStack.Attributes.GetString("seasoning"),
                sinkStack.Attributes["recipeCode"]
            );

            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(BlockMeal))]
public static class BlockMealPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(BlockCookedContainerBase.OnContainedInteractStart))]
    public static bool AddSeasoningInWorld(ref bool __result, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
    {
        ItemSlot? heldSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

        if (heldSlot == null) return true;

        ItemStack? heldStack = heldSlot.Itemstack;
        ItemStack? potStack = slot.Itemstack;

        if (heldStack == null || potStack == null) return true;

        if (heldStack.ItemAttributes.IsTrue("isSeasoning")
            && !potStack.Attributes.HasAttribute("seasoning")
            && potStack.Attributes.GetDecimal("quantityServings") > 0
            && potStack.Attributes.HasAttribute("recipeCode"))
        {
            int servings = (int)Math.Ceiling(potStack.Attributes.GetDecimal("quantityServings"));
            if (servings > heldStack.StackSize) return true;

            string seasoning = heldStack.Collectible.Code;
            heldSlot.TakeOut(servings);
            potStack.Attributes.SetString("seasoning", seasoning);

            byPlayer.Entity.World.Logger.Audit(
                "{0} added {1}x{2} seasoning to meal with recipe {3} at {4}",
                byPlayer.PlayerName,
                servings,
                potStack.Attributes.GetString("seasoning"),
                potStack.Attributes["recipeCode"],
                blockSel.Position
            );

            slot?.MarkDirty();
            be.MarkDirty(true);
            __result = true;
            return false;
        }

        return true;
    }


    [HarmonyPostfix]
    [HarmonyPatch(nameof(BlockMeal.GetContentNutritionProperties), [typeof(IWorldAccessor), typeof(ItemSlot), typeof(ItemStack?[]), typeof(EntityAgent), typeof(bool), typeof(float), typeof(float)])]
    public static void AddSeasoningNutrition(ref FoodNutritionProperties[] __result, IWorldAccessor world, ItemSlot inSlot, ItemStack?[]? contentStacks, EntityAgent? forEntity, bool mulWithStacksize = false, float nutritionMul = 1, float healthMul = 1)
    {
        if (contentStacks == null || inSlot.Itemstack is not ItemStack mealStack) return;
        if (mealStack.Attributes.GetString("seasoning") is not string seasoningCode) return;
        if (!mealStack.Attributes.HasAttribute("recipeCode")) return;

        CollectibleObject? seasoning = world.GetItem(seasoningCode);
        seasoning ??= world.GetBlock(seasoningCode);

        if (seasoning == null) return;

        ItemStack seasoningStack = new(seasoning);
        if (BlockMeal.GetIngredientStackNutritionProperties(world, seasoningStack, forEntity) is not FoodNutritionProperties seasoningProps) return;

        FoodNutritionProperties props = seasoningProps.Clone();

        float spoilState = 0;
        DummySlot slot = new DummySlot(seasoningStack, inSlot.Inventory);
        bool timeFrozen = mealStack.Attributes.GetBool("timeFrozen");
        if (!timeFrozen)
        {
            TransitionState? state = seasoningStack.Collectible.UpdateAndGetTransitionState(world, slot, EnumTransitionType.Perish);
            spoilState = state != null ? state.TransitionLevel : 0;
        }

        float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, mealStack, forEntity);
        float healthLoss = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, mealStack, forEntity);
        props.Satiety *= satLossMul * nutritionMul;
        props.Health *= healthLoss * healthMul;

        List<FoodNutritionProperties> propsList = [.. __result];
        propsList.Add(props);
        __result = [.. propsList];
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(BlockMeal.GetHeldItemInfo))]
    public static void AddInfoSeasonedText(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        if (inSlot.Itemstack?.Attributes.GetString("seasoning") is string seasoning)
        {
            dsc.AppendLine(Lang.Get("deartibusrusticis:meal-seasoned", Lang.Get(seasoning).ToLowerInvariant()));
        }
    }
}


[HarmonyPatch(typeof(BlockCookedContainerBase))]
public static class BlockCookedContainerBasePatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(BlockCookedContainerBase.OnContainedInteractStart))]
    public static bool AddSeasoningInWorld(ref bool __result, BlockEntityContainer be, ItemSlot slot, IPlayer byPlayer, BlockSelection blockSel)
    {
        ItemSlot? heldSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

        if (heldSlot == null) return true;

        ItemStack? heldStack = heldSlot.Itemstack;
        ItemStack? potStack = slot.Itemstack;

        if (heldStack == null || potStack == null) return true;

        if (heldStack.ItemAttributes.IsTrue("isSeasoning")
            && !potStack.Attributes.HasAttribute("seasoning")
            && potStack.Attributes.GetDecimal("quantityServings") > 0
            && potStack.Attributes.HasAttribute("recipeCode"))
        {
            int servings = (int)Math.Ceiling(potStack.Attributes.GetDecimal("quantityServings"));
            if (servings > heldStack.StackSize) return true;

            string seasoning = heldStack.Collectible.Code;
            heldSlot.TakeOut(servings);
            potStack.Attributes.SetString("seasoning", seasoning);

            byPlayer.Entity.World.Logger.Audit(
                "{0} added {1}x{2} seasoning to cooked container with recipe {3} at {4}",
                byPlayer.PlayerName,
                servings,
                potStack.Attributes.GetString("seasoning"),
                potStack.Attributes["recipeCode"],
                blockSel.Position
            );

            slot?.MarkDirty();
            be.MarkDirty(true);
            __result = true;
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(BlockCookedContainerBase.GetMergableQuantity))]
    public static bool AddSeasoningInventoryQuantity(ref int __result, ref ICoreAPI ___api, ItemStack sinkStack, ItemStack sourceStack, EnumMergePriority priority)
    {
        if (priority != EnumMergePriority.AutoMerge
            && sourceStack?.ItemAttributes.IsTrue("isSeasoning") == true
            && sinkStack?.Attributes.HasAttribute("seasoning") == false
            && (float)(sinkStack?.Attributes.GetDecimal("quantityServings") ?? 0) > 0
            && sinkStack?.Attributes.HasAttribute("recipeCode") == true)
        {
            // Don't season sealed crocks
            if (sinkStack.Collectible is BlockCrock && sinkStack.Attributes.GetBool("sealed"))
            {
                __result = 0;
                return false;
            }

            int servings = (int)Math.Ceiling(sinkStack!.Attributes.GetDecimal("quantityServings"));
            __result = sourceStack.StackSize >= servings ? servings : 0;
            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(BlockCookedContainerBase.TryMergeStacks))]
    public static bool AddSeasoningInventory(ItemStackMergeOperation op)
    {
        ItemStack sourceStack = op.SourceSlot.Itemstack!;
        ItemStack sinkStack = op.SinkSlot.Itemstack!;

        if (op.CurrentPriority == EnumMergePriority.DirectMerge
            && sourceStack.ItemAttributes.IsTrue("isSeasoning")
            && !sinkStack.Attributes.HasAttribute("seasoning")
            && (float)sinkStack.Attributes.GetDecimal("quantityServings") > 0)
        {
            // Don't season sealed crocks
            if (sinkStack.Collectible is BlockCrock && sinkStack.Attributes.GetBool("sealed")) return true;

            int servings = (int)Math.Ceiling(sinkStack.Attributes.GetDecimal("quantityServings") * sinkStack.StackSize);
            if (sourceStack.StackSize < servings) return true;

            string seasoning = sourceStack.Collectible.Code;
            op.SourceSlot.TakeOut(servings);
            op.SinkSlot.Itemstack!.Attributes.SetString("seasoning", seasoning);

            op.World.Logger.Audit(
                "{0} added {1}x{2} seasoning to cooked container with recipe {3} in inventory",
                op.ActingPlayer.PlayerName,
                servings,
                sinkStack.Attributes.GetString("seasoning"),
                sinkStack.Attributes["recipeCode"]
            );

            return false;
        }

        return true;
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(BlockCookedContainerBase.ServeIntoStack))]
    // __state stores pot seasoning before serving
    public static bool RequireSameSeasoningOnServeIntoStack(ref string? __state, ItemSlot bowlSlot, ItemSlot potslot, IWorldAccessor world)
    {
        string? potSeasoning = potslot.Itemstack?.Attributes.GetString("seasoning");
        string? bowlSeasoning = bowlSlot.Itemstack?.Attributes.GetString("seasoning");
        __state = potSeasoning;

        float quantityServings = (float)(potslot?.Itemstack?.Attributes.GetDecimal("quantityServings") ?? 0);
        float quantityServingsBowl = (float)(potslot?.Itemstack?.Attributes.GetDecimal("quantityServings") ?? 0);
        float servingsToTransfer = Math.Min(quantityServings, bowlSlot.Itemstack?.Collectible.Attributes["servingCapacity"].AsFloat(1) ?? 1);

        if (quantityServings > 0 && quantityServingsBowl > 0 && servingsToTransfer > 0
            && bowlSeasoning != null && potSeasoning != null && bowlSeasoning != potSeasoning)
        {
            return false;
        }

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(BlockCookedContainerBase.ServeIntoStack))]
    // __state stores pot seasoning before serving
    public static void TransferSeasoningIntoStack(ref string? __state, ItemSlot bowlSlot, ItemSlot potslot, IWorldAccessor world)
    {
        if (__state is not string potSeasoning) return;

        if (bowlSlot.Itemstack?.Attributes.HasAttribute("seasoning") == false)
        {
            bowlSlot.Itemstack?.Attributes.SetString("seasoning", potSeasoning);
        }
    }
}

[HarmonyPatch(typeof(BlockCookedContainer))]
public static class BlockCookedContainerPatch
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(BlockCookedContainer.GetHeldItemInfo))]
    public static void AddInfoSeasonedText(ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
    {
        if (inSlot.Itemstack?.Attributes.GetString("seasoning") is string seasoning)
        {
            dsc.AppendLine(Lang.Get("deartibusrusticis:meal-seasoned", Lang.Get(seasoning).ToLowerInvariant()));
        }
    }
}

[HarmonyPatch(typeof(BlockFirepit))]
public static class BlockFirepitPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(BlockFirepit.OnBlockInteractStart))]
    public static bool AddSeasoningFirepit(ref bool __result, IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
    {
        ItemSlot? heldSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

        if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is not BlockEntityFirepit bef) return true;
        ItemSlot? potSlot = bef.outputSlot?.Itemstack?.Collectible is BlockCookedContainer ? bef.outputSlot : null;

        if (heldSlot == null || potSlot == null) return true;

        if (heldSlot.Itemstack?.ItemAttributes.IsTrue("isSeasoning") == true
            && potSlot.Itemstack?.Attributes.HasAttribute("seasoning") == false
            && (float)potSlot.Itemstack.Attributes.GetDecimal("quantityServings") > 0)
        {
            int servings = (int)Math.Ceiling(potSlot.Itemstack.Attributes.GetDecimal("quantityServings") * potSlot.Itemstack.StackSize);
            if (heldSlot.StackSize < servings) return true;

            string seasoning = heldSlot.Itemstack.Collectible.Code;
            heldSlot.TakeOut(servings);
            bef.outputSlot!.Itemstack!.Attributes.SetString("seasoning", seasoning);

            world.Logger.Audit(
                "{0} added {1}x{2} seasoning to cooked container with recipe {3} in firepit at {4}",
                byPlayer.PlayerName,
                servings,
                potSlot.Itemstack.Attributes.GetString("seasoning"),
                potSlot.Itemstack.Attributes["recipeCode"],
                bef.Pos
            );

            (byPlayer as IClientPlayer)?.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            __result = true;

            return false;
        }

        return true;
    }
}
