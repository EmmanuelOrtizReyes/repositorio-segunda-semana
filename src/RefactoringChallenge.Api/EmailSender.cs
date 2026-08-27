namespace RefactoringChallenge.Api;

public class EmailSender : IEmailSender
{
    public async Task Send(string email, int orderId)
    {
        await Task.Delay(20);

        if (email.EndsWith(
                "@fail.test",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpRequestException(
                "Notification provider unavailable.");
        }

        Console.WriteLine(
            $"Confirmation sent to {email} for order {orderId}");
    }
}