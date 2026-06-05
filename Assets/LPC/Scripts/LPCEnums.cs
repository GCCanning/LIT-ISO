// LPCEnums.cs - Core enums for the LPC sprite system
//
// LPC = Liberated Pixel Cup, a CC-BY-SA pixel art asset library.
// Standard LPC sheet is 64x64 per frame, arranged in rows by animation.
// Each animation has 4 directions (N/W/S/E) per the universal LPC layout.

namespace LITISO.LPC
{
    /// <summary>Cardinal directions matching LPC sheet row ordering.</summary>
    public enum LPCDirection
    {
        North = 0,  // Row offset 0
        West  = 1,  // Row offset 1
        South = 2,  // Row offset 2
        East  = 3   // Row offset 3
    }

    /// <summary>
    /// Animation rows in a universal LPC sheet (in vertical order).
    /// Each entry occupies 4 consecutive rows (one per direction).
    /// </summary>
    public enum LPCAnimation
    {
        Spellcast,  // 7 frames
        Thrust,     // 8 frames
        Walk,       // 9 frames
        Slash,      // 6 frames
        Shoot,      // 13 frames
        Hurt,       // 6 frames (1 direction only - South)
        Idle,       // 2 frames
        Run,        // 8 frames
        Jump,       // 5 frames
        Sit,        // 3 frames
        Emote,      // 3 frames
        Climb,      // 6 frames
        Combat      // 2 frames (combat idle stance)
    }

    /// <summary>Body type variants supported by LPC.</summary>
    public enum LPCBodyType
    {
        Male,
        Female,
        Muscular,
        Teen,
        Pregnant,
        Child
    }

    /// <summary>Equipment slot z-order matches LPC layer system.</summary>
    public enum LPCLayer
    {
        Shadow        = 0,
        Body          = 10,
        Eyes          = 11,
        Hair          = 15,
        FacialHair    = 16,
        Head          = 20,  // hats, helmets
        Neck          = 25,  // capes (back-of-neck)
        Torso         = 30,  // shirts, armor body
        Legs          = 35,
        Feet          = 40,
        Belt          = 45,
        Arms          = 50,  // bracers, gloves
        Shoulders     = 55,
        WeaponBack    = 60,  // sheathed weapon
        Shield        = 65,
        WeaponMain    = 70,  // held weapon
        OverlayEffect = 80   // glows, fire, etc.
    }
}
