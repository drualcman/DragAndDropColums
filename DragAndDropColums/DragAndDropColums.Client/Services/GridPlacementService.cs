namespace DragAndDropColums.Client.Services;

public class GridPlacementService
{
    private readonly GridLayout _layout;
    private readonly GridCollisionService _collisionService;

    public GridPlacementService(GridLayout layout, GridCollisionService collisionService)
    {
        _layout = layout;
        _collisionService = collisionService;
    }

    public async Task<PlacementResult> PlaceItemAsync(GridItem item, int targetCol, int targetRow)
    {
        int boundedTargetCol = Math.Max(1, Math.Min(targetCol, _layout.Columns - item.ColumnSpan + 1));
        int boundedTargetRow = Math.Max(1, Math.Min(targetRow, _layout.Rows - item.RowSpan + 1));

        if (item.Column == boundedTargetCol && item.Row == boundedTargetRow)
        {
            return PlacementResult.NoMovement;
        }

        var originalPositions = _layout.Items.ToDictionary(i => i.Id, i => (i.Column, i.Row));

        item.Column = boundedTargetCol;
        item.Row = boundedTargetRow;

        var collisions = _collisionService.GetCollisionsAt(item, boundedTargetCol, boundedTargetRow);

        if (collisions.Count == 0)
        {
            return PlacementResult.Success;
        }

        int deltaCol = boundedTargetCol - originalPositions[item.Id].Column;
        int deltaRow = boundedTargetRow - originalPositions[item.Id].Row;

        var processedItems = new HashSet<Guid> { item.Id };
        bool success = true;

        foreach (GridItem collidingItem in collisions)
        {
            bool resolved = await ProcessCollisionAsync(collidingItem, item, deltaCol, deltaRow,
                processedItems, originalPositions);
            if (!resolved)
            {
                success = false;
                break;
            }
        }

        if (success && !HasAnyCollision())
        {
            return PlacementResult.Success;
        }

        RevertToOriginalPositions(originalPositions);

        return await TryAlternativePlacementAsync(item, boundedTargetCol, boundedTargetRow,
            deltaCol, deltaRow);
    }

