using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace AudioMuffler 
{

	//TODO optimize string comparisons
	
	[KSPAddon(KSPAddon.Startup.Flight, false)]
	public class Muffler : MonoBehaviour
	{
	    private AudioMixerFacade audioMixer;
		private VesselCacheManager cacheManager;

        void Awake()
	    {
			cacheManager = new VesselCacheManager();
            //Camera.onPostRender += DebugPostRender;
        }

	    void Start()
	    {
            string PluginDataFolder = Path.Combine(KSPUtil.ApplicationRootPath, "GameData/AudioMufflerRedux/PluginData/");

            GameEvents.onVesselChange.Add(VesselChange);
	        GameEvents.onVesselWasModified.Add(VesselWasModified);

			AudioSource[] audioSources = FindObjectsOfType(typeof(AudioSource)) as AudioSource[];
			audioMixer = AudioMixerFacade.InitializeMixer(Path.Combine(PluginDataFolder, "mixer.bundle"));
	        StockAudio.prepareAudioSources(audioMixer, audioSources);
			audioMixer.SetInVesselCutoff(AudioMufflerConfig.Instance.wallCutoff);
	    }

		void OnDestroy()
		{
            GameEvents.onVesselChange.Remove(VesselChange);
            GameEvents.onVesselWasModified.Remove(VesselWasModified);
        }

	    void VesselChange(Vessel v)
	    {
			cacheManager.RebuildAllCaches(FindObjectsOfType(typeof(AudioSource)) as AudioSource[]);
			DebugLog($"Vessel change {v?.name}");
	    }
	    
	    void VesselWasModified(Vessel vessel) {
	    	if (vessel != null && vessel.isActiveVessel) {
				cacheManager.SetSchedule(AudioMufflerConfig.Instance.minCacheUpdateInterval);
	    	}
	    }

	    void LateUpdate()
	    {
			if (!AudioMufflerConfig.Instance.enableMuffler)
	            return;

			AudioSource[] audioSources = FindObjectsOfType(typeof(AudioSource)) as AudioSource[];

			cacheManager.MaintainCaches(audioSources);

	        //Looking for a part containing the Ear:
	        Part earPart = null;

			//if (vesselGeometry.isPointInVesselBounds(earPosition))
			{
				if (CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA) {
					earPart = CameraManager.Instance.IVACameraActiveKerbal.InPart;
					DebugLog("Ear position = IVA");
				} else {
					Vector3 earPosition = CameraManager.GetCurrentCamera().transform.position;
					DebugLog($"Ear position = {earPosition}");
					for (int i = 0; i < FlightGlobals.ActiveVessel.Parts.Count && earPart == null; i++) {
						Part part = FlightGlobals.ActiveVessel.Parts[i];
						if (cacheManager.vesselGeometry.IsPointInPart(earPosition, part)) {
							earPart = part;
						}
					}
				}
			}

			DebugLog($"Ear part = {(earPart != null ? earPart.name : "null")}");

			//Setting up helmet channel:

			bool unmanned = FlightGlobals.ActiveVessel.crewableParts == 0;

			bool muteHelmet = (earPart == null) && !AudioMufflerConfig.Instance.helmetOutsideEVA && FlightGlobals.ActiveVessel.isEVA
				|| !FlightGlobals.ActiveVessel.isEVA && !unmanned && !AudioMufflerConfig.Instance.helmetOutsideIVA && !(CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.IVA)
				|| !AudioMufflerConfig.Instance.helmetForUnmanned && unmanned;
			
			//Setting up outside channel:
			float atmosphericCutoff = Mathf.Lerp(AudioMufflerConfig.Instance.minimalCutoff, 30000, (float)FlightGlobals.ActiveVessel.atmDensity);
			if (earPart != null) {
				audioMixer.SetOutsideCutoff(Mathf.Min(AudioMufflerConfig.Instance.wallCutoff, atmosphericCutoff));
				audioMixer.SetInVesselVolume(-2f);
				audioMixer.SetOutsideVolume(-12f);
			} else {
				audioMixer.SetOutsideCutoff(atmosphericCutoff);
				audioMixer.SetInVesselVolume(0f);
				audioMixer.SetOutsideVolume(0f);
			}

			//Handling Map view settings:
			bool isMapView = CameraManager.Instance.currentCameraMode == CameraManager.CameraMode.Map;
			muteHelmet = muteHelmet	|| !AudioMufflerConfig.Instance.helmetInMapView && isMapView;

			audioMixer.MuteHelmet(muteHelmet);
			audioMixer.MuteInVessel(!AudioMufflerConfig.Instance.vesselInMapView && isMapView);
			audioMixer.MuteOutside(!AudioMufflerConfig.Instance.outsideInMapView && isMapView);

			//Routing all current audio sources:
	        for (int i = 0; i < audioSources.Length; i++) {
				AudioSource audioSource = audioSources[i];

				/*
					Hereafter audio sources that are bound to a part's InternalModel are handled differently from the rest because InternalModel has its own reference system,
					so these two systems shouldn't be mixed until the way to convert coordinates between them is found
				*/

				if (AudioMufflerConfig.Instance.debug) {
					DebugLog($"Sound {i} clp={(audioSource.clip == null ? "null" : audioSource.clip.name)}: plyng={audioSource.isPlaying} trf.nm={audioSource.transform.name} trf.pos={audioSource.transform.position} amb={StockAudio.isAmbient(audioSource)} inves={StockAudio.isInVessel(audioSource)}");
				}

				//This "if" is here because of strange behaviour of StageManager's audio source which always has clip = null and !playing when checked
				if (StockAudio.isInVessel(audioSource)) { 
					DebugLog($"Sound {i}:{audioSource.name} IN VESSEL");
					audioSource.outputAudioMixerGroup = earPart != null ? audioMixer.InVesselGroup : audioMixer.OutsideGroup;
					continue;
				}
	        	
				if (/*audioSource.bypassEffects ||*/ StockAudio.isPreserved(audioSource) || (audioSource.clip == null) || (!audioSource.isPlaying)) {
	        		continue;
	        	}

				if (StockAudio.isAmbient(audioSource)) {
					DebugLog($"Sound {i}:{audioSource.name} OUTSIDE");
					audioSource.outputAudioMixerGroup = audioMixer.OutsideGroup;
					continue;
				}

				if (IsSoundInHelmet(audioSource)) {
					DebugLog($"Sound {i}:{audioSource.name} IN HELMET");
					audioSource.outputAudioMixerGroup = audioMixer.HelmetGroup;
					continue;
				}

				bool isRouted = false;
				if (earPart != null /*&& vesselGeometry.isPointInVesselBounds(audioSource.transform.position)*/) {

					Part boundToPartIVA = cacheManager.vesselSounds.GetPartForIVA(audioSource);
					if (boundToPartIVA != null) {
						if (earPart.Equals(boundToPartIVA)) {
							DebugLog($"Sound {i}:{audioSource.name} SAME AS EAR, INTERNAL");
							audioSource.outputAudioMixerGroup = null; //if audioSource is in the same part with the Ear then skipping filtering
							continue;
						} else {
							DebugLog($"Sound {i}:{audioSource.name} ANOTHER PART, INTERNAL");
							audioSource.outputAudioMixerGroup = audioMixer.InVesselGroup; //if audioSource is in another part of the vessel then applying constant muffling
							continue;
						}
					}
					
					//Below the following assumption is used: if an audioSource is bound to a part (by having the same transform) then it most likely is located inside that part,
					//so in the most cases it will be enough to test just the part's meshes instead of sequentially testing all of the vessel's meshes, thus greatly improve
					//performance in case of a high part count:

					//if (CameraManager.Instance.currentCameraMode != CameraManager.CameraMode.IVA) { //TODO remove this check when a proper way to transform coordinates between InternalModel and part's transform is found
						if (audioSource.transform.IsChildOf(earPart.transform) && cacheManager.vesselGeometry.IsPointInPart(audioSource.transform.position, earPart)) {
							DebugLog($"Sound {i}:{audioSource.name} SAME AS EAR");
							audioSource.outputAudioMixerGroup = null; //if audioSource is in the same part with the Ear then skipping filtering
							continue;
						}
					//}

					Part boundPart = cacheManager.vesselSounds.GetPartFor(audioSource);
					if (boundPart != null && !boundPart.Equals(earPart) && cacheManager.vesselGeometry.IsPointInPart(audioSource.transform.position, boundPart)) {
						DebugLog($"Sound {i}:{audioSource.name} ANOTHER PART");
						audioSource.outputAudioMixerGroup = audioMixer.InVesselGroup; //if audioSource is in another part of the vessel then applying constant muffling
						continue;
					}

					//To this point the audioSource should be already routed as in the game the vast majority of sounds are either helmet sounds, or bound to parts, or are ambient.
					//So only a few sounds are expected to pass to the following mesh-by-mesh test:

					for (int p = 0; p < FlightGlobals.ActiveVessel.Parts.Count && !isRouted; p++) {
						Part part = FlightGlobals.ActiveVessel.Parts[p];
						if (part.Equals(boundPart)) { //if the audioSource is bound to some part, then this part is already checked earlier
							continue;
						}

						if (cacheManager.vesselGeometry.IsPointInPart(audioSource.transform.position, part)) {
							if (part.Equals(earPart)) {
								DebugLog($"Sound {i}:{audioSource.name} SAME AS EAR");
								audioSource.outputAudioMixerGroup = null; //if audioSource is in the same part with the Ear then skipping filtering
							} else {
								DebugLog($"Sound {i}:{audioSource.name} ANOTHER PART");
								audioSource.outputAudioMixerGroup = audioMixer.InVesselGroup; //if audioSource is in another part of the vessel then applying constant muffling
							}
							isRouted = true;
						}
					}
				}

	        	if (isRouted) {
	        		continue;
	        	}

				DebugLog($"Sound {i}:{audioSource.name} OUTSIDE");
				audioSource.outputAudioMixerGroup = audioMixer.OutsideGroup;
	        }
	    }
	    
		private bool IsSoundInHelmet(AudioSource audioSource) {
			return !StockAudio.isAmbient(audioSource) && audioSource.transform.position == Vector3.zero;
		}

		public static void DebugLog(string message) {
			if (AudioMufflerConfig.Instance.debug && !Planetarium.Pause)
			{
				Log(message);
			}
		}

        public static void Log(string message)
        {
            UnityEngine.Debug.Log($"[Audio Muffler]: {message}");
        }

        private void VisualizeTransform(Transform transform, Color color) {
			if (transform == null) {
				return;
			}
			DebugLog($"TRANSFORM: {transform.position}");
			GameObject gameObject = transform.gameObject;
			Vector3 origin = transform.position;
			LineRenderer lineRenderer = gameObject.GetComponent<LineRenderer>();
			if (lineRenderer == null) {
				lineRenderer = gameObject.AddComponent<LineRenderer>();
				lineRenderer.material = new Material(Shader.Find("Particles/Additive"));
				lineRenderer.SetVertexCount(6);
			}
			lineRenderer.SetWidth(0.05f, 0.01f);
			lineRenderer.SetColors(Color.white, color);
			lineRenderer.SetPosition(0, origin);
			lineRenderer.SetPosition(1, transform.TransformPoint(Vector3.right));
			lineRenderer.SetPosition(2, origin);
			lineRenderer.SetPosition(3, transform.TransformPoint(Vector3.up));
			lineRenderer.SetPosition(4, origin);
			lineRenderer.SetPosition(5, transform.TransformPoint(Vector3.forward));
		}

		void DebugPostRender(Camera currentCamera) {
			//Log($"CAMERA: {currentCamera.name}");
			if ((currentCamera.name == "InternalCamera") || (currentCamera.tag == "MainCamera")) {
				DebugVisualizer.DrawPartMeshes(Color.red - new Color(0.9f, 0.9f, 0.9f,0));
				DebugVisualizer.VisualizeTransform(FlightGlobals.ActiveVessel.parts[0].transform, Color.green);
				DebugVisualizer.VisualizeTransform(FlightGlobals.ActiveVessel.parts[0].internalModel.transform, Color.blue);
				DebugVisualizer.VisualizeAudioSources(FindObjectsOfType(typeof(AudioSource)) as AudioSource[], Color.yellow);
				DebugVisualizer.VisualizeAudioSources(Array.FindAll(FindObjectsOfType(typeof(AudioSource)) as AudioSource[], a => a.clip != null && a.clip.name.Contains("Chatterer")), Color.red);
			}
		}
			    
	}
}
