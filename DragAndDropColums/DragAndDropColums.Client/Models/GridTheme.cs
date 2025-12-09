namespace DragAndDropColums.Client.Models;

public class GridTheme
{
    // Grid principal
    public string GridBackground { get; set; } = "#f8f9fa";        // Color de fondo del grid (gris claro)
    public string GridBorderColor { get; set; } = "#dee2e6";       // Color del borde del grid (gris medio claro)

    // Items normales
    public string ItemBorderColor { get; set; } = "#333";          // Color del borde de los items (gris oscuro)
    public string ItemShadowColor { get; set; } = "rgba(0, 0, 0, 0.1)";        // Color de la sombra normal (negro con 10% opacidad)
    public string ItemHoverShadowColor { get; set; } = "rgba(0, 0, 0, 0.15)"; // Color de la sombra al hacer hover (negro con 15% opacidad)

    // Estado seleccionado
    public string SelectedColor { get; set; } = "#007bff";         // Color del borde cuando está seleccionado (azul Bootstrap)
    public string SelectedGlowColor { get; set; } = "rgba(0, 123, 255, 0.3)"; // Color del resplandor seleccionado (azul con 30% opacidad)

    // Estado arrastrando
    public string DraggingColor { get; set; } = "#ff6b6b";         // Color del borde cuando se está arrastrando (rojo coral)
    public string DraggingGlowColor { get; set; } = "rgba(255, 107, 107, 0.3)"; // Color del resplandor al arrastrar (rojo coral con 30% opacidad)

    // Área de drop
    public string DropAreaColor { get; set; } = "#00b7ff";         // Color principal del área de drop (azul brillante)
    public string DropAreaBackground { get; set; } = "rgba(0, 183, 255, 0.2)"; // Fondo del área de drop (azul con 20% opacidad)
    public string DropAreaGlowColor { get; set; } = "rgba(0, 183, 255, 0.3)";  // Color del resplandor del área de drop (azul con 30% opacidad)
    public string DropAreaLightGlowColor { get; set; } = "rgba(0, 183, 255, 0.1)"; // Color del resplandor claro (azul con 10% opacidad)

    // Métodos factory para temas comunes
    public static GridTheme Default => new GridTheme();

    public static GridTheme DarkTheme => new GridTheme
    {
        GridBackground = "#2d3748",                    // Color de fondo del grid (gris azulado oscuro)
        GridBorderColor = "#4a5568",                   // Color del borde del grid (gris azulado medio)
        ItemBorderColor = "#718096",                   // Color del borde de los items (gris azulado claro)
        ItemShadowColor = "rgba(0, 0, 0, 0.3)",        // Color de la sombra normal (negro con 30% opacidad)
        ItemHoverShadowColor = "rgba(0, 0, 0, 0.4)",   // Color de la sombra al hacer hover (negro con 40% opacidad)
        SelectedColor = "#63b3ed",                     // Color del borde cuando está seleccionado (azul claro)
        SelectedGlowColor = "rgba(99, 179, 237, 0.3)", // Color del resplandor seleccionado (azul claro con 30% opacidad)
        DraggingColor = "#fc8181",                     // Color del borde cuando se está arrastrando (rojo claro)
        DraggingGlowColor = "rgba(252, 129, 129, 0.3)", // Color del resplandor al arrastrar (rojo claro con 30% opacidad)
        DropAreaColor = "#68d391",                     // Color principal del área de drop (verde claro)
        DropAreaBackground = "rgba(104, 211, 145, 0.2)", // Fondo del área de drop (verde claro con 20% opacidad)
        DropAreaGlowColor = "rgba(104, 211, 145, 0.3)",  // Color del resplandor del área de drop (verde claro con 30% opacidad)
        DropAreaLightGlowColor = "rgba(104, 211, 145, 0.1)" // Color del resplandor claro (verde claro con 10% opacidad)
    };

    public static GridTheme PastelTheme => new GridTheme
    {
        GridBackground = "#f7f9fc",                     // Color de fondo del grid (blanco azulado muy claro)
        GridBorderColor = "#e1e5eb",                    // Color del borde del grid (gris muy claro)
        ItemBorderColor = "#adb5bd",                    // Color del borde de los items (gris medio claro)
        ItemShadowColor = "rgba(173, 181, 189, 0.2)",   // Color de la sombra normal (gris con 20% opacidad)
        ItemHoverShadowColor = "rgba(173, 181, 189, 0.3)", // Color de la sombra al hacer hover (gris con 30% opacidad)
        SelectedColor = "#a663cc",                      // Color del borde cuando está seleccionado (morado pastel)
        SelectedGlowColor = "rgba(166, 99, 204, 0.2)",  // Color del resplandor seleccionado (morado con 20% opacidad)
        DraggingColor = "#ff8fa3",                      // Color del borde cuando se está arrastrando (rosa pastel)
        DraggingGlowColor = "rgba(255, 143, 163, 0.2)", // Color del resplandor al arrastrar (rosa con 20% opacidad)
        DropAreaColor = "#5a67d8",                      // Color principal del área de drop (azul índigo)
        DropAreaBackground = "rgba(90, 103, 216, 0.1)",  // Fondo del área de drop (azul índigo con 10% opacidad)
        DropAreaGlowColor = "rgba(90, 103, 216, 0.2)",   // Color del resplandor del área de drop (azul índigo con 20% opacidad)
        DropAreaLightGlowColor = "rgba(90, 103, 216, 0.05)" // Color del resplandor claro (azul índigo con 5% opacidad)
    };
}