using ProcesorFacturi.Core;

namespace ProcesorFacturi.Tests;

public class TransliterareTests
{
    [Theory]
    [InlineData("Mogoşoaia", "Mogosoaia")]
    [InlineData("ăâîșțĂÂÎȘȚ", "aaistAAIST")]
    [InlineData("Farmacie Bălceșteanu", "Farmacie Balcesteanu")]
    [InlineData("BUC", "BUC")]
    [InlineData("", "")]
    public void EliminaDiacritice_InlocuiesteCorect(string intrare, string asteptat)
    {
        Assert.Equal(asteptat, Transliterare.EliminaDiacritice(intrare));
    }

    [Fact]
    public void EliminaDiacritice_RezultatEsteIntotdeaunaAscii()
    {
        var rezultat = Transliterare.EliminaDiacritice("Mogoşoaia ăâîșț ĂÂÎȘȚ café");
        Assert.True(rezultat.All(c => c <= 127));
    }
}
