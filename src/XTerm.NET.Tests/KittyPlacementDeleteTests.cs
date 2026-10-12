using XTerm.Options;

namespace XTerm.Tests;

/// <summary>
/// Deleting a kitty placement by its ids, once the registry has trimmed the image it shows.
/// </summary>
/// <remarks>
/// A placement holds its own pixels, so trimming an image from the registry leaves its appearances
/// on screen; the client still means them when it names the id. Consolonia drags a dialog across a
/// picture by placing a new block image for every block along the dialog's edges each frame, which
/// turns the registry over in seconds, and every picture the dialog then covered stayed on screen.
/// </remarks>
public class KittyPlacementDeleteTests
{
    private const string Esc = "\u001b";
    private const string St = Esc + "\\";

    /// <summary>An 8x8 RGBA image: 256 bytes, one cell at the 8x16 cells set below.</summary>
    private static string Pixels(byte value)
    {
        var bytes = new byte[8 * 8 * 4];
        Array.Fill(bytes, value);
        return Convert.ToBase64String(bytes);
    }

    private static Terminal NewTerminal(long registryBudget) =>
        new(new TerminalOptions
        {
            Cols = 20,
            Rows = 5,
            CellWidthPixels = 8,
            CellHeightPixels = 16,
            MaxImageRegistryBytes = registryBudget,
        });

    private static void PlaceAt(Terminal terminal, uint image, uint placement, int col, int row)
    {
        terminal.Write($"{Esc}[{row + 1};{col + 1}H");
        terminal.Write($"{Esc}_Ga=p,i={image},p={placement},C=1,q=2{St}");
    }

    private static bool ShowsImage(Terminal terminal, int col, int row)
        => terminal.Buffer.Lines[row]!.TryGetImageAt(col, out _);

    [Fact]
    public void A_placement_is_deleted_by_its_ids_after_the_registry_trimmed_its_image()
    {
        // room for one 256-byte image: the second transmit trims the first
        var terminal = NewTerminal(registryBudget: 300);
        terminal.Write($"{Esc}_Ga=t,f=32,s=8,v=8,i=1,q=2;{Pixels(1)}{St}");
        PlaceAt(terminal, image: 1, placement: 1, col: 2, row: 1);
        Assert.True(ShowsImage(terminal, 2, 1), "sanity: the placement is on screen");

        terminal.Write($"{Esc}_Ga=t,f=32,s=8,v=8,i=2,q=2;{Pixels(2)}{St}");
        Assert.True(ShowsImage(terminal, 2, 1), "trimming the image from the registry leaves its appearance");

        terminal.Write($"{Esc}_Ga=d,d=i,i=1,p=1,q=2{St}");

        Assert.False(ShowsImage(terminal, 2, 1), "the client deleted the placement by its ids");
    }

    [Fact]
    public void Deleting_a_trimmed_image_drops_its_placements()
    {
        var terminal = NewTerminal(registryBudget: 300);
        terminal.Write($"{Esc}_Ga=t,f=32,s=8,v=8,i=1,q=2;{Pixels(1)}{St}");
        PlaceAt(terminal, image: 1, placement: 1, col: 0, row: 0);
        PlaceAt(terminal, image: 1, placement: 2, col: 5, row: 2);
        terminal.Write($"{Esc}_Ga=t,f=32,s=8,v=8,i=2,q=2;{Pixels(2)}{St}");

        terminal.Write($"{Esc}_Ga=d,d=I,i=1,q=2{St}");

        Assert.False(ShowsImage(terminal, 0, 0));
        Assert.False(ShowsImage(terminal, 5, 2));
    }

    [Fact]
    public void A_placement_of_an_image_still_in_the_registry_is_deleted_as_before()
    {
        var terminal = NewTerminal(registryBudget: 0);
        terminal.Write($"{Esc}_Ga=t,f=32,s=8,v=8,i=1,q=2;{Pixels(1)}{St}");
        PlaceAt(terminal, image: 1, placement: 1, col: 3, row: 3);
        PlaceAt(terminal, image: 1, placement: 2, col: 6, row: 3);

        terminal.Write($"{Esc}_Ga=d,d=i,i=1,p=1,q=2{St}");

        Assert.False(ShowsImage(terminal, 3, 3), "the named placement goes");
        Assert.True(ShowsImage(terminal, 6, 3), "the other placement of the image stays");
    }
}
