using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace DeArtibusRusticis.Rendering;

public class ItemOnBlockTexPositionSource : ITexPositionSource {

    private readonly Dictionary<string, (int? id, AssetLocation loc)> _texMapping = new();

    private readonly IBlockTextureAtlasAPI _atlas;
    
    public ItemOnBlockTexPositionSource(Item item, IBlockTextureAtlasAPI atlas) {
        _atlas = atlas;
        foreach (var tex in item.Textures) {
            _texMapping.Add(tex.Key, (null, tex.Value.Base));
        }
    }

    public TextureAtlasPosition? this[string textureCode] {
        get {
            var pos = (TextureAtlasPosition?)null;
            if (_texMapping.TryGetValue(textureCode, out var tex)) {
                _atlas.GetOrInsertTexture(tex.loc, out _, out pos);   
            }
            else if(_texMapping.TryGetValue("all", out tex)) {
                _atlas.GetOrInsertTexture(tex.loc, out _, out pos);   
            }
            
            return pos ?? _atlas.UnknownTexturePosition;
        }
    }

    public Size2i? AtlasSize => _atlas.Size;
}