using Microsoft.AspNetCore.Mvc;
using SynchronizerLibrary.Data;
using System.Diagnostics;
using Backend.Exceptions;
using Swashbuckle.AspNetCore.Annotations;
using NetCoreOidcExample.Helpers;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Data.Entity;

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
                    var rap_resources = (await GetRapByResourceName(db, userName, deviceName, fetchToDeleteResource)).ToList();

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
        public class AccessInitRequest
        {
            public string device { get; set; }
            public List<string> Users { get; set; }
        }

        [Authorize]
        [HttpPost]
        [Route("acces_init")]
        [SwaggerOperation("Get access status for a list of users for a specific device")]
        public async Task<ActionResult<Dictionary<string, bool>>> AccessInit([FromBody] AccessInitRequest request)
        {
            var accessStatuses = new Dictionary<string, bool>();
            try
            {
                
                using (var db = new RapContext())
                {
                    foreach (var user in request.Users)
                    {
                        var hasAccessList = await db.rap_resource
                            .Where(r => (r.resourceName == request.device && !r.toDelete && r.RAPName == ("RAP_" + user)))
                            .Select(r => r.access)
                            .ToListAsync();

                        var hasAccess = hasAccessList[0];
                        accessStatuses[user] = hasAccess;
                    }
                }
            } catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            return Ok(accessStatuses);
        }

        public class AccessRequest
        {
            public string signedInUser { get; set; }
            public string searchedDeviceName { get; set; }
            public string user { get; set; }
            public bool lockStatus { get; set; }
        }
    

        [Authorize]
        [HttpPost]
        [Route("access")]
        [SwaggerOperation("Toggle access status for a user for a specific device")]
        public async Task<ActionResult<bool>> Access([FromBody] AccessRequest request)
        {
            try
            {
                using (var db = new RapContext())
                {
                    var resourcesToUpdate = await db.rap_resource
                        .Where(r => (r.resourceName == request.searchedDeviceName && !r.toDelete && r.RAPName == ("RAP_" + request.user)))
                        .ToListAsync();

                    if (resourcesToUpdate.Any())
                    {
                        // Assuming there is only one resource per user-device pair, but it's a list to handle potential multiple records
                        foreach (var resource in resourcesToUpdate)
                        {
                            resource.access = !resource.access; // Toggle the access
                        }

                        await db.SaveChangesAsync(); // Save the changes
                        return Ok(resourcesToUpdate.First().access); // Return the new access status
                    }
                    else
                    {
                        // No resource found for the given conditions
                        return NotFound("Resource not found");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return StatusCode(500, "Internal Server Error");
            }
        }


            private async Task<IEnumerable<rap_resource>> GetRapByResourceName(RapContext db, string userName, string resourceName, bool fetchToDeleteResource)
                {
            try
            {
                List<string> userEGroups = null;
                string rapName = UserDevicesController.AddRAPToUser(userName);
                string rapOwner = UserDevicesController.AddDomainToRapOwner(userName);
                var fetched_resources = db.rap_resource
                    .Where(r =>
                        ((r.resourceName == resourceName) && (!r.toDelete || fetchToDeleteResource)))
                        .ToList();
                var fetched_resources_all_users = db.rap_resource
                    .Where(r =>
                        ((r.resourceName == resourceName) && (!r.toDelete || fetchToDeleteResource)))
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
                    Dictionary<string, object> deviceInfo = await UserController.ExecuteSOAPServiceApi(userName, resourceName, "false");

                    if (deviceInfo == null)
                    {
                        throw new InvalidFetchingException($"Device: {resourceName} does not exist!");
                    }

                    if (deviceInfo["validUser"] as string != userName)
                    {
                        throw new InvalidFetchingException($"User: {userName} does not exist!");
                    }

                    if (deviceInfo["domain"] as string != "OK")
                    {
                        throw new InvalidFetchingException($"The Network Domain(s) of the device {resourceName} are not allowed!");
                    }

                    if (deviceInfo["Error"] != null)
                    {
                        throw new InvalidFetchingException(deviceInfo["Error"] as string);
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
                        throw new InvalidFetchingException($"Error: Could not retrieve ownership data for the device: {resourceName}");
                    }

                    List<string> admins = deviceInfo["EGroupNames"] as List<string>;
                    List<string> egroupUsers = deviceInfo["EGroupUsers"] as List<string>;

                    if (admins?.Contains(userName) != true && egroupUsers?.Contains(userName) != true)
                    {
                        if (userName != responsiblePersonUsername && userName != userPersonUsername)
                        {
                            throw new InvalidFetchingException($"User: {userName} is not an owner or a user of the device: {resourceName}!");
                        }
                    }

                    //throw new InvalidFetchingException($"User: {userName} is not an owner or a user of the Device: {resourceName}!");
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
        public string PrimaryUser { get; set; }
        public string SignedInUser { get; set; }
        public string AddDeviceOrUser { get; set; }
    }


    [Route("api/add_pop_up")]
    [ApiController]
    public class UserController : ControllerBase
    {
        //private const string username = "";
        //private const string password = ".";
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
            user.UserName = user.UserName.Trim().ToLower();
            user.DeviceName = user.DeviceName.Trim().ToLower();

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
                            using (var db = new RapContext())
                            {
                                var hasAccessList = await db.rap_resource
                                .Where(r => (r.resourceName == user.DeviceName && !r.toDelete && r.RAPName == ("RAP_" + user.UserName)))
                                .Select(r => r.access)
                                .ToListAsync();
                                if (!hasAccessList[0])
                                {
                                    return "Access to this device is currently disabled for your account. Please contact the Main or Responsible user to enable your access. They can do so by using the 'Edit - Manage users for this decive' feature and toggling the lock button.";
                                }
                            }
                            return "Device already exists in the list below!";
                        }
                        else if (user.AddDeviceOrUser == "user")
                        {
                            return "User already exists in the list below!";
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

                if (deviceInfo["domain"] as string != "OK")
                {
                    return $"The Network Domain(s) of the device {user.DeviceName} are not allowed!";
                }

                if (user.PrimaryUser != "Primary")
                {
                    List<string> primaryAccounts = deviceInfo["PrimaryAccounts"] as List<string>;
                    if (!primaryAccounts.Contains(user.PrimaryUser) && user.AddDeviceOrUser == "user")
                    {
                        return $"Signed in user: {user.PrimaryUser} is not a primary account!\nOnly primary accounts can manage users!";
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

                List<string> admins = deviceInfo["EGroupNames"] as List<string>;
                List<string> egroupUsers = deviceInfo["EGroupUsers"] as List<string>;

                if (admins?.Contains(user.UserName) != true && admins?.Contains(user.SignedInUser) != true)
                {
                    if (user.UserName != responsiblePersonUsername && user.UserName != userPersonUsername && user.AddDeviceOrUser == "device")
                    {
                        if (egroupUsers?.Contains(user.UserName) != true)
                            return $"User: {user.UserName} is not an owner or a user of the device: {user.DeviceName}!";
                    }

                    if (user.SignedInUser != responsiblePersonUsername && user.SignedInUser != userPersonUsername && user.AddDeviceOrUser == "user")
                    {
                        if (egroupUsers?.Contains(user.SignedInUser) != true)
                            return $"You are not an owner or a main user of the device: {user.DeviceName}, you cannot edit users!";
                    }
                }

                if (responsiblePersonUsername.Length == 0 || responsiblePersonUsername == null)
                {
                    return $"The device: {user.DeviceName} does not have the responsible person (owner)!";
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
                            toDelete = false,
                            unsynchronizedGateways = ""
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
                            toDelete = false,
                            unsynchronizedGateways = ""
                            
                        };

                        db.rap_resource.Add(newRapResource);
                    }
                    else if (existingToDeleteRapResource != null && existingRapResource == null)
                    {
                        existingToDeleteRapResource.toDelete = false;
                        existingToDeleteRapResource.updateDate = DateTime.Now;
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
                    process.StartInfo.Arguments = $"{pathToScript} {userName} {computerName} {adminsOnly}";
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

                        if (result.Count < 6 && adminsOnly == "false")
                        {
                            string data = line.Replace("'", "").Replace("\r", "").Replace("\n", "");
                            switch(result.Count)
                            {
                                case 0:
                                    result["ResponsiblePersonName"] = data;
                                    break;
                                case 1:
                                    result["UserPersonFirstName"] = data;
                                    break;
                                case 2:
                                    result["UserPersonUsername"] = data;
                                    break;
                                case 3:
                                    result["ResponsiblePersonUsername"] = data;
                                    break;
                                case 4:
                                    result["validUser"] = data;
                                    break;
                                case 5:
                                    result["domain"] = data;
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
        //private const string username = "";
        //private const string password = ".";
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
                await Task.Run(() => CacheAdminInfo(true));

                string filePath = "/app/cacheData/admins_cache.json";
                if (System.IO.File.Exists(filePath))
                {
                    var content = await System.IO.File.ReadAllTextAsync(filePath);
                    var adminsInfo = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(content);
                    List<string> userEGroups = adminsInfo["EGroupNames"] as List<string>;

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
                    if (userEGroups != null && userEGroups.Contains(userName))
                    {
                        return Ok(false);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }

            Console.WriteLine("Cache does not exist or user not found in cache");
            return Ok(true);
        }

        private async Task CacheAdminInfo(bool load = false)
        {

            if (!System.IO.File.Exists("/app/cacheData/admins_cache.json") || load)
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
            Dictionary<string, bool> deviceStatuses = new Dictionary<string, bool>();
            string userName = request.UserName;
            try
            {
                using (var db = new RapContext())
                {
                    var rap_resources = GetRapByRAPName(db, AddRAPToUser(userName)).ToList();
                    //rap_resources.AddRange(GetRapByResourceOwner(db, AddDomainToRapOwner(userName)).ToList());
                    //devices.AddRange(rap_resources.Where(r => !r.toDelete).Select(r => r.synchronized && !r.exception.GetValueOrDefault()).ToList());
                    bool status;
                    foreach (var resource in rap_resources.Where(r => !r.toDelete))
                    {
                        if (!deviceStatuses.ContainsKey(resource.resourceName))
                        {

                            status = resource.synchronized && !resource.exception.GetValueOrDefault();
                            deviceStatuses[resource.resourceName] = status;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex}");
            }

            return Ok(deviceStatuses.Values.ToList());


            //List<bool> statuses = new List<bool>(new bool[request.DeviceNames.Count]);

            //return Ok(statuses);
        }
        
        [Authorize]
        [HttpPost]
        [Route("date_check")]
        [SwaggerOperation("Check all devices of the user.")]
        public async Task<ActionResult<IEnumerable<object>>> DateDevices([FromBody] DeviceCheckRequest request)
        {
            Dictionary<string, object> deviceDates = new Dictionary<string, object>();
            string userName = request.UserName;

            try
            {
                using (var db = new RapContext())
                {
                    var rap_resources = GetRapByRAPName(db, AddRAPToUser(userName)).ToList();
                    //rap_resources.AddRange(GetRapByResourceOwner(db, AddDomainToRapOwner(userName)).ToList());

                    foreach (var resource in rap_resources.Where(r => !r.toDelete))
                    {
                        // If the resourceName is not already added, add the resourceName with its date
                        if (!deviceDates.ContainsKey(resource.resourceName))
                        {
                            //deviceDates[resource.resourceName] = string.Format("{0:yyyy-MM-dd HH:mm:ss}", resource.createDate);
                            deviceDates[resource.resourceName] = (new
                            {
                                createDate = string.Format("{0:yyyy-MM-dd HH:mm:ss}", resource.createDate),
                                updateDate = string.Format("{0:yyyy-MM-dd HH:mm:ss}", resource.updateDate)
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex}");
            }

            return Ok(deviceDates.Values.ToList());
        }

        [Authorize]
        [HttpPost]
        [Route("uncompletedCheck")]
        [SwaggerOperation("Check all devices of the user.")]
        public async Task<ActionResult<IEnumerable<bool>>> CheckUncompleteDevices([FromBody] DeviceCheckRequest request)
        {
            Dictionary<string, bool> devicesUncomplete = new Dictionary<string, bool>();
            string userName = request.UserName;
            try
            {
                using (var db = new RapContext())
                {
                    var rap_resources = GetRapByRAPName(db, AddRAPToUser(userName)).ToList();

                    foreach (var resource in rap_resources.Where(r => !r.toDelete))
                    {

                        if (!devicesUncomplete.ContainsKey(resource.resourceName))
                        {
                            devicesUncomplete[resource.resourceName] = resource.synchronized && resource.exception.GetValueOrDefault();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex}");
            }
            return Ok(devicesUncomplete.Values.ToList());
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
                            resource.updateDate = DateTime.Now;
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
        public async Task<ActionResult<string>> RemoveDevice(string userName, string deviceName, string signedInUser, string primaryUser, string addDeviceOrUser)
        {

            if (userName != signedInUser)
            {
                Dictionary<string, object> deviceInfo = await UserController.ExecuteSOAPServiceApi(userName, deviceName, "false");

                if (deviceInfo == null)
                {
                    return $"Device: {deviceName} does not exist!";
                }

                if (deviceInfo["validUser"] as string != userName)
                {
                    return $"User: {userName} does not exist!";
                }

                if (primaryUser != "Primary")
                {
                    List<string> primaryAccounts = deviceInfo["PrimaryAccounts"] as List<string>;
                    if (!primaryAccounts.Contains(primaryUser) && addDeviceOrUser == "user")
                    {
                        return $"Signed in user: {primaryUser} is not a primary account!\nOnly primary accounts can manage users!";
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
                    return $"Error: Could not retrieve ownership data for the device: {deviceName}";
                }

                List<string> admins = deviceInfo["EGroupNames"] as List<string>;
                List<string> egroupUsers = deviceInfo["EGroupUsers"] as List<string>;

                if (admins?.Contains(userName) != true && admins?.Contains(signedInUser) != true)
                {
                    if (userName != responsiblePersonUsername && userName != userPersonUsername && addDeviceOrUser == "device")
                    {
                        if (egroupUsers?.Contains(userName) != true)
                            return $"User: {userName} is not an owner or a user of the device: {deviceName}!";
                    }

                    if (signedInUser != responsiblePersonUsername && signedInUser != userPersonUsername && addDeviceOrUser == "user")
                    {
                        if (egroupUsers?.Contains(signedInUser) != true)
                            return $"You are not an owner or a main user of the device: {deviceName}, you cannot edit users!";
                    }
                }
            }
            try
            {
                using (var db = new RapContext())
                {

                    string rapName = AddRAPToUser(userName);
                    string rapOwner = AddDomainToRapOwner(userName);
                    var resources_to_delete = db.rap_resource
                        .Where(r =>
                            ((r.resourceName == deviceName) &&
                            (r.RAPName == rapName) && !r.toDelete)
                        )
                        .ToList();
                    //var resources_to_delete = db.rap_resource
                    //    .Where(r =>
                    //        ((r.resourceName == deviceName) &&
                    //        (r.RAPName == rapName || r.resourceOwner == rapOwner) && !r.toDelete)
                    //    )
                    //    .ToList();

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
