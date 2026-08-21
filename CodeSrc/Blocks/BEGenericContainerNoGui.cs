using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace DeArtibusRusticis.Blocks;

public class BEGenericContainerNoGui : BlockEntityContainer {

    public override InventoryBase Inventory => _inv;

    public override string InventoryClassName => _invClassName;
    
    private InventoryGeneric _inv;
    private string _invClassName;
    
    public override void Initialize(ICoreAPI api) {
        var attribs = Block.Attributes;
        var slotCount = attribs["slotCount"].AsInt();
        _inv = new InventoryGeneric(slotCount, null, null);
        
        base.Initialize(api);
    }

    private void InitInventory() {
        var attributes = Block.Attributes;
        var inventoryClassName = attributes["inventoryClassName"].AsString("invalid-generic");
        var quantitySlots = attributes["quantitySlots"].AsInt(-1);
        _inv = new InventoryGeneric(quantitySlots, null, null);

        _inv.TransitionableSpeedMulByType = attributes["transitionSpeedMul"].AsObject<Dictionary<EnumTransitionType, float>>();
        _inv.PerishableFactorByFoodCategory = Block.Attributes["spoilSpeedMulByFoodCat"].AsObject<Dictionary<EnumFoodCategory, float>>();
        // _inv.SlotModified += this.OnSlotModified;
    }

}