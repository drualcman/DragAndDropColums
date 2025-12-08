namespace DragAndDropColums.Client.Components;

public partial class DraggableGridComponent : IDisposable
{
    [Parameter] public GridLayout Layout { get; set; } = new();
    [Parameter] public EventCallback<GridLayout> LayoutChanged { get; set; }

    private GridItem? _selectedItem;
    private DragState _dragState = new();
    private GridCollisionService _collisionService;
    private GridPlacementService _placementService;
    private GridStyleService _styleService;
    private DragService _dragService;
    private HashSet<(int, int)> _occupiedCells = new();

    public DraggableGridComponent()
    {
        InitializeServices();
    }

    private void InitializeServices()
    {
        _collisionService = new GridCollisionService(Layout);
        _placementService = new GridPlacementService(Layout, _collisionService);
        _styleService = new GridStyleService(Layout);
        _dragService = new DragService(Layout);
    }


    protected override void OnParametersSet()
    {
        if (Layout.Items.Any() && _selectedItem == null)
        {
            _selectedItem = Layout.Items.First();
        }
        UpdateOccupiedCells();
    }

    private string GetGridStyle() => _styleService.GetGridStyle();

    private string GetItemStyle(GridItem item)
    {
        bool isSelected = _selectedItem?.Id == item.Id;
        bool isDragging = _dragState.DraggingItem?.Id == item.Id;
        return _styleService.GetItemStyle(item, isSelected, isDragging);
    }

    private void StartMouseDrag(MouseEventArgs e, GridItem item)
    {
        _dragState.DraggingItem = item;
        _selectedItem = item;
        _dragState.IsDragging = true;
        _dragState.DragCursorClass = "dragging";
        _dragState.DragStartMouse = ((int)e.ClientX, (int)e.ClientY);
        _dragState.DragStartCell = (item.Column, item.Row);
        StateHasChanged();
    }

    private void HandleMouseMove(MouseEventArgs e)
    {
        if (!_dragState.IsDragging || _dragState.DraggingItem == null)
            return;

        _dragState.FinalDropTarget = _dragService.CalculateDragTarget(e, _dragState, _dragState.DraggingItem);

        if (_dragState.FinalDropTarget.HasValue)
        {
            (int col, int row) = _dragState.FinalDropTarget.Value;
            _dragState.HoverTarget = (col, row);
            StateHasChanged();
        }
    }

    private async Task HandleMouseUp(MouseEventArgs e)
    {
        if (_dragState.IsDragging && _dragState.DraggingItem != null &&
            _dragState.FinalDropTarget.HasValue)
        {
            (int col, int row) = _dragState.FinalDropTarget.Value;
            await _placementService.PlaceItemAsync(_dragState.DraggingItem, col, row);
            await LayoutChanged.InvokeAsync(Layout);
        }

        CleanUpDrag();
    }

    private void CleanUpDrag()
    {
        _dragState.Reset();
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
        if (_selectedItem == null)
            return;

        int newCol = _selectedItem.Column + deltaCol;
        int newRow = _selectedItem.Row + deltaRow;

        await _placementService.PlaceItemAsync(_selectedItem, newCol, newRow);
        await LayoutChanged.InvokeAsync(Layout);
        await InvokeAsync(StateHasChanged);
    }

    private async Task SelectItem(GridItem item)
    {
        if (_selectedItem?.Id == item.Id)
            return;
        _selectedItem = item;
        await InvokeAsync(StateHasChanged);
    }

    private async Task CellClicked(int col, int row)
    {
        if (_dragState.IsDragging)
            return;

        GridItem? itemAtCell = _collisionService.FindItemAtPosition(col, row);

        if (itemAtCell != null)
        {
            await SelectItem(itemAtCell);
            return;
        }

        if (_selectedItem != null)
        {
            await _placementService.PlaceItemAsync(_selectedItem, col, row);
            await LayoutChanged.InvokeAsync(Layout);
        }
    }

    private async Task DeselectItem()
    {
        _selectedItem = null;
        await InvokeAsync(StateHasChanged);
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
                if (_selectedItem != null)
                {
                    RemoveItem(_selectedItem);
                }
                break;
            case "Escape":
                await DeselectItem();
                break;
            case " ":
                if (_selectedItem != null)
                {
                    await DeselectItem();
                }
                else if (Layout.Items.Any())
                {
                    _selectedItem = Layout.Items.First();
                    await InvokeAsync(StateHasChanged);
                }
                break;
        }
    }

    private async Task RemoveItem(GridItem item)
    {
        Layout.Items.Remove(item);
        if (_selectedItem?.Id == item.Id)
        {
            _selectedItem = Layout.Items.FirstOrDefault();
        }

        await LayoutChanged.InvokeAsync(Layout);
        await InvokeAsync(StateHasChanged);
    }
    private void UpdateOccupiedCells()
    {
        _occupiedCells.Clear();
        foreach (var item in Layout.Items)
        {
            var cells = _collisionService.GetItemCells(item);
            _occupiedCells.UnionWith(cells);
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



    private async Task ResizeItemWidth(GridItem item, int delta)
    {
        int newColSpan = item.ColumnSpan + delta;

        if (newColSpan < 1)
        {
            newColSpan = 1;
        }

        if (newColSpan > Layout.Columns)
        {
            newColSpan = Layout.Columns;
        }

        if (item.Column + newColSpan - 1 > Layout.Columns)
        {
            return;
        }

        int originalColSpan = item.ColumnSpan;
        item.ColumnSpan = newColSpan;

        if (HasCollision(item, item.Column, item.Row))
        {
            item.ColumnSpan = originalColSpan;
            return;
        }

        await LayoutChanged.InvokeAsync(Layout);
        await InvokeAsync(StateHasChanged);
    }

    private async Task ResizeItemHeight(GridItem item, int delta)
    {
        int newRowSpan = item.RowSpan + delta;

        if (newRowSpan < 1)
        {
            newRowSpan = 1;
        }

        if (newRowSpan > Layout.Rows)
        {
            newRowSpan = Layout.Rows;
        }

        if (item.Row + newRowSpan - 1 > Layout.Rows)
        {
            return;
        }

        int originalRowSpan = item.RowSpan;
        item.RowSpan = newRowSpan;

        if (HasCollision(item, item.Column, item.Row))
        {
            item.RowSpan = originalRowSpan;
            return;
        }

        await LayoutChanged.InvokeAsync(Layout);
        await InvokeAsync(StateHasChanged);
    }
    private bool TryPlaceNewItem(GridItem item)
    {
        // Lógica para encontrar posición libre...
        return true;
    }

    private async Task AddNewItem()
    {
        var newItem = GridItemFactory.CreateNewItem(Layout.Items.Count + 1);

        if (TryPlaceNewItem(newItem))
        {
            Layout.Items.Add(newItem);
            _selectedItem = newItem;
            await LayoutChanged.InvokeAsync(Layout);
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ResetGrid()
    {
        Layout.Items.Clear();
        await LayoutChanged.InvokeAsync(Layout);
        _selectedItem = null;
        await InvokeAsync(StateHasChanged);
    }

    private void SaveLayout()
    {
        string json = JsonSerializer.Serialize(Layout, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine("Layout guardado:");
        Console.WriteLine(json);
    }

    private async Task CloseEditor()
    {
        _selectedItem = null;
        await InvokeAsync(StateHasChanged);
    }

    public void Dispose()
    {
        _collisionService = null;
        _placementService = null;
        _styleService = null;
        _dragService = null;

    }
}
