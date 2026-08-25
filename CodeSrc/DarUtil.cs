using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace DeArtibusRusticis;

using static MathF;

public static class DarUtil {

    private const float S = 0.5f;
    
    private static readonly float[][] CubePoints = [
        // [-S, -S, -S],
        [-S, -S,  S],
        [-S,  S, -S],
        [ S, -S, -S]
    ];
    
    public static Cuboidf CuboidFromMat4(float[] mat4) {
        Vec3f min = new Vec3f(); 
        Vec3f max = new Vec3f();

        float[] vec = new float[3];
        
        foreach (var point in CubePoints) {
            // Mat4f.MulWithVec3_Position(mat4, point, vec, 0);
            Mat4f.MulWithVec3(mat4, point, vec);
            min[0] = Min(vec[0], min[0]);
            min[1] = Min(vec[1], min[1]);
            min[2] = Min(vec[2], min[2]);
            
            max[0] = Max(vec[0], max[0]);
            max[1] = Max(vec[1], max[1]);
            max[2] = Max(vec[2], max[2]);
        }
        
        var cuboid = new Cuboidf(min, max);
        cuboid.Offset(mat4[12], mat4[13], mat4[14]);
        return cuboid;
    }
    
    public static float[] GetTotalApTransform(AttachmentPoint ap) {
        var parent = ap.ParentElement.GetInverseModelMatrix();
        Mat4f.Invert(parent);
        Mat4f.Translate(parent, parent, (float)ap.PosX, (float)ap.PosY, (float)ap.PosZ);
        var rot = new Vec3f((float)ap.RotationX, (float)ap.RotationX, (float)ap.RotationZ).Mul(GameMath.DEG2RAD);
        Mat4f.RotateByXYZ((Span<float>)parent, rot.X, rot.Y, rot.Z);
        return parent;
    }

    public static void DarLog(Action<string> logMethod, string content) {
        logMethod($"[De Artibus Rusticis] {content}");
    }
}