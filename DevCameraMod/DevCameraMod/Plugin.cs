using BepInEx;
using Cinemachine;
using GorillaNetworking;
using Photon.Pun;
using DevCameraMod.Models;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Net;
using System.Threading;
using Photon.Voice.PUN;
using System.Net.Http;
using Photon.Voice;

namespace DevCameraMod
{
    /// <summary>
    /// This is the main script for my camera mod.
    /// </summary>
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]
    public class Plugin : BaseUnityPlugin
    {
        // Instance data
        public static Plugin Instance { get; private set; }
        public bool Initialized;

        // Objects
        public GameObject nametagBase;
        public AudioListener playerListener;
        public AudioListener cameraListener;

        // Camera objects
        public Camera camera;
        public Transform cameraParent;
        public CinemachineVirtualCamera virtualCamera;
        public GameObject previewObject;
        public List<int> fixedCameras = new List<int>();
        public List<string> fixedCameraNames = new List<string>();

        // Sounds
        public AudioClip type;
        public AudioClip click;
        
        // Position/Rotation data
        public Vector3 cameraPosition;
        public Vector2 cameraRotation;
        public Quaternion cR3;
        public Vector3 finalPos;
        public Vector2 finalRot;
        public Quaternion finalcR3;

        // General camera variables
        public float currentSpeed = 2.5f;
        public float currentMultiplier = 1;
        public float rotationMultiplier = 8.5f;
        public float forward = -1.38f;
        public float right = 0.3566f;
        public float up = 0.8648f;
        public bool listener;
        public float cameraLerp = 0.07f;
        public float quatLerp = 0.085f;
        public float editFOV = 60;
        public float FOV = 60;
        public float editClipPlane = 0.03f;
        public float clipPlane = 0.03f;
        public float clipPlaneFar = 10000;
        public int intMode = 0;
        public int intMask = 0;
        public CameraModes cameraMode;
        public bool CanChangeMode;
        public bool nameTags;

        // General camera follow
        public VRRig vrrig;
        public VRRig toRig;
        public VRRig lastRig;
        public VRRig rigtostaredown;
        public int rigtofollow;
        public int lastplr;
        public bool canFocusOnOthersFace;
        public bool firstperson;
        public bool updateHideCosmetics = true;

        // AI camera follow
        public float findAnotherCooldown = 0;
        public float changeTransformCooldown = 0;
        public float ifNotTaggedCooldown = 0;
        public float tripsWithGuy = 0;
        public float scoreboardUpdate = 0;
        public float lastTrackedSurvivor;
        public bool spectatingSurvivor = true;
        public System.Random monkRand;

        // User interface
        public string lobbyToEnter = "CODE";
        public float canvasSize = 120;
        public float canvasScaleCurrent = 120;
        public CameraUI cameraUI;
        public bool ShowMenu = true;
        public bool ShowFrame;
        public bool shouldBeEnabled;
        public bool canBeShown = true;
        public WebClient webClient;
        public bool wasInRoom;
        public bool isAllowed;

        // Lap system
        public double currentTime = -10;
        public double lapTime = -10;
        public bool timeStart;
        public bool hasPassedzero;

        // Game
        public GorillaTagManager gtm;

        // Enums
        public enum CameraModes // TODO: rearrange this to make it work better
        {
            Default,
            FP,
            DefEnhanced,
            Freecam,
            SelectedPlayer,
            ActivitySpan,
            SurvivorFocus,
            LavaFocus,
            ArenaFocus
        }

        public Dictionary<CameraModes, string> fixedCameraModeName = new Dictionary<CameraModes, string>()
        {
            { CameraModes.Default, "Regular" },
            { CameraModes.DefEnhanced, "Enhanced" },
            { CameraModes.FP, "First Person" },
            { CameraModes.Freecam, "Spectator" },
            { CameraModes.SelectedPlayer, "PlayerFocus" },
            { CameraModes.ActivitySpan, "RandomFocus" },
            { CameraModes.SurvivorFocus, "SurvivorFocus" },
            { CameraModes.LavaFocus, "TaggerFocus" }
        };

        protected void Awake()
        {
            Instance = this;

            HarmonyPatches.HarmonyPatches.ApplyHarmonyPatches();
        }

        public void OnInitialized()
        {
            /* Code here runs after the game initializes (i.e. GorillaLocomotion.Player.Instance != null) */

            if (Initialized) return;

            camera = GorillaTagger.Instance.thirdPersonCamera.GetComponentInChildren<Camera>();
            cameraParent = GorillaTagger.Instance.thirdPersonCamera.transform;
            virtualCamera = camera.GetComponentInChildren<CinemachineVirtualCamera>();
            vrrig = GorillaTagger.Instance.offlineVRRig;

            CanChangeMode = true;

            fixedCameraNames.Add("VR");
            fixedCameras.Add(GorillaLocomotion.Player.Instance.GetComponentInChildren<Camera>().cullingMask);
            fixedCameraNames.Add("Spectator");
            fixedCameras.Add(camera.cullingMask);

            cameraUI = new CameraUI();

            Stream str = Assembly.GetExecutingAssembly().GetManifestResourceStream("DevCameraMod.Resources.devcameraui");
            AssetBundle bundle = AssetBundle.LoadFromStream(str);

            type = bundle.LoadAsset<AudioClip>("click");
            click = bundle.LoadAsset<AudioClip>("button");

            GameObject uiObject = Instantiate(bundle.LoadAsset<GameObject>("DevCameraUI"));

            nametagBase = bundle.LoadAsset<GameObject>("NametagBase");

            DontDestroyOnLoad(uiObject);
            cameraUI.canvas = uiObject.GetComponent<Canvas>();
            cameraUI.cameraSpectator = uiObject.transform.Find("Text").GetComponent<Text>();
            cameraUI.currentlySpectating = uiObject.transform.Find("Text (1)").GetComponent<Text>();
            cameraUI.leftPoints = uiObject.transform.Find("TeamPoints1").GetComponent<Text>();
            cameraUI.rightPoints = uiObject.transform.Find("TeamPoints2").GetComponent<Text>();
            cameraUI.leftTeam = uiObject.transform.Find("TeamName1").GetComponent<Text>();
            cameraUI.rightTeam = uiObject.transform.Find("TeamName2").GetComponent<Text>();
            cameraUI.currentTime = uiObject.transform.Find("CurrentTime").GetComponent<Text>();
            cameraUI.lapTime = uiObject.transform.Find("CurrentTime (1)").GetComponent<Text>();
            cameraUI.currentSpecImage = uiObject.transform.Find("RawImage").GetComponent<RawImage>();
            cameraUI.scoreboardText = uiObject.transform.Find("board1").GetComponent<Text>();
            cameraUI.scoreboardText2 = uiObject.transform.Find("board2").GetComponent<Text>();
            cameraUI.versionTex = uiObject.transform.Find("VersionTex").GetComponent<Text>();
            cameraUI.version2 = uiObject.transform.Find("VersionTexA").GetComponent<Text>();
            cameraUI.codeSecret = uiObject.transform.Find("Scoreheader (1)").GetComponent<Text>();
            cameraUI.scoreHeader = uiObject.transform.Find("Scoreheader").GetComponent<Text>();

            cameraUI.canvas.enabled = false;
            cameraUI.leftTeam.text = "null";
            cameraUI.rightTeam.text = "null";

            UpdateLap();

            playerListener = GorillaLocomotion.Player.Instance.GetComponentInChildren<AudioListener>();
            cameraListener = camera.gameObject.AddComponent<AudioListener>();
            Debug.unityLogger.logEnabled = true;
            camera.cullingMask = fixedCameras[intMask];

            cameraListener.enabled = false;
            Initialized = true;

            Thread thread = new Thread(UpdateThead);
            thread.Start();
        }

        public async void GetAllowed()
        {
            HttpClient client = new HttpClient();
            string result = await client.GetStringAsync("https://github.com/developer9998/DevCameraMod/blob/main/DevCameraMod/DevCameraMod/Allowed.txt");
            using (StringReader reader = new StringReader(result))
            {
                string line;
                while ((line = reader.ReadLine()) != null) if (PhotonNetwork.LocalPlayer.UserId == line) isAllowed = true;
            }

            client.Dispose();
        }

        void UpdateThead()
        {
            Thread.Sleep(10000);
            GetAllowed();
            webClient = new WebClient();
            cameraUI.version2.text = PluginInfo.Name + $"{(isAllowed ? string.Empty : "Free")} v" + PluginInfo.Version;
            try
            {
                while (true)
                {
                    Thread.Sleep(5000);
                    string webData = webClient.DownloadString("https://raw.githubusercontent.com/developer9998/DevCameraMod/main/DevCameraMod/DevCameraMod/ScreenText.txt");

                    cameraUI.versionTex.text = webData;
                }
            }
            finally { webClient.Dispose(); }
        }

        internal void SwitchModePress(bool leftButton, int minConstraints, int maxConstaints)
        {
            if (leftButton)
            {
                if (PhotonNetwork.InRoom) intMode = Mathf.Clamp(intMode - 1, 0, PhotonNetwork.CurrentRoom.IsVisible ? minConstraints : maxConstaints);
                else intMode = Mathf.Clamp(intMode - 1, 0, minConstraints);
                cameraMode = (CameraModes)intMode;
                if (click != null) GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(click);
                OnModeChange();
                return;
            }

            if (PhotonNetwork.InRoom) intMode = Mathf.Clamp(intMode + 1, 0, PhotonNetwork.CurrentRoom.IsVisible ? minConstraints : maxConstaints);
            else intMode = Mathf.Clamp(intMode + 1, 0, minConstraints);
            cameraMode = (CameraModes)intMode;
            if (click != null) GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(click);
            OnModeChange();
        }

        [STAThread]
        protected void OnGUI()
        {
            if (ShowMenu)
            {
                if (GUI.Button(new Rect(25, 25, 180, 20), $"{(!ShowFrame ? "Open" : "Close")} Spectator Menu"))
                {
                    if (click != null) GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(click);
                    ShowFrame = !ShowFrame;
                }

                if (ShowFrame)
                {
                    int modesWhenNotInRoom = 2;
                    int moesWhenInRoom = 7;
                    GUI.Box(new Rect(25, 60, 180, canvasScaleCurrent), "Dev's Camera Mod v" + PluginInfo.Version);

                    GUI.Label(new Rect((180 / 2) - 30, 100, 180, 20), fixedCameraModeName[cameraMode]);

                    if (GUI.Button(new Rect(25 + 5, 100, 20, 20), "<")) SwitchModePress(true, modesWhenNotInRoom, moesWhenInRoom);
                    if (GUI.Button(new Rect(180 - 5, 100, 20, 20), ">")) SwitchModePress(false, modesWhenNotInRoom, moesWhenInRoom);

                    bool lab = false;
                    if (!PhotonNetwork.InRoom) lab = true;
                    else if (PhotonNetwork.CurrentRoom.IsVisible) lab = true;

                    if (lab) GUI.Label(new Rect(35, 125, 180, 20), "<color=red>Join a private for all modes</color>");

                    GUI.Label(new Rect((180 / 2) - 35, 150, 180, 20), $"Culling: {fixedCameraNames[intMask]}");
                    if (GUI.Button(new Rect(25 + 5, 150, 20, 20), "<"))
                    {
                        intMask = Mathf.Clamp(intMask - 1, 0, 1);
                        if (click != null) GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(click);
                        camera.cullingMask = fixedCameras[intMask];
                    }

                    if (GUI.Button(new Rect(180 - 5, 150, 20, 20), ">"))
                    {
                        intMask = Mathf.Clamp(intMask + 1, 0, 1);
                        if (click != null) GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(click);
                        camera.cullingMask = fixedCameras[intMask];
                    }

                    canvasScaleCurrent = Mathf.Lerp(canvasScaleCurrent, canvasSize, 0.1f);

                    if (cameraMode != CameraModes.Default && cameraMode != CameraModes.FP && cameraMode != CameraModes.DefEnhanced && cameraUI != null)
                    {
                        string lastLeftName = cameraUI.LeftTeamName;
                        string lastRightName = cameraUI.RightTeamName;
                        float optionPosition = 180;

                        if (cameraMode == CameraModes.Freecam)
                        {
                            GUI.Label(new Rect(60, optionPosition, 160, 20), $"Camera Speed: {(currentSpeed.ToString().Length >= 3 ? currentSpeed.ToString().Substring(0, 3) : currentSpeed.ToString())}");
                            currentSpeed = GUI.HorizontalSlider(new Rect(25 + 10 / 2, optionPosition + 30, 170, 20), currentSpeed, 0, 5);
                            optionPosition += 50;
                        }

                        GUI.Label(new Rect(50, optionPosition, 160, 70), $"Position         Rotation");
                        cameraLerp = GUI.HorizontalSlider(new Rect(25 + 5, optionPosition + 30, 160 / 2, 20), cameraLerp, 0.02f, 0.3f);
                        quatLerp = GUI.HorizontalSlider(new Rect(180 - 65, optionPosition + 30, 160 / 2, 20), quatLerp, 0.02f, 0.3f);

                        optionPosition += 50;

                        GUI.Label(new Rect(50, optionPosition, 160, 70), $"  FOV            Clipping");
                        FOV = GUI.HorizontalSlider(new Rect(25 + 5, optionPosition + 30, 160 / 2, 20), FOV, 30f, 160f);
                        clipPlane = GUI.HorizontalSlider(new Rect(180 - 65, optionPosition + 30, 160 / 2, 20), clipPlane, 0.01f, 0.5f);

                        optionPosition += 50;
                        GUI.Label(new Rect(60, optionPosition, 160, 20), $"Camera listener: {(listener ? "On" : "Off")}");
                        float listenerFloat = GUI.HorizontalSlider(new Rect(25 + 10 / 2, optionPosition + 30, 170, 20), listener == true ? 1 : 0, 0, 1);

                        optionPosition += 50;
                        GUI.Label(new Rect(60, optionPosition, 160, 20), $"Left team: {cameraUI.LeftTeamName}");
                        string teamName = GUI.TextArea(new Rect(25 + 10 / 2, optionPosition + 30, 170, 20), cameraUI.LeftTeamName, 200);

                        optionPosition += 50;
                        GUI.Label(new Rect(60, optionPosition, 160, 20), $"Right team: {cameraUI.LeftTeamName}");
                        string rightTeamName = GUI.TextArea(new Rect(25 + 10 / 2, optionPosition + 30, 170, 20), cameraUI.RightTeamName, 200);

                        if (teamName.Length <= 7 && lastLeftName != teamName)
                        {
                            if (type != null) GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(type);
                            cameraUI.LeftTeamName = teamName.Replace(System.Environment.NewLine, "");
                            cameraUI.leftTeam.text = cameraUI.LeftTeamName;
                        }

                        if (rightTeamName.Length <= 7 && lastRightName != rightTeamName)
                        {
                            if (type != null) GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(type);
                            cameraUI.RightTeamName = rightTeamName.Replace(System.Environment.NewLine, "");
                            cameraUI.rightTeam.text = cameraUI.RightTeamName;
                        }

                        listener = listenerFloat == 1;

                        canvasSize = optionPosition;
                    }
                    else
                    {
                        string lastLobby = lobbyToEnter;
                        float optionPosition = 180;

                        if (!PhotonNetwork.InRoom)
                        {
                            GUI.Label(new Rect(40, optionPosition, 160, 20), $"Room: {lobbyToEnter}");
                            string lobbyTemp = GUI.TextArea(new Rect(25 + 10 / 2.5f, optionPosition + 30, 170, 20), lobbyToEnter, 200);

                            if (lobbyTemp.Length <= 12 && lastLobby != lobbyTemp)
                            {
                                if (type != null) GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(type);
                                lobbyToEnter = lobbyTemp.Replace(Environment.NewLine, string.Empty).ToUpper();
                            }

                            optionPosition += 28;

                            string joinText;
                            string[] joiningNames = new string[3] { "JoiningPublicRoom", "JoiningSpecificRoom", "JoiningFriend" };
                            string[] loadingNames = new string[2] { "Initialization", "DeterminingPingsAndPlayerCount" };

                            if (PhotonNetworkController.Instance == null) joinText = "Connecting";
                            else joinText = joiningNames.Contains(PhotonNetworkController.Instance.CurrentState()) ? "Cancel" : (loadingNames.Contains(PhotonNetworkController.Instance.CurrentState()) ? "Connecting" : "Join");

                            if (GUI.Button(new Rect(25 + 10 / 2, optionPosition + 30, 170, 20), joinText))
                            {
                                if (joinText == "Join")
                                {
                                    if (click != null) GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(click);
                                    if (!GorillaComputer.instance.CheckAutoBanListForName(lobbyToEnter)) return;
                                    if (lobbyToEnter.IsNullOrWhiteSpace()) return;
                                    PhotonNetworkController.Instance.AttemptToJoinSpecificRoom(lobbyToEnter);
                                }
                                else if (joinText == "Cancel")
                                {
                                    if (click != null) GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(click);
                                    PhotonNetworkController.Instance.AttemptDisconnect();
                                }
                            }

                            if (joinText == "Join")
                            {
                                optionPosition += 28;
                                if (GUI.Button(new Rect(25 + 10 / 2, optionPosition + 30, 170, 20), "Join via. Copy"))
                                {
                                    if (click != null) GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(click);

                                    string codeTemp = GUIUtility.systemCopyBuffer.ToUpper();
                                    if (codeTemp.IsNullOrWhiteSpace()) return;
                                    if (codeTemp.Length >= 13) codeTemp = codeTemp.Substring(0, 12);
 
                                    lobbyToEnter = codeTemp;
                                }
                            }
                        }
                        else
                        {
                            optionPosition -= 30;
                            if (GUI.Button(new Rect(25 + 10 / 2, optionPosition + 30, 170, 20), "Disconnect"))
                            {
                                if (click != null) GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(click);
                                PhotonNetworkController.Instance.AttemptDisconnect();
                            }
                        }

                        canvasSize = optionPosition;
                    }
                }
            }
        }


        public void OnFirstPersonToggle()
        {
            if (type != null) GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(type);
            camera.transform.SetParent(cameraParent, false);
            updateHideCosmetics = true;
        }

        public void OnModeChange()
        {
            cameraUI.canvas.enabled = false;
            shouldBeEnabled = false;
            firstperson = false;
            finalPos = camera.transform.position;
            updateHideCosmetics = true;
            camera.transform.SetParent(cameraParent, false);

            if (toRig != null) foreach (var cosmetic in toRig.cosmetics) if (cosmetic.transform.parent.parent.name == toRig.headMesh.name) cosmetic.layer = 0;

            PhotonNetworkController.Instance.disableAFKKick = true;
            camera.transform.SetParent(cameraParent, false);
            camera.gameObject.SetActive(cameraMode != CameraModes.FP);

            virtualCamera.enabled = cameraMode == CameraModes.Default;
            cameraPosition = camera.transform.position;
            cameraRotation = camera.transform.eulerAngles;
            finalPos = cameraPosition;
            finalRot = cameraRotation;

            findAnotherCooldown = 0;
            changeTransformCooldown = 0;
            ifNotTaggedCooldown = 0;
            lastplr = 0;
            tripsWithGuy = 0;
            toRig = null;
            rigtofollow = 0;
            rigtostaredown = null;

            if (previewObject != null) Destroy(previewObject);

            if (cameraMode == CameraModes.Freecam)
            {
                previewObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                previewObject.transform.SetParent(camera.transform, false);
                previewObject.transform.localPosition = Vector3.zero;
                previewObject.transform.localRotation = Quaternion.identity;
                previewObject.transform.localScale = Vector3.one * 0.085f;
                previewObject.GetComponent<Collider>().enabled = false;
                previewObject.GetComponent<Renderer>().material = vrrig.materialsToChangeTo[0];
            }

            gtm = null;
            if (GorillaGameManager.instance != null && GorillaGameManager.instance.GetComponent<GorillaTagManager>() != null) gtm = GorillaGameManager.instance.GetComponent<GorillaTagManager>();

            //cameraUI.canvas.enabled = false;
        }

        #region Player Searching
        public void SwitchPlayerMode(int plr)
        {
            if (PhotonNetwork.InRoom)
            {
                rigtofollow = Mathf.Clamp(plr, 0, GorillaParent.instance.vrrigs.Count - 1);
            }
            else
            {
                rigtofollow = 0;
            }
        }

        public bool CheckIfBiggerGroup(bool checkForSameGroup, bool checkForBiggerGroup)
        {
            if (toRig == null) return false;

            List<Vector3> recordedPositions = new List<Vector3>();
            int monkesInArea = 0;

            System.Random rng = new System.Random();
            var shuffledPlayers = GorillaParent.instance.vrrigs.OrderBy(a => rng.Next()).ToList();

            foreach (VRRig rig in shuffledPlayers) recordedPositions.Add(rig.headMesh.transform.position);

            Vector3 pos = toRig.headMesh.transform.position;
            for (int i = 0; i < recordedPositions.Count; i++)
            {
                if (pos != recordedPositions[i])
                {
                    if (Vector3.Distance(pos, recordedPositions[i]) < 3.5f)
                    {
                        monkesInArea++;
                    }
                }
            }

            if (monkesInArea > lastplr) return checkForBiggerGroup; // if theres more players in that group
            if (lastplr - monkesInArea <= Mathf.RoundToInt(PhotonNetwork.CurrentRoom.PlayerCount * 0.16f)) return checkForSameGroup; // if the group is still intact

            return false;
        }

        public void FindRoundBasedGroup(bool infected)
        {
            if (gtm == null) return; // Requires a GorillaTagManager
            if (gtm.currentInfected.Count == 0) return; // Requires infected gorillas
            if (gtm.isCasual) return; // Requires the Infection gamemode
            if (gtm.currentInfected.Count == PhotonNetwork.CurrentRoom.PlayerCount) // Requires survivor gorillas
            {
                monkRand = new System.Random(); // Randomizes the random value after the round is finished
                return;
            }

            // List of Infected and Survivor players
            List<VRRig> infectedGorillas = new List<VRRig>();
            List<VRRig> survivorGorillas = new List<VRRig>();

            try
            {
                // Generates the lists mentioned above

                foreach (var inf in gtm.currentInfected) if (gtm.FindVRRigForPlayer(inf).GetComponent<VRRig>() != null) infectedGorillas.Add(gtm.FindVRRigForPlayer(inf).GetComponent<VRRig>());
                foreach (var gorilla in GorillaParent.instance.vrrigs) if (!infectedGorillas.Contains(gorilla)) survivorGorillas.Add(gorilla);
            }
            catch (System.Exception e)
            {
                if (e.Message.ToLower().Contains("object reference"))
                {
                    // If this is run, it's probably because the host left incorrectly.

                    if (PhotonNetwork.MasterClient == null)
                    {
                        // If it's because of the host leaving, pause for a bit.

                        findAnotherCooldown = Time.time + 5f;
                        monkRand = new System.Random();
                        return;
                    }
                }
            }

            // IF the method needs to find infected players but focus on survivors if the infected is closer to the survivor
            // EX: Focus on an infected player if they're the closest to the survivors
            if (infected)
            {
                // The distance of all the survivors compared to a lava
                // EX: lavaDistances[0] == 20, lavaDistances[1] == 10
                List<float> lavaDistances = new List<float>();

                for (int i = 0; i < infectedGorillas.Count; i++)
                {
                    float totalDist;
                    List<float> dist = new List<float>();
                    VRRig LavaGorilla = infectedGorillas[i];
                    for (int index = 0; index < survivorGorillas.Count; index++)
                    {
                        VRRig SurvivorGorilla = survivorGorillas[index];
                        dist.Add(Vector3.Distance(SurvivorGorilla.headMesh.transform.position, LavaGorilla.headMesh.transform.position));
                    }
                    totalDist = dist.Sum();
                    lavaDistances.Add(totalDist);
                }

                VRRig currentToFollow = null;
                VRRig currentToStare = null;
                float closestDist = 0;
                if (lavaDistances.Count > 0)
                {
                    for (int i = 0; i < infectedGorillas.Count; i++)
                    {
                        VRRig LavaGorilla = infectedGorillas[i];
                        float dist = lavaDistances[i];
                        if (dist == lavaDistances.Min())
                        {
                            for (int index = 0; index < survivorGorillas.Count; index++)
                            {
                                VRRig SurvivorGorilla = survivorGorillas[index];
                                if (Vector3.Distance(SurvivorGorilla.headMesh.transform.position, LavaGorilla.headMesh.transform.position) < 7.5f)
                                {
                                    currentToFollow = LavaGorilla;
                                    currentToStare = SurvivorGorilla;
                                    closestDist = Vector3.Distance(SurvivorGorilla.headMesh.transform.position, LavaGorilla.headMesh.transform.position);
                                }
                            }
                            if (currentToFollow == null)
                            {
                                currentToFollow = LavaGorilla;
                                currentToStare = LavaGorilla;
                                closestDist = 7.5f;
                            }
                        }
                    }

                    if (currentToFollow != null)
                    {
                        if (currentToFollow != currentToStare) // if the lava is focussing in on another player, try doing another few things to ensure this.
                        {
                            for (int index = 0; index < survivorGorillas.Count; index++)
                            {
                                VRRig SurvivorGorilla = survivorGorillas[index];
                                if (currentToStare == SurvivorGorilla) // check to see if this move should be corrected
                                {
                                    List<float> distances = new List<float>();
                                    for (int i = 0; i < infectedGorillas.Count; i++)
                                    {
                                        VRRig LavaGorilla = infectedGorillas[i];
                                        distances.Add(Vector3.Distance(SurvivorGorilla.headMesh.transform.position, LavaGorilla.headMesh.transform.position));
                                    }

                                    if (distances.Count > 0)
                                    {
                                        float minDist = distances.Min();
                                        for (int i = 0; i < infectedGorillas.Count; i++)
                                        {
                                            VRRig LavaGorilla = infectedGorillas[i];
                                            float dist = distances[i];
                                            if (dist == minDist)
                                            {
                                                if (currentToFollow != LavaGorilla)
                                                {
                                                    closestDist = minDist;
                                                    currentToFollow = LavaGorilla;
                                                }
                                            }
                                        }
                                    }

                                }
                            }
                        }
                        else // if they're alone, try to look again for others.
                        {
                            List<float> closestDistance = new List<float>() { };
                            List<VRRig> closestLava = new List<VRRig>();
                            try
                            {
                                for (int index = 0; index < survivorGorillas.Count; index++)
                                {
                                    VRRig SurvivorGorilla = survivorGorillas[index];
                                    List<float> localDistances = new List<float>();
                                    List<VRRig> lavasNear = new List<VRRig>();
                                    for (int i = 0; i < infectedGorillas.Count; i++)
                                    {
                                        VRRig LavaGorilla = infectedGorillas[i];
                                        localDistances.Add(Vector3.Distance(SurvivorGorilla.headMesh.transform.position, LavaGorilla.headMesh.transform.position));
                                        lavasNear.Add(LavaGorilla);
                                    }
                                    for (int i = 0; i < localDistances.Count; i++)
                                    {
                                        if (lavasNear[i] != null)
                                        {
                                            float distance = localDistances[i];
                                            VRRig rig = lavasNear[i];
                                            if (distance == localDistances.Min())
                                            {
                                                closestDistance.Add(distance);
                                                closestLava.Add(rig);
                                            }
                                        }
                                    }
                                }
                            }
                            catch (System.Exception w)
                            {
                                Debug.Log($"ERROR: {w.Message} {w.Source}");
                            }

                            for (int index = 0; index < closestDistance.Count; index++)
                            {
                                float distance = closestDistance[index];
                                VRRig rig = closestLava[index];

                                if (distance == closestDistance.Max())
                                {
                                    closestDist = closestDistance.Max();
                                    currentToFollow = rig;
                                    currentToStare = rig;
                                }
                            }
                        }
                    }
                }

                if (currentToFollow != null)
                {
                    if (toRig != null)
                    {
                        float distBetween = Vector3.Distance(currentToStare.headMesh.transform.position, toRig.headMesh.transform.position);
                        if (distBetween < infectedGorillas.Count / 4f) return;
                    }
                    if (toRig == currentToFollow)
                    {
                        if (currentToStare != currentToFollow)
                        {
                            if (Vector3.Distance(currentToStare.headMesh.transform.position, currentToFollow.headMesh.transform.position) > 5)
                            {
                                currentToStare = currentToFollow;
                            }
                        }

                        if (closestDist < 3f && tripsWithGuy >= 2)
                        {
                            tripsWithGuy = 0;
                            toRig = currentToFollow;
                            rigtostaredown = currentToStare ?? currentToFollow;
                            findAnotherCooldown = Time.time + 1.75f;
                            return;
                        }
                        else if (closestDist < 1.75f)
                        {
                            tripsWithGuy = 0;
                            toRig = currentToFollow;
                            rigtostaredown = currentToStare ?? currentToFollow;
                            findAnotherCooldown = Time.time + 1.85f;
                            return;
                        }
                        tripsWithGuy++;
                        findAnotherCooldown = Time.time + 1.45f;
                    }
                    else
                    {
                        tripsWithGuy = 0;
                        toRig = currentToFollow;
                        rigtostaredown = currentToStare ?? currentToFollow;
                        findAnotherCooldown = Time.time + 1.3f;
                    }
                    return;
                }
            }
            else
            {
                // The distance of all the survivors compared to a lava
                // EX: lavaDistances[0] == survivorGorillas, lavaDistances[1] == 10
                List<float> lavaDistances = new List<float>();
                for (int i = 0; i < survivorGorillas.Count; i++)
                {
                    float totalDist;
                    List<float> dist = new List<float>();
                    VRRig LavaGorilla = survivorGorillas[i];
                    for (int index = 0; index < infectedGorillas.Count; index++)
                    {
                        VRRig SurvivorGorilla = infectedGorillas[index];
                        dist.Add(Vector3.Distance(SurvivorGorilla.headMesh.transform.position, LavaGorilla.headMesh.transform.position));
                    }
                    totalDist = dist.Sum();
                    lavaDistances.Add(totalDist);
                }

                VRRig currentToFollow = null;
                VRRig currentToStare = null;
                float closestDist = 0;
                if (lavaDistances.Count > 0)
                {
                    for (int i = 0; i < survivorGorillas.Count; i++)
                    {
                        VRRig LavaGorilla = survivorGorillas[i];
                        float dist = lavaDistances[i];
                        if (dist == lavaDistances.Min())
                        {
                            for (int index = 0; index < infectedGorillas.Count; index++)
                            {
                                VRRig SurvivorGorilla = infectedGorillas[index];
                                if (Vector3.Distance(SurvivorGorilla.headMesh.transform.position, LavaGorilla.headMesh.transform.position) < 7.5f)
                                {
                                    currentToFollow = LavaGorilla;
                                    currentToStare = SurvivorGorilla;
                                    closestDist = Vector3.Distance(SurvivorGorilla.headMesh.transform.position, LavaGorilla.headMesh.transform.position);
                                }
                            }
                            if (currentToFollow == null)
                            {
                                currentToFollow = LavaGorilla;
                                currentToStare = LavaGorilla;
                                closestDist = 7.5f;
                            }
                        }
                    }
                    if (currentToFollow != null)
                    {
                        if (currentToFollow != currentToStare) // if the lava is focussing in on another player, try doing another few things to ensure this.
                        {
                            for (int index = 0; index < infectedGorillas.Count; index++)
                            {
                                VRRig SurvivorGorilla = infectedGorillas[index];
                                if (currentToStare == SurvivorGorilla) // check to see if this move should be corrected
                                {
                                    List<float> distances = new List<float>();
                                    for (int i = 0; i < survivorGorillas.Count; i++)
                                    {
                                        VRRig LavaGorilla = survivorGorillas[i];
                                        distances.Add(Vector3.Distance(SurvivorGorilla.headMesh.transform.position, LavaGorilla.headMesh.transform.position));
                                    }

                                    if (distances.Count > 0)
                                    {
                                        float minDist = distances.Min();
                                        for (int i = 0; i < survivorGorillas.Count; i++)
                                        {
                                            VRRig LavaGorilla = survivorGorillas[i];
                                            float dist = distances[i];
                                            if (dist == minDist)
                                            {
                                                if (currentToFollow != LavaGorilla)
                                                {
                                                    closestDist = minDist;
                                                    currentToFollow = LavaGorilla;
                                                }
                                            }
                                        }
                                    }

                                }
                            }
                        }
                        else // if they're alone, try to look again for others.
                        {
                            List<float> closestDistance = new List<float>() { };
                            List<VRRig> closestLava = new List<VRRig>();
                            try
                            {
                                for (int index = 0; index < infectedGorillas.Count; index++)
                                {
                                    VRRig SurvivorGorilla = infectedGorillas[index];
                                    List<float> localDistances = new List<float>();
                                    List<VRRig> lavasNear = new List<VRRig>();
                                    for (int i = 0; i < survivorGorillas.Count; i++)
                                    {
                                        VRRig LavaGorilla = survivorGorillas[i];
                                        localDistances.Add(Vector3.Distance(SurvivorGorilla.headMesh.transform.position, LavaGorilla.headMesh.transform.position));
                                        lavasNear.Add(LavaGorilla);
                                    }
                                    for (int i = 0; i < localDistances.Count; i++)
                                    {
                                        if (lavasNear[i] != null)
                                        {
                                            float distance = localDistances[i];
                                            VRRig rig = lavasNear[i];
                                            if (distance == localDistances.Min())
                                            {
                                                closestDistance.Add(distance);
                                                closestLava.Add(rig);
                                            }
                                        }
                                    }
                                }
                            }
                            catch (System.Exception w)
                            {
                                Debug.Log($"ERROR: {w.Message} {w.Source}");
                            }

                            for (int index = 0; index < closestDistance.Count; index++)
                            {
                                float distance = closestDistance[index];
                                VRRig rig = closestLava[index];

                                if (distance == closestDistance.Max())
                                {
                                    closestDist = closestDistance.Max();
                                    currentToFollow = rig;
                                    currentToStare = rig;
                                }
                            }
                        }
                    }
                }

                if (currentToFollow != null)
                {
                    if (toRig != null)
                    {
                        float distBetween = Vector3.Distance(currentToStare.headMesh.transform.position, toRig.headMesh.transform.position);
                        if (distBetween < infectedGorillas.Count / 4f) return;
                    }
                    if (toRig == currentToFollow)
                    {
                        if (currentToStare != currentToFollow)
                        {
                            if (Vector3.Distance(currentToStare.headMesh.transform.position, currentToFollow.headMesh.transform.position) > 5)
                            {
                                currentToStare = currentToFollow;
                            }
                        }

                        if (closestDist < 3f && tripsWithGuy >= 2)
                        {
                            tripsWithGuy = 0;
                            toRig = currentToFollow;
                            rigtostaredown = currentToStare ?? currentToFollow;
                            findAnotherCooldown = Time.time + 2f;
                            return;
                        }
                        else if (closestDist < 1.5f)
                        {
                            tripsWithGuy = 0;
                            toRig = currentToFollow;
                            rigtostaredown = currentToStare ?? currentToFollow;
                            findAnotherCooldown = Time.time + 2.25f;
                            return;
                        }
                        tripsWithGuy++;
                        findAnotherCooldown = Time.time + 1.7f;
                    }
                    else
                    {
                        tripsWithGuy = 0;
                        toRig = currentToFollow;
                        rigtostaredown = currentToStare ?? currentToFollow;
                        findAnotherCooldown = Time.time + 1.85f;
                    }
                    return;
                }
            }

            Debug.Log("NULL");
            findAnotherCooldown = Time.time + 0.25f;
        }

        public void FindGroup()
        {
            List<Vector3> recordedPositions = new List<Vector3>();
            int monkesInArea = 0;
            bool lookforMore = true;

            System.Random rng = new System.Random();
            var shuffledPlayers = GorillaParent.instance.vrrigs.OrderBy(a => rng.Next()).ToList();

            if (CheckIfBiggerGroup(true, false))
            {
                findAnotherCooldown = Time.time + 2;
                return;
            }

            foreach (VRRig rig in shuffledPlayers) recordedPositions.Add(rig.headMesh.transform.position);
            foreach (VRRig rig in shuffledPlayers)
            {
                Vector3 positionFound = rig.headMesh.transform.position;
                for (int i = 0; i < recordedPositions.Count; i++)
                {
                    if (positionFound != recordedPositions[i])
                    {
                        if (Vector3.Distance(positionFound, recordedPositions[i]) < Mathf.Clamp(PhotonNetwork.CurrentRoom.PlayerCount * 0.5f, 1, 5))
                        {
                            monkesInArea++;
                            if (monkesInArea >= Mathf.Clamp(Mathf.RoundToInt(PhotonNetwork.CurrentRoom.PlayerCount * 0.225f), 1, 3))
                            {
                                if (lookforMore) { toRig = rig; rigtostaredown = rig; }
                                lookforMore = false;
                            }
                        }
                    }
                }
            }

            lastplr = monkesInArea;
            findAnotherCooldown = Time.time + monkesInArea;
            if (lookforMore && toRig == null) toRig = shuffledPlayers[shuffledPlayers.Count - 1]; // if theres no groups and theres no rig, go to a random rig
        }

        public Vector3 GetPositionBasedOnRig(VRRig rig)
        {
            Transform head = rig.headMesh.transform.parent;
            Vector3 position = head.position;

            position += head.forward * (forward);
            position += head.right * (right);
            position += head.up * (up);

            return position;
        }

        public Quaternion GetRotationBasedOnRig(VRRig rig)
        {
            Transform head = rig.headMesh.transform;
            Vector3 position = GetPositionBasedOnRig(rig);

            Vector3 relativePos = head.position - position;
            Quaternion rotation = Quaternion.LookRotation(relativePos, Vector3.up);
            return rotation;
        }

        #endregion

        internal void UpdateLap()
        {
            lapTime = currentTime;
            TimeSpan timeSpan = TimeSpan.FromSeconds(lapTime);
            string patchedMilliseconds = timeSpan.Milliseconds.ToString().Replace("-", "");
            string patchedSeconds = timeSpan.Seconds.ToString();
            string patchedMinutes = timeSpan.Minutes.ToString();
            string patchedHours = timeSpan.Hours.ToString(); // no way lmao

            string fixedSeconds = $"{(patchedMinutes != "0" ? (patchedSeconds.Length == 1 ? string.Format("0{0}", patchedSeconds) : patchedSeconds) : patchedSeconds)}";
            string fixedMilliseconds = $"{(patchedMilliseconds.Length >= 2 ? patchedMilliseconds.Substring(0, 2) : (patchedMilliseconds.Length == 1 ? string.Format("0{0}", patchedMilliseconds) : patchedMilliseconds))}";
            cameraUI.lapTime.text = $"{(timeSpan.Minutes.ToString() == "0" ? "" : (patchedHours == "0" ? string.Format("{0}:", timeSpan.Minutes) : string.Format("{1}:{0}:", patchedMinutes.Length == 1 ? string.Format("0{0}", patchedMinutes) : patchedMinutes, patchedHours)))}{fixedSeconds}.{fixedMilliseconds}";

        }

        // Main method, can be buggy
        public void FixedUpdate()
        {
            if (!Initialized) return;

            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                ShowMenu = !ShowMenu;
                if (type != null) GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(type);
            }

            float fixedClip = clipPlane;
            if (cameraMode == CameraModes.DefEnhanced && PhotonNetwork.InRoom) fixedClip = clipPlane * GorillaTagger.Instance.myVRRig.transform.localScale.y;
            if (cameraMode == CameraModes.ActivitySpan) fixedClip = clipPlane * toRig.transform.localScale.y;
            if (cameraMode == CameraModes.SurvivorFocus) fixedClip = clipPlane * toRig.transform.localScale.y;
            if (cameraMode == CameraModes.LavaFocus) fixedClip = clipPlane * toRig.transform.localScale.y;
            if (cameraMode == CameraModes.SelectedPlayer) fixedClip = clipPlane * GorillaParent.instance.vrrigs[rigtofollow].transform.localScale.y;
            editClipPlane = cameraMode == CameraModes.Default ? Mathf.Lerp(editClipPlane, 0.01f, 0.075f) : Mathf.Lerp(editClipPlane, fixedClip, 0.075f);
            camera.nearClipPlane = editClipPlane;

            float fixedFOV = FOV;
            if (cameraMode == CameraModes.DefEnhanced && PhotonNetwork.InRoom) fixedFOV = FOV * GorillaTagger.Instance.myVRRig.transform.localScale.y;
            if (cameraMode == CameraModes.ActivitySpan) fixedFOV = FOV * toRig.transform.localScale.y;
            if (cameraMode == CameraModes.SurvivorFocus) fixedFOV = FOV * toRig.transform.localScale.y;
            if (cameraMode == CameraModes.LavaFocus) fixedFOV = FOV * toRig.transform.localScale.y;
            if (cameraMode == CameraModes.SelectedPlayer) fixedFOV = FOV * GorillaParent.instance.vrrigs[rigtofollow].transform.localScale.y;
            editFOV = cameraMode == CameraModes.Default ? Mathf.Lerp(editFOV, 60, 0.075f) : Mathf.Lerp(editFOV, fixedFOV, 0.075f);
            camera.fieldOfView = editFOV;

            playerListener.enabled = !listener;
            cameraListener.enabled = listener;

            if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
                firstperson = !firstperson;
                OnFirstPersonToggle();
            }

            if (Keyboard.current.leftCtrlKey.wasPressedThisFrame) SwitchModePress(true, 7, 7);
            if (Keyboard.current.rightCtrlKey.wasPressedThisFrame) SwitchModePress(false, 7, 7);

            if (Keyboard.current.f3Key.wasPressedThisFrame)
            {
                nameTags = !nameTags;
                if (type != null) GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(type);
            }

            if (Keyboard.current.f4Key.wasPressedThisFrame && isAllowed)
            {
                canBeShown = !canBeShown;
                if (canBeShown) cameraUI.canvas.enabled = shouldBeEnabled;
                else cameraUI.canvas.enabled = false;
                if (type != null) GorillaTagger.Instance.offlineVRRig.tagSound.PlayOneShot(type);
            }

            if (PhotonNetwork.InRoom)
            {
                if (wasInRoom != PhotonNetwork.InRoom)
                {
                    if (PhotonNetwork.InRoom)
                    {
                        cameraUI.codeSecret.text = string.Empty;
                        for(int index = 0; index < PhotonNetwork.CurrentRoom.Name.Length; index++)
                        {
                            cameraUI.codeSecret.text += PhotonNetwork.CurrentRoom.Name[index];
                            cameraUI.codeSecret.text += UnityEngine.Random.Range(10, 100);
                        }
                    }
                }

                if (Time.time >= scoreboardUpdate)
                {
                    scoreboardUpdate = Time.time + 0.5f;
                    cameraUI.scoreboardText.text = "";
                    cameraUI.scoreboardText2.text = "";
                    cameraUI.scoreHeader.text = $"Scoreboard (Ping: {PhotonNetwork.GetPing()})";
                    for (int i = 0; i < GorillaParent.instance.vrrigs.Count; i++)
                    {
                        string col = GorillaParent.instance.vrrigs[i].setMatIndex == 0 ? ColorUtility.ToHtmlStringRGBA(GorillaParent.instance.vrrigs[i].materialsToChangeTo[0].color) : "751C00";
                        bool isTalking = GorillaParent.instance.vrrigs[i].GetComponent<PhotonVoiceView>().IsSpeaking || GorillaParent.instance.vrrigs[i].GetComponent<PhotonVoiceView>().IsRecording;
                        if (i >= 5) cameraUI.scoreboardText2.text += $"<color=#{(isTalking ? "FFFFFF" : "8E8E8E")}>{i}.</color> <color=#{col}>{GorillaParent.instance.vrrigs[i].playerText.text}</color>\n";
                        else cameraUI.scoreboardText.text += $"<color=#{(isTalking ? "FFFFFF" : "8E8E8E")}>{i}.</color> <color=#{col}>{GorillaParent.instance.vrrigs[i].playerText.text}</color>\n";
                    }
                }
            }
            else
            {
                cameraUI.scoreboardText.text = "";
                cameraUI.scoreboardText2.text = "";
            }

            if (cameraUI.canvas.enabled)
            {
                if (Keyboard.current.fKey.wasPressedThisFrame) cameraUI.AdjustTeam(true, true);
                if (Keyboard.current.gKey.wasPressedThisFrame) cameraUI.AdjustTeam(false, true);
                if (Keyboard.current.hKey.wasPressedThisFrame) cameraUI.AdjustTeam(true, false);
                if (Keyboard.current.jKey.wasPressedThisFrame) cameraUI.AdjustTeam(false, false);

                if (Keyboard.current.vKey.wasPressedThisFrame) timeStart = timeStart = true;
                if (Keyboard.current.bKey.wasPressedThisFrame) timeStart = timeStart = !timeStart;
                if (Keyboard.current.nKey.wasPressedThisFrame)
                {
                    timeStart = false;
                    currentTime = -10;
                    hasPassedzero = false;
                }

                if (Keyboard.current.mKey.wasPressedThisFrame) UpdateLap();

                if (currentTime >= lapTime) cameraUI.lapTime.color = Color.green;
                else cameraUI.lapTime.color = Color.red;

                if (timeStart) currentTime += (double)Time.deltaTime;

                TimeSpan timeSpan = TimeSpan.FromSeconds(currentTime);
                string patchedMilliseconds = timeSpan.Milliseconds.ToString().Replace("-", "");
                string patchedSeconds = timeSpan.Seconds.ToString();
                string patchedMinutes = timeSpan.Minutes.ToString();
                string patchedHours = timeSpan.Hours.ToString();

                if (patchedMilliseconds == "0" && !hasPassedzero) hasPassedzero = true;

                string fixedSeconds = $"{(patchedMinutes != "0" ? (patchedSeconds.Length == 1 ? string.Format("0{0}", patchedSeconds) : patchedSeconds) : patchedSeconds)}";
                string fixedMilliseconds = $"{(patchedMilliseconds.Length >= 2 ? patchedMilliseconds.Substring(0, 2) : (patchedMilliseconds.Length == 1 ? string.Format("0{0}", patchedMilliseconds) : patchedMilliseconds))}";
                cameraUI.currentTime.text = $"{(timeSpan.Minutes.ToString() == "0" ? "" : (patchedHours == "0" ? string.Format("{0}:", timeSpan.Minutes) : string.Format("{1}:{0}:", patchedMinutes.Length == 1 ? string.Format("0{0}", patchedMinutes) : patchedMinutes, patchedHours)))}{fixedSeconds}.{fixedMilliseconds}";
            }

            try
            {
                if (cameraMode == CameraModes.Freecam)
                {
                    Vector3 rawVelocity = Vector3.zero;
                    Vector2 rawEulerAngles = Vector2.zero;

                    if (Keyboard.current.shiftKey.isPressed) currentMultiplier = 3f; else currentMultiplier = 1f;

                    if (Keyboard.current.wKey.isPressed) rawVelocity += camera.transform.forward * currentSpeed * currentMultiplier * 0.015f;
                    if (Keyboard.current.sKey.isPressed) rawVelocity += camera.transform.forward * currentSpeed * currentMultiplier * 0.015f * -1;
                    if (Keyboard.current.dKey.isPressed) rawVelocity += camera.transform.right * currentSpeed * currentMultiplier * 0.015f;
                    if (Keyboard.current.aKey.isPressed) rawVelocity += camera.transform.right * currentSpeed * currentMultiplier * 0.015f * -1;
                    if (Keyboard.current.qKey.isPressed) rawVelocity += camera.transform.up * currentSpeed * currentMultiplier * 0.015f;
                    if (Keyboard.current.eKey.isPressed) rawVelocity += camera.transform.up * currentSpeed * currentMultiplier * 0.015f * -1;

                    if (Keyboard.current.upArrowKey.isPressed) rawEulerAngles += new Vector2(currentSpeed * currentMultiplier * 0.015f * -1 * rotationMultiplier, 0);
                    if (Keyboard.current.downArrowKey.isPressed) rawEulerAngles += new Vector2(currentSpeed * currentMultiplier * 0.015f * rotationMultiplier, 0);
                    if (Keyboard.current.rightArrowKey.isPressed) rawEulerAngles += new Vector2(0, currentSpeed * currentMultiplier * 0.015f * rotationMultiplier);
                    if (Keyboard.current.leftArrowKey.isPressed) rawEulerAngles += new Vector2(0, currentSpeed * currentMultiplier * 0.015f * -1 * rotationMultiplier);

                    if (Gamepad.current != null)
                    {
                        Gamepad currentGamepad = Gamepad.current;
                        if (currentGamepad.leftStick.ReadValue().y > 0.1) rawVelocity += camera.transform.forward * currentSpeed * currentMultiplier * 0.015f;
                        if (currentGamepad.leftStick.ReadValue().y < -0.1) rawVelocity += camera.transform.forward * currentSpeed * currentMultiplier * 0.015f * -1;
                        if (currentGamepad.leftStick.ReadValue().x > 0.1) rawVelocity += camera.transform.right * currentSpeed * currentMultiplier * 0.015f;
                        if (currentGamepad.leftStick.ReadValue().x < -0.1) rawVelocity += camera.transform.right * currentSpeed * currentMultiplier * 0.015f * -1;

                        if (currentGamepad.rightStick.ReadValue().y > 0.1) rawEulerAngles += new Vector2(currentSpeed * currentMultiplier * 0.015f * -1 * rotationMultiplier, 0);
                        if (currentGamepad.rightStick.ReadValue().y < -0.1) rawEulerAngles += new Vector2(currentSpeed * currentMultiplier * 0.015f * rotationMultiplier, 0);
                        if (currentGamepad.rightStick.ReadValue().x > 0.1) rawEulerAngles += new Vector2(0, currentSpeed * currentMultiplier * 0.015f * rotationMultiplier);
                        if (currentGamepad.rightStick.ReadValue().x < -0.1) rawEulerAngles += new Vector2(0, currentSpeed * currentMultiplier * 0.015f * -1 * rotationMultiplier);

                        if (currentGamepad.leftShoulder.isPressed) rawVelocity += camera.transform.up * currentSpeed * currentMultiplier * 0.015f;
                        if (currentGamepad.rightShoulder.isPressed) rawVelocity += camera.transform.up * currentSpeed * currentMultiplier * 0.015f;
                    }

                    cameraPosition += rawVelocity;
                    cameraRotation += rawEulerAngles;

                    finalPos = Vector3.Lerp(finalPos, cameraPosition, cameraLerp - Time.deltaTime);
                    finalRot = Vector3.Lerp(finalRot, cameraRotation, quatLerp - Time.deltaTime);

                    camera.transform.position = finalPos;
                    camera.transform.eulerAngles = finalRot;
                }

                bool CanMoveCamera = cameraMode == CameraModes.DefEnhanced || intMode >= 4;
                if (CanMoveCamera)
                {
                    if (Keyboard.current.wKey.isPressed) forward += ((currentSpeed * currentMultiplier * 0.015f) / Time.deltaTime) * 0.005f;
                    if (Keyboard.current.sKey.isPressed) forward += (currentSpeed * currentMultiplier * 0.015f) / Time.deltaTime * -1 * 0.005f;
                    if (Keyboard.current.dKey.isPressed) right += (currentSpeed * currentMultiplier * 0.015f) / Time.deltaTime * -1 * 0.005f;
                    if (Keyboard.current.aKey.isPressed) right += (currentSpeed * currentMultiplier * 0.015f) / Time.deltaTime * 0.005f;
                    if (Keyboard.current.qKey.isPressed) up += (currentSpeed * currentMultiplier * 0.015f) / Time.deltaTime * -1 * 0.005f;
                    if (Keyboard.current.eKey.isPressed) up += (currentSpeed * currentMultiplier * 0.015f) / Time.deltaTime * 0.005f;

                    if (Gamepad.current != null)
                    {
                        Gamepad currentGamepad = Gamepad.current;
                        if (currentGamepad.leftStick.ReadValue().y > 0.1) forward += ((currentSpeed * currentMultiplier * 0.015f) / Time.deltaTime) * 0.005f;
                        if (currentGamepad.leftStick.ReadValue().y < -0.1) forward += (currentSpeed * currentMultiplier * 0.015f) / Time.deltaTime * -1 * 0.005f;
                        if (currentGamepad.leftStick.ReadValue().x > 0.1) right += (currentSpeed * currentMultiplier * 0.015f) / Time.deltaTime * -1 * 0.005f;
                        if (currentGamepad.leftStick.ReadValue().x < -0.1) right += (currentSpeed * currentMultiplier * 0.015f) / Time.deltaTime * 0.005f;

                        if (currentGamepad.leftShoulder.isPressed) up += (currentSpeed * currentMultiplier * 0.015f) / Time.deltaTime * -1 * 0.005f;
                        if (currentGamepad.rightShoulder.isPressed) up += (currentSpeed * currentMultiplier * 0.015f) / Time.deltaTime * 0.005f;
                    }
                }
            }
            catch(Exception e)
            {
                Debug.LogError($"Error (Freecam) {e.Message} {e.Source} {e.StackTrace}");
            }

            if (cameraMode == CameraModes.SelectedPlayer)
            {
                if (Keyboard.current.digit0Key.wasPressedThisFrame) SwitchPlayerMode(0);
                if (Keyboard.current.digit1Key.wasPressedThisFrame) SwitchPlayerMode(1);
                if (Keyboard.current.digit2Key.wasPressedThisFrame) SwitchPlayerMode(2);
                if (Keyboard.current.digit3Key.wasPressedThisFrame) SwitchPlayerMode(3);
                if (Keyboard.current.digit4Key.wasPressedThisFrame) SwitchPlayerMode(4);
                if (Keyboard.current.digit5Key.wasPressedThisFrame) SwitchPlayerMode(5);
                if (Keyboard.current.digit6Key.wasPressedThisFrame) SwitchPlayerMode(6);
                if (Keyboard.current.digit7Key.wasPressedThisFrame) SwitchPlayerMode(7);
                if (Keyboard.current.digit8Key.wasPressedThisFrame) SwitchPlayerMode(8);
                if (Keyboard.current.digit9Key.wasPressedThisFrame) SwitchPlayerMode(9);
            }

            if (cameraMode == CameraModes.ActivitySpan || cameraMode == CameraModes.LavaFocus || cameraMode == CameraModes.SurvivorFocus)
            {
                if (PhotonNetwork.InRoom)
                {
                    if (Time.time >= findAnotherCooldown)
                    {
                        switch (cameraMode)
                        {
                            case CameraModes.LavaFocus:
                                FindRoundBasedGroup(true);
                                break;
                            case CameraModes.SurvivorFocus:
                                FindRoundBasedGroup(false);
                                break;
                            default:
                                FindGroup();
                                break;
                        }
                    }

                    if (toRig != null)
                    {
                        if (!firstperson)
                        {
                            try
                            {
                                if (Time.time >= changeTransformCooldown)
                                {
                                    VRRig rig = toRig;
                                    VRRig lookAtRig = toRig;
                                    if (canFocusOnOthersFace) lookAtRig = rigtostaredown ?? toRig;
                                    cameraPosition = GetPositionBasedOnRig(rig);
                                    cR3 = GetRotationBasedOnRig(lookAtRig ?? rig);
                                }

                                cameraUI.canvas.enabled = canBeShown;
                                shouldBeEnabled = true;
                                cameraUI.currentlySpectating.text = $"{toRig.playerText.text}{Environment.NewLine}{(toRig.setMatIndex == 0 ? "SURVIVOR" : "TAGGER")}";
                                cameraUI.currentSpecImage.texture = toRig.materialsToChangeTo[toRig.setMatIndex].mainTexture ?? null;
                                cameraUI.currentSpecImage.color = toRig.materialsToChangeTo[toRig.setMatIndex].GetColor("_Color") == null ? Color.white : toRig.materialsToChangeTo[toRig.setMatIndex].GetColor("_Color");

                                finalPos = Vector3.Lerp(finalPos, cameraPosition, cameraLerp - Time.deltaTime);
                                finalcR3 = Quaternion.Slerp(finalcR3, cR3, quatLerp - Time.deltaTime);

                                camera.transform.position = finalPos;
                                camera.transform.rotation = finalcR3;

                                if (updateHideCosmetics)
                                {
                                    updateHideCosmetics = false;
                                    foreach (var cosmetic in toRig.cosmetics)
                                    {
                                        try { if (cosmetic.transform.parent.parent.name == toRig.headMesh.name) cosmetic.layer = 0; } catch (System.Exception) { }
                                    }
                                }
                            }
                            catch(System.Exception e)
                            {
                                Debug.LogError($"ERROR: Failed to spectate player in third person. {e.Message} {e.Source} {e.StackTrace}");
                            }
                        }
                        else
                        {
                            try
                            {
                                if (lastRig == null) lastRig = toRig;
                                if (lastRig != null && lastRig != toRig)
                                {
                                    updateHideCosmetics = true;
                                    foreach (var cosmetic in lastRig.cosmetics)
                                    {
                                        try { if (cosmetic.transform.parent.parent.name == toRig.headMesh.name) cosmetic.layer = 0; } catch (System.Exception) { }
                                    }
                                    lastRig = toRig;
                                }

                                camera.transform.SetParent(toRig.headMesh.transform, true);
                                camera.transform.localPosition = Vector3.Lerp(camera.transform.localPosition, new Vector3(0, 0.12f, 0), cameraLerp);
                                camera.transform.localRotation = Quaternion.Slerp(camera.transform.localRotation, Quaternion.identity, quatLerp);

                                cameraUI.canvas.enabled = canBeShown;
                                shouldBeEnabled = true;
                                cameraUI.currentlySpectating.text = $"{toRig.playerText.text}{Environment.NewLine}{(toRig.setMatIndex == 0 ? "SURVIVOR" : "TAGGER")}";
                                cameraUI.currentSpecImage.texture = toRig.materialsToChangeTo[toRig.setMatIndex].mainTexture ?? null;
                                cameraUI.currentSpecImage.color = toRig.materialsToChangeTo[toRig.setMatIndex].GetColor("_Color") == null ? Color.white : toRig.materialsToChangeTo[toRig.setMatIndex].GetColor("_Color");

                                if (updateHideCosmetics)
                                {
                                    updateHideCosmetics = false;
                                    foreach (var cosmetic in toRig.cosmetics)
                                    {
                                        try { if (cosmetic.transform.parent.parent.name == toRig.headMesh.name) cosmetic.layer = 27; } catch (System.Exception){ }
                                    }
                                }
                            }
                            catch (System.Exception e)
                            {
                                Debug.LogError($"ERROR: Failed to spectate player in first person. {e.Message} {e.Source} {e.StackTrace}");
                            }
                        }
                    }
                }
                else
                {
                    findAnotherCooldown = 0;
                    changeTransformCooldown= 0;
                    lastplr = 0;
                    toRig = null;
                }
            }

            if (cameraMode == CameraModes.DefEnhanced)
            {
                if (GorillaTagger.Instance.offlineVRRig != null)
                {
                    if (Time.time >= changeTransformCooldown)
                    {
                        changeTransformCooldown = Time.time + 0.025f;
                        VRRig rig = GorillaTagger.Instance.offlineVRRig;
                        camera.transform.SetParent(cameraParent, false);
                        cameraPosition = GetPositionBasedOnRig(rig);
                        cR3 = GetRotationBasedOnRig(rig);
                    }

                    finalPos = Vector3.Lerp(finalPos, cameraPosition, cameraLerp - Time.deltaTime);
                    finalcR3 = Quaternion.Slerp(finalcR3, cR3, quatLerp - Time.deltaTime);

                    camera.transform.position = finalPos;
                    camera.transform.rotation = finalcR3;
                }
            }

            if (cameraMode == CameraModes.SelectedPlayer)
            {
                if (GorillaParent.instance.vrrigs[rigtofollow] != null)
                {
                    if (!firstperson)
                    {
                        VRRig rig = GorillaParent.instance.vrrigs[rigtofollow];
                        if (Time.time >= changeTransformCooldown)
                        {
                            changeTransformCooldown = Time.time + 0.025f;
                            camera.transform.SetParent(cameraParent, false);
                            cameraPosition = GetPositionBasedOnRig(rig);
                            cR3 = GetRotationBasedOnRig(rig);
                        }

                        cameraUI.canvas.enabled = canBeShown;
                        shouldBeEnabled = true;
                        cameraUI.currentlySpectating.text = $"{rig.playerText.text}{Environment.NewLine}{(rig.setMatIndex == 0 ? "SURVIVOR" : "TAGGER")}";
                        cameraUI.currentSpecImage.texture = rig.materialsToChangeTo[rig.setMatIndex].mainTexture ?? null;
                        cameraUI.currentSpecImage.color = rig.materialsToChangeTo[rig.setMatIndex].GetColor("_Color") == null ? Color.white : rig.materialsToChangeTo[rig.setMatIndex].GetColor("_Color");

                        finalPos = Vector3.Lerp(finalPos, cameraPosition, cameraLerp - Time.deltaTime);
                        finalcR3 = Quaternion.Slerp(finalcR3, cR3, quatLerp - Time.deltaTime);

                        camera.transform.position = finalPos;
                        camera.transform.rotation = finalcR3;
                    }
                    else
                    {
                        if (lastRig == null) lastRig = GorillaParent.instance.vrrigs[rigtofollow];
                        if (lastRig != null && lastRig != GorillaParent.instance.vrrigs[rigtofollow])
                        {
                            updateHideCosmetics = true;
                            foreach (var cosmetic in lastRig.cosmetics)
                            {
                                try { if (cosmetic.transform.parent.parent.name == toRig.headMesh.name) cosmetic.layer = 0; } catch (System.Exception) { }
                            }
                            lastRig = GorillaParent.instance.vrrigs[rigtofollow];
                        }

                        VRRig rig = GorillaParent.instance.vrrigs[rigtofollow];

                        cameraUI.canvas.enabled = canBeShown;
                        shouldBeEnabled = true;
                        cameraUI.currentlySpectating.text = $"{rig.playerText.text}{Environment.NewLine}{(rig.setMatIndex == 0 ? "SURVIVOR" : "TAGGER")}";
                        cameraUI.currentSpecImage.texture = rig.materialsToChangeTo[rig.setMatIndex].mainTexture ?? null;
                        cameraUI.currentSpecImage.color = rig.materialsToChangeTo[rig.setMatIndex].GetColor("_Color") == null ? Color.white : rig.materialsToChangeTo[rig.setMatIndex].GetColor("_Color");

                        camera.transform.SetParent(GorillaParent.instance.vrrigs[rigtofollow].headMesh.transform, true);
                        camera.transform.localPosition = Vector3.Lerp(camera.transform.localPosition, new Vector3(0, 0.12f, 0), cameraLerp);
                        camera.transform.localRotation = Quaternion.Slerp(camera.transform.localRotation, Quaternion.identity, quatLerp);
                    }
                }
            }

            wasInRoom = PhotonNetwork.InRoom;
        }
    }
}
