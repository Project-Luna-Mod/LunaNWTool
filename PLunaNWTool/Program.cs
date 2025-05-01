using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;

namespace PLunaNWTool
{
    class Program
    {
        // Main UI (well, "UI") of the application
        static void Main()
        {
            Console.Title = "Configure a Wi-Fi network";
            Console.WriteLine("Welcome! This is Project Luna's network tool!");
            Console.WriteLine("This tool let's you connect to WPA2/WPA3 networks.\n");

            var ssids = ParseSSIDs(RunCommand("netsh", "wlan show networks"));

            if (ssids.Count == 0)
            {
                Console.WriteLine(IsEthernetConnected()
                    ? "You are currently connected to Ethernet."
                    : "No network connection detected. You might have not installed Wi-Fi drivers, install them and try again.");

                Console.ReadKey();
                return;
            }

            Console.WriteLine($"{ssids.Count} network(s) found:");
            ssids.Select((ssid, i) => new { ssid, i }).ToList().ForEach(item => Console.WriteLine($"[{item.i + 1}] {item.ssid}"));

            Console.Write("\nSelect a network number: ");
            if (!int.TryParse(Console.ReadLine(), out int selected) || selected < 1 || selected > ssids.Count)
            {
                Console.WriteLine("Invalid selection.");
                return;
            }

            string chosenSSID = ssids[selected - 1];
            Console.Write($"Enter password for '{chosenSSID}': ");

            ConnectToNetwork(chosenSSID, Console.ReadLine());
            Console.WriteLine($"\nAttempted connection to '{chosenSSID}'");
            Console.ReadKey();
        }

        // Parse all of the SSIDs from the netsh wlan show networks command
        static List<string> ParseSSIDs(string netshOutput) =>
            netshOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => line.Trim().StartsWith("SSID ", StringComparison.OrdinalIgnoreCase))
                .Select(line => line.Split(':').Last().Trim())
                .Where(ssid => !string.IsNullOrEmpty(ssid))
                .Distinct()
                .ToList();

        // The main application logic it's self, does the command thingy and the profile thingy and put those 2 together and WOW, you have connection to the Internet!! (magical technology, i know)
        static void ConnectToNetwork(string ssid, string password)
        {
            string profileXml = CreateNetworkProfile(ssid, password, "WPA2PSK", "AES");
            string xmlPath = Path.Combine(Path.GetTempPath(), $"{ssid}.xml");
            File.WriteAllText(xmlPath, profileXml);

            RunCommand("netsh", $"wlan add profile filename=\"{xmlPath}\"");
            RunCommand("netsh", $"wlan connect name=\"{ssid}\"");

            File.Delete(xmlPath);

            System.Threading.Thread.Sleep(3000);
            string interfaceInfo = RunCommand("netsh", "wlan show interfaces");

            if (interfaceInfo.Contains($"SSID                   : {ssid}"))
            {
                Console.WriteLine($"\nSuccessfully connected to '{ssid}'");
                System.Threading.Thread.Sleep(3000);
                Environment.Exit(0);
            }
            else
            {
                Console.WriteLine($"\nFailed to connect to '{ssid}'. Check the password and try again.");
                Console.Write("Do you want to retry? (y/n): ");
                var retry = Console.ReadLine();
                if (retry?.Trim().ToLower() == "y")
                {
                    Console.Write($"Re-enter password for '{ssid}': ");
                    string newPassword = Console.ReadLine();
                    ConnectToNetwork(ssid, newPassword);
                }
            }
        }

        // This is the actual profile used for connecting to networks in Windows
        // It should work for most WPA2 / WPA3 networks, atleast in testing.
        static string CreateNetworkProfile(string ssid, string password, string authType, string encryptionType)
        {
            return $@"
                <WLANProfile xmlns=""http://www.microsoft.com/networking/WLAN/profile/v1"">
                    <name>{ssid}</name>
                    <SSIDConfig>
                        <SSID>
                            <name>{ssid}</name>
                        </SSID>
                    </SSIDConfig>
                    <connectionType>ESS</connectionType>
                    <connectionMode>manual</connectionMode>
                    <MSM>
                        <security>
                            <authEncryption>
                                <authentication>{authType}</authentication>
                                <encryption>{encryptionType}</encryption>
                                <useOneX>false</useOneX>
                            </authEncryption>
                            <sharedKey>
                                <keyType>passPhrase</keyType>
                                <protected>false</protected>
                                <keyMaterial>{password}</keyMaterial>
                            </sharedKey>
                        </security>
                    </MSM>
                </WLANProfile>";
        }

        // I think you know what this does.
        static string RunCommand(string fileName, string arguments) =>
            Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            })?.StandardOutput.ReadToEnd() ?? string.Empty;

        // Checks if the user is on ethernet to prevent anything from happening
        // Well, not like anything is gonna happen but like, eh
        static bool IsEthernetConnected()
        {
            var output = RunCommand("netsh", "interface show interface");
            return output.Contains("Ethernet") && output.Contains("Connected");
        }
    }
}