using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.DirectoryServices.AccountManagement;


namespace SOAPServicesApi
{
    internal class Program
    {
        private static readonly string password = "GeForce9800GT.";
        private static readonly string userName = "pstojkov";

        static void Main(string[] args)
        {
            string deviceName = args[0];
            string purpose = args[1];

            SOAPNetworkService ms = new SOAPNetworkService();
			string auth = ms.getAuthToken(userName, password, "NICE");
			ms.AuthValue = new Auth();
			ms.AuthValue.token = auth;


            try
            {
                DeviceInfo deviceInfo = ms.getDeviceInfo(deviceName);
                switch (purpose)
                {
                    case "userNames":
                        getUserNames(deviceInfo);
                        break;

                    case "goodbye":
                        Console.WriteLine("Goodbye!");
                        break;

                    default:
                        Console.WriteLine("Unknown command");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Device not found");
            }


        }

        static void getUserNames(DeviceInfo deviceInfo)
        {
            string outputString = "";

            using (PrincipalContext ctx = new PrincipalContext(ContextType.Domain))
            {
                UserPrincipal criteria = new UserPrincipal(ctx);

                if (deviceInfo.UserPerson != null)
                {

                    criteria.EmailAddress = deviceInfo.UserPerson.Email;

                    using (PrincipalSearcher searcher = new PrincipalSearcher(criteria))
                    {
                        UserPrincipal result = (UserPrincipal)searcher.FindOne();

                        if (result != null)
                        {
                            outputString += $"{result.SamAccountName}\n";
                        }
                        else
                        {
                            outputString += $"User not found\n";
                        }
                    }
                }


                if (deviceInfo.ResponsiblePerson != null)
                {

                    criteria.EmailAddress = deviceInfo.ResponsiblePerson.Email;

                    using (PrincipalSearcher searcher = new PrincipalSearcher(criteria))
                    {
                        UserPrincipal result = (UserPrincipal)searcher.FindOne();

                        if (result != null)
                        {
                            outputString += $"{result.SamAccountName}";
                        }
                        else
                        {
                            outputString += $"User not found";
                        }
                    }
                }
            }
            Console.Write($"{outputString}");
        }
    }
}
