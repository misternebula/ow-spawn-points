using OWML.Common;
using OWML.ModHelper;
using UnityEngine;
using OWML.ModHelper.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using OWML.Common.Menus;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Net;

namespace OWSpawnPoints
{
    public class OWSpawnPoints : ModBehaviour
    {
        private FluidDetector _fluidDetector;
        SpawnPoint _prevSpawnPoint;
        AstroObject _prevAstroObject;
        SaveFile _saveFile;
        bool _isSolarSystemLoaded;
        const string SAVE_FILE = "savefile.json";
        Socket _socket;

        private void SocketState_Completed(object sender, SocketAsyncEventArgs e)
        {
            if (e.SocketError != SocketError.Success)
            {
                throw new SocketException((int)e.SocketError);
            }

            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Connect:
                    ProcessConnect(e);
                    break;
                case SocketAsyncOperation.Receive:
                    ProcessReceive(e);
                    break;
                case SocketAsyncOperation.Send:
                    ProcessSend(e);
                    break;
                default:
                    throw new Exception("Invalid operation completed.");
            }
        }

        private void ProcessConnect(SocketAsyncEventArgs e)
        {
            byte[] buffer = Encoding.UTF8.GetBytes("Hello World");
            e.SetBuffer(buffer, 0, buffer.Length);
            bool willRaiseEvent = _socket.SendAsync(e);
            if (!willRaiseEvent)
            {
                ProcessSend(e);
            }
        }

        // Called when a ReceiveAsync operation completes
        private void ProcessReceive(SocketAsyncEventArgs e)
        {
            string message = Encoding.UTF8.GetString(e.Buffer, 0, e.Buffer.Length);
        }

        // Called when a SendAsync operation completes
        private void ProcessSend(SocketAsyncEventArgs e)
        {
            bool willRaiseEvent = _socket.ReceiveAsync(e);
            if (!willRaiseEvent)
            {
                ProcessReceive(e);
            }
        }

        private void Start()
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress ipAdd = IPAddress.Parse("127.0.0.1");
            IPEndPoint remoteEP = new IPEndPoint(ipAdd, 1234);
            _socket.Connect(remoteEP);

            byte[] byData = Encoding.ASCII.GetBytes("Connecting");
            _socket.Send(byData);

            ModHelper.Events.Subscribe<Flashlight>(Events.AfterStart);
            ModHelper.Events.OnEvent += OnEvent;

            _saveFile = ModHelper.Storage.Load<SaveFile>(SAVE_FILE);

