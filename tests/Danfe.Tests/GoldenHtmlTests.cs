using Direction.NFSe.Danfe;
using Xunit;

public class GoldenHtmlTests
{
    [Fact]
    public void Html_snapshot_should_match()
    {
        var service = new DanfeService(); ;
        var result = service.Generate(File.ReadAllText("Fixtures/nfse.xml"), DanfeEnvironment.Production);

        var normalized = HtmlNormalization.Normalize(result.Html);

        var approvedPath = Path.Combine("Approved", "nfse.approved.html");
        var approved = File.ReadAllText(approvedPath);

        Assert.Equal(approved, normalized);
    }
}
