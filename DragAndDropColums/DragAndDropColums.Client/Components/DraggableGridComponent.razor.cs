using Microsoft.AspNetCore.Components.Web;

namespace DragAndDropColums.Client.Components;

public partial class DraggableGridComponent
{
    [Parameter] public GridLayout Layout { get; set; } = new();
    [Parameter] public EventCallback<GridLayout> LayoutChanged { get; set; }

    private ElementReference containerRef;
    private GridItem? SelectedItem { get; set; }
    private GridItem? DraggingItem { get; set; }
    private bool IsDragging { get; set; }
    private string? DragCursorClass { get; set; }
    private (int ClientX, int ClientY)? DragStartMouse { get; set; }
    private (int Col, int Row)? DragStartCell { get; set; }
    private (int Col, int Row)? HoverTarget { get; set; }
    private (int Col, int Row)? FinalDropTarget { get; set; }

    protected override void OnParametersSet()
    {
        if (Layout.Items.Any() && SelectedItem == null)
        {
            SelectedItem = Layout.Items.First();
        }
        UpdateOccupiedCells();
    }

    private string GetGridStyle()
    {
        return "display: grid; " +
               $"grid-template-columns: repeat({Layout.Columns}, {Layout.CellSize}px); " +
               $"grid-template-rows: repeat({Layout.Rows}, {Layout.CellSize}px); " +
               $"gap: {Layout.Gap}px; " +
               $"width: {Layout.Columns * (Layout.CellSize + Layout.Gap)}px; " +
               $"min-height: {Layout.Rows * (Layout.CellSize + Layout.Gap)}px;";
    }

    private string GetItemStyle(GridItem item)
    {
        bool isSelected = SelectedItem?.Id == item.Id;
        bool isDragging = DraggingItem?.Id == item.Id;

        string borderColor = isSelected ? "#007bff" :
                             isDragging ? "#ff6b6b" : "#333";
        string borderStyle = isSelected ? "3px solid" :
                             isDragging ? "3px dashed" : "2px solid";
        string boxShadow = isSelected ? "0 0 0 3px rgba(0, 123, 255, 0.3)" : "none";

        return $"grid-column: {item.Column} / span {item.ColumnSpan}; " +
               $"grid-row: {item.Row} / span {item.RowSpan}; " +
               $"background-color: {item.BackgroundColor}; " +
               $"border: {borderStyle} {borderColor}; " +
               $"box-shadow: {boxShadow}; " +
               $"z-index: {(isSelected || isDragging ? "100" : "10")};" +
               $"{(isDragging ? "opacity: 0.8; cursor: grabbing;" : "cursor: grab;")}";
    }

    private void StartMouseDrag(MouseEventArgs e, GridItem item)
    {
        DraggingItem = item;
        SelectedItem = item;
        IsDragging = true;
        DragCursorClass = "dragging";

        DragStartMouse = ((int)e.ClientX, (int)e.ClientY);
        DragStartCell = (item.Column, item.Row);

        StateHasChanged();
    }

    private void HandleMouseMove(MouseEventArgs e)
    {
        // If we are not dragging, do nothing.
        if (!IsDragging || DraggingItem == null || !DragStartMouse.HasValue || !DragStartCell.HasValue)
        {
            return;
        }

        (int startX, int startY) = DragStartMouse.Value;
        (int startCol, int startRow) = DragStartCell.Value;

        int deltaX = (int)e.ClientX - startX;
        int deltaY = (int)e.ClientY - startY;
        int cellSizeWithGap = Layout.CellSize + Layout.Gap;

        // Only process if mouse moved enough to avoid jitter.
        if (Math.Abs(deltaX) < 8 && Math.Abs(deltaY) < 8)
        {
            return;
        }

        // Compute delta columns and rows using directional floor/ceiling:
        // - For positive movement use Floor so small movements do not round up prematurely.
        // - For negative movement use Ceiling so small negative movements do not round down prematurely.
        int deltaCol;
        int deltaRow;

        if (deltaX >= 0)
        {
            deltaCol = (int)Math.Floor(deltaX / (double)cellSizeWithGap);
        }
        else
        {
            deltaCol = (int)Math.Ceiling(deltaX / (double)cellSizeWithGap);
        }

        if (deltaY >= 0)
        {
            deltaRow = (int)Math.Floor(deltaY / (double)cellSizeWithGap);
        }
        else
        {
            deltaRow = (int)Math.Ceiling(deltaY / (double)cellSizeWithGap);
        }

        // Compute a hover target based on top-left of the dragging item.
        int hoverCol = startCol + deltaCol;
        int hoverRow = startRow + deltaRow;

        // Clamp hover inside grid (1-based coordinates)
        if (hoverCol < 1)
        {
            hoverCol = 1;
        }

        if (hoverCol > Layout.Columns)
        {
            hoverCol = Layout.Columns;
        }

        if (hoverRow < 1)
        {
            hoverRow = 1;
        }

        if (hoverRow > Layout.Rows)
        {
            hoverRow = Layout.Rows;
        }

        HoverTarget = (hoverCol, hoverRow);

        // FinalDropTarget: where the item top-left can actually fit (prevent overflow)
        int maxCol = Layout.Columns - DraggingItem.ColumnSpan + 1;
        int maxRow = Layout.Rows - DraggingItem.RowSpan + 1;

        int finalColCandidate = hoverCol;
        if (finalColCandidate < 1)
        {
            finalColCandidate = 1;
        }

        if (finalColCandidate > maxCol)
        {
            finalColCandidate = maxCol;
        }

        int finalRowCandidate = hoverRow;
        if (finalRowCandidate < 1)
        {
            finalRowCandidate = 1;
        }

        if (finalRowCandidate > maxRow)
        {
            finalRowCandidate = maxRow;
        }

        FinalDropTarget = (finalColCandidate, finalRowCandidate);

        StateHasChanged();
    }