            LoadManager.OnCompleteSceneLoad += OnSceneLoaded;
            Console.OpenStandardInput();
            Console.OpenStandardOutput();
            Console.WriteLine("hello " + count++);
            Console.Out.Flush();
            InvokeRepeating("Print", 1, 1);
        }

        int count = 0;
        void Print()
        {
            ModHelper.Console.WriteLine("hello mod " + count);
            Debug.Log("DEBUG LOG " + count);
            Console.Out.WriteLine("outerino");
            Console.WriteLine("hello " + count++);
            Console.Out.Flush();
            Console.Out.Close();
            byte[] byData = Encoding.ASCII.GetBytes("Hello mother fucker");
            _socket.Send(byData);
        }

        void OnSceneLoaded(OWScene originalScene, OWScene scene)
        {
            if (scene == OWScene.SolarSystem || scene == OWScene.EyeOfTheUniverse)
            {
                _isSolarSystemLoaded = true;
                SpawnAtInitialPoint();
            }
        }

        private void OnEvent(MonoBehaviour behaviour, Events ev)
        {
            if (behaviour.GetType() == typeof(Flashlight) && ev == Events.AfterStart)
            {
                Init();
                SpawnAtInitialPoint();
            }
        }

        private void Init()
        {
            _fluidDetector = Locator.GetPlayerCamera().GetComponentInChildren<FluidDetector>();

            var mainButton = ModHelper.Menus.PauseMenu.OptionsButton.Duplicate("Teleport to...");

            var shipSpawnMenu = ModHelper.Menus.PauseMenu.Copy("Ship Spawn Points");
            shipSpawnMenu.Buttons.ForEach(button => button.Hide());
            shipSpawnMenu.Menu.transform.localScale *= 0.5f;
            shipSpawnMenu.Menu.transform.localPosition *= 0.5f;

            var playerSpawnMenu = ModHelper.Menus.PauseMenu.Copy("Player Spawn Points");
            playerSpawnMenu.Buttons.ForEach(button => button.Hide());
            playerSpawnMenu.Menu.transform.localScale *= 0.5f;
            playerSpawnMenu.Menu.transform.localPosition *= 0.5f;

            var sourceButton = shipSpawnMenu.Buttons[0];

            mainButton.OnClick += () => (PlayerState.IsInsideShip() ? shipSpawnMenu : playerSpawnMenu).Open();

            var astroObjects = FindObjectsOfType<AstroObject>().ToList();
            var astroSpawnPoints = new Dictionary<AstroObject, SpawnPoint[]>();

            foreach (var astroObject in astroObjects)
            {
                astroSpawnPoints[astroObject] = astroObject.GetComponentsInChildren<SpawnPoint>(true);
            }

            astroObjects.Sort((a, b) => astroSpawnPoints[a].Length.CompareTo(astroSpawnPoints[b].Length));

            void CloseMenu()
            {
                shipSpawnMenu.Close();
                playerSpawnMenu.Close();
                ModHelper.Menus.PauseMenu.Close();
            }

            void CreateSpawnPointButton(SpawnPoint spawnPoint, AstroObject astroObject, IModPopupMenu spawnMenu, string name)
            {
                var subButton = spawnMenu.AddButton(sourceButton.Copy(name));
                subButton.OnClick += () =>
                {
                    spawnMenu.Close();
                    CloseMenu();
                    SpawnAt(spawnPoint);
                    _prevSpawnPoint = spawnPoint;
                    _prevAstroObject = astroObject;
                };
                subButton.Show();
            }

            void CreateSpawnPointList(List<SpawnPoint> spawnPoints, AstroObject astroObject, IModPopupMenu spawnMenu)
            {
                var subMenu = ModHelper.Menus.PauseMenu.Copy("Spawn Points");
                subMenu.Buttons.ForEach(button => button.Hide());
                subMenu.Menu.transform.localScale *= 0.5f;
                subMenu.Menu.transform.localPosition *= 0.5f;

                var subButton = spawnMenu.AddButton(sourceButton.Copy($"{GetAstroObjectName(astroObject)}..."));
                subButton.OnClick += () => subMenu.Open();
                subButton.Show();

                for (var i = 0; i < spawnPoints.Count; i++)
                {
                    var point = spawnPoints[i];
                    CreateSpawnPointButton(point, astroObject, subMenu, point.name);
                }
            }

            foreach (var astroObject in astroObjects)
            {
                var allSpawnPoints = astroSpawnPoints[astroObject];
                if (allSpawnPoints.Length == 0)
                {
                    continue;
                }

                var shipSpawnPoints = allSpawnPoints.Where(point => point.IsShipSpawn()).ToList();
                var playerSpawnPoints = allSpawnPoints.Where(point => !point.IsShipSpawn()).ToList();

                var astroName = GetAstroObjectName(astroObject);

                if (shipSpawnPoints.Count > 1)
                {
                    CreateSpawnPointList(shipSpawnPoints, astroObject, shipSpawnMenu);
                }
                else if (shipSpawnPoints.Count == 1)
                {
                    CreateSpawnPointButton(shipSpawnPoints[0], astroObject, shipSpawnMenu, astroName);
                }

                if (playerSpawnPoints.Count > 1)
                {
                    CreateSpawnPointList(playerSpawnPoints, astroObject, playerSpawnMenu);
                }
                else if (playerSpawnPoints.Count == 1)
                {
                    CreateSpawnPointButton(playerSpawnPoints[0], astroObject, playerSpawnMenu, astroName);
                }
            }

            var clearSaveButton = sourceButton.Copy("RESET INITIAL SPAWN POINT");
            clearSaveButton.OnClick += () =>
            {
                ResetInitialSpawnPoint();
                CloseMenu();
            };
            clearSaveButton.Show();
            shipSpawnMenu.AddButton(clearSaveButton);
            playerSpawnMenu.AddButton(clearSaveButton);

            var saveButton = sourceButton.Copy("SAVE LAST USED AS INITIAL");
            saveButton.OnClick += () =>
            {
                SetInitialSpawnPoint();
                CloseMenu();
            };
            saveButton.Show();
            shipSpawnMenu.AddButton(saveButton);
            playerSpawnMenu.AddButton(saveButton);
        }

        string GetAstroObjectName(AstroObject astroObject)
        {
            var astroNameEnum = astroObject.GetAstroObjectName();
            var astroName = astroNameEnum.ToString();

            if (astroNameEnum == AstroObject.Name.CustomString)
            {
                return astroObject.GetCustomName();
            }
            else if (astroNameEnum == AstroObject.Name.None || astroName == null || astroName == "")
            {
                return astroObject.name;
            }

            return astroName;
        }

        private void SetInitialSpawnPoint()
        {
            _saveFile.initialAstroObject = _prevAstroObject.gameObject.name;
            _saveFile.initialSpawnPoint = _prevSpawnPoint.gameObject.name;
            ModHelper.Storage.Save(_saveFile, SAVE_FILE);
        }

        private void ResetInitialSpawnPoint()
        {
            _saveFile.initialAstroObject = "";
            _saveFile.initialSpawnPoint = "";
            ModHelper.Storage.Save(_saveFile, SAVE_FILE);
        }

        void SpawnAtInitialPoint()
        {
            var astroName = _saveFile.initialAstroObject;
            var spawnPointName = _saveFile.initialSpawnPoint;
            if (astroName == "" || spawnPointName == "") return;

            var astroObjectGO = GameObject.Find(astroName);
            if (astroObjectGO == null) return;

            var astroObject = astroObjectGO.GetComponent<AstroObject>();
            if (astroObject == null) return;

            var spawnPoints = astroObject.GetComponentsInChildren<SpawnPoint>();
            foreach (var point in spawnPoints)
            {
                if (point.gameObject.name == spawnPointName)
                {
                    FindObjectOfType<PlayerSpawner>().SetInitialSpawnPoint(point);
                    return;
                }
            }
        }

        private void SpawnAt(SpawnPoint point)
        {
            var body = PlayerState.IsInsideShip() ? Locator.GetShipBody() : Locator.GetPlayerBody();

            body.WarpToPositionRotation(point.transform.position, point.transform.rotation);
            body.SetVelocity(point.GetPointVelocity());
            point.AddObjectToTriggerVolumes(Locator.GetPlayerDetector().gameObject);
            point.AddObjectToTriggerVolumes(_fluidDetector.gameObject);
            point.OnSpawnPlayer();
            OWTime.Unpause(OWTime.PauseType.Menu);
        }

        private void InstantWakeUp()
        {
            _isSolarSystemLoaded = false;
            // Skip wake up animation.
            var cameraEffectController = FindObjectOfType<PlayerCameraEffectController>();
            cameraEffectController.OpenEyes(0, true);
            cameraEffectController.SetValue("_wakeLength", 0f);
            cameraEffectController.SetValue("_waitForWakeInput", false);

            // Skip wake up prompt.
            LateInitializerManager.pauseOnInitialization = false;
            Locator.GetPauseCommandListener().RemovePauseCommandLock();
            Locator.GetPromptManager().RemoveScreenPrompt(cameraEffectController.GetValue<ScreenPrompt>("_wakePrompt"));
            OWTime.Unpause(OWTime.PauseType.Sleeping);
            cameraEffectController.Invoke("WakeUp");

            // Enable all inputs immedeately.
            OWInput.ChangeInputMode(InputMode.Character);
            typeof(OWInput).SetValue("_inputFadeFraction", 0f);
            GlobalMessenger.FireEvent("TakeFirstFlashbackSnapshot");

            Locator.GetPlayerSuit().SuitUp();
        }

        void LateUpdate()
        {
            if (_isSolarSystemLoaded && _saveFile.initialAstroObject != "" && _saveFile.initialSpawnPoint != "")
            {
                InstantWakeUp();
            }
        }
    }
}
