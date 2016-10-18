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
	public class Undockinator : MonoBehaviour
#endif
	{
		// toolbar
		private bool useBlizzy = false;
		private bool blizzyAvailable = false;
		private IButton blizzyButton;
		private Texture2D appTexture = null;
		private bool setupApp = false;
		private ApplicationLauncherButton appButton = null;

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
		private bool showRename = false;
		private UndockablePort renamePort;
		private string renameName = null;

		// ship survey
		private List<UndockablePort> portList = new List<UndockablePort>();
		Vessel currentVessel;

		struct UndockablePort
		{
			public ModuleDockingNode pm;
			public ModuleDockingNode partnerPM;
			public Part part;
			public Part partner;
			public string portName;
			public string shipName;
			public uint partnerID;
			public uint myID;
			public bool dpai;
			public PartModule dpaiModule;

			public UndockablePort(PartModule newPartModule)
			{
				pm = (ModuleDockingNode)newPartModule;
				part = newPartModule.part;

				partner = null;
				partnerPM = null;

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

				portName = pm.part.partInfo.title;

				// get DPAI module, extract name (if any)
				dpai = false;
				dpaiModule = null;
				string dpaiName = null;
				for (int j = part.Modules.Count - 1; j >= 0; --j)
				{
					if (part.Modules[j].moduleName == "ModuleDockingNodeNamed")
					{
						if (pm.controlTransformName == (string)part.Modules[j].GetType().GetField("controlTransformName").GetValue(part.Modules[j]))
						{
							dpaiModule = part.Modules[j];
							dpaiName = (string)dpaiModule.GetType().GetField("portName").GetValue(dpaiModule);
						}
					}
				}
				if (dpaiName != null && dpaiName != "")
				{
					portName = dpaiName;
					dpai = true;
				}

				// Shrink the part names if they're using the default ones
				switch (portName)
				{
					case "Clamp-O-Tron Docking Port":
						portName = "Clamp-O-Tron"; break;
					case "Clamp-O-Tron Docking Port Jr.":
						portName = "Clamp-O-Tron Jr"; break;
					case "Clamp-O-Tron Docking Port Sr.":
						portName = "Clamp-O-Tron Sr"; break;
					case "Clamp-O-Tron Shielded Docking Port":
						portName = "Clamp-O-Tron Shielded"; break;
				}
			}
		}

		public void scanVessel(Vessel gameEventVessel = null)
		{
#if DEBUG
			UDprint("Scanning vessel for undockable parts");
#endif
			currentVessel = FlightGlobals.ActiveVessel;
			portList.Clear();
			maxShipNameWidth = -1f;
			maxPartNameWidth = -1f;
			scrollPosition = new Vector2(0, 0);
			ModuleDockingNode pm;

			// Gather docking ports
			for (int i = currentVessel.Parts.Count - 1; i >= 0; --i)
			{
				for (int j = currentVessel.parts[i].Modules.Count - 1; j >= 0; --j)
				{
					if (currentVessel.parts[i].Modules[j].moduleName == "ModuleDockingNode")
					{
						pm = (ModuleDockingNode)currentVessel.parts[i].Modules[j];
						if (pm.state.StartsWith("Docked") || pm.state.StartsWith("PreAttached"))
						{
							portList.Add(new UndockablePort(currentVessel.parts[i].Modules[j]));
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

			for (int i = portList.Count - 1; i >= 0; --i)
			{
				g = new GUIContent(portList[i].portName);
				//width = nonbreakingLabelStyle.CalcSize(g).x;
				width = GUI.skin.GetStyle("Button").CalcSize(g).x;
				if (width > maxPartNameWidth) { maxPartNameWidth = width; };

				g = new GUIContent(portList[i].shipName);
				//width = nonbreakingLabelStyle.CalcSize(g).x;
				width = GUI.skin.GetStyle("Button").CalcSize(g).x;
				if (width > maxShipNameWidth) { maxShipNameWidth = width; };
			}
		}

		public void Awake()
		{
			if (ToolbarManager.ToolbarAvailable)
			{
				UDprint("Blizzy's toolbar available");
				blizzyButton = ToolbarManager.Instance.add("Undockinator", "UndockinatorButton");
				blizzyButton.TexturePath = "Undockinator/undockinator";
				blizzyButton.ToolTip = "Undockinator";
				blizzyButton.Visibility = new GameScenesVisibility(GameScenes.FLIGHT);
				blizzyButton.OnClick += (e) =>
				{
					buttonPressed();
				};
				blizzyButton.Visible = useBlizzy;
				blizzyAvailable = true;
			}
			else
			{
				UDprint("Blizzy's toolbar not available, using stock toolbar");
				blizzyButton = null;
				useBlizzy = false;
				blizzyAvailable = false;
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
			if (appButton != null)
			{
				ApplicationLauncher.Instance.RemoveModApplication(appButton);
				appButton = null;
				setupApp = false;
			}

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

			if (blizzyAvailable)
			{
				GUILayout.BeginHorizontal();
				useBlizzy = GUILayout.Toggle(useBlizzy, "Use Blizzy's toolbar");
				GUILayout.EndHorizontal();
				if (useBlizzy)
				{
					appButton.VisibleInScenes = ApplicationLauncher.AppScenes.NEVER;
				}
				else {
					appButton.VisibleInScenes = ApplicationLauncher.AppScenes.FLIGHT;
				}
				blizzyButton.Visible = useBlizzy;
			}
			if (portList.Count == 0)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Label("No docked docking ports found");
				GUILayout.EndHorizontal();
			}
			else if (showRename)
			{
				GUILayout.BeginHorizontal();
				GUILayout.Label("Rename port: ");
				renameName = GUILayout.TextField(renameName);
				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				if (GUILayout.Button("OK"))
				{
					showRename = false;
					renamePort.dpaiModule.GetType().GetField("portName").SetValue(renamePort.dpaiModule, renameName);
					renamePort.portName = renameName;
					scanVessel();
				}
				GUILayout.FlexibleSpace();
				if (GUILayout.Button("Cancel"))
				{
					showRename = false;
				}
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
			}
			else
			{
				scrollPosition = GUILayout.BeginScrollView(scrollPosition,
														   GUILayout.Width(maxPartNameWidth + maxShipNameWidth + 30),
														   GUILayout.Height(Math.Min(400, portList.Count * 40)));
				GUILayout.BeginVertical();
				GUILayout.BeginHorizontal();
				GUILayout.Label("Part name", GUILayout.Width(maxPartNameWidth));
				GUILayout.Label("Undock", GUILayout.Width(maxShipNameWidth));
				GUILayout.EndHorizontal();
				for (int i = portList.Count - 1; i >= 0; --i)
				{
					GUILayout.BeginHorizontal();
					if (portList[i].dpai)
					{
						if (GUILayout.Button(portList[i].portName, GUILayout.Width(maxPartNameWidth)))
						{
							UDprint("Showing rename window");
							showRename = true;
							renameName = portList[i].portName;
							renamePort = portList[i];
						}
					}
					else
					{
						GUILayout.Label(portList[i].portName, GUILayout.Width(maxPartNameWidth));
					}
					if (GUILayout.Button(portList[i].shipName, GUILayout.Width(maxShipNameWidth)))
					{
						if (portList[i].shipName == "PreAttached")
						{
							portList[i].pm.Decouple();
						}
						else {
							portList[i].pm.Undock();
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
						highlightPart = portList[i].part;
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
				UDprint("already set up");
				ApplicationLauncher.Instance.RemoveModApplication(appButton);
				appButton = null;
				setupApp = false;
			}
			else {
				if (ApplicationLauncher.Ready)
				{
					setupApp = true;
					ApplicationLauncher appinstance = ApplicationLauncher.Instance;
					UDprint("Setting up AppLauncher Button");
					appTexture = loadTexture("Undockinator/undockinator-app");
					appButton = appinstance.AddModApplication(buttonPressed, buttonPressed, doNothing, doNothing, doNothing, doNothing, ApplicationLauncher.AppScenes.NEVER, appTexture);
					if (useBlizzy)
					{
						appButton.VisibleInScenes = ApplicationLauncher.AppScenes.NEVER;
					}
					else {
						appButton.VisibleInScenes = ApplicationLauncher.AppScenes.FLIGHT;
					}
				}
				else {
					UDprint("ApplicationLauncher.Ready is false");
				}
				if (blizzyButton != null)
				{
					blizzyButton.Visible = useBlizzy;
				}
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

		public static void UDprint(string taco)
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
