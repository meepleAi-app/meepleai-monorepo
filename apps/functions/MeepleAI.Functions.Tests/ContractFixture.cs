namespace MeepleAI.Functions.Tests;

internal static class ContractFixture
{
    public static string PathOf(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tests", "fixtures", "contracts", fileName)))
        {
            dir = dir.Parent;
        }

        return dir is null
            ? throw new FileNotFoundException($"tests/fixtures/contracts/{fileName} not found above {AppContext.BaseDirectory}")
            : Path.Combine(dir.FullName, "tests", "fixtures", "contracts", fileName);
    }

    public static string ReadV1() => File.ReadAllText(PathOf("pdf-uploaded.v1.json"));
}
