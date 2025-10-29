using Dreamness.RA3.Map.Lua.SyntaxChecker;

namespace Dreamness.RA3.Map.Lua.Test;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        var code = """
                   local = 1
                   """;

        var syntaxCheckResult = LuaSyntaxChecker.CheckSyntax(code);
        
        Console.WriteLine(syntaxCheckResult);
    }
}