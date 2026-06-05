namespace IsoCore.Foundation
{
    /// <summary>A square block of cells, sampled once and cached.</summary>
    public class IsoChunk
    {
        public readonly int Cx;       // chunk coordinate (not cell)
        public readonly int Cy;
        public readonly int Size;
        public readonly IsoCell[] Cells;

        public IsoChunk(int cx, int cy, int size)
        {
            Cx = cx; Cy = cy; Size = size;
            Cells = new IsoCell[size * size];
        }

        public int Index(int localX, int localY) => localY * Size + localX;
    }
}
