using Microsoft.AspNetCore.Mvc;
using SynchronizerLibrary.Data;
using System.Diagnostics;
using Backend.Exceptions;
using Swashbuckle.AspNetCore.Annotations;
using NetCoreOidcExample.Helpers;
using System.Text.RegularExpressions;
using System.Text.Json;


namespace Backend.Controllers
{
    public interface IUserService
    {
        Task<ActionResult<IEnumerable<string>>> Search(string userName, string deviceName, bool fetchToDeleteResource);
    }

    [Route("api/search_tabel")]
    [ApiController]
    public class DeviceSearcher : ControllerBase, IUserService
    {
        [Authorize]
        [HttpGet]
        [Route("search")]
        [SwaggerOperation("Search for all users of the device")]
        public async Task<ActionResult<IEnumerable<string>>> Search(string userName, string deviceName, bool fetchToDeleteResource)
        {
            deviceName = deviceName.ToUpper();

            List<string> users = new List<string>();
            
            try
            {
                using (var db = new RapContext())
                {
                    var rap_resources = GetRapByResourceName(db, userName, deviceName, fetchToDeleteResource).ToList();
                    //users.AddRange(rap_resources.Select(r => RemoveDomainFromRapOwner(r.resourceOwner)).ToList());
                    users.AddRange(rap_resources.Select(r => RemoveRAPFromUser(r.RAPName)).ToList());
                }
            }
            catch (InvalidFetchingException ex)
            {
                //Dictionary<string, object> deviceInfo = Task.Run(async () => await UserController.ExecuteSOAPServiceApi(userName, deviceName, "false")).Result;
                //string userPersonUsername = deviceInfo["UserPersonUsername"] as string;

                //if (userPersonUsername != userName)
                return BadRequest(ex.Message);
                //users = deviceInfo["EGroupUsers"] as List<string>;
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error!");
            }

            return Ok(new HashSet<string>(users));
        }

