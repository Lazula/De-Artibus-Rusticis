using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace DeArtibusRusticis.Blocks;

public class BlockMultiSelection : Block {

    private Cuboidf[] _selections = null!;
    
    public override void OnLoaded(ICoreAPI api) {
        base.OnLoaded(api);
        var pointIds = Attributes["selectionBoxes"].AsArray<string>([]);
        var shape = Vintagestory.API.Common.Shape.TryGet(api, Shape.Base.CopyWithPathPrefixAndAppendix("shapes/", ".json"));
        shape.ResolveReferences(api.World.Logger, Shape.Base);
        _selections = new Cuboidf[pointIds.Length];
        
        var rot = Shape.RotateXYZCopy.Mul(GameMath.DEG2RAD);
        float[] half = [0.5f, 0.5f, 0.5f];
        float[] halfNeg = [-0.5f, -0.5f, -0.5f];
        
        for (int i = 0; i < pointIds.Length; i++) {
            var id = pointIds[i];
            if (id.StartsWith("@")) {
                var n = int.Parse(id.Substring(1));
                _selections[i] = SelectionBoxes[n];
                continue;
            }
            var element = shape.GetElementByName(id);
            var mat = new float[16];
            float xSize = (float) (element.To![0] - element.From![0]) / 16f;
            float ySize = (float) (element.To![1] - element.From![1]) / 16f;
            float zSize = (float) (element.To![2] - element.From![2]) / 16f;
            Mat4f.Identity(mat);
            Mat4f.Translate(mat, mat, half);
            Mat4f.RotateByXYZ(mat, rot.X, rot.Y, rot.Z);
            Mat4f.Translate(mat, mat, halfNeg);
            
            Mat4f.Translate(mat, mat, [xSize/2f, ySize/2f, zSize/2f]);
            
            var elementMat = element.GetInverseModelMatrix();
            Mat4f.Invert(elementMat, elementMat);
            Mat4f.Mul(mat, mat, elementMat);
            
            Mat4f.Scale(mat, mat, [ xSize, ySize, zSize ]);
            _selections[i] = DarUtil.CuboidFromMat4(mat);
        }
    }
    
    public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos) {
        Cuboidf TransformBox(Cuboidf box) {
            box.OffsetCopy(pos);
            // var cube = new Cuboidf(new Size3f(box.XSize / 2, box.YSize / 2, box.ZSize / 2));
            // var center = box.Center;
            // cube.Offset((float) -center.X, (float) -center.Y, (float) -center.Z);
            return box.Clone();//.OmniGrowBy(0.05f);
        }
        return _selections.Select(TransformBox).ToArray();
    }

    public void RenderSelectionBoxes(BlockSelection blockSel, RenderBoxDelegate renderBoxHandler) {
        
    }

    public override void OnCollectTextures(ICoreAPI api, ITextureLocationDictionary textureDict) {
        base.OnCollectTextures(api, textureDict);
        textureDict.GetOrAddTextureLocation(new AssetLocationAndSource(new AssetLocation(Code.Domain, "item/food/herb/chives"), "", Code));
    }
}