namespace DragAndDropColums.Client.Services;

public class DragService
{
    private readonly GridLayout _layout;

    public DragService(GridLayout layout)
    {
        _layout = layout;
    }

    public (int Col, int Row)? CalculateDragTarget(MouseEventArgs e,
        DragState dragState, GridItem draggingItem)
    {
        if (!dragState.DragStartMouse.HasValue || !dragState.DragStartCell.HasValue)
            return null;

        (int startX, int startY) = dragState.DragStartMouse.Value;
        (int startCol, int startRow) = dragState.DragStartCell.Value;

        int deltaX = (int)e.ClientX - startX;
        int deltaY = (int)e.ClientY - startY;
        int cellSizeWithGap = _layout.CellSize + _layout.Gap;

        if (Math.Abs(deltaX) < 8 && Math.Abs(deltaY) < 8)
            return null;

        int deltaCol = CalculateDelta(deltaX, cellSizeWithGap);
        int deltaRow = CalculateDelta(deltaY, cellSizeWithGap);

        int hoverCol = startCol + deltaCol;
        int hoverRow = startRow + deltaRow;

        hoverCol = Math.Clamp(hoverCol, 1, _layout.Columns);
        hoverRow = Math.Clamp(hoverRow, 1, _layout.Rows);

        int maxCol = _layout.Columns - draggingItem.ColumnSpan + 1;
        int maxRow = _layout.Rows - draggingItem.RowSpan + 1;

        int finalCol = Math.Clamp(hoverCol, 1, maxCol);
        int finalRow = Math.Clamp(hoverRow, 1, maxRow);

        return (finalCol, finalRow);
    }

    private int CalculateDelta(int delta, int cellSizeWithGap)
    {
        return delta >= 0
            ? (int)Math.Floor(delta / (double)cellSizeWithGap)
            : (int)Math.Ceiling(delta / (double)cellSizeWithGap);
    }
}
