// Generated from the tile PNGs - do not hand edit.
// w/h are world units at TilePPU; surface is how far below the sprite
// top the flat standable body starts; solid is how far down it stays.
using System.Collections.Generic;

public static class TileData
{
    public struct T { public float w, h, surface, solid;
        public T(float a, float b, float c, float d) { w=a; h=b; surface=c; solid=d; } }

    public static readonly Dictionary<string, T> Map = new Dictionary<string, T>
    {
        { "block_rune", new T(1.7244f, 1.4615f, 0.1731f, 1.4038f) },
        { "block_small", new T(1.7115f, 1.0064f, 0.1410f, 1.0000f) },
        { "edge_broken", new T(1.4808f, 1.5385f, 0.1154f, 0.7692f) },
        { "ground_left", new T(1.7244f, 1.6987f, 0.7115f, 1.6923f) },
        { "ground_mid", new T(1.7885f, 1.0769f, 0.0897f, 1.0705f) },
        { "ground_right", new T(1.7756f, 1.0962f, 0.1154f, 1.0897f) },
        { "ledge", new T(1.5385f, 1.3718f, 0.0769f, 0.5128f) },
        { "pillar_base", new T(1.7885f, 1.7949f, 1.2885f, 1.7821f) },
        { "pillar_mid", new T(1.2115f, 1.8269f, 0.0000f, 1.8205f) },
        { "pillar_top", new T(1.7308f, 1.8718f, 0.0577f, 1.0577f) },
        { "plat_left", new T(1.7179f, 2.0128f, 0.0000f, 1.1218f) },
        { "plat_mid", new T(1.9038f, 1.8205f, 0.0000f, 1.2821f) },
        { "plat_right", new T(1.7500f, 1.8846f, 0.0000f, 1.1090f) },
        { "plat_single", new T(1.8013f, 1.5962f, 0.0000f, 1.0705f) },
        { "slab", new T(1.9872f, 0.8397f, 0.1346f, 0.8013f) },
        { "stairs", new T(1.7756f, 1.4808f, 0.9295f, 1.4359f) },
    };
}
