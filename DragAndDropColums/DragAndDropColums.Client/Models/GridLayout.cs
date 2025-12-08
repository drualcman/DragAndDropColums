namespace DragAndDropColums.Client.Models;

public class GridLayout
{
    public int Columns { get; set; } = 8;
    public int Rows { get; set; } = 10;
    public int CellSize { get; set; } = 60; // px
    public int Gap { get; set; } = 5; // px
    public List<GridItem> Items { get; set; } = new();
}
