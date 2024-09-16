
namespace Golem.Tools;


public class ConsoleHelper
{
    public static void WaitForCtrlC()
    {
        Console.TreatControlCAsInput = true;

        ConsoleKeyInfo cki;
        do
        {
            cki = Console.ReadKey();
        } while (!(((cki.Modifiers & ConsoleModifiers.Control) != 0) && (cki.Key == ConsoleKey.C)));
    }
}