    private async Task HandleMouseUp(MouseEventArgs e)
    {
        if (IsDragging && DraggingItem != null && FinalDropTarget.HasValue)
        {
            (int col, int row) = FinalDropTarget.Value;
            await SimplePlaceItem(DraggingItem, col, row);
        }

        CleanUpDrag();
    }

    private void CleanUpDrag()
    {
        IsDragging = false;
        DraggingItem = null;
        HoverTarget = null;
        FinalDropTarget = null;
        DragStartMouse = null;
        DragStartCell = null;
        DragCursorClass = null;
        StateHasChanged();
    }

    private bool ItemsCollide(GridItem item1, int col1, int row1, GridItem item2)
    {
        bool colOverlap = col1 < item2.Column + item2.ColumnSpan && col1 + item1.ColumnSpan > item2.Column;
        bool rowOverlap = row1 < item2.Row + item2.RowSpan && row1 + item1.RowSpan > item2.Row;

        return colOverlap && rowOverlap;
    }

    private bool HasCollision(GridItem item, int col, int row, List<Guid>? ignoreIds = null)
    {
        if (col < 1)
        {
            return true;
        }

        if (row < 1)
        {
            return true;
        }

        if (col + item.ColumnSpan - 1 > Layout.Columns)
        {
            return true;
        }

        if (row + item.RowSpan - 1 > Layout.Rows)
        {
            return true;
        }

        foreach (GridItem other in Layout.Items)
        {
            if (other.Id == item.Id)
            {
                continue;
            }

            if (ignoreIds != null && ignoreIds.Contains(other.Id))
            {
                continue;
            }

            if (ItemsCollide(item, col, row, other))
            {
                return true;
            }
        }

        return false;
    }

    private List<GridItem> GetCollisionsAt(GridItem item, int col, int row)
    {
        List<GridItem> collisions = new List<GridItem>();

        foreach (GridItem other in Layout.Items)
        {
            if (other.Id == item.Id)
            {
                continue;
            }

            if (ItemsCollide(item, col, row, other))
            {
                collisions.Add(other);
            }
        }

        return collisions;
    }

    private async Task SimplePlaceItem(GridItem item, int targetCol, int targetRow)
    {
        int boundedTargetCol = Math.Max(1, Math.Min(targetCol, Layout.Columns - item.ColumnSpan + 1));
        int boundedTargetRow = Math.Max(1, Math.Min(targetRow, Layout.Rows - item.RowSpan + 1));

        if (item.Column == boundedTargetCol && item.Row == boundedTargetRow)
        {
            return;
        }

        int deltaCol = boundedTargetCol - item.Column;
        int deltaRow = boundedTargetRow - item.Row;

        Dictionary<Guid, (int Column, int Row)> originalPositions = Layout.Items.ToDictionary(i => i.Id, i => (i.Column, i.Row));

        item.Column = boundedTargetCol;
        item.Row = boundedTargetRow;

        List<GridItem> collisions = GetCollisionsAt(item, boundedTargetCol, boundedTargetRow);

        if (collisions.Count == 0)
        {
            await LayoutChanged.InvokeAsync(Layout);
            StateHasChanged();
            return;
        }

        bool success = true;
        HashSet<Guid> processedItems = new HashSet<Guid> { item.Id };

        foreach (GridItem collidingItem in collisions)
        {
            bool resolved = await ProcessCollision(collidingItem, item, deltaCol, deltaRow, processedItems, originalPositions);
            if (!resolved)
            {
                success = false;
                break;
            }
        }

        if (success)
        {
            bool hasNewCollisions = false;
            foreach (GridItem gridItem in Layout.Items)
            {
                if (HasCollision(gridItem, gridItem.Column, gridItem.Row))
                {
                    hasNewCollisions = true;
                    break;
                }
            }

            if (!hasNewCollisions)
            {
                await LayoutChanged.InvokeAsync(Layout);
                StateHasChanged();
                return;
            }
        }

        RevertToOriginalPositions(originalPositions);

        await TrySwapPlacement(item, boundedTargetCol, boundedTargetRow, deltaCol, deltaRow);
    }

