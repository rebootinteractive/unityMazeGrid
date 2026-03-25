using System;
using System.Collections.Generic;

namespace MazeGrid
{
    public enum BorderPieceType { None, Center, Edge, OuterCorner, InnerCorner }

    /// <summary>
    /// Describes a single border piece to place at a grid vertex.
    /// </summary>
    public struct BorderPiece
    {
        /// <summary>Position in the vertex grid (0..width, 0..height).</summary>
        public int vertexX;
        public int vertexY;

        /// <summary>Type of piece to place.</summary>
        public BorderPieceType type;

        /// <summary>Y-axis rotation in degrees (0, 90, 180, or 270).</summary>
        public float rotationY;

        public BorderPiece(int vx, int vy, BorderPieceType type, float rotationY)
        {
            this.vertexX = vx;
            this.vertexY = vy;
            this.type = type;
            this.rotationY = rotationY;
        }
    }

    /// <summary>
    /// Generates border piece placement data using marching squares.
    ///
    /// Given a 2D grid of solid/empty cells, produces a list of BorderPiece structs
    /// describing what piece to place at each vertex and its rotation. The caller
    /// is responsible for converting vertex positions to world space and spawning prefabs.
    ///
    /// This class has no dependency on MazeGrid — it works with any bool grid via a delegate.
    ///
    /// Vertex grid is (width+1) × (height+1). Each vertex (vx, vy) sits at the top-left
    /// corner of cell (vx, vy). The 4 cells touching a vertex are:
    ///   TL = (vx-1, vy-1)   TR = (vx, vy-1)
    ///   BL = (vx-1, vy)     BR = (vx, vy)
    /// </summary>
    public static class GridBorderBuilder
    {
        /// <summary>
        /// Generates border pieces for a grid using marching squares.
        /// </summary>
        /// <param name="width">Grid width in cells (columns).</param>
        /// <param name="height">Grid height in cells (rows).</param>
        /// <param name="isSolid">Returns true if cell at (col, row) is solid.
        /// Must handle out-of-bounds gracefully (return false).</param>
        /// <returns>List of border pieces. Does not include vertices where type is None.</returns>
        public static List<BorderPiece> Generate(int width, int height, Func<int, int, bool> isSolid)
        {
            var pieces = new List<BorderPiece>();

            // Iterate all vertices: (width+1) × (height+1)
            for (int vy = 0; vy <= height; vy++)
            {
                for (int vx = 0; vx <= width; vx++)
                {
                    // Sample the 4 cells touching this vertex
                    bool tl = isSolid(vx - 1, vy - 1);
                    bool tr = isSolid(vx, vy - 1);
                    bool bl = isSolid(vx - 1, vy);
                    bool br = isSolid(vx, vy);

                    // Build 4-bit case index: TL=8, TR=4, BL=2, BR=1
                    int caseIndex = (tl ? 8 : 0) | (tr ? 4 : 0) | (bl ? 2 : 0) | (br ? 1 : 0);

                    ClassifyVertex(vx, vy, caseIndex, pieces);
                }
            }

            return pieces;
        }

        /// <summary>
        /// Classifies a vertex based on its 4-bit marching squares case and adds
        /// the appropriate piece(s) to the list.
        ///
        /// Case bits: TL=8, TR=4, BL=2, BR=1
        /// </summary>
        private static void ClassifyVertex(int vx, int vy, int caseIndex, List<BorderPiece> pieces)
        {
            switch (caseIndex)
            {
                case 0: // 0000: all empty — no piece
                    break;

                case 15: // 1111: all solid — center (ground fill)
                    pieces.Add(new BorderPiece(vx, vy, BorderPieceType.Center, 0f));
                    break;

                // --- 1 solid: outer corner ---
                case 1: // 0001: BR only
                    pieces.Add(new BorderPiece(vx, vy, BorderPieceType.OuterCorner, 0f));
                    break;
                case 2: // 0010: BL only
                    pieces.Add(new BorderPiece(vx, vy, BorderPieceType.OuterCorner, 90f));
                    break;
                case 4: // 0100: TR only
                    pieces.Add(new BorderPiece(vx, vy, BorderPieceType.OuterCorner, 270f));
                    break;
                case 8: // 1000: TL only
                    pieces.Add(new BorderPiece(vx, vy, BorderPieceType.OuterCorner, 180f));
                    break;

                // --- 2 adjacent solid: edge ---
                case 3: // 0011: BL + BR (bottom row solid, border faces up)
                    pieces.Add(new BorderPiece(vx, vy, BorderPieceType.Edge, 0f));
                    break;
                case 5: // 0101: TR + BR (right column solid, border faces left)
                    pieces.Add(new BorderPiece(vx, vy, BorderPieceType.Edge, 270f));
                    break;
                case 10: // 1010: TL + BL (left column solid, border faces right)
                    pieces.Add(new BorderPiece(vx, vy, BorderPieceType.Edge, 90f));
                    break;
                case 12: // 1100: TL + TR (top row solid, border faces down)
                    pieces.Add(new BorderPiece(vx, vy, BorderPieceType.Edge, 180f));
                    break;

                // --- 3 solid: inner corner ---
                case 7: // 0111: TL missing
                    pieces.Add(new BorderPiece(vx, vy, BorderPieceType.InnerCorner, 0f));
                    break;
                case 11: // 1011: TR missing
                    pieces.Add(new BorderPiece(vx, vy, BorderPieceType.InnerCorner, 90f));
                    break;
                case 13: // 1101: BL missing
                    pieces.Add(new BorderPiece(vx, vy, BorderPieceType.InnerCorner, 270f));
                    break;
                case 14: // 1110: BR missing
                    pieces.Add(new BorderPiece(vx, vy, BorderPieceType.InnerCorner, 180f));
                    break;

                // --- 2 diagonal (saddle cases): emit two outer corners ---
                case 6: // 0110: TL empty, TR solid, BL solid, BR empty → diagonal
                    pieces.Add(new BorderPiece(vx, vy, BorderPieceType.OuterCorner, 90f));  // BL
                    pieces.Add(new BorderPiece(vx, vy, BorderPieceType.OuterCorner, 270f)); // TR
                    break;
                case 9: // 1001: TL solid, TR empty, BL empty, BR solid → diagonal
                    pieces.Add(new BorderPiece(vx, vy, BorderPieceType.OuterCorner, 0f));   // BR
                    pieces.Add(new BorderPiece(vx, vy, BorderPieceType.OuterCorner, 180f)); // TL
                    break;
            }
        }
    }
}
