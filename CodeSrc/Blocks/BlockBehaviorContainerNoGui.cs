using System.Linq;
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

    private int?[] _selectionSlots = []; // _selectionSlots[boxIdx] = slot or null;

    private WorldInteraction[] _interactionHelp = [];
    
    public BlockBehaviorContainerNoGui(Block block) : base(block) { }

    public override void Initialize(JsonObject properties) {
        base.Initialize(properties);
        _allowedCollectibleTagsStr = properties["allowedCollectibleTags"].AsArray<string>([])!; //Just don't put null strings in the array
        _maxTransferPerInteraction = properties["maxTransferAmount"].AsInt(1);
        var selSlotsRaw = properties["selectionSlots"].AsArray([-1]); // [slotId] = boxId
        var lastBox = selSlotsRaw.Max();
        var boxCount = lastBox + 1;
        _selectionSlots = new int?[boxCount];
        if (boxCount > 0) {
            for (var i = 0; i < selSlotsRaw.Length; i++) {
                _selectionSlots[selSlotsRaw[i]] = i;
            }
        }
    }

    public override void OnLoaded(ICoreAPI api) {
        base.OnLoaded(api);
        var collectibleTagRegistry = api.CollectibleTagRegistry;
        var result = collectibleTagRegistry.TryCreateTagSetAndLogIssues(out _allowedCollectibleTags, _allowedCollectibleTagsStr);

        ItemStack[] allowedStacks = [
            ..api.World.Collectibles
                .Where(o => _allowedCollectibleTags.Overlaps(o.Tags))
                .Select(o => new ItemStack(o))
        ];
        _interactionHelp = [
            //Put Single
            new() {
                ActionLangCode = "deartibusrusticis:block-interact-herbrack-put",
                MouseButton = EnumMouseButton.Right,
                Itemstacks = allowedStacks,
                RequireFreeHand = false,
            },
            //Put Many
            new() {
                ActionLangCode = "deartibusrusticis:block-interact-herbrack-put-many",
                MouseButton = EnumMouseButton.Right,
                HotKeyCode = "ctrl",
                Itemstacks = allowedStacks,
                RequireFreeHand = false,
            },
            
            // Take Single
            new() {
                ActionLangCode = "deartibusrusticis:block-interact-herbrack-take",
                MouseButton = EnumMouseButton.Right,
                RequireFreeHand = true
            },
            //Take many
            new() {
                ActionLangCode = "deartibusrusticis:block-interact-herbrack-take-many",
                MouseButton = EnumMouseButton.Right,
                HotKeyCode = "ctrl",
                RequireFreeHand = true
            }
        ];
    }

    public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ref EnumHandling handling) {
        var playerSlot = byPlayer.InventoryManager.ActiveHotbarSlot;
        var stack = playerSlot.Itemstack;
        if (stack == null || _allowedCollectibleTags.Overlaps(stack.Collectible.Tags)) {
            var transferDir = playerSlot.Empty ? TransferDirection.Take : TransferDirection.Store;
            var be = world.BlockAccessor.GetBlockEntity<BlockEntityContainer>(blockSel.Position);
            
            //Transfer selected first (allows for take filtering)
            if (TryGetSlotForSelection(blockSel.SelectionBoxIndex, out var selectedSlot)) {
                TransferSlot(world, be, playerSlot, selectedSlot, transferDir);
            }
            //Transfer other if ctrl is pressed
            if (byPlayer.Entity.Controls.CtrlKey) {
                foreach (var slot in _selectionSlots) {
                    //Don't overcount current slot
                    if (slot.HasValue && slot != selectedSlot) {
                        TransferSlot(world, be, playerSlot, slot.Value, transferDir);
                    }
                }
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
        return _interactionHelp;
    }

    private bool TryGetSlotForSelection(int selectionIdx, out int slot) {
        if (selectionIdx < _selectionSlots.Length) {
            var nul = _selectionSlots[selectionIdx];
            slot = nul ?? -1;
            return nul.HasValue;
        }
        slot = -1;
        return false;
    }

    private void TransferSlot(IWorldAccessor world, BlockEntityContainer be, ItemSlot fromSlot, int targetSlot, TransferDirection dir) {
        var containerSlot = be.Inventory[targetSlot];
        if (containerSlot == null) {
            DarUtil.DarLog(world.Logger.Error,"Slot out of range");
            return;
        }
        if (world.Side == EnumAppSide.Server) {
            if (dir == TransferDirection.Store) {
                fromSlot.TryPutInto(world, containerSlot, _maxTransferPerInteraction);
            }
            else {
                containerSlot.TryPutInto(world, fromSlot, _maxTransferPerInteraction);
            }
            world.BlockAccessor.MarkBlockEntityDirty(be.Pos);
        }
        else {
            containerSlot.MarkDirty();
        }
    }

    private enum TransferDirection {
        Store,
        Take
    }
}