    private async Task<bool> ProcessCollision(GridItem collidingItem, GridItem pushingItem,
                                            int deltaCol, int deltaRow, HashSet<Guid> processedItems,
                                            Dictionary<Guid, (int Column, int Row)> originalPositions)
    {
        if (processedItems.Contains(collidingItem.Id))
        {
            return true;
        }

        processedItems.Add(collidingItem.Id);

        int newCol = collidingItem.Column;
        int newRow = collidingItem.Row;

        if (deltaCol != 0)
        {
            newCol = collidingItem.Column + Math.Sign(deltaCol);
            newRow = collidingItem.Row;
        }
        else if (deltaRow != 0)
        {
            newCol = collidingItem.Column;
            newRow = collidingItem.Row + Math.Sign(deltaRow);
        }

        bool hitBoundary = false;

        if (deltaCol > 0 && newCol + collidingItem.ColumnSpan - 1 > Layout.Columns)
        {
            newCol = pushingItem.Column - collidingItem.ColumnSpan;
            hitBoundary = true;
        }
        else if (deltaCol < 0 && newCol < 1)
        {
            newCol = pushingItem.Column + pushingItem.ColumnSpan;
            hitBoundary = true;
        }
        else if (deltaRow > 0 && newRow + collidingItem.RowSpan - 1 > Layout.Rows)
        {
            newRow = pushingItem.Row - collidingItem.RowSpan;
            hitBoundary = true;
        }
        else if (deltaRow < 0 && newRow < 1)
        {
            newRow = pushingItem.Row + pushingItem.RowSpan;
            hitBoundary = true;
        }

        newCol = Math.Max(1, Math.Min(newCol, Layout.Columns - collidingItem.ColumnSpan + 1));
        newRow = Math.Max(1, Math.Min(newRow, Layout.Rows - collidingItem.RowSpan + 1));

        int originalCol = collidingItem.Column;
        int originalRow = collidingItem.Row;

        collidingItem.Column = newCol;
        collidingItem.Row = newRow;

        List<GridItem> newCollisions = GetCollisionsAt(collidingItem, newCol, newRow)
            .Where(c => !processedItems.Contains(c.Id))
            .ToList();

        foreach (GridItem newCollision in newCollisions)
        {
            bool ok = await ProcessCollision(newCollision, collidingItem,
                                      deltaCol, deltaRow, processedItems, originalPositions);
            if (!ok)
            {
                collidingItem.Column = originalCol;
                collidingItem.Row = originalRow;
                return false;
            }
        }

        if (hitBoundary && HasCollision(collidingItem, newCol, newRow))
        {
            bool foundAlternative = FindAlternativePosition(collidingItem, pushingItem, newCol, newRow, deltaCol, deltaRow);
            if (!foundAlternative)
            {
                collidingItem.Column = originalCol;
                collidingItem.Row = originalRow;
                return false;
            }

            newCollisions = GetCollisionsAt(collidingItem, collidingItem.Column, collidingItem.Row)
                .Where(c => !processedItems.Contains(c.Id))
                .ToList();

            foreach (GridItem newCollision in newCollisions)
            {
                bool ok = await ProcessCollision(newCollision, collidingItem,
                                          deltaCol, deltaRow, processedItems, originalPositions);
                if (!ok)
                {
                    collidingItem.Column = originalCol;
                    collidingItem.Row = originalRow;
                    return false;
                }
            }
        }

        return true;
    }

