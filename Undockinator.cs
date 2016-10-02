using System;
using UnityEngine;
using KSP.UI.Screens;
using KSP.IO;
using System.Reflection;
using System.Collections.Generic;
#if DEBUG
using KramaxReloadExtensions;
#endif

namespace Undockinator
{
	[KSPAddon(KSPAddon.Startup.Flight, false)]
#if DEBUG
	public class Undockinator : ReloadableMonoBehaviour
#else
	public class Undockinator: MonoBehaviour
#endif
	{
		// toolbar
		bool useBlizzy = false;
		//private IButton aspButton;
		private static Texture2D appTexture = null;
		bool setupApp = false;
		private static ApplicationLauncherButton appButton = null;

		// GUI
#if DEBUG
		bool visible = true;
#else
		bool visible = false;
#endif
		string versionString = null;
		private Rect windowRect = new Rect(200, 200, 1, 1);
		private int windowID = new System.Random().Next(int.MaxValue);
		GUIStyle nonbreakingLabelStyle;
		static float maxPartNameWidth = -1f;
		static float maxShipNameWidth = -1f;
		Part highlightPart = null;
		public Vector2 scrollPosition;

		// ship survey
		private List<UndockablePart> partList = new List<UndockablePart>();
		Vessel currentVessel;

		struct UndockablePart
		{
			public ModuleDockingNode pm;
			public ModuleDockingNode partnerPM;
			public Part part;
			public Part partner;
			public string partName;
			public string shipName;
			public uint partnerID;
			public uint myID;
			public bool dpai;

			public UndockablePart(PartModule newPartModule)
			{
				pm = (ModuleDockingNode)newPartModule;
				part = newPartModule.part;

				partner = null;
				partnerPM = null;

				dpai = false;

				if (pm.state.StartsWith("PreAttached"))
				{
					shipName = "PreAttached";
				}
				else {
					DockedVesselInfo dv = pm.vesselInfo;
					if (dv != null)
					{
						shipName = dv.name + " (" + dv.vesselType.ToString() + ")";

						if (pm.otherNode != null)
						{
							partnerPM = pm.otherNode;
							partner = partnerPM.part;
							shipName = shipName + " - " + partnerPM.vesselInfo.name + " (" + partnerPM.vesselInfo.vesselType.ToString() + ")";
						}
					}
					else
					{
						shipName = "???";
					}
				}

				myID = part.flightID;
				partnerID = pm.dockedPartUId;

				partName = pm.part.partInfo.title;

				switch (partName)
				{
					case "Clamp-O-Tron Docking Port":
						partName = "Clamp-O-Tron"; break;
					case "Clamp-O-Tron Docking Port Jr.":
						partName = "Clamp-O-Tron Jr"; break;
					case "Clamp-O-Tron Docking Port Sr.":
						partName = "Clamp-O-Tron Sr"; break;
					case "Clamp-O-Tron Shielded Docking Port":
						partName = "Clamp-O-Tron Shielded"; break;
				}

				/*string dpainame = null;
				for (int j = part.Modules.Count - 1; j >= 0; --j)
				{
					if (part.Modules[j].moduleName == "ModuleDockingNodeNamed")
					{
						dpai = true;
						dpainame = (string)part.Modules[j].GetType().GetField("portName").GetValue(part.Modules[j]);
						if (dpainame != null && dpainame != "")
						{
							partName = dpainame;
						}
					}
				}*/

			}

		}

		public void scanVessel(Vessel gameEventVessel = null)
		{
			UDprint("Scanning vessel for undockable parts");
			currentVessel = FlightGlobals.ActiveVessel;
			partList.Clear();
			maxShipNameWidth = -1f;
			maxPartNameWidth = -1f;
			scrollPosition = new Vector2(0, 0);
			ModuleDockingNode pm;

			for (int i = currentVessel.Parts.Count - 1; i >= 0; --i)
			{
				for (int j = currentVessel.parts[i].Modules.Count - 1; j >= 0; --j)
				{
					if (currentVessel.parts[i].Modules[j].moduleName == "ModuleDockingNode")
					{
						pm = (ModuleDockingNode)currentVessel.parts[i].Modules[j];
						if (pm.state.StartsWith("Docked") || pm.state.StartsWith("PreAttached"))
						{
							//if (partList.FindAll(s => s.myID==pm.part.flightID).Count==0)
							//{
							partList.Add(new UndockablePart(currentVessel.parts[i].Modules[j]));
							//}
						}
					}
				}
			}
		}

		public void getMaxWidths()
		{
			GUIContent g;
			float width;

			maxPartNameWidth = 0f;
			maxShipNameWidth = 0f;

			for (int i = partList.Count - 1; i >= 0; --i)
			{
				g = new GUIContent(partList[i].partName);
				//width = nonbreakingLabelStyle.CalcSize(g).x;
				width = GUI.skin.GetStyle("Button").CalcSize(g).x;
				if (width > maxPartNameWidth) { maxPartNameWidth = width; };

				g = new GUIContent(partList[i].shipName);
				//width = nonbreakingLabelStyle.CalcSize(g).x;
				width = GUI.skin.GetStyle("Button").CalcSize(g).x;
				if (width > maxShipNameWidth) { maxShipNameWidth = width; };
			}
			//maxShipNameWidth = maxShipNameWidth * 1.1f;
			//maxPartNameWidth = maxPartNameWidth * 1.1f;

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
			else */
			{
				UDprint("Blizzy's toolbar not available, using stock toolbar");
				//aspButton = null;
				useBlizzy = false;
			}

			//setup app launcher after toolbar in case useBlizzy=true but user removed toolbar
			GameEvents.onGUIApplicationLauncherReady.Add(setupAppButton);

#if DEBUG
			// don't load configs because KramaxReload screws up PluginConfiguration
#else
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
#endif
		}

		public void OnDestroy()
		{
#if DEBUG
			// don't save configs because KramaxReload screws up PluginConfiguration
#else
			UDprint("Saving config values");
			PluginConfiguration config = PluginConfiguration.CreateForType<Undockinator>();
			config.SetValue("useBlizzy", useBlizzy);
			config.SetValue("windowRectX", (int)windowRect.x);
			config.SetValue("windowRectY", (int)windowRect.y);
			config.save();
#endif
		}

		public void OnGUI()
		{
			/*			if (highlightPart != null && highlightPart.HighlightActive)
						{
							highlightPart.SetHighlightDefault();
						}*/

			if (visible)
			{
				if (maxShipNameWidth == -1)
				{
					getMaxWidths();
				}
				windowRect = GUILayout.Window(windowID, clampToScreen(windowRect), OnWindow, "The Undockinator " + versionString, GUILayout.MinWidth(300));
			}

			if (Event.current.type == EventType.Repaint && !windowRect.Contains(Event.current.mousePosition))
			{
				if (highlightPart != null && highlightPart.HighlightActive)
				{
					highlightPart.SetHighlightDefault();
					highlightPart = null;
				}
			}
		}

		public void OnWindow(int windowID)
		{
			//GUILayout.BeginHorizontal();
			//GUILayout.Label(maxPartNameWidth.ToString() + "/"+maxShipNameWidth.ToString());
			//GUILayout.EndHorizontal();

			if (partList.Count == 0)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Label("No docked docking ports found");
				GUILayout.EndHorizontal();
			}
			else
			{
				scrollPosition = GUILayout.BeginScrollView(scrollPosition,
														   GUILayout.Width(maxPartNameWidth + maxShipNameWidth + 30),
														   GUILayout.Height(Math.Min(400, partList.Count * 40)));
				GUILayout.BeginVertical();
				GUILayout.BeginHorizontal();
				GUILayout.Label("Part name", GUILayout.Width(maxPartNameWidth));
				GUILayout.Label("Undock", GUILayout.Width(maxShipNameWidth));
				GUILayout.EndHorizontal();
				for (int i = partList.Count - 1; i >= 0; --i)
				{
					GUILayout.BeginHorizontal();
					/*if (partList[i].dpai)
					{
						if (GUILayout.Button(partList[i].partName, GUILayout.Width(maxPartNameWidth)))
						{

						}
					}
					else*/ {
						GUILayout.Label(partList[i].partName, GUILayout.Width(maxPartNameWidth));
					}
					if (GUILayout.Button(partList[i].shipName, GUILayout.Width(maxShipNameWidth)))
					{
						if (partList[i].shipName == "PreAttached")
						{
							partList[i].pm.Decouple();
						}
						else {
							partList[i].pm.Undock();
						}
						visible = false;
					}
					GUILayout.EndHorizontal();

					if (Event.current.type == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
					{
						if (highlightPart != null && highlightPart.HighlightActive)
						{
							highlightPart.SetHighlightDefault();
						}
						highlightPart = partList[i].part;
						if (!highlightPart.HighlightActive)
						{
							highlightPart.SetHighlight(true, false);
							highlightPart.highlightType = Part.HighlightType.AlwaysOn;
							highlightPart.SetHighlightColor(Color.yellow);
							highlightPart.highlighter.OccluderOn();
						}

					}
					//if ((i % 2 == 0) && (i > 0))
					//{
					//	GUILayout.Space(10);
					//}
				}
				GUILayout.EndVertical();
				GUILayout.EndScrollView();
			}

			GUI.DragWindow();
		}

		public void buttonPressed()
		{
			if (!visible)
			{
				scanVessel();
			}
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
			GameEvents.onVesselChange.Add(scanVessel);
			// onVesselChange - switching between vessels with [ or ] keys

			GameEvents.onVesselStandardModification.Add(scanVessel);
			// onVesselStandardModification collects various vessel events and fires them off with a single one.
			// Specifically - onPartAttach,onPartRemove,onPartCouple,onPartDie,onPartUndock,onVesselWasModified,onVesselPartCountChanged

			versionString = Assembly.GetCallingAssembly().GetName().Version.ToString();

			nonbreakingLabelStyle = new GUIStyle();
			nonbreakingLabelStyle.wordWrap = false;
			nonbreakingLabelStyle.normal.textColor = Color.white;
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
