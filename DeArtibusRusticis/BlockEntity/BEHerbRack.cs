using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace DeArtibusRusticis;

public class BlockEntityHerbRack : BlockEntityDisplay, ITexPositionSource
{
    protected InventoryGeneric inventory = null!;
    public override InventoryBase Inventory => inventory;
    public override string InventoryClassName => "herbrack";
    public override string AttributeTransformCode => "herbRackTransform";

    public MeshData? mesh = null;

    public BlockEntityHerbRack()
    {
        inventory = new InventoryDisplayed(this, 12, "herbrack-0", null, null);
    }

    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        RegisterGameTickListener(RotDrop, 3000);
        Inventory.OnAcquireTransitionSpeed += Inventory_OnAcquireTransitionSpeed;
    }

    protected override MeshData getOrCreateMesh(ItemSlot slot, int index)
    {
        MeshData? mesh = getMesh(slot);
        if (mesh != null) return mesh;

        ItemStack? stack = slot.Itemstack;
        if (stack == null) return new();

        Shape? shape = capi.Assets.TryGet(stack.ItemAttributes["herbRackShapeLoc"].AsString(""))?.ToObject<Shape>();

        Dictionary<string, CompositeTexture> stackTextures = [];
        if (stack.Collectible is Block)
        {
            stackTextures.AddRange(stack.Block.Textures);
        }
        else
        {
            stackTextures.AddRange(stack.Item.Textures);
        }

        Dictionary<string, AssetLocation> stackTextureLocs = [];
        foreach ((string name, CompositeTexture texture) in stackTextures)
        {
            stackTextureLocs[name] = texture.Base;
        }

        if (shape != null)
        {
            List<string> selectiveElements = ["display"];

            for (int i = 0; i < stack.StackSize; i++)
            {
                selectiveElements.Add($"origin/item{i + 1}/*");
            }

            shape.Textures.AddRange(stackTextureLocs);
            nowTesselatingObj = stack.Collectible;
            nowTesselatingShape = shape;
            capi.Tesselator.TesselateShape("herbrack item shape", shape, out mesh, this, null, 0, 0, 0, null, [.. selectiveElements]);
        }
        else
        {
            IContainedMeshSource? meshSource = stack.Collectible?.GetCollectibleInterface<IContainedMeshSource>();

            if (meshSource != null)
            {
                mesh = meshSource.GenMesh(slot, capi.BlockTextureAtlas, Pos);
            }
        }

        mesh ??= getDefaultMesh(stack);

        applyDefaultTranforms(stack, mesh);

        string key = getMeshCacheKey(slot);
        MeshCache[key] = mesh;

        return mesh;
    }

    protected override string getMeshCacheKey(ItemSlot slot)
    {
        return $"herbRackShape-{slot.Itemstack?.Collectible.Code.ToShortString()}-{slot.Itemstack?.StackSize}";
    }

    Vec3d? dropPos;
    private void RotDrop(float dt)
    {
        dropPos ??= Pos.ToVec3d().Add(0.5, -1, 0.5);
        inventory.DropSlots(dropPos, [.. inventory.Where(slot => slot.Itemstack?.Collectible.FirstCodePart() == "rot").Select(inventory.GetSlotId)]);
    }

    internal bool OnInteract(IPlayer byPlayer, BlockSelection blockSel)
    {
        ItemSlot slot = byPlayer.InventoryManager.ActiveHotbarSlot;

        if (slot.Empty)
        {
            return TryTake(byPlayer, blockSel);
        }
        else if (slot.Itemstack?.ItemAttributes?["herbrackable"].AsBool() == true && TryPut(slot, blockSel, byPlayer.Entity.Controls.CtrlKey))
        {
            if (slot.Itemstack?.Block?.Sounds?.Place != null)
            {
                Api.World.PlaySoundAt(slot.Itemstack.Block.Sounds.Place, byPlayer.Entity, byPlayer);
            }
            else
            {
                Api.World.PlaySoundAt("sounds/player/build", byPlayer.Entity, byPlayer, true, 16, 1f);
            }
            return true;
        }

        return false;
    }

    private float Inventory_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float baseMul)
    {
        if (Api == null) return 1;
        if (transType == EnumTransitionType.Cure) return Block.Attributes["cureRate"].AsFloat(3);
        if (transType == EnumTransitionType.Dry) return Block.Attributes["dryRate"].AsFloat(8);
        return baseMul;
    }

    private bool TryPut(ItemSlot slot, BlockSelection blockSel, bool bulk)
    {
        int index = blockSel.SelectionBoxIndex;
        int maxStackSize = inventory[index].Itemstack?.ItemAttributes["herbRackStackSize"].AsInt(inventory[index].MaxSlotStackSize) ?? inventory[index].MaxSlotStackSize;
        int count = bulk ? slot.Itemstack?.ItemAttributes["herbRackBulkMoveCount"].AsInt(maxStackSize) ?? maxStackSize : 1;
        count = Math.Min(count, maxStackSize - inventory[index].StackSize);

        if (count > 0 && slot.TryPutInto(Api.World, inventory[index], count) > 0)
        {
            updateMesh(index);
            MarkDirty(true);
            return true;
        }

        return false;
    }

    private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
    {
        int index = blockSel.SelectionBoxIndex;
        bool bulk = byPlayer.Entity.Controls.CtrlKey;
        int maxStackSize = inventory[index].Itemstack?.ItemAttributes["herbRackStackSize"].AsInt(inventory[index].MaxSlotStackSize) ?? inventory[index].MaxSlotStackSize;
        int count = bulk ? inventory[index].Itemstack?.ItemAttributes["herbRackBulkMoveCount"].AsInt(maxStackSize) ?? maxStackSize : 1;

        if (!inventory[index].Empty)
        {
            ItemStack stack = inventory[index].TakeOut(count);

            if (byPlayer.InventoryManager.TryGiveItemstack(stack))
            {
                if (stack?.Block?.Sounds?.Place != null)
                {
                    Api.World.PlaySoundAt(stack.Block.Sounds.Place, byPlayer.Entity, byPlayer);
                }
                else
                {
                    Api.World.PlaySoundAt("sounds/player/build", byPlayer.Entity, byPlayer, true, 16, 1f);
                }
            }

            if (stack?.StackSize > 0)
            {
                Api.World.SpawnItemEntity(stack, Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }

            updateMesh(index);
            MarkDirty(true);
            return true;
        }

        return false;
    }

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
    {
        sb.AppendLine();

        if (forPlayer?.CurrentBlockSelection == null) return;

        int index = forPlayer.CurrentBlockSelection.SelectionBoxIndex;

        if (!inventory[index].Empty)
        {
            if (inventory[index]!.Itemstack?.Collectible.TransitionableProps != null && inventory[index].Itemstack?.Collectible.TransitionableProps.Length > 0)
            {
                sb.AppendLine(PerishableInfoCompact(Api, inventory[index], 0));
            }
            else sb.AppendLine(inventory[index].Itemstack?.GetName());
        }
    }

    protected override float[][] genTransformationMatrices()
    {
        float[][] tfMatrices = new float[DisplayedItems][];

        for (int index = 0; index < 2; index++)
        {
            int rnd = 0;
            Cuboidf selectionBox = Block.SelectionBoxes[index];

            int rotateZ = Block.LastCodePart() switch
            {
                "north" => 180,
                "east" => 90,
                "south" => 0,
                "west" => 270,
                _ => 0
            };

            tfMatrices[index] =
                new Matrixf()
                .Translate(selectionBox.MidX, selectionBox.MinY, selectionBox.MidZ)
                .RotateDeg(Block.Shape.RotateXYZCopy)
                .RotateXDeg(-90)
                .RotateYDeg(90 + rnd)
                .Values
            ;
        }

        return tfMatrices;
    }

    public string PerishableInfoCompact(ICoreAPI Api, ItemSlot contentSlot, float ripenRate, bool withStackName = true)
    {
        if (contentSlot.Empty) return "";

        StringBuilder dsc = new();

        if (withStackName) dsc.Append(contentSlot.Itemstack.GetName());

        TransitionState[]? transitionStates = contentSlot.Itemstack.Collectible.UpdateAndGetTransitionStates(Api.World, contentSlot);

        bool nowSpoiling = false;

        if (transitionStates != null)
        {
            bool appendLine = false;
            for (int i = 0; i < transitionStates.Length; i++)
            {
                TransitionState state = transitionStates[i];

                TransitionableProperties prop = state.Props;
                float perishRate = contentSlot.Itemstack.Collectible.GetTransitionRateMul(Api.World, contentSlot, prop.Type);

                if (perishRate <= 0) continue;

                float transitionLevel = state.TransitionLevel;
                float freshHoursLeft = state.FreshHoursLeft / perishRate;
                double hoursPerday = Api.World.Calendar.HoursPerDay;
                switch (prop.Type)
                {
                    case EnumTransitionType.Perish:
                        appendLine = true;

                        if (transitionLevel > 0)
                        {
                            nowSpoiling = true;
                            dsc.Append("\n" + Lang.Get("itemstack-perishable-spoiling", (int)Math.Round(transitionLevel * 100)));
                        }
                        else
                        {
                            if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                            {
                                dsc.Append("\n" + Lang.Get("itemstack-perishable-fresh-years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                            }
                            else if (freshHoursLeft > hoursPerday)
                            {
                                dsc.Append("\n" + Lang.Get("itemstack-perishable-fresh-days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                            }
                            else
                            {
                                dsc.Append("\n" + Lang.Get("itemstack-perishable-fresh-hours", Math.Round(freshHoursLeft, 1)));
                            }
                        }
                        break;
                    case EnumTransitionType.Cure:
                        if (nowSpoiling) break;

                        appendLine = true;

                        if (transitionLevel > 0)
                        {
                            int hoursLeft = (int)((state.TransitionHours - (state.TransitionedHours - state.FreshHours)) / Block.Attributes["cureRate"].AsFloat(3f));

                            dsc.Append("\n" + Lang.Get("itemstack-curable-cured", Math.Round(transitionLevel * 100)));

                            if (hoursLeft > hoursPerday) dsc.Append(", " + Lang.Get("{0:0.#} days left", hoursLeft / hoursPerday));
                            else dsc.Append(", " + Lang.Get("{0:0} hrs left", hoursLeft));
                        }
                        else
                        {
                            if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                            {
                                dsc.Append("\n" + Lang.Get("will cure in ") + Lang.Get("{0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                            }
                            else if (freshHoursLeft > hoursPerday)
                            {
                                dsc.Append("\n" + Lang.Get("itemstack-curable-duration-days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                            }
                            else
                            {
                                dsc.Append("\n" + Lang.Get("itemstack-curable-duration-hours", Math.Round(freshHoursLeft, 1)));
                            }
                        }
                        break;
                    case EnumTransitionType.Dry:
                        if (nowSpoiling) break;

                        appendLine = true;

                        if (transitionLevel > 0)
                        {
                            int hoursLeft = (int)((state.TransitionHours - (state.TransitionedHours - state.FreshHours)) / Block.Attributes["dryRate"].AsFloat(6f));

                            dsc.Append("\n" + Lang.Get("itemstack-dryable-dried", Math.Round(transitionLevel * 100)));

                            if (hoursLeft > hoursPerday) dsc.Append(", " + Lang.Get("{0:0.#} days left", hoursLeft / hoursPerday));
                            else dsc.Append(", " + Lang.Get("{0:0} hrs left", hoursLeft));
                        }
                        else
                        {
                            if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                            {
                                dsc.Append("\n" + Lang.Get("will dry in ") + Lang.Get("{0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                            }
                            else if (freshHoursLeft > hoursPerday)
                            {
                                dsc.Append("\n" + Lang.Get("itemstack-dryable-duration-days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                            }
                            else
                            {
                                dsc.Append("\n" + Lang.Get("itemstack-dryable-duration-hours", Math.Round(freshHoursLeft, 1)));
                            }
                        }
                        break;
                }
            }

            if (appendLine) dsc.AppendLine();
        }

        return dsc.ToString();
    }
}