    private bool FindAlternativePosition(GridItem item, GridItem pushingItem,
                                       int preferredCol, int preferredRow,
                                       int deltaCol, int deltaRow)
    {
        int originalCol = item.Column;
        int originalRow = item.Row;

        if (deltaCol != 0)
        {
            for (int rowOffset = 1; rowOffset <= Layout.Rows; rowOffset++)
            {
                int testRow = preferredRow + rowOffset;
                if (testRow + item.RowSpan - 1 <= Layout.Rows)
                {
                    if (!HasCollision(item, preferredCol, testRow, new List<Guid> { pushingItem.Id }))
                    {
                        item.Column = preferredCol;
                        item.Row = testRow;
                        return true;
                    }
                }

                testRow = preferredRow - rowOffset;
                if (testRow >= 1)
                {
                    if (!HasCollision(item, preferredCol, testRow, new List<Guid> { pushingItem.Id }))
                    {
                        item.Column = preferredCol;
                        item.Row = testRow;
                        return true;
                    }
                }
            }
        }
        else if (deltaRow != 0)
        {
            for (int colOffset = 1; colOffset <= Layout.Columns; colOffset++)
            {
                int testCol = preferredCol + colOffset;
                if (testCol + item.ColumnSpan - 1 <= Layout.Columns)
                {
                    if (!HasCollision(item, testCol, preferredRow, new List<Guid> { pushingItem.Id }))
                    {
                        item.Column = testCol;
                        item.Row = preferredRow;
                        return true;
                    }
                }

                testCol = preferredCol - colOffset;
                if (testCol >= 1)
                {
                    if (!HasCollision(item, testCol, preferredRow, new List<Guid> { pushingItem.Id }))
                    {
                        item.Column = testCol;
                        item.Row = preferredRow;
                        return true;
                    }
                }
            }
        }

        for (int row = 1; row <= Layout.Rows - item.RowSpan + 1; row++)
        {
            for (int col = 1; col <= Layout.Columns - item.ColumnSpan + 1; col++)
            {
                if (!HasCollision(item, col, row, new List<Guid> { pushingItem.Id }))
                {
                    item.Column = col;
                    item.Row = row;
                    return true;
                }
            }
        }

        item.Column = originalCol;
        item.Row = originalRow;
        return false;
    }

    private async Task TrySwapPlacement(GridItem item, int targetCol, int targetRow, int deltaCol, int deltaRow)
    {
        GridItem? itemAtTarget = FindItemAtPosition(targetCol, targetRow);

        if (itemAtTarget != null && itemAtTarget.Id != item.Id)
        {
            int tempCol = item.Column;
            int tempRow = item.Row;

            itemAtTarget.Column = tempCol;
            itemAtTarget.Row = tempRow;

            item.Column = targetCol;
            item.Row = targetRow;

            bool hasCollisions = false;
            foreach (GridItem gridItem in Layout.Items)
            {
                if (HasCollision(gridItem, gridItem.Column, gridItem.Row))
                {
                    hasCollisions = true;
                    break;
                }
            }

            if (!hasCollisions)
            {
                await LayoutChanged.InvokeAsync(Layout);
                StateHasChanged();
                return;
            }
            else
            {
                itemAtTarget.Column = targetCol;
                itemAtTarget.Row = targetRow;
                item.Column = tempCol;
                item.Row = tempRow;
            }
        }

        await FindClosestFreePosition(item, targetCol, targetRow);
    }

    private GridItem? FindItemAtPosition(int col, int row)
    {
        return Layout.Items.FirstOrDefault(item =>
            col >= item.Column && col < item.Column + item.ColumnSpan &&
            row >= item.Row && row < item.Row + item.RowSpan);
    }

    private async Task FindClosestFreePosition(GridItem item, int targetCol, int targetRow)
    {
        HashSet<(int, int)> visited = new HashSet<(int, int)>();
        Queue<(int col, int row, int distance)> queue = new Queue<(int col, int row, int distance)>();

        queue.Enqueue((targetCol, targetRow, 0));
        visited.Add((targetCol, targetRow));

        while (queue.Count > 0)
        {
            (int col, int row, int distance) = queue.Dequeue();

            if (!HasCollision(item, col, row))
            {
                item.Column = col;
                item.Row = row;
                await LayoutChanged.InvokeAsync(Layout);
                StateHasChanged();
                return;
            }

            (int, int)[] neighbors = new (int, int)[]
            {
                (col + 1, row), (col - 1, row),
                (col, row + 1), (col, row - 1)
            };

            foreach ((int nCol, int nRow) in neighbors)
            {
                if (nCol >= 1 && nCol <= Layout.Columns - item.ColumnSpan + 1 &&
                    nRow >= 1 && nRow <= Layout.Rows - item.RowSpan + 1 &&
                    !visited.Contains((nCol, nRow)))
                {
                    visited.Add((nCol, nRow));
                    queue.Enqueue((nCol, nRow, distance + 1));
                }
            }
        }
    }

