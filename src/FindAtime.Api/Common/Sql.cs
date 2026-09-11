public static class Sql
{
    public static string Load(string fileName)
    {
        var assembly = typeof(Sql).Assembly;
        var resourceName = assembly
            .GetManifestResourceNames()
            .Single(name => name.EndsWith(fileName));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}