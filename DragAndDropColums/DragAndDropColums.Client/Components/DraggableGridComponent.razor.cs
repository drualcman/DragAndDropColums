namespace DragAndDropColums.Client.Components;

public partial class DraggableGridComponent<TData>
{
    [Parameter] public GridLayout<TData> Layout { get; set; } = new();
    [Parameter] public EventCallback<GridLayout<TData>> LayoutChanged { get; set; }

    private GridItem<TData>? _selectedItem;
    private GridVisualization<TData>? _gridVisualization;

    private async Task OnSelectedItemChanged(GridItem<TData>? item)
    {
        _selectedItem = item;
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnLayoutChanged(GridLayout<TData> layout)
    {
        Layout = layout;
        await LayoutChanged.InvokeAsync(Layout);
        await InvokeAsync(StateHasChanged);
    }

    private async Task AddNewItem()
    {
        var newItem = GridItemFactory<TData>.CreateNewItem(Layout.Items.Count + 1);
        Layout.Items.Add(newItem);
        _selectedItem = newItem;
        await LayoutChanged.InvokeAsync(Layout);
        await InvokeAsync(StateHasChanged);
    }

    private async Task ResetGrid()
    {
        Layout.Items.Clear();
        _selectedItem = null;
        await LayoutChanged.InvokeAsync(Layout);
        await InvokeAsync(StateHasChanged);
    }

    private async Task RemoveItem(GridItem<TData> item)
    {
        Layout.Items.Remove(item);
        if (_selectedItem?.Id == item.Id)
        {
            _selectedItem = Layout.Items.FirstOrDefault();
        }

        await LayoutChanged.InvokeAsync(Layout);
        await InvokeAsync(StateHasChanged);
    }

    private async Task MoveSelectedItem(int deltaCol, int deltaRow)
    {
        if (_gridVisualization != null)
        {
            await _gridVisualization.MoveItemByDelta(deltaCol, deltaRow);
        }
    }

    private async Task ResizeItemWidth(GridItem<TData> item, int delta)
    {
        if (_gridVisualization != null)
        {
            await _gridVisualization.ResizeItemWidth(item, delta);
        }
    }

    private async Task ResizeItemHeight(GridItem<TData> item, int delta)
    {
        if (_gridVisualization != null)
        {
            await _gridVisualization.ResizeItemHeight(item, delta);
        }
    }

    private async Task DeselectItem()
    {
        if (_gridVisualization != null)
        {
            await _gridVisualization.DeselectItem();
        }
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
}
