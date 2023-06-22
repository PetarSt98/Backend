using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Modules.RequestValidation {
    public class RequestValidator : IRequestValidator {
        private readonly Regex _regex = new Regex(@"[^\w\.-]+$");
        private const string LettersAndDashPattern = "^[a-z][a-z-]+[a-z]$";
        private const string ComputerNamePattern = @"^[a-zA-Z][a-zA-Z\-]*[a-zA-Z0-9]+$";

        public RequestValidationResult ValidateComputerName(string computerName) {
            if (string.IsNullOrWhiteSpace(computerName)) {
                return new RequestValidationResult("Computer name cannot be empty.");
            }

            if (computerName.StartsWith("-") || computerName.EndsWith("-")) {
                return new RequestValidationResult("Computer name should start and end with a letter or a number.");
            }

            if (computerName.Trim().Length > 50) {
                return new RequestValidationResult("Computer name should be no longer than 50 characters.");
            }
            
            if (!Regex.IsMatch(computerName, ComputerNamePattern)) {
                return new RequestValidationResult(
                    "Computer name should contain only letters, numbers and '-' character.");
            }

            return new RequestValidationResult(true);
        }

        public RequestValidationResult ValidateMemberNameLogin(string login) {
            if (string.IsNullOrWhiteSpace(login)) {
                return new RequestValidationResult("Member name cannot be empty.");
            }

            if (login.StartsWith("-") || login.EndsWith("-")) {
                return new RequestValidationResult("Member name should start and end with a letter.");
            }
            if (!Regex.IsMatch(login, LettersAndDashPattern)) {
                return new RequestValidationResult("Member name can only contain letters and '-' character.");
            }

            if (login.Trim().Length > 32) {
                return new RequestValidationResult("Member name cannot be longer than 32 characters.");
            }

            return new RequestValidationResult(true);
        }

        public RequestValidationResult ValidateNewLogin(string login) {
            if (string.IsNullOrWhiteSpace(login)) {
                return new RequestValidationResult("Member name cannot be empty.");
            }
            if (login.StartsWith("-") || login.EndsWith("-")) {
                return new RequestValidationResult("Member name should start and end with a letter.");
            }
            if (!Regex.IsMatch(login, LettersAndDashPattern)) {
                return new RequestValidationResult("Member name can only contain letters and '-' character.");
            }
            if (login.Contains("-") && login.Trim().Length < 9) {
                return new RequestValidationResult("An e-group name must be at least 9 characters long.");
            }
            if (login.Trim().Length > 32) {
                return new RequestValidationResult("Member name cannot be longer than 32 characters.");
            }

            return new RequestValidationResult(true);
        }
    }
}