    public HashSet<(int, int)> GetItemCells(DragState dragState)
    {
        GridItem item = dragState.DraggingItem;
        int? targetCol = dragState.FinalDropTarget.Value.Col;
        int? targetRow = dragState.FinalDropTarget.Value.Row;

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

    private async Task<bool> ProcessCollisionAsync(GridItem collidingItem, GridItem pushingItem,
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

        if (deltaCol > 0 && newCol + collidingItem.ColumnSpan - 1 > _layout.Columns)
        {
            newCol = pushingItem.Column - collidingItem.ColumnSpan;
            hitBoundary = true;
        }
        else if (deltaCol < 0 && newCol < 1)
        {
            newCol = pushingItem.Column + pushingItem.ColumnSpan;
            hitBoundary = true;
        }
        else if (deltaRow > 0 && newRow + collidingItem.RowSpan - 1 > _layout.Rows)
        {
            newRow = pushingItem.Row - collidingItem.RowSpan;
            hitBoundary = true;
        }
        else if (deltaRow < 0 && newRow < 1)
        {
            newRow = pushingItem.Row + pushingItem.RowSpan;
            hitBoundary = true;
        }

        newCol = Math.Max(1, Math.Min(newCol, _layout.Columns - collidingItem.ColumnSpan + 1));
        newRow = Math.Max(1, Math.Min(newRow, _layout.Rows - collidingItem.RowSpan + 1));

        int originalCol = collidingItem.Column;
        int originalRow = collidingItem.Row;

        collidingItem.Column = newCol;
        collidingItem.Row = newRow;

        List<GridItem> newCollisions = _collisionService.GetCollisionsAt(collidingItem, newCol, newRow)
            .Where(c => !processedItems.Contains(c.Id))
            .ToList();

        foreach (GridItem newCollision in newCollisions)
        {
            bool ok = await ProcessCollisionAsync(newCollision, collidingItem, deltaCol, deltaRow, processedItems, originalPositions);
            if (!ok)
            {
                collidingItem.Column = originalCol;
                collidingItem.Row = originalRow;
                return false;
            }
        }

        if (hitBoundary && _collisionService.HasCollision(collidingItem, newCol, newRow))
        {
            bool foundAlternative = await ProcessCollisionAsync(collidingItem, pushingItem, deltaCol, deltaRow, processedItems, originalPositions);
            if (!foundAlternative)
            {
                collidingItem.Column = originalCol;
                collidingItem.Row = originalRow;
                return false;
            }

            newCollisions = _collisionService.GetCollisionsAt(collidingItem, collidingItem.Column, collidingItem.Row)
                .Where(c => !processedItems.Contains(c.Id))
                .ToList();

            foreach (GridItem newCollision in newCollisions)
            {
                bool ok = await ProcessCollisionAsync(newCollision, collidingItem,
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

    private bool HasAnyCollision()
    {
        bool result = false;
        int i = 0;
        while (i < _layout.Items.Count)
        {
            if (_collisionService.HasCollision(_layout.Items[i], _layout.Items[i].Column, _layout.Items[i].Row))
            {
                result = true;
                i = _layout.Items.Count;
            }
            i++;
        }
        return result;
    }

    private void RevertToOriginalPositions(Dictionary<Guid, (int Column, int Row)> originalPositions)
    {
        foreach (GridItem item in _layout.Items)
        {
            if (originalPositions.TryGetValue(item.Id, out var originalPos))
            {
                item.Column = originalPos.Column;
                item.Row = originalPos.Row;
            }
        }
    }

    private async Task<PlacementResult> TryAlternativePlacementAsync(GridItem item,
        int targetCol, int targetRow, int deltaCol, int deltaRow)
    {
        // Implementación de intentos alternativos...
        return PlacementResult.Failed;
    }

    public async Task<PlacementResult> FindClosestFreePositionAsync(GridItem item,
        int targetCol, int targetRow)
    {

        HashSet<(int, int)> visited = new HashSet<(int, int)>();
        Queue<(int col, int row, int distance)> queue = new Queue<(int col, int row, int distance)>();

        queue.Enqueue((targetCol, targetRow, 0));
        visited.Add((targetCol, targetRow));

        while (queue.Count > 0)
        {
            (int col, int row, int distance) = queue.Dequeue();

            if (!_collisionService.HasCollision(item, col, row))
            {
                item.Column = col;
                item.Row = row;
                queue.Clear();
            }

            (int, int)[] neighbors = new (int, int)[]
            {
                (col + 1, row), (col - 1, row),
                (col, row + 1), (col, row - 1)
            };

            foreach ((int nCol, int nRow) in neighbors)
            {
                if (nCol >= 1 && nCol <= _layout.Columns - item.ColumnSpan + 1 &&
                    nRow >= 1 && nRow <= _layout.Rows - item.RowSpan + 1 &&
                    !visited.Contains((nCol, nRow)))
                {
                    visited.Add((nCol, nRow));
                    queue.Enqueue((nCol, nRow, distance + 1));
                }
            }
        }
        return PlacementResult.Success;
    }

    public bool TryPlaceNewItem(GridItem newItem)
    {
        bool placed = false;
        for (int row = 1; row <= _layout.Rows && !placed; row++)
        {
            for (int col = 1; col <= _layout.Columns && !placed; col++)
            {
                if (col + newItem.ColumnSpan - 1 <= _layout.Columns &&
                    row + newItem.RowSpan - 1 <= _layout.Rows &&
                    !_collisionService.HasCollision(newItem, col, row))
                {
                    newItem.Column = col;
                    newItem.Row = row;
                    placed = true;
                }
            }
        }

        if (!placed)
        {
            newItem.ColumnSpan = 1;
            newItem.RowSpan = 1;

            for (int row = 1; row <= _layout.Rows && !placed; row++)
            {
                for (int col = 1; col <= _layout.Columns && !placed; col++)
                {
                    if (!_collisionService.HasCollision(newItem, col, row))
                    {
                        newItem.Column = col;
                        newItem.Row = row;
                        placed = true;
                    }
                }
            }
        }
        return placed;
    }

}