    private void RevertToOriginalPositions(Dictionary<Guid, (int Column, int Row)> originalPositions)
    {
        foreach (GridItem item in Layout.Items)
        {
            if (originalPositions.TryGetValue(item.Id, out var originalPos))
            {
                item.Column = originalPos.Column;
                item.Row = originalPos.Row;
            }
        }
    }

    private async Task MoveSelectedItem(int deltaCol, int deltaRow)
    {
        if (SelectedItem == null)
        {
            return;
        }

        int newCol = SelectedItem.Column + deltaCol;
        int newRow = SelectedItem.Row + deltaRow;

        await SimplePlaceItem(SelectedItem, newCol, newRow);
    }

    private async Task SelectItem(GridItem item)
    {
        if (SelectedItem?.Id == item.Id)
        {
            return;
        }

        SelectedItem = item;
        await InvokeAsync(StateHasChanged);
    }

    private async Task CellClicked(int col, int row)
    {
        if (IsDragging)
        {
            return;
        }

        GridItem? itemAtCell = FindItemAtCell(col, row);

        if (itemAtCell != null)
        {
            await SelectItem(itemAtCell);
            return;
        }

        if (SelectedItem != null)
        {
            await SimplePlaceItem(SelectedItem, col, row);
        }
    }

    private void DeselectItem()
    {
        SelectedItem = null;
        StateHasChanged();
    }

    private GridItem? FindItemAtCell(int col, int row)
    {
        return Layout.Items.FirstOrDefault(item =>
            col >= item.Column && col < item.Column + item.ColumnSpan &&
            row >= item.Row && row < item.Row + item.RowSpan);
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "ArrowUp":
                await MoveSelectedItem(0, -1);
                break;
            case "ArrowDown":
                await MoveSelectedItem(0, 1);
                break;
            case "ArrowLeft":
                await MoveSelectedItem(-1, 0);
                break;
            case "ArrowRight":
                await MoveSelectedItem(1, 0);
                break;
            case "Delete":
            case "Backspace":
                if (SelectedItem != null)
                {
                    RemoveItem(SelectedItem);
                }
                break;
            case "Escape":
                DeselectItem();
                break;
            case " ":
                if (SelectedItem != null)
                {
                    DeselectItem();
                }
                else if (Layout.Items.Any())
                {
                    SelectedItem = Layout.Items.First();
                    StateHasChanged();
                }
                break;
        }
    }

    private void RemoveItem(GridItem item)
    {
        Layout.Items.Remove(item);
        if (SelectedItem?.Id == item.Id)
        {
            SelectedItem = Layout.Items.FirstOrDefault();
        }

        LayoutChanged.InvokeAsync(Layout);
        StateHasChanged();
    }
    private HashSet<(int, int)> _occupiedCells = new();
    private void UpdateOccupiedCells()
    {
        _occupiedCells.Clear();
        foreach (var item in Layout.Items)
        {
            for (int r = item.Row; r < item.Row + item.RowSpan; r++)
            {
                for (int c = item.Column; c < item.Column + item.ColumnSpan; c++)
                {
                    _occupiedCells.Add((c, r));
                }
            }
        }
    }

    private HashSet<(int, int)> GetItemCells(GridItem item, int? targetCol = null, int? targetRow = null)
    {
        var cells = new HashSet<(int, int)>();
        int startCol = targetCol ?? item.Column;
        int startRow = targetRow ?? item.Row;

        for (int r = startRow; r < startRow + item.RowSpan; r++)
        {
            for (int c = startCol; c < startCol + item.ColumnSpan; c++)
            {
                cells.Add((c, r));
            }
        }

        return cells;
    }

    // Método para determinar si una celda es una esquina del área de drop
    private bool IsDropAreaCorner(int col, int row, GridItem item, (int Col, int Row) target)
    {
        return (col == target.Col && row == target.Row) || // Esquina superior izquierda
               (col == target.Col + item.ColumnSpan - 1 && row == target.Row) || // Esquina superior derecha
               (col == target.Col && row == target.Row + item.RowSpan - 1) || // Esquina inferior izquierda
               (col == target.Col + item.ColumnSpan - 1 && row == target.Row + item.RowSpan - 1); // Esquina inferior derecha
    }
}
