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
using System.Security.Authentication;

namespace Backend.Controllers
{
    public interface IUserService
    {
        Task<ActionResult<IEnumerable<string>>> Search(string userName, string deviceName);
    }

    [Route("api/search_tabel")]
    [ApiController]
    public class DeviceSearcher : ControllerBase, IUserService
    {
        [Authorize]
        [HttpGet]
        [Route("search")]
        [SwaggerOperation("Search for all users of the device")]
        public async Task<ActionResult<IEnumerable<string>>> Search(string userName, string deviceName)
        {
            List<string> users = new List<string>();

            try
            {
                using (var db = new RapContext())
                {
                    var rap_resources = GetRapByResourceName(db, userName, deviceName).ToList();

                    users.AddRange(rap_resources.Select(r => RemoveDomainFromRapOwner(r.resourceOwner)).ToList());
                    users.AddRange(rap_resources.Select(r => RemoveRAPFromUser(r.RAPName)).ToList());
                }
            }
            catch (InvalidFetchingException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error!");
            }

            return Ok(new HashSet<string>(users));
        }

        private IEnumerable<rap_resource> GetRapByResourceName(RapContext db, string userName, string resourceName)
        {
            try
            {

                string rapName = UserDevicesController.AddRAPToUser(userName);
                string rapOwner = UserDevicesController.AddDomainToRapOwner(userName);
                var fetched_resources = db.rap_resource
                    .Where(r =>
                        r.resourceName.Contains(resourceName))
                        .ToList();

                if (fetched_resources.Count() == 0)
                {
                    throw new InvalidFetchingException($"Device: {resourceName} does not exist!");
                }

                fetched_resources =  fetched_resources
                                    .Where(r => r.RAPName == rapName || r.resourceOwner == rapOwner)
                                    .ToList();

                if (fetched_resources.Count() == 0)
                {
                    throw new InvalidFetchingException($"User: {userName} is not an owner or a user of the Device: {resourceName}!");
                }

                return fetched_resources;

            }
            catch (Exception ex)
            {
                //LoggerSingleton.General.Fatal($"Failed query: {ex}");
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

    [Route("api/add_pop_up")]
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
                var searchResult = await _userSearcher.Search(user.UserName, user.DeviceName);

                if (searchResult.Result is StatusCodeResult status && status.StatusCode == 500)
                {
                    return StatusCode(500, "Failed to search for user!");
                }

                if (searchResult.Result is OkObjectResult okObjectResult)
                {
                    var userList = okObjectResult.Value as IEnumerable<string>;
                    if (userList.Contains(user.UserName))
                    {
                        return "Device already exists!";
                    }
                }

                Dictionary<string, string> deviceInfo = ExecutePowerShellSOAPScript(user.DeviceName);

                if (deviceInfo == null)
                {
                    return $"Device: {user.DeviceName} does not exist!";
                }

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
                        return BadRequest("Unable to contact SOAP or device name not found!");
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
                return "Unsuccessful update or device does not exists!";
            }

            return "Successfully added the device!";
        }


        public static Dictionary<string, string> ExecutePowerShellSOAPScript(string computerName)
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
                    //LoggerSingleton.Raps.Error($"Unable to use SOAP operations for device: {computerName}");
                    return null;
                }

                Dictionary<string, string> result = ConvertStringToDictionary(output);
                process.WaitForExit();

                return result;
            }
            catch (ComputerNotFoundInActiveDirectoryException ex)
            {
                Console.WriteLine($"{ex.Message} Unable to use SOAP operations for device: {computerName}");
                //LoggerSingleton.Raps.Error($"{ex.Message} Unable to use SOAP operations for device: {computerName}");
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

    [Route("api/devices_tabel")]
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
                    var rap_resources = GetRapByRAPName(db, AddRAPToUser(userName)).ToList();
                    rap_resources.AddRange(GetRapByResourceOwner(db, AddDomainToRapOwner(userName)).ToList());
                    devices.AddRange(rap_resources.Select(r => r.resourceName).ToList());
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex}");
            }

            return Ok(new HashSet<string>(devices));
        }

        [Authorize]
        [HttpDelete]
        [Route("remove")]
        [SwaggerOperation("Search for all users of the device")]
        public async Task<ActionResult<string>> RemoveDevice(string userName, string deviceName)
        {
            try
            {
                using (var db = new RapContext())
                {

                    string rapName = AddRAPToUser(userName);
                    string rapOwner = AddDomainToRapOwner(userName);
                    var resources_to_delete = db.rap_resource
                        .Where(r =>
                            r.resourceName.Contains(deviceName) &&
                            (r.RAPName == rapName || r.resourceOwner == rapOwner)
                        )
                        .ToList();

                    if (resources_to_delete.Any())
                    {
                        foreach (rap_resource resource in resources_to_delete)
                        {
                                resource.toDelete = true;
                        }
                        
                        //db.SaveChanges();
                    }
                    else
                    {
                        //LoggerSingleton.General.Warn($"Resource with name '{deviceName}' not found");
                        return "Unsuccessful user removal from device! No such device found for the user!";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return $"Unsuccessful user removal! Error: {ex.Message}";
            }

            return "Successful user removal!";
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
                //LoggerSingleton.General.Fatal($"Failed query: {ex}");
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
                //LoggerSingleton.General.Fatal($"Failed query: {ex}");
                throw;
            }
        }

        static public string AddDomainToRapOwner(string rapOwner)
        {
            return rapOwner.StartsWith(@"CERN\")
                ? rapOwner
                : @"CERN\" + rapOwner;
        }

        static public string AddRAPToUser(string user)
        {
            return user.StartsWith("RAP_")
                ? user
                : "RAP_" + user;
        }
    }
}
