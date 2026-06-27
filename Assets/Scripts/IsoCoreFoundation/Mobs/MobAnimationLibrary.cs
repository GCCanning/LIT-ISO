using System;
using UnityEngine;

namespace IsoCore.Foundation
{
    public enum MobAnimState { Idle, Walk, Run, Attack, Hurt, Death }

    /// <summary>Four directional frame lists for one animation state. Rows of a sheet.</summary>
    [Serializable]
    public class MobDirectionFrames
    {
        public Sprite[] front; // facing the camera (down)
        public Sprite[] back;  // away (up)
        public Sprite[] left;
        public Sprite[] right;

        public Sprite[] For(int dir)
        {
            switch (dir)
            {
                case 1: return back;
                case 2: return left;
                case 3: return right;
                default: return front;
            }
        }

        public bool HasAny =>
            (front != null && front.Length > 0) || (back != null && back.Length > 0) ||
            (left != null && left.Length > 0) || (right != null && right.Length > 0);
    }

    [Serializable]
    public class MobAnimEntry
    {
        public string mobId;
        public float secondsPerFrame = 0.15f; // ~150ms/frame per the source .tmx
        public MobDirectionFrames idle = new();
        public MobDirectionFrames walk = new();
        public MobDirectionFrames run = new();
        public MobDirectionFrames attack = new();
        public MobDirectionFrames hurt = new();
        public MobDirectionFrames death = new();

        public MobDirectionFrames State(MobAnimState s)
        {
            switch (s)
            {
                case MobAnimState.Walk: return walk;
                case MobAnimState.Run: return run;
                case MobAnimState.Attack: return attack;
                case MobAnimState.Hurt: return hurt;
                case MobAnimState.Death: return death;
                default: return idle;
            }
        }
    }

    /// <summary>
    /// Sliced directional sprites for animated mobs (predator plants today). Authored by the
    /// editor bake tool from the imported sheets and loaded at runtime from
    /// <c>Resources/Mobs/MobAnimationLibrary</c>. Mobs without an entry fall back to a blob.
    /// </summary>
    public class MobAnimationLibrary : ScriptableObject
    {
        public MobAnimEntry[] entries = Array.Empty<MobAnimEntry>();

        static MobAnimationLibrary _cached;
        static bool _loaded;

        public static MobAnimationLibrary Load()
        {
            if (_loaded) return _cached;
            _loaded = true;
            _cached = Resources.Load<MobAnimationLibrary>("Mobs/MobAnimationLibrary");
            return _cached;
        }

        public MobAnimEntry Find(string mobId)
        {
            if (string.IsNullOrEmpty(mobId) || entries == null) return null;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i] != null && entries[i].mobId == mobId)
                    return entries[i];
            return null;
        }

        /// <summary>Map a planar/aim direction to a sheet row: 0 front, 1 back, 2 left, 3 right.</summary>
        public static int DirIndex(Vector2 dir)
        {
            if (dir.sqrMagnitude < 0.0001f) return 0;
            if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
                return dir.x < 0f ? 2 : 3;
            return dir.y > 0f ? 1 : 0;
        }
    }
}
