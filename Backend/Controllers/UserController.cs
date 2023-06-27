using Microsoft.AspNetCore.Mvc;
using SynchronizerLibrary.Loggers;
using SynchronizerLibrary.Data;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Diagnostics;
using SynchronizerLibrary.Loggers;
using Backend.Exceptions;
using Swashbuckle.AspNetCore.Annotations;
using NetCoreOidcExample.Helpers;
using NetCoreOidcExample.Helpers;
using NetCoreOidcExample.Models;

namespace Backend.Controllers
{
    public interface IUserService
    {
        Task<ActionResult<IEnumerable<string>>> Search(string userName);
    }

    [Route("api/[controller]")]
    [ApiController]
    public class UserSearcher : ControllerBase, IUserService
    {

        [Authorize]
        [HttpGet]
        [Route("all")]
        [SwaggerOperation("Return values - for authenticated users only.")]
        public async Task<ActionResult<IEnumerable<string>>> Search(string userName)
        {
            // In a real application, replace the following with actual logic to search in the database
            List<string> devices = new List<string>();

            //LoggerSingleton.General.Info("Started validation of  DB RAPs and corresponding Resources.");
            Console.WriteLine("Started validation of  DB RAPs and corresponding Resources.");
            var rap_resources = new List<rap_resource>();
            try
            {
                using (var db = new RapContext())
                {
                    rap_resources.AddRange(GetRapByResourceName(db, userName));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            devices.AddRange(rap_resources.Select(r => RemoveDomainFromRapOwner(r.resourceOwner)).ToList());
            devices.AddRange(rap_resources.Select(r => RemoveRAPFromUser(r.RAPName)).ToList());
            HashSet<string> uniqueDevices= new HashSet<string>(devices);
            devices = uniqueDevices.ToList();

            return Ok(devices);
        }

        private IEnumerable<rap_resource> GetRapByResourceName(RapContext db, string resourceName)
        {
            IEnumerable<rap_resource> results = null;
            try
            {
                results = db.rap_resource
                            .Where(r => r.resourceName.Contains(resourceName))
                            .ToList();
            }
            catch (Exception)
            {
                LoggerSingleton.General.Fatal("Failed query.");
                Console.WriteLine("Failed query.");
            }
            return results;
        }

        private string RemoveDomainFromRapOwner(string rapOwner)
        {
            if (rapOwner.StartsWith(@"CERN\"))
            {
                return rapOwner.Substring(@"CERN\".Length);
            }
            else
            {
                return rapOwner;
            }
        }

        private string RemoveRAPFromUser(string user)
        {
            if (user.StartsWith("RAP_"))
            {
                return user.Substring("RAP_".Length);
            }
            else
            {
                return user;
            }
        }
    }
    public class User
    {
        public string UserName { get; set; }
        public string DeviceName { get; set; }
    }


    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private const string username = "pstojkov";
        private const string password = "GeForce9800GT.";
        private readonly IUserService _userSearcher;

        public UserController(IUserService userService)
        {
            _userSearcher = userService;
        }

        [HttpPost("Add")]
        public async Task<ActionResult<string>> CreateUser(User user)
        {
            var pepi = _userSearcher.Search(user.DeviceName);
            //LoggerSingleton.General.Info("Started validation of  DB RAPs and corresponding Resources.");
            Console.WriteLine("Started validation of  DB RAPs and corresponding Resources.");
            var rap_resources = new List<rap_resource>();
            try
            {
                using (var db = new RapContext())
                {
                    // Create a new rap
                    var newRap = new rap
                    {
                        name = "RAP_" + user.UserName,
                        description = "",
                        login = user.UserName,
                        port = "3389",
                        resourceGroupName = "LG-" + user.UserName,
                        resourceGroupDescription = "",
                        synchronized = false,
                        lastModified = DateTime.Now,
                        toDelete = false
                    };

                    // Add the new rap to the raps DbSet
                    db.raps.Add(newRap);

                    Dictionary<string, string> deviceInfo = ExecutePowerShellSOAPScript(user.DeviceName, username, password);

                    if (deviceInfo != null) throw new ArgumentNullException("Unable to contact SOAP or device name not founded");
                    // Create a new rap_resource
                    var newRapResource = new rap_resource
                    {
                        RAPName = "RAP_" + user.UserName,
                        resourceName = user.DeviceName,
                        resourceOwner = "CERN\\" + deviceInfo["ResponsiblePersonUsername"],
                        access = true,
                        synchronized = false,
                        invalid = false,
                        exception = false,
                        createDate = DateTime.Now,
                        updateDate = DateTime.Now,
                        toDelete = false
                    };

                    // Add the new rap_resource to the rap_resource DbSet
                    db.rap_resource.Add(newRapResource);

                    // Save the changes to the database
                    // db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return "Unsuccessful user update";
            }

            return "Successful user update";
        }

        static Dictionary<string, string> ExecutePowerShellSOAPScript(string computerName, string userName, string password)
        {
            try
            {
                string scriptPath = $@"{Directory.GetParent(Environment.CurrentDirectory).FullName}\PowerShellScripts\SOAPNetworkService.ps1";
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -SetName1 \"{computerName}\" -UserName1 \"{userName}\" -Password1 \"{password}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process process = new Process { StartInfo = startInfo };
                process.Start();

                string output = process.StandardOutput.ReadToEnd();
                string errors = process.StandardError.ReadToEnd();

                if (output.Length == 0 || errors.Length > 0) throw new ComputerNotFoundInActiveDirectoryException(errors);

                Dictionary<string, string> result = ConvertStringToDictionary(output);
                process.WaitForExit();

                return result;
            }
            catch (ComputerNotFoundInActiveDirectoryException ex)
            {
                Console.WriteLine($"{ex.Message} Unable to use SOAP operations for device: {computerName}");
                LoggerSingleton.Raps.Error($"{ex.Message} Unable to use SOAP operations for device: {computerName}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        public static Dictionary<string, string> ConvertStringToDictionary(string input)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            string[] lines = input.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string line in lines)
            {
                int separatorIndex = line.IndexOf(':');
                if (separatorIndex > 0)
                {
                    string key = line.Substring(0, separatorIndex).Trim();
                    string value = line.Substring(separatorIndex + 1).Trim();
                    result[key] = value;
                }
            }

            return result;
        }
    }

}
