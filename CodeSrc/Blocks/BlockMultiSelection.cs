using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace DeArtibusRusticis.Blocks;

public class BlockMultiSelection : Block {

    private Cuboidf[] _selections;
    
    public override void OnLoaded(ICoreAPI api) {
        base.OnLoaded(api);
        var pointIds = Attributes["selectionBoxes"].AsArray<string>([]);
        var shape = Vintagestory.API.Common.Shape.TryGet(api, Shape.Base.CopyWithPathPrefixAndAppendix("shapes/", ".json"));
        
        _selections = new Cuboidf[pointIds.Length];
        for (int i = 0; i < pointIds.Length; i++) {
            var id = pointIds[i];
            if (id.StartsWith("@")) {
                var n = int.Parse(id.Substring(1));
                _selections[i] = SelectionBoxes[n];
                continue;
            }
            var element = shape.GetElementByName(id);
            var mat = element.GetInverseModelMatrix();
            _selections[i] = MathUtil.CuboidFromMat4(mat);
        }
    }
    
    public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos) {
        Cuboidf TransformBox(Cuboidf box) {
            box.OffsetCopy(pos);
            var cube = new Cuboidf(new Size3f(box.XSize / 2, box.YSize / 2, box.ZSize / 2));
            return cube;
        }
        return _selections.Select(TransformBox).ToArray();
    }
    
}