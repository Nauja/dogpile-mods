using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Gameplay;
using HarmonyLib;
using NativeWebSocket;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace ViewersChoice
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;
        HttpClient idClient = new()
        {
            BaseAddress = new System.Uri("https://id.twitch.tv")
        };

        HttpClient apiClient = new()
        {
            BaseAddress = new System.Uri("https://api.twitch.tv")
        };

        private ConfigEntry<string> clientId;
        private string accessToken = "";
        private string webSocketSessionId = "";
        private NativeWebSocket.WebSocket webSocket;
        internal static AssetBundle assetBundle;

        public Plugin()
        {
            // Plugin startup logic
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");

            assetBundle = LoadAssetBundle();

            //Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), null);
        }

        [HarmonyPatch(typeof(Gameplay.GameManager), "Awake")]
        static class GameManager_Awake_Patch
        {
            static void Postfix(Gameplay.GameManager __instance)
            {
                var cube = Instantiate(assetBundle.LoadAsset<GameObject>("Cube"));
                cube.name = "MyCube";
                cube.transform.parent = __instance.transform;
            }
        }

        private async void Awake()
        {
            clientId = Config.Bind("General", "ClientId", "1234", new ConfigDescription("Id of the client"));

            //GameEvents.ActiveShop.AddListener(new UnityEngine.Events.UnityAction<Gameplay.Shops.Shop>(OnShopActive));

            await Authenticate();

            MainThreadUtil.Setup();

            webSocket = new NativeWebSocket.WebSocket("wss://eventsub.wss.twitch.tv/ws");
            webSocket.OnOpen += () =>
            {
                Logger.LogInfo("Connection open!");
            };

            webSocket.OnError += (e) =>
            {
                Logger.LogInfo("Error! " + e);
            };

            webSocket.OnClose += (e) =>
            {
                Logger.LogInfo("Connection closed!");
            };

            webSocket.OnMessage += async (bytes) =>
            {
                Logger.LogInfo("OnMessage!");
                Logger.LogInfo(bytes);

                // getting the message as a string
                var message = System.Text.Encoding.UTF8.GetString(bytes);
                var data = JObject.Parse(message);
                switch (data["metadata"].Value<string>("message_type"))
                {
                    case "session_welcome":
                        Logger.LogInfo("Welcome");
                        webSocketSessionId = data["payload"]["session"].Value<string>("id");

                        await SubscribeToEvents();

                        break;
                    case "notification":
                        switch(data["metadata"].Value<string>("subscription_type"))
                        {
                            case "channel.chat.message":
                                var eventData = data["payload"]["event"];
                                Logger.LogInfo($"{eventData.Value<string>("chatter_user_login")}: {eventData["message"].Value<string>("text")}");
                                break;
                        }
                        break;
                }
            };

            // waiting for messages
            await webSocket.Connect();

            var cube = Instantiate(assetBundle.LoadAsset<GameObject>("Cube"));
            cube.name = "MyCube";
            cube.transform.parent = transform;
        }

        private void OnShopActive(Gameplay.Shops.Shop shop)
        {
            Logger.LogInfo($"Shop active {shop}");
        }



        private AssetBundle LoadAssetBundle()
        {
            // Load the asset bundle
            var assetBundleName = "viewerschoice.assets";
            var assetBundlePath = Path.Combine(Path.GetDirectoryName(Info.Location), assetBundleName);
            if (!File.Exists(assetBundlePath))
            {
                Logger.LogError($"Asset bundle {assetBundlePath} not found");
                return null;
            }

            var assetBundleBuffer = File.ReadAllBytes(assetBundlePath);
            AssetBundle assetBundle = AssetBundle.LoadFromMemory(assetBundleBuffer);
            Logger.LogInfo($"Asset bundle {assetBundleName} loaded");
            return assetBundle;
        }

        async Task Authenticate()
        {
            var response = await idClient.PostAsync("oauth2/device", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"client_id", clientId.Value },
                {"scopes", "user:read:chat" }
            }));
            var content = await response.Content.ReadAsStringAsync();
            var data = JObject.Parse(content);
            var deviceCode = data.Value<string>("device_code");

            Application.OpenURL(data.Value<string>("verification_uri"));

            while (true)
            {
                response = await idClient.PostAsync("oauth2/token", new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    {"client_id", clientId.Value },
                    {"scopes", "user:read:chat" },
                    {"device_code",  deviceCode},
                    {"grant_type", "urn:ietf:params:oauth:grant-type:device_code" }
                }));
                if (response.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    Logger.LogInfo("Authenticated");
                    content = await response.Content.ReadAsStringAsync();
                    data = JObject.Parse(content);
                    accessToken = data.Value<string>("access_token");
                    break;
                }

                Logger.LogInfo("Waiting for authentication...");
                await WaitForSecondsAsync(5.0f);
            }
        }

        async Task SubscribeToEvents()
        {
            var postParams = new
            {
                type = "channel.chat.message",
                version = "1",
                condition = new
                {
                    broadcaster_user_id = "42506102",
                    user_id = "42506102"

                },
                transport = new
                {
                    method = "websocket",
                    session_id = webSocketSessionId

                }
            };

            apiClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            apiClient.DefaultRequestHeaders.Add("Client-Id", clientId.Value);

            var response = await apiClient.PostAsync(
                "helix/eventsub/subscriptions",
                new StringContent(JObject.FromObject(postParams).ToString(), null, "application/json")
               );

            if (response.StatusCode != System.Net.HttpStatusCode.Accepted)
            {
                Logger.LogError("Failed to subscribe");
            }
            else
            {
                Logger.LogInfo("Subscribed");
            }
        }

        void Update()
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if (webSocket != null)
            {
                webSocket.DispatchMessageQueue();
            }
#endif
        }

        private async void OnApplicationQuit()
        {
            if (webSocket != null)
            {
                await webSocket.Close();
            }
        }


        async Task WaitForSecondsAsync(float delay)
        {
            await Task.Delay(TimeSpan.FromSeconds(delay));
        }
    }
}
