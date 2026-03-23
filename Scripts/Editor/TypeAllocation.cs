namespace MazeGrid.Editor
{
    public struct TypeAllocation
    {
        public int typeIndex;
        public int instanceCount;

        public TypeAllocation(int typeIndex, int instanceCount)
        {
            this.typeIndex = typeIndex;
            this.instanceCount = instanceCount;
        }
    }
}
