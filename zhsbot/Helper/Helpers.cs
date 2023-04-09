namespace zhsbot.Helper;

public static class Helpers
{
  public static void WriteLine(string? s, ConsoleColor color = ConsoleColor.White)
  {
    Console.ForegroundColor = color;
    Console.WriteLine(s);
    Console.ResetColor();
  }
}