        private IEnumerable<rap_resource> GetRapByResourceName(RapContext db, string userName, string resourceName, bool fetchToDeleteResource)
        {
            try
            {
                List<string> userEGroups = null;
                string rapName = UserDevicesController.AddRAPToUser(userName);
                string rapOwner = UserDevicesController.AddDomainToRapOwner(userName);
                var fetched_resources = db.rap_resource
                    .Where(r =>
                        ((r.resourceName == resourceName) && r.access && (!r.toDelete || fetchToDeleteResource)))
                        .ToList();
                var fetched_resources_all_users = db.rap_resource
                    .Where(r =>
                        ((r.resourceName == resourceName) && r.access && (!r.toDelete || fetchToDeleteResource)))
                        .ToList();


                if (fetched_resources.Count() == 0)
                {
                    throw new InvalidFetchingException($"Device: {resourceName} does not exist!");
                }

                if (System.IO.File.Exists("/app/cacheData/admins_cache.json"))
                {
                    try 
                    { 
                        var content = System.IO.File.ReadAllText("/app/cacheData/admins_cache.json");
                        Dictionary<string, object> adminsInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
                        userEGroups = adminsInfo["EGroupNames"] as List<string>;

                        if (userEGroups == null)
                        {
                            if (adminsInfo.TryGetValue("EGroupNames", out var eGroupNamesObj))
                            {
                                var jArray = eGroupNamesObj as Newtonsoft.Json.Linq.JArray;
                                if (jArray != null)
                                {
                                    userEGroups = jArray.ToObject<List<string>>();
                                }
                            }
                        }

                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                        Console.WriteLine("Admin authentication is unsuccessful!");
                        userEGroups = null;
                    }
                }
                else
                {
                    Console.WriteLine("Admin authentication is unsuccessful!");
                }

                if (userEGroups?.Contains(userName) != true)
                {
                    fetched_resources = fetched_resources
                                        .Where(r => r.RAPName == rapName || r.resourceOwner == rapOwner)
                                        .ToList();
                }

                if (fetched_resources.Count() == 0)
                {
                    throw new InvalidFetchingException($"User: {userName} is not an owner or a user of the Device: {resourceName}!");
                }

                return fetched_resources_all_users;

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
        public string SignedInUser { get; set; }
        public string AddDeviceOrUser { get; set; }
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
                if (user.DeviceName == "" || user.UserName == "")
                {
                    if (user.AddDeviceOrUser == "device")
                    {
                        return "Device name is empty!";
                    }
                    else if (user.AddDeviceOrUser == "user")
                    {
                        return "User name is empty";
                    }
                }

                user.DeviceName = user.DeviceName.ToUpper();

                var searchResult = await _userSearcher.Search(user.UserName, user.DeviceName, false);

                if (searchResult.Result is StatusCodeResult status && status.StatusCode == 500)
                {
                    return StatusCode(500, "Failed to search for user!");
                }

                if (searchResult.Result is OkObjectResult okObjectResult)
                {
                    var userList = okObjectResult.Value as IEnumerable<string>;
                    if (userList.Contains(user.UserName))
                    {
                        if (user.AddDeviceOrUser == "device")
                        {
                            return "Device already exists!";
                        }
                        else if (user.AddDeviceOrUser == "user")
                        {
                            return "User already exists!";
                        }
                    }
                }

                Dictionary<string, object> deviceInfo = await ExecuteSOAPServiceApi(user.UserName, user.DeviceName, "false");

                if (deviceInfo == null)
                {
                    return $"Device: {user.DeviceName} does not exist!";
                }

                if (deviceInfo["validUser"] as string != user.UserName)
                {
                    return $"User: {user.UserName} does not exist!";
                }

                if (user.SignedInUser != "Primary")
                {
                    List<string> primaryAccounts = deviceInfo["PrimaryAccounts"] as List<string>;
                    if (!primaryAccounts.Contains(user.SignedInUser) && user.AddDeviceOrUser == "user")
                    {
                        return $"Signed in user: {user.SignedInUser} is not a primary account!\nOnly primary accounts can manage users!";
                    }
                }
                if (deviceInfo["Error"] != null)
                {
                    return deviceInfo["Error"] as string;
                }
                string responsiblePersonUsername;
                if ((deviceInfo["UserPersonFirstName"] as string).Contains("E-GROUP"))
                {
                    responsiblePersonUsername = deviceInfo["ResponsiblePersonName"] as string;
                }
                else
                { 
                    responsiblePersonUsername = deviceInfo["ResponsiblePersonUsername"] as string;
                }
                string userPersonUsername = deviceInfo["UserPersonUsername"] as string;

                if (responsiblePersonUsername == null || userPersonUsername == null)
                {
                    return $"Error: Could not retrieve ownership data for the device: {user.DeviceName}";
                }

                List<string> userEGroups = deviceInfo["EGroupNames"] as List<string>;
                if (userEGroups?.Contains(user.UserName) != true)
                {
                    if (user.UserName != responsiblePersonUsername && user.UserName != userPersonUsername && user.AddDeviceOrUser == "device")
                    {
                        return $"User: {user.UserName} is not an owner or a user of the device: {user.DeviceName}!";
                    }
                }
                using (var db = new RapContext())
                {
                    string rapName = "RAP_" + user.UserName;
                    string resourceGroupName = "LG-" + user.UserName;

                    var existingRap = db.raps.FirstOrDefault(rap =>
                        rap.name == rapName &&
                        rap.login == user.UserName &&
                        rap.resourceGroupName == resourceGroupName &&
                        rap.toDelete == false);

                    // If the rap does not exist in the database, then create a new one
                    if (existingRap == null)
                    {
                        var newRap = new rap
                        {
                            name = rapName,
                            login = user.UserName,
                            port = "3389",
                            resourceGroupName = resourceGroupName,
                            synchronized = false,
                            lastModified = DateTime.Now,
                            toDelete = false
                        };

                        db.raps.Add(newRap);
                    }

                    if (deviceInfo == null)
                    {
                        return BadRequest("Unable to contact SOAP or device name not found!");
                    }

                    string resourceOwner = "CERN\\" + responsiblePersonUsername;
                    var existingRapResource = db.rap_resource.FirstOrDefault(rr =>
                        rr.RAPName == rapName &&
                        rr.resourceName == user.DeviceName &&
                        rr.resourceOwner == resourceOwner &&
                        rr.toDelete == false);

                    var existingToDeleteRapResource = db.rap_resource.FirstOrDefault(rr =>
                        rr.RAPName == rapName &&
                        rr.resourceName == user.DeviceName &&
                        rr.resourceOwner == resourceOwner &&
                        rr.toDelete == true);
                    // If the rap_resource does not exist in the database, then create a new one
                    if (existingRapResource == null && existingToDeleteRapResource == null)
                    {
                        var newRapResource = new rap_resource
                        {
                            RAPName = rapName,
                            resourceName = user.DeviceName,
                            resourceOwner = resourceOwner,
                            access = true,
                            synchronized = false,
                            invalid = false,
                            exception = false,
                            createDate = DateTime.Now,
                            updateDate = DateTime.Now,
                            toDelete = false
                        };

                        db.rap_resource.Add(newRapResource);
                    }
                    else if (existingToDeleteRapResource != null && existingRapResource == null)
                    {
                        existingToDeleteRapResource.toDelete = false;
                    }
                    else 
                    {
                        if (user.AddDeviceOrUser == "device")
                        {
                            return "Device already exists!";
                        }
                        else if (user.AddDeviceOrUser == "user")
                        {
                            return "User already exists!";
                        }
                        else
                        {
                            return "Unsuccessful update";
                        }
                    }

                    db.SaveChanges();
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                if (user.AddDeviceOrUser == "device")
                {
                    return "Unsuccessful update or device does not exists!";
                }
                else if (user.AddDeviceOrUser == "user")
                {
                    return "Unsuccessful update or device does not exists!";
                }
                else
                {
                    return "Unsuccessful update";
                }
            }
            if (user.AddDeviceOrUser == "device")
            {
                return "Successfully added the device!";
            }
            else if (user.AddDeviceOrUser == "user")
            {
                return "Successfully added the user!";
            }
            else
            {
                return "Successful update";
            }
        }


        public static async Task<Dictionary<string, object>> ExecuteSOAPServiceApi(string userName, string computerName, string adminsOnly)
        {
            try
            {
                string pathToScript = Path.Combine(Directory.GetCurrentDirectory(), "SOAPNetworkService.py");
                Dictionary<string, object> result = new Dictionary<string, object>();
                List<string> eGroupNames = new List<string>();
                List<string> eGroupUsers = new List<string>();
                List<string> primaryUsers = new List<string>();
                string pattern = @"CN=(.*?),OU";

                using (var process = new Process())
                {
                    process.StartInfo.FileName = "python2.7";
                    process.StartInfo.Arguments = $"{pathToScript} {userName} {computerName} {username} {password} {adminsOnly} cernts-TSgateway-AllowedNonPrimaryAccount";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.Start();
                    bool SOAPFlag = true;
                    bool primaryGroupFlag = false;
                    while (!process.StandardOutput.EndOfStream)
                    {
                        string line = process.StandardOutput.ReadLine();
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        if (result.Count < 5 && adminsOnly == "false")
                        {
                            string username = line.Replace("'", "").Replace("\r", "").Replace("\n", "");
                            switch(result.Count)
                            {
                                case 0:
                                    result["ResponsiblePersonName"] = username;
                                    break;
                                case 1:
                                    result["UserPersonFirstName"] = username;
                                    break;
                                case 2:
                                    result["UserPersonUsername"] = username;
                                    break;
                                case 3:
                                    result["ResponsiblePersonUsername"] = username;
                                    break;
                                case 4:
                                    result["validUser"] = username;
                                    break;
                                default:
                                    throw new Exception("SOAP py error!");
                            }
                        }
                        else
                        {
                            Match match = Regex.Match(line, pattern);
                            if (match.Success && SOAPFlag && !primaryGroupFlag)
                            {
                                string eGroupName = match.Groups[1].Value;
                                eGroupNames.Add(eGroupName);
                            }
                            else if (match.Success && !SOAPFlag && !primaryGroupFlag)
                            {
                                string eGroupName = match.Groups[1].Value;
                                eGroupUsers.Add(eGroupName);
                            }
                            else if (match.Success && !SOAPFlag && primaryGroupFlag)
                            {
                                string eGroupName = match.Groups[1].Value;
                                primaryUsers.Add(eGroupName);
                            }

                            if (line.Contains("-------------------------"))
                            {
                                if (!SOAPFlag)
                                {
                                    primaryGroupFlag = true;
                                }
                                SOAPFlag = false;
                            }
                        }
                    }

                    string errors = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (eGroupNames.Count == 0 || !string.IsNullOrEmpty(errors))
                    {
                        throw new Exception(errors);
                    }

                    if (errors.Contains("Device not found"))
                    {
                        Console.WriteLine($"Unable to use SOAP operations for device: {computerName}");
                        return null;
                    }

                    result["EGroupNames"] = eGroupNames;
                    result["EGroupUsers"] = eGroupUsers;
                    result["PrimaryAccounts"] = primaryUsers;
                }
                result["Error"] = null;
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                Dictionary<string, object> result = new Dictionary<string, object>
                {
                    ["UserPersonUsername"] = null,
                    ["ResponsiblePersonUsername"] = null,
                    ["EGroupNames"] = null,
                    ["Error"] = null
                };

                if (ex.Message.Contains("not found in database"))
                {
                    result["Error"] = $"Device: {computerName} does not exist!";
                }
                else
                {
                    result["Error"] = ex.Message;
                }

                return result;
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
                    //rap_resources.AddRange(GetRapByResourceOwner(db, AddDomainToRapOwner(userName)).ToList());
                    devices.AddRange(rap_resources.Where(r => !r.toDelete).Select(r => r.resourceName).ToList());
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex}");
            }

            Task.Run(() => CacheAdminInfo());

            return Ok(new HashSet<string>(devices));
        }

