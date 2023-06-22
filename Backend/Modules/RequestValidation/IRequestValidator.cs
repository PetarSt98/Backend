using Backend.Models;

namespace Backend.Modules.RequestValidation {
    public interface IRequestValidator {
        RequestValidationResult ValidateComputerName(string computerName);
        RequestValidationResult ValidateMemberNameLogin(string login);
        RequestValidationResult ValidateNewLogin(string login);
    }
}
