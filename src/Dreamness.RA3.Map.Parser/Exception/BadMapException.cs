namespace Dreamness.Ra3.Map.Parser.Exception;

public class BadMapException: System.Exception
{
    public BadMapException(): base("Failed to parse map, it is probably corrupted or encryted. ")
    {
    }
}