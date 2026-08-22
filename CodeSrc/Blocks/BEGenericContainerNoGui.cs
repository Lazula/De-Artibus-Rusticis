using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace DeArtibusRusticis.Blocks;

public class BEGenericContainerNoGui : BlockEntityContainer {

    public override InventoryBase Inventory => _inv;

    public override string InventoryClassName => _invClassName;
    
    private InventoryGeneric _inv = new InventoryGeneric(1, null, null);
    private string _invClassName;
    
    public override void Initialize(ICoreAPI api) {
        InitInventory();
        
        base.Initialize(api);
    }

    private void InitInventory() {
        var attributes = Block.Attributes;
        _invClassName = attributes["inventoryClassName"].AsString("invalid-generic");
        var quantitySlots = attributes["quantitySlots"].AsInt(-1);
        _inv.AddSlots(Math.Max(quantitySlots - _inv.Count, 0));

        _inv.TransitionableSpeedMulByType = attributes["transitionSpeedMul"].AsObject<Dictionary<EnumTransitionType, float>>();
        _inv.PerishableFactorByFoodCategory = Block.Attributes["spoilSpeedMulByFoodCat"].AsObject<Dictionary<EnumFoodCategory, float>>();
        // _inv.SlotModified += this.OnSlotModified;
    }
    //
    // private ItemSlot NewSlot(int ids, ) {
    //     
    // }

}