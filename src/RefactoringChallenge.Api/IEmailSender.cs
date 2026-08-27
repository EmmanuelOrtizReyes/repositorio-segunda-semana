namespace RefactoringChallenge.Api;

public interface IEmailSender
{
    Task Send(string email, int orderId);
}