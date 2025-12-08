using Microsoft.AspNetCore.Components.Web;
using System.Text.Json;

namespace DragAndDropColums.Client.Components;

public partial class DraggableGrid
{
    [Parameter] public GridLayout Layout { get; set; } = new();
    [Parameter] public EventCallback<GridLayout> LayoutChanged { get; set; }

    private GridItem? SelectedItem { get; set; }
    private GridItem? DraggingItem { get; set; }
    private bool IsDragging { get; set; }
    private string? DragCursorClass { get; set; }
    private (int ClientX, int ClientY)? DragStartMouse { get; set; }
    private (int Col, int Row)? DragStartCell { get; set; }
    private (int Col, int Row)? HoverTarget { get; set; }
    private (int Col, int Row)? FinalDropTarget { get; set; }

    protected override void OnInitialized()
    {
        if (Layout.Items.Any() && SelectedItem == null)
        {
            SelectedItem = Layout.Items.First();
        }
    }

    private string GetGridStyle()
    {
        return $"display: grid; " +
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
        if (!IsDragging || DraggingItem == null || !DragStartMouse.HasValue || !DragStartCell.HasValue)
            return;

        var (startX, startY) = DragStartMouse.Value;
        var (startCol, startRow) = DragStartCell.Value;

        int deltaX = (int)e.ClientX - startX;
        int deltaY = (int)e.ClientY - startY;
        int cellSizeWithGap = Layout.CellSize + Layout.Gap;

        // Solo procesar si se movió lo suficiente
        if (Math.Abs(deltaX) < 8 && Math.Abs(deltaY) < 8)
            return;

        // Cálculo correcto con Math.Round (el más natural)
        int deltaCol = (int)Math.Round(deltaX / (double)cellSizeWithGap);
        int deltaRow = (int)Math.Round(deltaY / (double)cellSizeWithGap);

        // HoverTarget: celda bajo el ratón
        int hoverCol = startCol + deltaCol;
        int hoverRow = startRow + deltaRow;
        hoverCol = Math.Max(1, Math.Min(hoverCol, Layout.Columns));
        hoverRow = Math.Max(1, Math.Min(hoverRow, Layout.Rows));
        HoverTarget = (hoverCol, hoverRow);

        // FinalDropTarget: donde realmente cabe
        int maxCol = Layout.Columns - DraggingItem.ColumnSpan + 1;
        int maxRow = Layout.Rows - DraggingItem.RowSpan + 1;
        int finalCol = Math.Max(1, Math.Min(hoverCol, maxCol));
        int finalRow = Math.Max(1, Math.Min(hoverRow, maxRow));
        FinalDropTarget = (finalCol, finalRow);

        StateHasChanged();
    }

