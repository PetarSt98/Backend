namespace Backend.ExchangeTokenService
{
    public interface ITokenService
    {
        Task<string> GetTokenAsync();
    }
}
