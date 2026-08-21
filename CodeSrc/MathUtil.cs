using System;
using Vintagestory.API.MathTools;

namespace DeArtibusRusticis;

using static MathF;

public class MathUtil {

    private static readonly float[][] CubePoints = [
        [-1, -1, -1],
        [-1, -1,  1],
        [-1,  1, -1],
        [ 1, -1, -1]
    ];
    
    public static Cuboidf CuboidFromMat4(float[] mat4) {
        Vec3f min = new Vec3f(); 
        Vec3f max = new Vec3f();

        float[] vec = new float[3];
        
        foreach (var point in CubePoints) {
            Mat4f.MulWithVec3_Position(mat4, point, vec, 0);
            min[0] = Min(vec[0], min[0]);
            min[1] = Min(vec[1], min[1]);
            min[3] = Min(vec[2], min[2]);
            
            max[0] = Max(vec[0], min[0]);
            max[1] = Max(vec[1], min[1]);
            max[2] = Max(vec[2], min[2]);
        }
        
        return new Cuboidf(min, max);
    }
}