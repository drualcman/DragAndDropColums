namespace DragAndDropColums.Client.Models;

public class DragState<TData>
{
    public GridItem<TData>? DraggingItem { get; set; }
    public bool IsDragging { get; set; }
    public string? DragCursorClass { get; set; }
    public (int ClientX, int ClientY)? DragStartMouse { get; set; }
    public (int Col, int Row)? DragStartCell { get; set; }
    public (int Col, int Row)? HoverTarget { get; set; }
    public (int Col, int Row)? FinalDropTarget { get; set; }

    public void Reset()
    {
        DraggingItem = null;
        IsDragging = false;
        DragCursorClass = null;
        DragStartMouse = null;
        DragStartCell = null;
        HoverTarget = null;
        FinalDropTarget = null;
    }
}
