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

    public static readonly float[] Half = [0.5f, 0.5f, 0.5f];
    
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
        
        float[] mat = new float[16];
        var span = (Span<float>)mat;
        Mat4f.NewIdentity(span);
        
        // Mat4f.RotateByXYZ(span, modelRot.X, modelRot.Y, modelRot.Z);

        // Mat4f.Translate(span, -0.5f, -0.5f, -0.5f);
        Mat4f.Mul(span, parent);
        var rot = new Vec3f((float)ap.RotationX, (float)ap.RotationY, (float)ap.RotationZ).Mul(GameMath.DEG2RAD);
        Mat4f.RotateByXYZ(span, rot.X, rot.Y, rot.Z);
        Mat4f.Translate(span, (float)ap.PosX / 16f, (float)ap.PosY / 16f, (float)ap.PosZ / 16f);
        
        return mat;
    }

    public static void Mat4fRotateAround(Span<float> mat, float[] rotXYZ, float[] origin) {
        Mat4f.Translate(mat, origin[0], origin[1], origin[2]); //last
        Mat4f.RotateByXYZ(mat, rotXYZ[0], rotXYZ[1], rotXYZ[2]); //second
        Mat4f.Translate(mat, -origin[0], -origin[1], -origin[2]); //first
    }

    public static void DarLog(Action<string> logMethod, string content) {
        logMethod($"[De Artibus Rusticis] {content}");
    }
}