
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

    // Based on: https://www.meziantou.net/handling-cancelkeypress-using-a-cancellationtoken.htm
    public static async Task WaitForCancellation()
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            // We'll stop the process manually by using the CancellationToken
            e.Cancel = true;

            // Change the state of the CancellationToken to "Canceled"
            // - Set the IsCancellationRequested property to true
            // - Call the registered callbacks
            cts.Cancel();
        };


        while (true)
        {
            try
            {
                // We can't pass TimeSpan.MaxValue because it will throw an exception.
                await Task.Delay(TimeSpan.FromHours(1), cts.Token);
            }
            catch (Exception e) when (e.IsCancelled())
            {
                Console.WriteLine("Application received cancellation signal. Exiting...");
                return;
            }
        }
    }
}
