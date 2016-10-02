using System;
using UnityEngine;
using KSP.UI.Screens;
using KSP.IO;
using System.Reflection;

namespace Undockinator
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class Undockinator : MonoBehaviour
	{
		// toolbar
		bool useBlizzy = false;
		//private IButton aspButton;
		private static Texture2D appTexture = null;
		bool setupApp = false;
		private static ApplicationLauncherButton appButton = null;

		// GUI window
		bool visible = false;
		string versionString = null;
		private Rect windowRect = new Rect(0, 0, 1, 1);
		private int windowID = new System.Random().Next(int.MaxValue);

		public void scanVessel(Vessel gameEventVessel = null)
		{
		}

		public void Awake()
		{
			/*
			if (ToolbarManager.ToolbarAvailable)
			{
				UDprint("Blizzy's toolbar available");
				aspButton = ToolbarManager.Instance.add("Undockinator", "aspButton");
				aspButton.TexturePath = "Undockinator/undockinator";
				aspButton.ToolTip = "Undockinator";
				aspButton.Visibility = new GameScenesVisibility(GameScenes.FLIGHT);
				aspButton.OnClick += (e) =>
				{
					buttonPressed();
				};
				aspButton.Visible = useBlizzy;
			}
			else */{
				UDprint("Blizzy's toolbar not available, using stock toolbar");
				//aspButton = null;
				useBlizzy = false;
			}

			//setup app launcher after toolbar in case useBlizzy=true but user removed toolbar
			GameEvents.onGUIApplicationLauncherReady.Add(setupAppButton);

			UDprint("Loading config values");
			PluginConfiguration config = PluginConfiguration.CreateForType<Undockinator>();
			config.load();
			useBlizzy = config.GetValue<bool>("useBlizzy");
			windowRect.x = (float)config.GetValue<int>("windowRectX");
			windowRect.y = (float)config.GetValue<int>("windowRectY");
			if ((windowRect.x == 0) && (windowRect.y == 0))
			{
				windowRect.x = Screen.width * 0.35f;
				windowRect.y = Screen.height * 0.1f;
			}
		}

		public void OnDestroy()
		{
			UDprint("Saving config values");
			PluginConfiguration config = PluginConfiguration.CreateForType<Undockinator>();
			config.SetValue("useBlizzy", useBlizzy);
			config.SetValue("windowRectX", (int)windowRect.x);
			config.SetValue("windowRectY", (int)windowRect.y);
			config.save();
		}

		public void OnGUI()
		{
			if (visible)
			{
				windowRect = GUILayout.Window(windowID, clampToScreen(windowRect), OnWindow, "Undockinator " + versionString);
			}
		}

		public void OnWindow(int windowID)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label("Undockinator");
			GUILayout.EndHorizontal();

			GUI.DragWindow();
		}

		public void buttonPressed()
		{
			visible = !visible;
		}

		public void doNothing()
		{
		}

		public void setupAppButton()
		{
			UDprint("setupAppButton");
			if (setupApp)
			{
				UDprint("app Button already set up");
			}
			else if (ApplicationLauncher.Ready)
			{
				setupApp = true;
				if (appButton == null)
				{

					UDprint("Setting up AppLauncher");
					ApplicationLauncher appinstance = ApplicationLauncher.Instance;
					UDprint("Setting up AppLauncher Button");
					appTexture = loadTexture("Undockinator/undockinator-app");
					appButton = appinstance.AddModApplication(buttonPressed, buttonPressed, doNothing, doNothing, doNothing, doNothing, ApplicationLauncher.AppScenes.FLIGHT, appTexture);
					if (useBlizzy)
					{
						appButton.VisibleInScenes = ApplicationLauncher.AppScenes.NEVER;
					}
					else {
						appButton.VisibleInScenes = ApplicationLauncher.AppScenes.FLIGHT;
					}
				}
				else {
					appButton.onTrue = buttonPressed;
					appButton.onFalse = buttonPressed;
				}
			}
			else {
				UDprint("ApplicationLauncher.Ready is false");
			}
		}

		public void Start()
		{
			scanVessel();
			GameEvents.onVesselChange.Add(scanVessel);
			// onVesselChange - switching between vessels with [ or ] keys

			GameEvents.onVesselStandardModification.Add(scanVessel);
			// onVesselStandardModification collects various vessel events and fires them off with a single one.
			// Specifically - onPartAttach,onPartRemove,onPartCouple,onPartDie,onPartUndock,onVesselWasModified,onVesselPartCountChanged

			versionString = Assembly.GetCallingAssembly().GetName().Version.ToString();

		}

		private static void UDprint(string taco)
		{
			print("[Undockinator] " + taco);
		}

		private static Texture2D loadTexture(string path)
		{
			UDprint("loading texture: " + path);
			return GameDatabase.Instance.GetTexture(path, false);
		}

		private static Rect clampToScreen(Rect rect)
		{
			rect.width = Mathf.Clamp(rect.width, 0, Screen.width);
			rect.height = Mathf.Clamp(rect.height, 0, Screen.height);
			rect.x = Mathf.Clamp(rect.x, 0, Screen.width - rect.width);
			rect.y = Mathf.Clamp(rect.y, 0, Screen.height - rect.height);
			return rect;
		}
	}

}
