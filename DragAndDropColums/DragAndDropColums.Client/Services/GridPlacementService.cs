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

        // Guardar posición original del item que se mueve
        int originalItemCol = item.Column;
        int originalItemRow = item.Row;

        // Obtener colisiones iniciales en la posición objetivo
        var collisions = _collisionService.GetCollisionsAt(item, boundedTargetCol, boundedTargetRow);

        // CASO 1: Si no hay colisiones, mover directamente
        if (collisions.Count == 0)
        {
            item.Column = boundedTargetCol;
            item.Row = boundedTargetRow;
            return PlacementResult.Success;
        }

        // CASO 2: Intentar empujar en la dirección del movimiento (PRIMERO)
        // Calcular dirección del movimiento
        int deltaCol = Math.Sign(boundedTargetCol - originalItemCol);
        int deltaRow = Math.Sign(boundedTargetRow - originalItemRow);

        // Si el movimiento es diagonal, priorizamos la dirección más fuerte
        if (deltaCol != 0 && deltaRow != 0)
        {
            int colDistance = Math.Abs(boundedTargetCol - originalItemCol);
            int rowDistance = Math.Abs(boundedTargetRow - originalItemRow);

            if (colDistance >= rowDistance)
            {
                deltaRow = 0; // Priorizar movimiento horizontal
            }
            else
            {
                deltaCol = 0; // Priorizar movimiento vertical
            }
        }

        // Si hay movimiento en alguna dirección, intentar empujar primero
        if (deltaCol != 0 || deltaRow != 0)
        {
            var pushResult = await TryPushInDirectionAsync(
                item,
                boundedTargetCol,
                boundedTargetRow,
                deltaCol,
                deltaRow,
                originalPositions);

            if (pushResult == PlacementResult.Success)
            {
                return PlacementResult.Success;
            }
        }

        // CASO 3: Si el empuje falló, verificar si hay EXACTAMENTE UNA colisión para intercambio
        if (collisions.Count == 1)
        {
            GridItem targetItem = collisions[0];

            // Verificar si los elementos tienen el mismo tamaño (intercambio directo posible)
            bool canSwapDirectly = item.ColumnSpan == targetItem.ColumnSpan &&
                                   item.RowSpan == targetItem.RowSpan;

            if (canSwapDirectly)
            {
                // Intercambio directo de posiciones
                return await TrySwapItemsAsync(item, targetItem, originalPositions);
            }
            else
            {
                // Intercambio complejo - el elemento objetivo busca nueva posición
                return await TryComplexSwapAsync(item, targetItem, originalPositions);
            }
        }

        // CASO 4: Si nada funcionó, intentar colocación alternativa
        return await TryAlternativePlacementAsync(item, boundedTargetCol, boundedTargetRow);
    }

    private async Task<PlacementResult> TryPushInDirectionAsync(
        GridItem item,
        int targetCol,
        int targetRow,
        int deltaCol,
        int deltaRow,
        Dictionary<Guid, (int Column, int Row)> originalPositions)
    {
        // Guardar la posición original del item
        int originalItemCol = item.Column;
        int originalItemRow = item.Row;

        // Mover el item principal a la posición objetivo
        item.Column = targetCol;
        item.Row = targetRow;

        var processedItems = new HashSet<Guid> { item.Id };
        bool success = true;

        // Obtener colisiones iniciales
        var collisions = _collisionService.GetCollisionsAt(item, targetCol, targetRow);

        foreach (GridItem collidingItem in collisions)
        {
            bool resolved = await ProcessCollisionAsync(
                collidingItem,
                item,
                deltaCol,
                deltaRow,
                processedItems,
                originalPositions,
                0); // Nivel inicial de recursión

            if (!resolved)
            {
                success = false;
                break;
            }
        }

        if (success)
        {
            // Verificar que no haya colisiones después del procesamiento
            bool finalCollision = _collisionService.GetCollisionsAt(item, item.Column, item.Row)
                .Any(c => c.Id != item.Id);

            if (!finalCollision && !HasAnyCollision())
            {
                return PlacementResult.Success;
            }
        }

        // Si falla, revertir a las posiciones originales
        RevertToOriginalPositions(originalPositions);

        return PlacementResult.Failed;
    }


    private async Task<PlacementResult> TrySwapItemsAsync(
        GridItem movingItem,
        GridItem targetItem,
        Dictionary<Guid, (int Column, int Row)> originalPositions)
    {
        // Guardar posiciones temporales
        int movingCol = movingItem.Column;
        int movingRow = movingItem.Row;
        int targetCol = targetItem.Column;
        int targetRow = targetItem.Row;

        // Intercambiar posiciones
        movingItem.Column = targetCol;
        movingItem.Row = targetRow;
        targetItem.Column = movingCol;
        targetItem.Row = movingRow;

        // Verificar que no haya nuevas colisiones
        bool movingItemCollides = _collisionService.HasCollision(movingItem, movingItem.Column, movingItem.Row);
        bool targetItemCollides = _collisionService.HasCollision(targetItem, targetItem.Column, targetItem.Row);

        if (!movingItemCollides && !targetItemCollides)
        {
            return PlacementResult.Success;
        }
        else
        {
            // Revertir si hay colisiones
            movingItem.Column = movingCol;
            movingItem.Row = movingRow;
            targetItem.Column = targetCol;
            targetItem.Row = targetRow;
            return PlacementResult.Failed;
        }
    }

    private async Task<PlacementResult> TryComplexSwapAsync(
        GridItem movingItem,
        GridItem targetItem,
        Dictionary<Guid, (int Column, int Row)> originalPositions)
    {
        // Guardar posición original del elemento objetivo
        int targetOriginalCol = targetItem.Column;
        int targetOriginalRow = targetItem.Row;

        // Mover el elemento principal a la posición del objetivo
        movingItem.Column = targetItem.Column;
        movingItem.Row = targetItem.Row;

        // Buscar nueva posición para el elemento objetivo
        bool foundNewPosition = false;

        // Primero intentar mover el objetivo en la dirección opuesta al movimiento
        int deltaCol = movingItem.Column - originalPositions[movingItem.Id].Column;
        int deltaRow = movingItem.Row - originalPositions[movingItem.Id].Row;

        if (deltaCol != 0 || deltaRow != 0)
        {
            int newTargetCol = targetItem.Column - Math.Sign(deltaCol);
            int newTargetRow = targetItem.Row - Math.Sign(deltaRow);

            // Ajustar a límites del grid
            newTargetCol = Math.Max(1, Math.Min(newTargetCol,
                _layout.Columns - targetItem.ColumnSpan + 1));
            newTargetRow = Math.Max(1, Math.Min(newTargetRow,
                _layout.Rows - targetItem.RowSpan + 1));

            // Verificar si la posición está libre
            if (!_collisionService.HasCollision(targetItem, newTargetCol, newTargetRow))
            {
                targetItem.Column = newTargetCol;
                targetItem.Row = newTargetRow;
                foundNewPosition = true;
            }
        }

        // Si no funcionó, buscar posición cercana
        if (!foundNewPosition)
        {
            var searchResult = await FindClosestFreePositionForItemAsync(
                targetItem,
                targetOriginalCol,
                targetOriginalRow);

            if (searchResult.success)
            {
                targetItem.Column = searchResult.col;
                targetItem.Row = searchResult.row;
                foundNewPosition = true;
            }
        }

        if (foundNewPosition)
        {
            // Verificar que no haya colisiones después del intercambio
            bool movingCollides = _collisionService.HasCollision(movingItem, movingItem.Column, movingItem.Row);
            bool targetCollides = _collisionService.HasCollision(targetItem, targetItem.Column, targetItem.Row);

            if (!movingCollides && !targetCollides)
            {
                return PlacementResult.Success;
            }
        }

        // Revertir si falla
        movingItem.Column = originalPositions[movingItem.Id].Column;
        movingItem.Row = originalPositions[movingItem.Id].Row;
        targetItem.Column = targetOriginalCol;
        targetItem.Row = targetOriginalRow;

        return PlacementResult.Failed;
    }

    private async Task<bool> ProcessCollisionAsync(
            GridItem collidingItem,
            GridItem pushingItem,
            int deltaCol,
            int deltaRow,
            HashSet<Guid> processedItems,
            Dictionary<Guid, (int Column, int Row)> originalPositions,
            int recursionLevel)
    {
        // Evitar recursión infinita
        if (recursionLevel > _layout.Items.Count * 2)
        {
            return false;
        }

        if (processedItems.Contains(collidingItem.Id))
        {
            return true;
        }

        processedItems.Add(collidingItem.Id);

        // Guardar posición original
        int originalCol = collidingItem.Column;
        int originalRow = collidingItem.Row;

        // Calcular nueva posición en la dirección del empuje
        int newCol = collidingItem.Column + deltaCol;
        int newRow = collidingItem.Row + deltaRow;

        // Verificar límites del grid
        bool hitBoundary = false;

        if (deltaCol > 0)
        {
            // Movimiento hacia la derecha
            if (newCol + collidingItem.ColumnSpan - 1 > _layout.Columns)
            {
                hitBoundary = true;
                // Intentar mover a la izquierda del elemento que empuja
                newCol = pushingItem.Column - collidingItem.ColumnSpan;
            }
        }
        else if (deltaCol < 0)
        {
            // Movimiento hacia la izquierda
            if (newCol < 1)
            {
                hitBoundary = true;
                // Intentar mover a la derecha del elemento que empuja
                newCol = pushingItem.Column + pushingItem.ColumnSpan;
            }
        }
        else if (deltaRow > 0)
        {
            // Movimiento hacia abajo
            if (newRow + collidingItem.RowSpan - 1 > _layout.Rows)
            {
                hitBoundary = true;
                // Intentar mover arriba del elemento que empuja
                newRow = pushingItem.Row - collidingItem.RowSpan;
            }
        }
        else if (deltaRow < 0)
        {
            // Movimiento hacia arriba
            if (newRow < 1)
            {
                hitBoundary = true;
                // Intentar mover abajo del elemento que empuja
                newRow = pushingItem.Row + pushingItem.RowSpan;
            }
        }

        // Asegurar que la nueva posición esté dentro de los límites
        newCol = Math.Max(1, Math.Min(newCol, _layout.Columns - collidingItem.ColumnSpan + 1));
        newRow = Math.Max(1, Math.Min(newRow, _layout.Rows - collidingItem.RowSpan + 1));

        // Aplicar la nueva posición
        collidingItem.Column = newCol;
        collidingItem.Row = newRow;

        // Si encontramos un borde, intentar una colocación alternativa
        if (hitBoundary)
        {
            // Buscar posición libre más cercana en la dirección opuesta
            var alternativePosition = await FindAlternativePositionForBlockedItem(
                collidingItem,
                pushingItem,
                deltaCol,
                deltaRow);

            if (alternativePosition.success)
            {
                collidingItem.Column = alternativePosition.col;
                collidingItem.Row = alternativePosition.row;
            }
            else
            {
                // No se encontró posición alternativa, revertir
                collidingItem.Column = originalCol;
                collidingItem.Row = originalRow;
                return false;
            }
        }

        // Verificar colisiones con el nuevo elemento
        var newCollisions = _collisionService.GetCollisionsAt(collidingItem, collidingItem.Column, collidingItem.Row)
            .Where(c => !processedItems.Contains(c.Id))
            .ToList();

        // Procesar colisiones recursivamente
        foreach (GridItem newCollision in newCollisions)
        {
            bool resolved = await ProcessCollisionAsync(
                newCollision,
                collidingItem,
                deltaCol,
                deltaRow,
                processedItems,
                originalPositions,
                recursionLevel + 1);

            if (!resolved)
            {
                // Si falla, revertir este elemento
                collidingItem.Column = originalCol;
                collidingItem.Row = originalRow;
                return false;
            }
        }

        return true;
    }

    private async Task<(bool success, int col, int row)> FindAlternativePositionForBlockedItem(
        GridItem blockedItem,
        GridItem pushingItem,
        int deltaCol,
        int deltaRow)
    {
        // Primero intentar en la dirección opuesta
        for (int distance = 1; distance <= Math.Max(_layout.Columns, _layout.Rows); distance++)
        {
            int tryCol = blockedItem.Column;
            int tryRow = blockedItem.Row;

            if (deltaCol != 0)
            {
                // Movimiento horizontal original, intentar verticalmente
                tryRow = blockedItem.Row + (distance % 2 == 0 ? distance / 2 : -(distance / 2 + 1));
            }
            else if (deltaRow != 0)
            {
                // Movimiento vertical original, intentar horizontalmente
                tryCol = blockedItem.Column + (distance % 2 == 0 ? distance / 2 : -(distance / 2 + 1));
            }

            // Ajustar a los límites del grid
            tryCol = Math.Max(1, Math.Min(tryCol, _layout.Columns - blockedItem.ColumnSpan + 1));
            tryRow = Math.Max(1, Math.Min(tryRow, _layout.Rows - blockedItem.RowSpan + 1));

            // Verificar si la posición está libre
            if (!_collisionService.HasCollision(blockedItem, tryCol, tryRow))
            {
                return (true, tryCol, tryRow);
            }
        }

        return (false, blockedItem.Column, blockedItem.Row);
    }

    private async Task<(bool success, int col, int row)> FindClosestFreePositionForItemAsync(
        GridItem item,
        int startCol,
        int startRow)
    {
        HashSet<(int, int)> visited = new HashSet<(int, int)>();
        Queue<(int col, int row, int distance)> queue = new Queue<(int col, int row, int distance)>();

        queue.Enqueue((startCol, startRow, 0));
        visited.Add((startCol, startRow));

        while (queue.Count > 0)
        {
            (int col, int row, int distance) = queue.Dequeue();

            if (!_collisionService.HasCollision(item, col, row))
            {
                return (true, col, row);
            }

            // Explorar en las 4 direcciones
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

        return (false, startCol, startRow);
    }

    private bool HasAnyCollision()
    {
        foreach (var item in _layout.Items)
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

    private async Task<PlacementResult> TryAlternativePlacementAsync(GridItem item, int targetCol, int targetRow)
    {
        // Intentar encontrar la posición libre más cercana
        var result = await FindClosestFreePositionForItemAsync(item, targetCol, targetRow);

        if (result.success)
        {
            item.Column = result.col;
            item.Row = result.row;
            return PlacementResult.Success;
        }

        return PlacementResult.Failed;
    }

    public HashSet<(int, int)> GetItemCells(DragState dragState)
    {
        GridItem item = dragState.DraggingItem;
        int? targetCol = dragState.FinalDropTarget?.Col;
        int? targetRow = dragState.FinalDropTarget?.Row;

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

    public async Task<PlacementResult> FindClosestFreePositionAsync(GridItem item, int targetCol, int targetRow)
    {
        var result = await FindClosestFreePositionForItemAsync(item, targetCol, targetRow);

        if (result.success)
        {
            item.Column = result.col;
            item.Row = result.row;
            return PlacementResult.Success;
        }

        return PlacementResult.Failed;
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