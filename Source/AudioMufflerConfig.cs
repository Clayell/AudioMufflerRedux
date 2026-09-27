using ClickThroughFix;
using KSP.UI.Screens;
using ToolbarControl_NS;
using UnityEngine;

namespace AudioMuffler
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)] // startup on main menu according to https://github.com/linuxgurugamer/ToolbarControl/wiki/Registration
    public class RegisterToolbar : MonoBehaviour
    {
        void Start()
        {
            ToolbarControl.RegisterMod("AudioMufflerRedux", "AudioMufflerRedux");
        }
    }

    [KSPScenario(ScenarioCreationOptions.AddToAllGames, GameScenes.FLIGHT, GameScenes.SPACECENTER, GameScenes.TRACKSTATION)]
    public class AudioMufflerConfig : ScenarioModule
    {
        internal static AudioMufflerConfig Instance { get; private set; }

        [KSPField(isPersistant = true)] public bool enableMuffler = true;
        [KSPField(isPersistant = true)] public bool helmetOutsideIVA = true;
        [KSPField(isPersistant = true)] public bool helmetOutsideEVA = true;
        [KSPField(isPersistant = true)] public bool helmetForUnmanned = true;
        [KSPField(isPersistant = true)] public bool helmetInMapView = false;
        [KSPField(isPersistant = true)] public bool vesselInMapView = true;
        [KSPField(isPersistant = true)] public bool outsideInMapView = false;
        [KSPField(isPersistant = true)] public bool roundValues = false;
        [KSPField(isPersistant = true)] public bool debug = false;

        [KSPField(isPersistant = true)] public int minCacheUpdateInterval = 300;
        [KSPField(isPersistant = true)] public float wallCutoff = 400;
        [KSPField(isPersistant = true)] public float minimalCutoff = 0;

        [KSPField(isPersistant = true)] bool showMainWindow = false;
        bool isKSPGUIActive = true; // for some reason, this initially only turns to true when you turn off and on the KSP GUI
        bool isLoading = false;
        bool isBadUI = false;

        Rect mainRect = new Rect(200, 200, -1, -1);
        [KSPField(isPersistant = true)] Vector2 mainRectPos = new Vector2(200, 200);

        ToolbarControl toolbarControl = null;

        public override void OnAwake()
        {
            Instance = this;

            GameEvents.onShowUI.Add(KSPShowGUI);
            GameEvents.onHideUI.Add(KSPHideGUI);

            GameEvents.onGameSceneLoadRequested.Add(OnSceneChange);
            GameEvents.onLevelWasLoadedGUIReady.Add(OnSceneLoaded);

            GameEvents.onGUIAstronautComplexSpawn.Add(HideBadUI);
            GameEvents.onGUIRnDComplexSpawn.Add(HideBadUI);
            GameEvents.onGUIAdministrationFacilitySpawn.Add(HideBadUI);
            GameEvents.onGUIAstronautComplexDespawn.Add(ShowBadUI);
            GameEvents.onGUIRnDComplexDespawn.Add(ShowBadUI);
            GameEvents.onGUIAdministrationFacilityDespawn.Add(ShowBadUI);
        }

        void Start()
        {
            InitToolbar();

            Tooltip.RecreateInstance();
        }

        private void InitToolbar()
        {
            if (toolbarControl == null)
            {
                toolbarControl = gameObject.AddComponent<ToolbarControl>();
                toolbarControl.AddToAllToolbars(ToggleWindow, ToggleWindow,
                    ApplicationLauncher.AppScenes.ALWAYS,
                    "AudioMufflerRedux",
                    "AudioMufflerRedux_Button",
                    "AudioMufflerRedux/PluginData/ToolbarIcons/AudioMufflerRedux-64",
                    "AudioMufflerRedux/PluginData/ToolbarIcons/AudioMufflerRedux-24",
                    "AudioMufflerRedux"
                );
            }
        }

        private void ToggleWindow() => showMainWindow = !showMainWindow;

        private void KSPShowGUI() => isKSPGUIActive = true;

        private void KSPHideGUI() => isKSPGUIActive = false;

        private void HideBadUI() => isBadUI = true;

        private void ShowBadUI() => isBadUI = false;

        private void OnSceneLoaded(GameScenes s)
        {
            if (s != GameScenes.MAINMENU)
            {
                isLoading = false;
            }
        }

        private void OnSceneChange(GameScenes s)
        {
            isLoading = true;
        }

        public override void OnLoad(ConfigNode node)
        {
            void MakeRect(ref Rect rect, Vector2 pos)
            {
                rect = new Rect(pos.x, pos.y, rect.width, rect.height);
            }

            MakeRect(ref mainRect, mainRectPos);
        }

        void OnDestroy()
        {
            Destroy(toolbarControl);
            toolbarControl = null;

            GameEvents.onShowUI.Remove(KSPShowGUI);
            GameEvents.onHideUI.Remove(KSPHideGUI);

            GameEvents.onGameSceneLoadRequested.Remove(OnSceneChange);
            GameEvents.onLevelWasLoadedGUIReady.Remove(OnSceneLoaded);

            GameEvents.onGUIAstronautComplexSpawn.Remove(HideBadUI);
            GameEvents.onGUIRnDComplexSpawn.Remove(HideBadUI);
            GameEvents.onGUIAdministrationFacilitySpawn.Remove(HideBadUI);
            GameEvents.onGUIAstronautComplexDespawn.Remove(ShowBadUI);
            GameEvents.onGUIRnDComplexDespawn.Remove(ShowBadUI);
            GameEvents.onGUIAdministrationFacilityDespawn.Remove(ShowBadUI);

            if (Instance == this)
            {
                Instance = null;
            }
        }

        void OnGUI()
        {
            void ClampToScreen(ref Rect rect)
            {
                float left = Mathf.Clamp(rect.x, 0, Screen.width - rect.width);
                float top = Mathf.Clamp(rect.y, 0, Screen.height - rect.height);
                rect = new Rect(left, top, rect.width, rect.height);
            }

            void SetRectPos(ref Vector2 pos, Rect rect)
            {
                pos.x = rect.xMin;
                pos.y = rect.yMin;
            }

            if (showMainWindow && isKSPGUIActive && !isLoading && !isBadUI)
            {
                if (GUI.skin != HighLogic.Skin)
                {
                    GUI.skin = HighLogic.Skin;
                }

                int id = GetHashCode();

                mainRect = ClickThruBlocker.GUILayoutWindow(id, mainRect, MakeMainWindow, "Audio Muffler Redux", GUILayout.Width(300));
                ClampToScreen(ref mainRect);
                Tooltip.Instance?.ShowTooltip(id);
                SetRectPos(ref mainRectPos, mainRect);
            }
        }

        private bool BoolButtonAuto(ref bool value, string label, string tooltip = "", params GUILayoutOption[] options)
        {
            label = value ? $"Disable {label}" : $"Enable {label}";

            return BoolButton(ref value, label, tooltip, options);
        }

        private bool BoolButton(ref bool value, string label, string tooltip = "", params GUILayoutOption[] options)
        {
            return BoolButton(ref value, new GUIContent(label, tooltip), HighLogic.Skin.button, options);
        }

        private bool BoolButton(ref bool value, GUIContent content, GUIStyle style, params GUILayoutOption[] options)
        {
            if (GUILayout.Button(content, style, options))
            {
                value = !value;
                return true;
            }
            else return false;
        }

        private void LabelValueFloat(string label, float value, string unit, string labelTooltip = "", bool includeSpace = true, bool includeUnitSpace = true)
        {
            string space = includeUnitSpace ? " " : "";

            LabelValue(label, $"{value:G5}{space}{unit}", $"{value:G17}{space}{unit}", labelTooltip, includeSpace);
        }

        private void Box(string value, string boxTooltip = "")
        {
            using (new GUILayout.VerticalScope())
            {
                GUILayout.Space(7); // Box is weirdly offset, need to shift it down
                GUILayout.Box(new GUIContent(value, boxTooltip), GUILayout.ExpandWidth(true));
            }
        }

        private void LabelValue(string label, string value, string boxTooltip = "", string labelToolTip = "", bool includeSpace = true)
        {
            if (includeSpace) GUILayout.Space(5);
            using (new GUILayout.HorizontalScope())
            {
                GUILayout.Label(new GUIContent(label, labelToolTip), GUILayout.ExpandWidth(true));
                Box(value, boxTooltip);
            }
        }

        private void MakeMainWindow(int id)
        {
            if (HighLogic.LoadedScene != GameScenes.FLIGHT && HighLogic.LoadedScene != GameScenes.TRACKSTATION && HighLogic.LoadedScene != GameScenes.SPACECENTER) return;

            if (BoolButtonAuto(ref enableMuffler, "Muffler", "Enable or disable the audio muffler in flight.") && !enableMuffler)
            {
                Muffler.Instance.RestoreAudio();
            }
            BoolButtonAuto(ref helmetOutsideIVA, "Helmet Outside IVA", "Hear cabin sounds only when inside a pod. This generally relates to 3rd party mods like Chatterer etc.");
            BoolButtonAuto(ref helmetOutsideEVA, "Helmet Outside EVA", "Hear helmet sounds (Chatterer etc.) only when the view is inside a kerbal (i.e. 1st person view).");
            BoolButtonAuto(ref helmetForUnmanned, "Helmet For Unmanned", "Hear all \"in helmet\" sounds in unmanned vessels even when viewing the vessel from outside.");
            BoolButtonAuto(ref helmetInMapView, "Helmet In Map View", "Hear helmet sounds (Chatterer etc.) in the Map View.");
            BoolButtonAuto(ref vesselInMapView, "Vessel In Map View", "Hear sounds from distant parts of the active vessel in the Map View.");
            BoolButtonAuto(ref outsideInMapView, "Outside In Map View", "Hear sounds that are outside your vessel in the Map View.");
            BoolButtonAuto(ref roundValues, "Round Values", "Round the update interval slider to the nearest 10 and the frequency sliders to the nearest 100.");
            BoolButtonAuto(ref debug, "Debug", "Spew various messages as stuff happens inside the plugin.");

            if (roundValues)
            {
                LabelValue("Minimum Cache Update Interval", $"{minCacheUpdateInterval} ms", labelToolTip: "Performance setting in milliseconds. Too low may cause lags on active vessel crash, too high may render the cache ineffective and cause FPS drops in flight.");
                minCacheUpdateInterval = (int)(GUILayout.HorizontalSlider(minCacheUpdateInterval, 10, 1000) / 10f) * 10;

                LabelValueFloat("Wall Cutoff Frequency", wallCutoff, "hz", "Sound that pass through a part's wall will be reduced to this frequency.");
                wallCutoff = Mathf.Round(GUILayout.HorizontalSlider(wallCutoff, 0f, Muffler.maxFrequency) / 100f) * 100f;

                LabelValueFloat("Minimal Cutoff Frequency", minimalCutoff, "hz", "Minimal frequency to which sound is reduced by air sparseness. 300 is the setting of the old Audio Muffler (makes sounds become a deep bass rumble in vacuum instead of total silence).");
                minimalCutoff = Mathf.Round(GUILayout.HorizontalSlider(minimalCutoff, 0f, Muffler.maxFrequency) / 100f) * 100f;
            }
            else
            {
                LabelValue("Minimum Cache Update Interval", $"{minCacheUpdateInterval} ms", labelToolTip: "Performance setting in milliseconds. Too low may cause lags on active vessel crash, too high may render the cache ineffective and cause FPS drops in flight.");
                minCacheUpdateInterval = (int)GUILayout.HorizontalSlider(minCacheUpdateInterval, 10, 1000);

                LabelValueFloat("Wall Cutoff Frequency", wallCutoff, "hz", "Sound that pass through a part's wall will be reduced to this frequency.");
                wallCutoff = GUILayout.HorizontalSlider(wallCutoff, 0f, Muffler.maxFrequency);

                LabelValueFloat("Minimal Cutoff Frequency", minimalCutoff, "hz", "Minimal frequency to which sound is reduced by air sparseness. 300 is the setting of the old Audio Muffler (makes sounds become a deep bass rumble in vacuum instead of total silence).");
                minimalCutoff = GUILayout.HorizontalSlider(minimalCutoff, 0f, Muffler.maxFrequency);
            }
            
            if (GUILayout.Button(new GUIContent("Reset to Default", "Reset to Default Settings.")))
            {
                enableMuffler = true;
                helmetOutsideIVA = true;
                helmetOutsideEVA = true;
                helmetForUnmanned = true;
                helmetInMapView = false;
                vesselInMapView = true;
                outsideInMapView = false;
                debug = false;

                minCacheUpdateInterval = 300;
                wallCutoff = 400f;
                minimalCutoff = 0f;
            }

            using (new GUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                BoolButton(ref showMainWindow, "X", "Close this window.", GUILayout.Width(25));
            }

            Tooltip.Instance?.RecordTooltip(id);
            GUI.DragWindow();
        }
    }
}
