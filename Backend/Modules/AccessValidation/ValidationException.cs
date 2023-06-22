using System;

namespace Backend.Modules.AccessValidation {
    public class ValidationException : Exception {
        public ValidationException() : base() { }
        public ValidationException(string message) : base(message) { }
    }
}
