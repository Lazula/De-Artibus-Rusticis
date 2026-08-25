using System.Collections.Concurrent;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace DeArtibusRusticis.Blocks;

public class BEBehaviorDisplayContainerContent : BlockEntityBehavior, ITexPositionSource {

    private Dictionary<int, int> _slotsToRender = new();
    private SlotRenderPoint[] _slotRenderPoints = [];
    // private ITesselatorAPI? _tesselator;
    private BlockEntityContainer? _beContainer;

    private ConcurrentDictionary<int, CollectibleObject?> _dirtySlots = new();
    
    public BEBehaviorDisplayContainerContent(BlockEntity blockentity) : base(blockentity) {
        _beContainer = blockentity as BlockEntityContainer;
    }

    public override void Initialize(ICoreAPI api, JsonObject properties) {
        base.Initialize(api, properties);
        if (api is ICoreClientAPI capi) {
            // _tesselator = capi.Tesselator;
            if (_beContainer == null) { 
                DarUtil.DarLog(Api.Logger.Error, $"BE with behavior of type DAR.DisplayContainerContents, is of invalid type: {Blockentity.GetType()}. Must have a class deriving form {nameof(BlockEntityContainer)}");
                return;
            }
            var slotDefs = properties["renderedSlots"].AsArray<SlotRenderPointDef>([]);
            var slotRenderPoints = new List<SlotRenderPoint>(slotDefs.Length);
            
            var cShape = Block.Shape;
            var shape = Shape.TryGet(api, cShape.Base.CopyWithPathPrefixAndAppendix("shapes/", ".json"));
            shape.ResolveReferences(api.World.Logger, cShape.Base);

            var attachmentPoints = new Dictionary<string, AttachmentPoint>();
            shape.WalkElements("*", element => {
                var aps = element.AttachmentPoints;
                if (aps == null) {
                    return;
                }
                foreach (var ap in aps) {
                    if (!attachmentPoints.TryAdd(ap.Code, ap)) {
                        DarUtil.DarLog(Api.Logger.Warning, $"Skipping duplicate attachment point on shape: {cShape.Base}");
                    }
                }
            });
            
            for (int i = 0; i < slotDefs.Length; i++) {
                var def = slotDefs[i];
                if (_slotsToRender.ContainsKey(def.slotIdx)) {
                    DarUtil.DarLog(Api.Logger.Warning, $"Duplicate slot id {def.slotIdx}");
                    continue;
                }
                if (!attachmentPoints.TryGetValue(def.attachmentPoint, out var ap)) {
                    DarUtil.DarLog(Api.Logger.Warning, $"No attachment point {def.attachmentPoint} in shape {def.slotIdx}");
                    continue;
                }
                
                _slotsToRender.Add(def.slotIdx, slotRenderPoints.Count);
                slotRenderPoints.Add(new SlotRenderPoint {
                    BaseTransform = DarUtil.GetTotalApTransform(ap),
                    transform = new float[16]
                });
            }
            _slotRenderPoints = slotRenderPoints.ToArray();
            
            _beContainer.Inventory.SlotModified += HandleOnSlotModified;
        }
    }

    private void HandleOnSlotModified(int slotIdx) {
        if (_slotsToRender.TryGetValue(slotIdx, out var pointId)) {
            if (Api.Side == EnumAppSide.Client) {
                var slot = _beContainer!.Inventory[slotIdx];
                if (slot == null) {
                    return;
                }
                var col = slot.Itemstack?.Collectible;
                _dirtySlots.AddOrUpdate(pointId, col, (i, o) => col);
                Api.World.BlockAccessor.MarkBlockDirty(Pos);
            }
        }
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator) {
        foreach (var dirty in _dirtySlots) {
            RemeshSlot(tessThreadTesselator, dirty.Key, dirty.Value);
        }
        // _beContainer.Inventory.SlotModified += HandleOnSlotModified;
        _dirtySlots.Clear(); //May potentially cause not up-to-date meshes

        for (int i = 0; i < _slotRenderPoints.Length; i++) {
            if (_slotRenderPoints[i].meshData == null) {
                continue;
            }
            mesher.AddMeshData(_slotRenderPoints[i].meshData, _slotRenderPoints[i].transform);
        }
        return false;
    }

    //A very inefficient way making meshes
    private void RemeshSlot(ITesselatorAPI tesselator, int renderSlotIdx, CollectibleObject? collectible) {
        ref var rp = ref _slotRenderPoints[renderSlotIdx];
        rp.meshData?.Dispose();
        if (collectible != null) {
            //TODO do this only if actual change occurs or schedule
            MeshData mesh;
            switch(collectible) {
                case Item item: 
                    tesselator.TesselateItem(item, out mesh, this);
                    break;
                case Block block: 
                    tesselator.TesselateBlock(block, out mesh);
                    break;
                default:
                    return;
            }
            rp.meshData = mesh;
            Mat4f.Copy(rp.transform, rp.BaseTransform);
        }
        else {
            rp.meshData = null;
        }
    }
    
    public struct SlotRenderPoint {
        public required float[] BaseTransform { get; init; }
        public float[]? transform;
        public MeshData? meshData;
    }
    
    public struct SlotRenderPointDef {
        public int slotIdx;
        public string attachmentPoint;
    }

    public TextureAtlasPosition? this[string textureCode] {
        get {
            return ((ICoreClientAPI)Api).BlockTextureAtlas.GetPosition(Block, textureCode);
        }
    }


    public Size2i? AtlasSize => ((ICoreClientAPI)Api).BlockTextureAtlas.Size;
}