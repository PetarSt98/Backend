using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Backend.Exceptions
{
    class InvalidPolicyException : Exception
    {
        public InvalidPolicyException() : base() { }
        public InvalidPolicyException(string message) : base(message) { }
    }
}


namespace Backend.Exceptions
{
    class ComputerNotFoundInActiveDirectoryException : Exception
    {
        public ComputerNotFoundInActiveDirectoryException() : base() { }
        public ComputerNotFoundInActiveDirectoryException(string message) : base(message) { }
    }
}


namespace Backend.Exceptions
{
    class LoginNotFoundInActiveDirectoryException : Exception
    {
        public LoginNotFoundInActiveDirectoryException() : base() { }
        public LoginNotFoundInActiveDirectoryException(string message) : base(message) { }
    }
}


namespace Backend.Exceptions
{
    public class ServerSyncProcessAlreadyStartedException : Exception
    {
        public ServerSyncProcessAlreadyStartedException()
        {
        }
        public ServerSyncProcessAlreadyStartedException(string message) : base(message)
        {
        }
    }
}


namespace Backend.Exceptions
{
    public class ServerSyncProcessNotStartedException : Exception
    {
        public ServerSyncProcessNotStartedException() { }

        public ServerSyncProcessNotStartedException(string message) : base(message)
        {
        }
    }
}


namespace Backend.Exceptions
{
    class ValidatorException : Exception
    {
        public ValidatorException() : base() { }

        public ValidatorException(string message) : base(message) { }
    }
}


namespace Backend.Exceptions
{
    class NoAccesToDomain : Exception
    {
        public NoAccesToDomain() : base() { }

        public NoAccesToDomain(string message) : base(message) { } 
    }
}


namespace Backend.Exceptions
{
    class CloningException : Exception
    {
        public CloningException() : base() { }

        public CloningException(string message) : base(message) { }
    }
}
