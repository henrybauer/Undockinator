using System;
using UnityEngine;
using KSP.UI.Screens;
using KSP.IO;
using System.Reflection;
using System.Collections.Generic;
#if DEBUG
using KramaxReloadExtensions;
#endif
using ToolbarControl_NS;
using ClickThroughFix;

namespace Undockinator
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class RegisterToolbar : MonoBehaviour
    {
        void Start()
        {
            ToolbarControl.RegisterMod(Undockinator.MODID, Undockinator.MODNAME);
        }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
#if DEBUG
	public class Undockinator : ReloadableMonoBehaviour
#else
    public class Undockinator : MonoBehaviour
#endif
    {
        // toolbar
        internal const string MODID = "Undockinator";
        internal const string MODNAME = "Undockinator";
        ToolbarControl toolbarControl;

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
        Part highlightPartnerPart = null;
        public Vector2 scrollPosition;
        private bool showRename = false;
        private UndockablePort renamePort;
        private string renameName = null;
        FlightCamera flightCamera;
        float pivotTranslateSharpness = 0;

        // ship survey
        private List<UndockablePort> portList = new List<UndockablePort>();
        Vessel currentVessel;

        class UndockablePort
        {
            private ModuleDockingNode pm;
            private ModuleDockingNode partnerPM;
            private ModuleGrappleNode gm;
            public Part part;
            public Part partner;
            public string portName;
            public string shipName;
            public bool dpai;
            private PartModule dpaiModule;

            public void rename(string newName)
            {
                dpaiModule.GetType().GetField("portName").SetValue(dpaiModule, newName);
                portName = newName;

            }

            public void undock()
            {
                if (pm == null)
                {
                    gm.Decouple();
                }
                else
                {
                    if (shipName == "PreAttached")
                    {
                        pm.Decouple();
                    }
                    else
                    {
                        pm.Undock();
                    }
                }
            }

            public UndockablePort(PartModule newPartModule)
            {
                part = newPartModule.part;
                portName = part.partInfo.title;

                partner = null;
                partnerPM = null;

                if (newPartModule.moduleName == "ModuleGrappleNode")
                {
                    pm = null;
                    gm = (ModuleGrappleNode)newPartModule;

                    // gm.vesselInfo just points to our own ship, so don't use that
                    shipName = portName;
                }
                else
                {
                    pm = (ModuleDockingNode)newPartModule;
                    gm = null;
                    if (pm.state.StartsWith("PreAttached"))
                    {
                        shipName = "PreAttached";
                    }
                    else
                    {
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

                }


                // get DPAI module, extract name (if any)
                dpai = false;
                dpaiModule = null;
                if (pm != null) // DPAI only supports ModuleDockingNode, not ModuleGrappleNode
                {
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
            resetHighLight();
            currentVessel = FlightGlobals.ActiveVessel;
            portList.Clear();
            maxShipNameWidth = -1f;
            maxPartNameWidth = -1f;
            scrollPosition = new Vector2(0, 0);
            ModuleDockingNode pm;
            ModuleGrappleNode gm;

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
                            Boolean uniquePort = true;
                            for (int k = portList.Count - 1; k >= 0; --k)
                            {
                                if (portList[k].partner == currentVessel.parts[i])
                                {
                                    uniquePort = false;
                                }
                            }

                            if (uniquePort)
                            {
                                portList.Add(new UndockablePort(currentVessel.parts[i].Modules[j]));
                            }
                        }
                    }
                    if (currentVessel.parts[i].Modules[j].moduleName == "ModuleGrappleNode")
                    {
                        gm = (ModuleGrappleNode)currentVessel.parts[i].Modules[j];
                        print("Grapple: " + gm.state.ToString());
                        if (gm.state.StartsWith("Grappled"))
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

#if DEBUG
			// don't load configs because KramaxReload screws up PluginConfiguration
#else
            UDprint("Loading config values");
            PluginConfiguration config = PluginConfiguration.CreateForType<Undockinator>();
            config.load();
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
            toolbarControl.OnDestroy();
            Destroy(toolbarControl);
#if DEBUG
			// don't save configs because KramaxReload screws up PluginConfiguration
#else
            UDprint("Saving config values");
            PluginConfiguration config = PluginConfiguration.CreateForType<Undockinator>();
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
                windowRect = ClickThruBlocker.GUILayoutWindow(windowID, clampToScreen(windowRect), OnWindow, "The Undockinator " + versionString, GUILayout.MinWidth(300));
            }
        }

        public void resetHighLight()
        {
            if (highlightPart != null && highlightPart.HighlightActive)
            {
                highlightPart.SetHighlightDefault();
                highlightPart = null;
            }
            if (highlightPartnerPart != null && highlightPartnerPart.HighlightActive)
            {
                highlightPartnerPart.SetHighlightDefault();
                highlightPartnerPart = null;
            }
        }

        public void resetCamera()
        {
            flightCamera.pivotTranslateSharpness = pivotTranslateSharpness;
            flightCamera.transform.parent.position = FlightGlobals.ActiveVessel.GetWorldPos3D();
        }

        public void OnWindow(int windowID)
        {

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
                    renamePort.rename(renameName);
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
                                                           GUILayout.Height(Math.Min(400, (portList.Count + 1) * 40)));
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
                        resetCamera();
                        resetHighLight();
                        portList[i].undock();
                        visible = false;
                    }
                    GUILayout.EndHorizontal();

                    if (Event.current.type == EventType.Repaint && GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
                    {
                        if (highlightPart != null && highlightPart.HighlightActive)
                        {
                            highlightPart.SetHighlightDefault();
                        }
                        if (highlightPartnerPart != null && highlightPartnerPart.HighlightActive)
                        {
                            highlightPartnerPart.SetHighlightDefault();
                        }
                        highlightPart = portList[i].part;

                        flightCamera.pivotTranslateSharpness = 0;
                        flightCamera.transform.parent.position = portList[i].part.transform.position;

                        highlightPartnerPart = portList[i].partner;

                        if (!highlightPart.HighlightActive)
                        {
                            highlightPart.SetHighlight(true, false);
                            highlightPart.highlightType = Part.HighlightType.AlwaysOn;
                            highlightPart.SetHighlightColor(Color.yellow);
                            highlightPart.highlighter.OccluderOn();
                        }
                        if (highlightPartnerPart != null)
                        {
                            if (!highlightPartnerPart.HighlightActive)
                            {
                                highlightPartnerPart.SetHighlight(true, false);
                                highlightPartnerPart.highlightType = Part.HighlightType.AlwaysOn;
                                highlightPartnerPart.SetHighlightColor(Color.yellow);
                                highlightPartnerPart.highlighter.OccluderOn();
                            }
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
            if (visible)
            {
                resetCamera();
                resetHighLight();
            }
            else
            {
                scanVessel();
            }
            visible = !visible;
        }

        public void doNothing()
        {
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

            flightCamera = FlightCamera.fetch;
            pivotTranslateSharpness = flightCamera.pivotTranslateSharpness;

            toolbarControl = gameObject.AddComponent<ToolbarControl>();
            toolbarControl.AddToAllToolbars(buttonPressed, buttonPressed,
                  ApplicationLauncher.AppScenes.FLIGHT,
                  MODID,
                  "UndockinatorButton",
                  "Undockinator/undockinator-app",
                  "Undockinator/undockinator",
                  MODNAME
                  );
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