namespace DragAndDropColums.Client.Models;

public class GridItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";

    // Posición lógica (fila y columna donde empieza)
    public int Row { get; set; }
    public int Column { get; set; }  // siempre 1 o 2

    // ¿Ocupa las 2 columnas?
    public bool IsFullWidth { get; set; } = false;

    // Orden visual (importante para el rendering)
    public int Order { get; set; }
}