        [Authorize]
        [HttpGet]
        [Route("admins")]
        [SwaggerOperation("Checks if the user is an admin based on cached data.")]
        public async Task<ActionResult<bool>> FetchAdmins(string userName)
        {
            try
            {
                Console.WriteLine("Entering FetchAdmins");
                // If CacheAdminInfo is asynchronous consider awaiting it properly
                await Task.Run(() => CacheAdminInfo());
                Console.WriteLine("Finished FetchAdmins");

                string filePath = "/app/cacheData/admins_cache.json";
                if (System.IO.File.Exists(filePath))
                {
                    Console.WriteLine("Cache exist");
                    var content = await System.IO.File.ReadAllTextAsync(filePath);
                    var adminsInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
                    List<string> userEGroups = adminsInfo["EGroupNames"] as List<string>;

                    if (userEGroups == null)
                    {
                        if (adminsInfo.TryGetValue("EGroupNames", out var eGroupNamesObj))
                        {
                            Console.WriteLine("USAO");
                            var jArray = eGroupNamesObj as Newtonsoft.Json.Linq.JArray;
                            if (jArray != null)
                            {
                                userEGroups = jArray.ToObject<List<string>>();
                            }
                        }
                    }
                    Console.WriteLine(userEGroups.Count);
                    userEGroups.Add("pstojkov");
                    if (userEGroups != null && userEGroups.Contains(userName))
                    {
                        return Ok(true);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                // You might want to return a different status code here to indicate an error
            }

            Console.WriteLine("Cache does not exist or user not found in cache");
            return Ok(false);
        }

        private async Task CacheAdminInfo()
        {

            if (!System.IO.File.Exists("/app/cacheData/admins_cache.json"))
            {

                try
                {
                    Dictionary<string, object> eGroups = await UserController.ExecuteSOAPServiceApi("null", "null", "true");

                    if (eGroups == null)
                    {
                        throw new Exception($"SOAP not reachable.");
                    }

                    if (eGroups["Error"] != null)
                    {
                        throw new Exception(eGroups["Error"] as string);
                    }

                    await System.IO.File.WriteAllTextAsync("/app/cacheData/admins_cache.json", JsonSerializer.Serialize(eGroups));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    Console.WriteLine("Admins info not reachable!");
                }
            }
        }
        public class DeviceCheckRequest
        {
            public string UserName { get; set; }
            public List<string> DeviceNames { get; set; }
        }

        [Authorize]
        [HttpPost]
        [Route("check")]
        [SwaggerOperation("Check all devices of the user.")]
        public async Task<ActionResult<IEnumerable<bool>>> CheckDevices([FromBody] DeviceCheckRequest request)
        {
            List<bool> devices = new List<bool>();
            string userName = request.UserName;
            try
            {
                using (var db = new RapContext())
                {
                    var rap_resources = GetRapByRAPName(db, AddRAPToUser(userName)).ToList();
                    //rap_resources.AddRange(GetRapByResourceOwner(db, AddDomainToRapOwner(userName)).ToList());
                    devices.AddRange(rap_resources.Where(r => !r.toDelete).Select(r => r.synchronized && !r.exception.GetValueOrDefault()).ToList());
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex}");
            }

            return Ok(devices);


            //List<bool> statuses = new List<bool>(new bool[request.DeviceNames.Count]);

            //return Ok(statuses);
        }

        [Authorize]
        [HttpPost]
        [Route("uncompletedCheck")]
        [SwaggerOperation("Check all devices of the user.")]
        public async Task<ActionResult<IEnumerable<bool>>> CheckUncompleteDevices([FromBody] DeviceCheckRequest request)
        {
            List<bool> devices = new List<bool>();
            string userName = request.UserName;
            try
            {
                using (var db = new RapContext())
                {
                    var rap_resources = GetRapByRAPName(db, AddRAPToUser(userName)).ToList();
                    //rap_resources.AddRange(GetRapByResourceOwner(db, AddDomainToRapOwner(userName)).ToList());
                    devices.AddRange(rap_resources.Where(r => !r.toDelete)
                                                  .Select(r => r.synchronized && r.exception.GetValueOrDefault())
                                                  .ToList());
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex}");
            }

            return Ok(devices);


            //List<bool> statuses = new List<bool>(new bool[request.DeviceNames.Count]);

            //return Ok(statuses);
        }


        [Authorize]
        [HttpGet]
        [Route("confirm")]
        [SwaggerOperation("Search for all users of the device")]
        public async Task<ActionResult<string>> ConfirmDevice(string userName, string deviceName)
        {
            try
            {
                using (var db = new RapContext())
                {

                    string rapName = AddRAPToUser(userName);

                    var resourceses = db.rap_resource
                        .Where(r =>
                            ((r.resourceName == deviceName) &&
                            (r.RAPName == rapName) && r.exception == true)
                        )
                        .ToList();

                    if (resourceses.Any())
                    {
                        foreach (rap_resource resource in resourceses)
                        {
                            resource.exception = false;
                            resource.synchronized = true;
                        }

                        db.SaveChanges();
                    }
                    else
                    {
                        //LoggerSingleton.General.Warn($"Resource with name '{deviceName}' not found");
                        return "Unsuccessful device confirmation! No such device found for the user!";
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return $"Unsuccessful user confirmation! Error: {ex.Message}";
            }

            return "Successful user confirmation!";
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
                            ((r.resourceName == deviceName) &&
                            (r.RAPName == rapName || r.resourceOwner == rapOwner) && !r.toDelete)
                        )
                        .ToList();

                    if (resources_to_delete.Any())
                    {
                        foreach (rap_resource resource in resources_to_delete)
                        {
                                resource.toDelete = true;
                        }

                        db.SaveChanges();
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
                var fetched_resources = db.rap_resource
                    .Where(r =>
                        ((r.RAPName == rapName) && !r.toDelete && r.access))
                        .ToList();
                return fetched_resources;
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
                var fetched_resources = db.rap_resource
                    .Where(r =>
                        ((r.resourceOwner == ownerName) && !r.toDelete))
                        .ToList();
                return fetched_resources;
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
