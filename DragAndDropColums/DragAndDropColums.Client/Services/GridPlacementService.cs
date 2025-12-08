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

    private async Task<bool> ProcessCollisionAsync(GridItem collidingItem, GridItem pushingItem,
        int deltaCol, int deltaRow, HashSet<Guid> processedItems,
        Dictionary<Guid, (int Column, int Row)> originalPositions)
    {
        // Implementación del método...
        // (Similar a tu código original, pero más organizado)
        return true;
    }

    private bool HasAnyCollision()
    {
        foreach (GridItem item in _layout.Items)
        {
            if (_collisionService.HasCollision(item, item.Column, item.Row))
            {
                return true;
            }
        }
        return false;
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
        // Implementación de búsqueda BFS...
        return PlacementResult.Success;
    }
}