    private async Task HandleMouseUp(MouseEventArgs e)
    {
        if (IsDragging && DraggingItem != null && FinalDropTarget.HasValue)
        {
            var (col, row) = FinalDropTarget.Value;
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
        if (col < 1 || row < 1)
            return true;
        if (col + item.ColumnSpan - 1 > Layout.Columns)
            return true;
        if (row + item.RowSpan - 1 > Layout.Rows)
            return true;

        foreach (var other in Layout.Items)
        {
            if (other.Id == item.Id || (ignoreIds != null && ignoreIds.Contains(other.Id)))
                continue;

            if (ItemsCollide(item, col, row, other))
                return true;
        }

        return false;
    }

    private List<GridItem> GetCollisionsAt(GridItem item, int col, int row)
    {
        var collisions = new List<GridItem>();

        foreach (var other in Layout.Items)
        {
            if (other.Id == item.Id)
                continue;

            if (ItemsCollide(item, col, row, other))
                collisions.Add(other);
        }

        return collisions;
    }

    private async Task SimplePlaceItem(GridItem item, int targetCol, int targetRow)
    {
        targetCol = Math.Max(1, Math.Min(targetCol, Layout.Columns - item.ColumnSpan + 1));
        targetRow = Math.Max(1, Math.Min(targetRow, Layout.Rows - item.RowSpan + 1));

        if (item.Column == targetCol && item.Row == targetRow)
            return;

        int deltaCol = targetCol - item.Column;
        int deltaRow = targetRow - item.Row;

        var originalPositions = Layout.Items.ToDictionary(i => i.Id, i => (i.Column, i.Row));

        item.Column = targetCol;
        item.Row = targetRow;

        var collisions = GetCollisionsAt(item, targetCol, targetRow);

        if (collisions.Count == 0)
        {
            await LayoutChanged.InvokeAsync(Layout);
            StateHasChanged();
            return;
        }

        bool success = true;
        var processedItems = new HashSet<Guid> { item.Id };

        foreach (var collidingItem in collisions)
        {
            if (!await ProcessCollision(collidingItem, item, deltaCol, deltaRow, processedItems, originalPositions))
            {
                success = false;
                break;
            }
        }

        if (success)
        {
            bool hasNewCollisions = false;
            foreach (var gridItem in Layout.Items)
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

        await TrySwapPlacement(item, targetCol, targetRow, deltaCol, deltaRow);
    }

    private async Task<bool> ProcessCollision(GridItem collidingItem, GridItem pushingItem,
                                            int deltaCol, int deltaRow, HashSet<Guid> processedItems,
                                            Dictionary<Guid, (int Column, int Row)> originalPositions)
    {
        if (processedItems.Contains(collidingItem.Id))
            return true;

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

        var newCollisions = GetCollisionsAt(collidingItem, newCol, newRow)
            .Where(c => !processedItems.Contains(c.Id))
            .ToList();

        foreach (var newCollision in newCollisions)
        {
            if (!await ProcessCollision(newCollision, collidingItem,
                                      deltaCol, deltaRow, processedItems, originalPositions))
            {
                collidingItem.Column = originalCol;
                collidingItem.Row = originalRow;
                return false;
            }
        }

        if (hitBoundary && HasCollision(collidingItem, newCol, newRow))
        {
            if (!FindAlternativePosition(collidingItem, pushingItem, newCol, newRow, deltaCol, deltaRow))
            {
                collidingItem.Column = originalCol;
                collidingItem.Row = originalRow;
                return false;
            }

            newCollisions = GetCollisionsAt(collidingItem, collidingItem.Column, collidingItem.Row)
                .Where(c => !processedItems.Contains(c.Id))
                .ToList();

            foreach (var newCollision in newCollisions)
            {
                if (!await ProcessCollision(newCollision, collidingItem,
                                          deltaCol, deltaRow, processedItems, originalPositions))
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
                if (testRow + item.RowSpan - 1 <= Layout.Rows &&
                    !HasCollision(item, preferredCol, testRow, new List<Guid> { pushingItem.Id }))
                {
                    item.Column = preferredCol;
                    item.Row = testRow;
                    return true;
                }

                testRow = preferredRow - rowOffset;
                if (testRow >= 1 &&
                    !HasCollision(item, preferredCol, testRow, new List<Guid> { pushingItem.Id }))
                {
                    item.Column = preferredCol;
                    item.Row = testRow;
                    return true;
                }
            }
        }
        else if (deltaRow != 0)
        {
            for (int colOffset = 1; colOffset <= Layout.Columns; colOffset++)
            {
                int testCol = preferredCol + colOffset;
                if (testCol + item.ColumnSpan - 1 <= Layout.Columns &&
                    !HasCollision(item, testCol, preferredRow, new List<Guid> { pushingItem.Id }))
                {
                    item.Column = testCol;
                    item.Row = preferredRow;
                    return true;
                }

                testCol = preferredCol - colOffset;
                if (testCol >= 1 &&
                    !HasCollision(item, testCol, preferredRow, new List<Guid> { pushingItem.Id }))
                {
                    item.Column = testCol;
                    item.Row = preferredRow;
                    return true;
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
        var itemAtTarget = FindItemAtPosition(targetCol, targetRow);

        if (itemAtTarget != null && itemAtTarget.Id != item.Id)
        {
            int tempCol = item.Column;
            int tempRow = item.Row;

            itemAtTarget.Column = tempCol;
            itemAtTarget.Row = tempRow;

            item.Column = targetCol;
            item.Row = targetRow;

            bool hasCollisions = false;
            foreach (var gridItem in Layout.Items)
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
        var visited = new HashSet<(int, int)>();
        var queue = new Queue<(int col, int row, int distance)>();

        queue.Enqueue((targetCol, targetRow, 0));
        visited.Add((targetCol, targetRow));

        while (queue.Count > 0)
        {
            var (col, row, distance) = queue.Dequeue();

            if (!HasCollision(item, col, row))
            {
                item.Column = col;
                item.Row = row;
                await LayoutChanged.InvokeAsync(Layout);
                StateHasChanged();
                return;
            }

            var neighbors = new[]
            {
                (col + 1, row), (col - 1, row),
                (col, row + 1), (col, row - 1)
            };

            foreach (var (nCol, nRow) in neighbors)
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
        foreach (var item in Layout.Items)
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
            return;

        int newCol = SelectedItem.Column + deltaCol;
        int newRow = SelectedItem.Row + deltaRow;

        await SimplePlaceItem(SelectedItem, newCol, newRow);
    }

    private async Task SelectItem(GridItem item)
    {
        if (SelectedItem?.Id == item.Id)
            return;

        SelectedItem = item;
        StateHasChanged();
    }

    private async Task CellClicked(int col, int row)
    {
        if (IsDragging)
            return;

        var itemAtCell = FindItemAtCell(col, row);

        if (itemAtCell != null)
        {
            if (SelectedItem?.Id != itemAtCell.Id)
            {
                await SelectItem(itemAtCell);
            }
        }
        else if (SelectedItem != null)
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
                    RemoveItem(SelectedItem);
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

    private void ResizeItemWidth(GridItem item, int delta)
    {
        int newColSpan = item.ColumnSpan + delta;

        if (newColSpan < 1)
            newColSpan = 1;
        if (newColSpan > Layout.Columns)
            newColSpan = Layout.Columns;
        if (item.Column + newColSpan - 1 > Layout.Columns)
            return;

        int originalColSpan = item.ColumnSpan;
        item.ColumnSpan = newColSpan;

        if (HasCollision(item, item.Column, item.Row))
        {
            item.ColumnSpan = originalColSpan;
            return;
        }

        LayoutChanged.InvokeAsync(Layout);
        StateHasChanged();
    }

    private void ResizeItemHeight(GridItem item, int delta)
    {
        int newRowSpan = item.RowSpan + delta;

        if (newRowSpan < 1)
            newRowSpan = 1;
        if (newRowSpan > Layout.Rows)
            newRowSpan = Layout.Rows;
        if (item.Row + newRowSpan - 1 > Layout.Rows)
            return;

        int originalRowSpan = item.RowSpan;
        item.RowSpan = newRowSpan;

        if (HasCollision(item, item.Column, item.Row))
        {
            item.RowSpan = originalRowSpan;
            return;
        }

        LayoutChanged.InvokeAsync(Layout);
        StateHasChanged();
    }

    private void AddNewItem()
    {
        var colors = new[] { "#FFCCCC", "#CCFFCC", "#CCCCFF", "#FFFFCC", "#FFCCFF", "#CCFFFF" };

        var newItem = new GridItem
        {
            Content = $"Elemento {Layout.Items.Count + 1}",
            Column = 1,
            Row = 1,
            ColumnSpan = 2,
            RowSpan = 2,
            BackgroundColor = colors[Layout.Items.Count % colors.Length]
        };

        bool placed = false;
        for (int row = 1; row <= Layout.Rows && !placed; row++)
        {
            for (int col = 1; col <= Layout.Columns && !placed; col++)
            {
                if (col + newItem.ColumnSpan - 1 <= Layout.Columns &&
                    row + newItem.RowSpan - 1 <= Layout.Rows &&
                    !HasCollision(newItem, col, row))
                {
                    newItem.Column = col;
                    newItem.Row = row;
                    Layout.Items.Add(newItem);
                    SelectedItem = newItem;
                    placed = true;
                }
            }
        }

        if (!placed)
        {
            newItem.ColumnSpan = 1;
            newItem.RowSpan = 1;

            for (int row = 1; row <= Layout.Rows && !placed; row++)
            {
                for (int col = 1; col <= Layout.Columns && !placed; col++)
                {
                    if (!HasCollision(newItem, col, row))
                    {
                        newItem.Column = col;
                        newItem.Row = row;
                        Layout.Items.Add(newItem);
                        SelectedItem = newItem;
                        placed = true;
                    }
                }
            }
        }

        LayoutChanged.InvokeAsync(Layout);
        StateHasChanged();
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

    private void ResetGrid()
    {
        Layout.Items.Clear();
        LayoutChanged.InvokeAsync(Layout);
        SelectedItem = null;
        StateHasChanged();
    }

    private void SaveLayout()
    {
        var json = JsonSerializer.Serialize(Layout, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine("Layout guardado:");
        Console.WriteLine(json);
    }

    private void CloseEditor()
    {
        SelectedItem = null;
        StateHasChanged();
    }
}