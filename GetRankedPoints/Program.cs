using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using RestSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GetRankedPoints
{
    class Program
    {
        
        public static string AccessToken { get; set; }
        public static string EntitlementToken { get; set; }
        public static string username { get; set; }
        public static string password { get; set; }
        public static string UserID  { get; set; }
        public static string region { get; set; }
        
        static void Main(string[] args)
        {
            Console.WriteLine("Welcome to the Ranked Point Checker");
            Console.WriteLine("Checking Config File.."); 
            if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "config.json")))
            {
                Console.WriteLine("Config File not found, enter your username:");
                username = Console.ReadLine();
                Console.WriteLine("enter password");
                var pass = string.Empty;
                ConsoleKey key;
                do
                {
                    var keyInfo = Console.ReadKey(intercept: true);
                    key = keyInfo.Key;

                    if (key == ConsoleKey.Backspace && pass.Length > 0)
                    {
                        Console.Write("\b \b");
                        pass = pass[0..^1];
                    }
                    else if (!char.IsControl(keyInfo.KeyChar))
                    {
                        Console.Write("*");
                        pass += keyInfo.KeyChar;
                    }
                } while (key != ConsoleKey.Enter);
                password=pass;
                Console.WriteLine("\nenter region (eu/na/ap/ko/br): ");
                region=Console.ReadLine();
            }
            else{
                Console.WriteLine("Config File found, Reading File...");

                ReadConfig();
                Console.WriteLine("Finished Reading Config, Logging in..");
            }
            Login();
            Console.WriteLine("Finished Logging in, checking rank progression...");
            CheckRankedUpdates();
        }

        static void ReadConfig()
        {
            StreamReader r = new StreamReader(Path.Combine(Directory.GetCurrentDirectory(), "config.json"));
            string json = r.ReadToEnd();
            // DEBUGGING IGNORE Console.WriteLine(json);
            var localJSON = JsonConvert.DeserializeObject(json);
            JToken localObj = JToken.FromObject(localJSON);
            username = localObj["username"].Value<string>();
            password = localObj["password"].Value<string>();
            region = localObj["region"].Value<string>();
            Console.WriteLine($"Found Username: {username}");
            
        }
        
        
        static void Login()
        {
            try
            {
                CookieContainer cookie = new CookieContainer();
                Authentication.GetAuthorization(cookie);

                var authJson = JsonConvert.DeserializeObject(Authentication.Authenticate(cookie, username, password));
                JToken authObj = JObject.FromObject(authJson);

                string authURL = authObj["response"]["parameters"]["uri"].Value<string>();
                var access_tokenVar = Regex.Match(authURL, @"access_token=(.+?)&scope=").Groups[1].Value;
                AccessToken = $"{access_tokenVar}";

                RestClient client = new RestClient(new Uri("https://entitlements.auth.riotgames.com/api/token/v1"));
                RestRequest request = new RestRequest(Method.POST);

                request.AddHeader("Authorization", $"Bearer {AccessToken}");
                request.AddJsonBody("{}");

                string response = client.Execute(request).Content;
                var entitlement_token = JsonConvert.DeserializeObject(response);
                JToken entitlement_tokenObj = JObject.FromObject(entitlement_token);

                EntitlementToken = entitlement_tokenObj["entitlements_token"].Value<string>();


                RestClient userid_client = new RestClient(new Uri("https://auth.riotgames.com/userinfo"));
                RestRequest userid_request = new RestRequest(Method.POST);

                userid_request.AddHeader("Authorization", $"Bearer {AccessToken}");
                userid_request.AddJsonBody("{}");

                string userid_response = userid_client.Execute(userid_request).Content;
                dynamic userid = JsonConvert.DeserializeObject(userid_response);
                JToken useridObj = JObject.FromObject(userid);

                //Console.WriteLine(userid_response);

                UserID = useridObj["sub"].Value<string>();

                Console.WriteLine($"Logged in successfully! ");
            }
            catch (Exception e)
            {
                Console.WriteLine("BAD LOGIN INFORMATION!!");
                Console.ReadKey();
                throw;
            }
            
        }

        static void CheckRankedUpdates()
        {
            try
            {
                RestClient ranked_client = new RestClient(new Uri($"https://pd.{region}.a.pvp.net/mmr/v1/players/{UserID}/competitiveupdates?startIndex=0&endIndex=20"));
                RestRequest ranked_request = new RestRequest(Method.GET);
            
                ranked_request.AddHeader("Authorization", $"Bearer {AccessToken}");
                ranked_request.AddHeader("X-Riot-Entitlements-JWT", EntitlementToken);
            
                IRestResponse rankedresp = ranked_client.Get(ranked_request);
                if (rankedresp.IsSuccessful)
                {
                    dynamic RankedJson = JsonConvert.DeserializeObject<JObject>(rankedresp.Content);
                    // Debugging IGNORE
                    Console.WriteLine(RankedJson);
                    var store = RankedJson["Matches"];
                    foreach (var game in store)
                    {
                        if (game["CompetitiveMovement"] != "MOVEMENT_UNKNOWN")
                        {
                            Console.WriteLine("Ranked Game detected.");
                            int before = game["TierProgressBeforeUpdate"];
                            int after = game["TierProgressAfterUpdate"];
                            Console.WriteLine($"Points Before: {before}");
                            Console.WriteLine($"Points After: {after}");

                            int num = after - before;

                            string str = before < after ? str = $"Congrats you gained: {num} points"
                                : str = $"Congrats you lost: {num * -1} points";

                            Console.WriteLine(str);
                            Console.ReadKey();
                            Environment.Exit(1);

                            //int num = before - after;

                            //Console.WriteLine($"Net gain/loss: {num} points");
                        }
                        else if (game["CompetitiveMovement"] == "PROMOTED")
                        {
                            Console.WriteLine($"Detected Rank up in last match, Current points in Rank: {game["TierProgressAfterUpdate"].ToString()}");
                            Console.ReadKey();
                            Environment.Exit(1);
                        }
                        else
                        {
                            // Game does not register as a ranked game.
                        }
                    }
                }
            
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
        }
    }
}