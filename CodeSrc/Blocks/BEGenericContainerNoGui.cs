using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace DeArtibusRusticis.Blocks;

public class BEGenericContainerNoGui : BlockEntityContainer {

    public override InventoryBase Inventory => _inv;

    public override string InventoryClassName => _invClassName;

    private InventoryGeneric _inv; 
    private string _invClassName = null!;

    private int _slotStackSize = 1;
    
    public BEGenericContainerNoGui() {
        _inv = new InventoryGeneric(1, null, null, NewSlot);
    }

    public override void Initialize(ICoreAPI api) {
        InitInventory();
        
        base.Initialize(api);
    }

    private void InitInventory() {
        var attributes = Block.Attributes;
        _invClassName = attributes["inventoryClassName"].AsString("invalid-generic");
        _slotStackSize = attributes["stackSizeLimit"].AsInt(999999);
        var quantitySlots = attributes["quantitySlots"].AsInt(-1);
        _inv.AddSlots(Math.Max(quantitySlots - _inv.Count, 0));
        
        var transitions = attributes["transitionSpeedMul"].AsObject<Dictionary<EnumTransitionType, float>>();
        NormalizeTransitionSpeeds(transitions);
        _inv.TransitionableSpeedMulByType = transitions;
        var perish = Block.Attributes["spoilSpeedMulByFoodCat"].AsObject<Dictionary<EnumFoodCategory, float>>();
        _inv.PerishableFactorByFoodCategory = perish;
    }
    
    private ItemSlot NewSlot(int ids, InventoryGeneric inv) {
        return new ItemSlot(inv) {
            MaxSlotStackSize = _slotStackSize
        };
    }

    private void NormalizeTransitionSpeeds(Dictionary<EnumTransitionType, float>? transitions) {
        if (transitions == null) {
            return;
        }
        //For Dry and Melt the game has a hardcoded additional multiplier of 0.25, we offset that here
        if (transitions.TryGetValue(EnumTransitionType.Dry, out var speed)) {
            speed *= 4f;
            transitions[EnumTransitionType.Dry] = speed;
        }
        if (transitions.TryGetValue(EnumTransitionType.Melt, out speed)) {
            speed *= 4f;
            transitions[EnumTransitionType.Melt] = speed;
        }
    }

    public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving) {
        InitInventory();
        base.FromTreeAttributes(tree, worldForResolving);
    }

    // public override void ToTreeAttributes(ITreeAttribute tree) {
    //     base.ToTreeAttributes(tree);
    // }

}