using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DeArtibusRusticis.Rendering;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.Server;

namespace DeArtibusRusticis.Blocks;

public class BEBehaviorDisplayContainerContent : BlockEntityBehavior {

    private readonly Dictionary<int, int> _slotsToRender = new();
    private SlotRenderPoint[] _slotRenderPoints = [];

    private Vec3f _globalShapeRotation = null!;
    private readonly BlockEntityContainer _beContainer;
    private string _transformName;

    private const int PacketId = 2137;

    private List<CollectibleObject?> _lastSeenCollectibles = new();
    
    private ConcurrentDictionary<int, CollectibleObject?> _dirtySlots = new();
    
    public BEBehaviorDisplayContainerContent(BlockEntity blockentity) : base(blockentity) {
        if (blockentity is not BlockEntityContainer be) { 
            DarUtil.DarLog(Api.Logger.Error, $"BE with behavior of type DAR.DisplayContainerContents, is of invalid type: {Blockentity.GetType()}. Must have a class deriving form {nameof(BlockEntityContainer)}");
            return;
        }
        _beContainer = be;
    }

    public override void Initialize(ICoreAPI api, JsonObject properties) {
        base.Initialize(api, properties);
        if (api is ICoreClientAPI capi) {
            _transformName = properties["transformToUse"].AsString("originTransform");
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

            _globalShapeRotation = cShape.RotateXYZCopy.Mul(GameMath.DEG2RAD);
            
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
                
                _slotsToRender.Add(def.slotIdx, _slotsToRender.Count);
                slotRenderPoints.Add(new SlotRenderPoint {
                    BaseTransform = DarUtil.GetTotalApTransform(ap),
                    transform = new float[16]
                });
            }
            _slotRenderPoints = slotRenderPoints.ToArray();
            
            capi.Network.SendBlockEntityPacket(Pos, PacketId, []);
        }
        else if (api is ICoreServerAPI sapi) {
            _beContainer.Inventory.SlotModified += HandleOnSlotModified;
            
            var slotDefs = properties["renderedSlots"].AsArray<SlotRenderPointDef>([]);
            for (int i = 0; i < slotDefs.Length; i++) {
                var def = slotDefs[i];
                if (_slotsToRender.ContainsKey(def.slotIdx)) {
                    DarUtil.DarLog(Api.Logger.Warning, $"Duplicate slot id {def.slotIdx}");
                    continue;
                }
                
                _slotsToRender.Add(def.slotIdx, _slotsToRender.Count);
            }
        }
    }

    public override void OnBlockRemoved() {
        DisposeMeshes();
    }

    public override void OnBlockUnloaded() {
        DisposeMeshes();
    }

    private void HandleOnSlotModified(int slotIdx) {
        if (Api is ICoreServerAPI server) {
            if (_slotsToRender.TryGetValue(slotIdx, out var pointId)) {
                CheckAndSendPacketForSlot(server, slotIdx, pointId);
            }
        }
    }

    private void CheckAndSendPacketForSlot(ICoreServerAPI sapi, int slotIdx, int pointId, IServerPlayer? sendFor = null) {
        var slot = _beContainer!.Inventory[slotIdx];
        if (slot == null) {
            return;
        }
        var stack = slot.Itemstack;
        
        if (_lastSeenCollectibles.Count <= slotIdx) {
            _lastSeenCollectibles.AddRange(Enumerable.Repeat<CollectibleObject?>(null, slotIdx + 1 - _lastSeenCollectibles.Count));
        }

        var col = stack?.Collectible;
        if (_lastSeenCollectibles[slotIdx] == col || stack is { Collectible: null }) {
            return;
        }
        _lastSeenCollectibles[slotIdx] = col;

        var packet = new Packet {
            pointId = pointId,
            collectibleId = col?.Id ?? stack?.Id ?? -1,
            isBlock = col is Block
        };
        if (sendFor != null) {
            sapi.Network.SendBlockEntityPacket(sendFor, Pos, PacketId, packet);
        }
        else {
            sapi.Network.BroadcastBlockEntityPacket(Pos, PacketId, packet);
        }
    }
    
    public override void OnReceivedServerPacket(int packetid, byte[] data) {
        if (packetid == PacketId) {
            var packet = SerializerUtil.Deserialize<Packet>(data);
            var world = Api.World;
            var col = packet.collectibleId == -1 ? null : packet.isBlock ? (CollectibleObject)world.GetBlock(packet.collectibleId) : (CollectibleObject)world.GetItem(packet.collectibleId);
            _dirtySlots.AddOrUpdate(packet.pointId, col, (i, o) => col);
            world.BlockAccessor.MarkBlockDirty(Pos);
        }
    }

    public override void OnReceivedClientPacket(IPlayer fromPlayer, int packetid, byte[] data) {
        if (packetid != PacketId) {
            return;
        }
        foreach (var slot in _slotsToRender) {
            CheckAndSendPacketForSlot((ICoreServerAPI) Api, slot.Key, slot.Value, (ServerPlayer)fromPlayer);
        }
    }

    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessThreadTesselator) {
        foreach (var dirty in _dirtySlots) {
            RemeshSlot(tessThreadTesselator, dirty.Key, dirty.Value);
        }
        _dirtySlots.Clear(); //May potentially cause not up-to-date meshes

        for (int i = 0; i < _slotRenderPoints.Length; i++) {
            if (_slotRenderPoints[i].meshData == null) {
                continue;
            }
            mesher.AddMeshData(_slotRenderPoints[i].meshData, _slotRenderPoints[i].transform);
        }
        return false;
    }

    private void DisposeMeshes() {
        foreach (var slotRenderPoint in _slotRenderPoints) {
            slotRenderPoint.meshData?.Dispose();
        }
    }
    
    //A very inefficient way of making meshes
    private void RemeshSlot(ITesselatorAPI tesselator, int renderSlotIdx, CollectibleObject? collectible) {
        ref var rp = ref _slotRenderPoints[renderSlotIdx];
        rp.meshData?.Dispose();
        var capi = (ICoreClientAPI)Api;
        if (collectible != null) {
            //TODO do this only if actual change occurs or schedule
            MeshData mesh;
            switch(collectible) {
                case Item item: 
                    tesselator.TesselateItem(item, out mesh, new ItemOnBlockTexPositionSource(item, capi.BlockTextureAtlas));
                    break;
                case Block block: 
                    tesselator.TesselateBlock(block, out mesh);
                    break;
                default:
                    return;
            }
            rp.meshData = mesh;
            var mat = collectible.Attributes[_transformName].AsObject(ModelTransform.NoTransform).AsMatrix;
            Mat4f.Identity(rp.transform);
            DarUtil.Mat4fRotateAround(rp.transform, [_globalShapeRotation.X, _globalShapeRotation.Y, _globalShapeRotation.Z], DarUtil.Half);
            // Mat4f.Translate(rp.transform, -0.390625f, -0.05f, -0.5f);
            Mat4f.Mul(rp.transform, rp.BaseTransform);
            Mat4f.Mul(rp.transform, mat);
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

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public struct Packet {
        public int pointId;
        public int collectibleId;
        public bool isBlock;
    }
}