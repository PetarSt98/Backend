using Microsoft.AspNetCore.Mvc;
using SynchronizerLibrary.Loggers;
using SynchronizerLibrary.Data;
using System;
using System.Diagnostics;
using Backend.Exceptions;
using Swashbuckle.AspNetCore.Annotations;
using NetCoreOidcExample.Helpers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Backend.Controllers
{
    public interface IUserService
    {
        Task<ActionResult<IEnumerable<string>>> Search(string userName);
    }

    [Route("api/[controller]")]
    [ApiController]
    public class DeviceSearcher : ControllerBase, IUserService
    {
        [Authorize]
        [HttpGet]
        [Route("search")]
        [SwaggerOperation("Search for all users of the device")]
        public async Task<ActionResult<IEnumerable<string>>> Search(string deviceName)
        {
            List<string> users = new List<string>();

            try
            {
                using (var db = new RapContext())
                {
                    var rap_resources = GetRapByResourceName(db, deviceName).ToList();
                    users.AddRange(rap_resources.Select(r => RemoveDomainFromRapOwner(r.resourceOwner)).ToList());
                    users.AddRange(rap_resources.Select(r => RemoveRAPFromUser(r.RAPName)).ToList());
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex}");
            }

            return Ok(new HashSet<string>(users));
        }

        private IEnumerable<rap_resource> GetRapByResourceName(RapContext db, string resourceName)
        {
            try
            {
                return db.rap_resource
                    .Where(r => r.resourceName.Contains(resourceName))
                    .ToList();
            }
            catch (Exception ex)
            {
                LoggerSingleton.General.Fatal($"Failed query: {ex}");
                throw;
            }
        }

        private string RemoveDomainFromRapOwner(string rapOwner)
        {
            return rapOwner.StartsWith(@"CERN\")
                ? rapOwner.Substring(@"CERN\".Length)
                : rapOwner;
        }

        private string RemoveRAPFromUser(string user)
        {
            return user.StartsWith("RAP_")
                ? user.Substring("RAP_".Length)
                : user;
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

        [Authorize]
        [HttpPost("add")]
        [SwaggerOperation("Add a new user to the device.")]
        public async Task<ActionResult<string>> CreateUser([FromBody] User user)
        {
            try
            {
                var searchResult = await _userSearcher.Search(user.DeviceName);

                if (searchResult.Result is StatusCodeResult status && status.StatusCode == 500)
                {
                    return StatusCode(500, "Failed to search for user!");
                }

                if (searchResult.Result is OkObjectResult okObjectResult)
                {
                    var userList = okObjectResult.Value as IEnumerable<string>;
                    if (userList.Contains(user.UserName))
                    {
                        return "User already exists!";
                    }
                }

                Dictionary<string, string> deviceInfo = ExecutePowerShellSOAPScript(user.DeviceName);

                if (user.UserName != deviceInfo["ResponsiblePersonUsername"] && user.UserName != deviceInfo["UserPersonUsername"])
                {
                    return $"User: {user.UserName} is not an owner or a user of the device: {user.DeviceName}";
                }

                using (var db = new RapContext())
                {
                    var newRap = new rap
                    {
                        name = "RAP_" + user.UserName,
                        login = user.UserName,
                        port = "3389",
                        resourceGroupName = "LG-" + user.UserName,
                        synchronized = false,
                        lastModified = DateTime.Now,
                        toDelete = false
                    };

                    db.raps.Add(newRap);

                    if (deviceInfo == null)
                    {
                        return BadRequest("Unable to contact SOAP or device name not found.");
                    }

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

                    db.rap_resource.Add(newRapResource);
                    //db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return "Unsuccessful user update or device does not exists";
            }

            return "Successful user update";
        }


        private Dictionary<string, string> ExecutePowerShellSOAPScript(string computerName)
        {
            try
            {
                string scriptPath = $@"{Directory.GetParent(Environment.CurrentDirectory).FullName}\PowerShellScripts\SOAPNetworkService.ps1";
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-ExecutionPolicy Bypass -File \"{scriptPath}\" -SetName1 \"{computerName}\" -UserName1 \"{username}\" -Password1 \"{password}\"",
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

                if (output.Contains("Device not found"))
                {
                    Console.WriteLine($"Unable to use SOAP operations for device: {computerName}");
                    LoggerSingleton.Raps.Error($"Unable to use SOAP operations for device: {computerName}");
                    return null;
                }

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

    [Route("api/[controller]")]
    [ApiController]
    public class UserDevicesController : ControllerBase
    {
        private const string username = "pstojkov";
        private const string password = "GeForce9800GT.";
        private readonly IUserService _userSearcher;

        public UserDevicesController(IUserService userService)
        {
            _userSearcher = userService;
        }

        [Authorize]
        [HttpGet]
        [Route("search")]
        [SwaggerOperation("Fetch all devices of the user.")]
        public async Task<ActionResult<IEnumerable<string>>> FetchDevices(string userName)
        {
            List<string> devices = new List<string>();

            try
            {
                using (var db = new RapContext())
                {
                    var rap_resources = GetRapByRAPName(db, AddRAPFromUser(userName)).ToList();
                    rap_resources.AddRange(GetRapByResourceOwner(db, AddDomainFromRapOwner(userName)).ToList());
                    devices.AddRange(rap_resources.Select(r => r.resourceName).ToList());
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex}");
            }

            return Ok(new HashSet<string>(devices));
        }
        private IEnumerable<rap_resource> GetRapByRAPName(RapContext db, string rapName)
        {
            try
            {
                return db.rap_resource
                    .Where(r => r.RAPName.Contains(rapName))
                    .ToList();
            }
            catch (Exception ex)
            {
                LoggerSingleton.General.Fatal($"Failed query: {ex}");
                throw;
            }
        }

        private IEnumerable<rap_resource> GetRapByResourceOwner(RapContext db, string ownerName)
        {
            try
            {
                return db.rap_resource
                    .Where(r => r.resourceOwner.Contains(ownerName))
                    .ToList();
            }
            catch (Exception ex)
            {
                LoggerSingleton.General.Fatal($"Failed query: {ex}");
                throw;
            }
        }

        private string AddDomainFromRapOwner(string rapOwner)
        {
            return rapOwner.StartsWith(@"CERN\")
                ? rapOwner
                : @"CERN\" + rapOwner;
        }

        private string AddRAPFromUser(string user)
        {
            return user.StartsWith("RAP_")
                ? user
                : "RAP_" + user;
        }
    }
}
