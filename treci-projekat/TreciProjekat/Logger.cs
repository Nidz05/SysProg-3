namespace TreciProjekat;

public static class Logger
{
    private static readonly object _lock = new();

    public static void Info(string message) => Write("INFO", ConsoleColor.Gray, message);
    public static void Request(string message) => Write("REQ", ConsoleColor.Cyan, message);
    public static void Response(string message) => Write("RESP", ConsoleColor.Green, message);
    public static void Error(string message) => Write("ERR", ConsoleColor.Red, message);

    private static void Write(string level, ConsoleColor color, string message)
    {
        lock (_lock)
        {
            var old = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{level}] {message}");
            Console.ForegroundColor = old;
        }
    }
}
