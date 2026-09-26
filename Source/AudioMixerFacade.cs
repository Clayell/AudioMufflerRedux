using System;
using UnityEngine;
using UnityEngine.Audio;

namespace AudioMuffler {

	/// <summary>
	/// Description of AudioMixerFacade.
	/// </summary>
	public class AudioMixerFacade
	{
		
		private static bool BundleLoaded = false;
		
		private static AudioMixer audioMixer;
		
		public AudioMixerGroup MasterGroup {get; set;}
		public AudioMixerGroup InVesselGroup {get; set;}
		public AudioMixerGroup OutsideGroup {get; set;}
		public AudioMixerGroup HelmetGroup {get; set;}
		
		public void MuteInVessel(bool mute) {
			audioMixer.SetFloat("InVesselVolume", mute ? -80 : 0);
		}

		public void MuteOutside(bool mute) {
			audioMixer.SetFloat("OutsideVolume", mute ? -80 : 0);
		}

		public void SetInVesselVolume(float volume) {
			audioMixer.SetFloat("InVesselVolume", volume);
		}

		public void SetInVesselCutoff(float cutoff) {
			audioMixer.SetFloat("InVesselCutoff", cutoff);
		}

		public void SetOutsideVolume(float volume) {
			audioMixer.SetFloat("OutsideVolume", volume);
		}
		
		public void SetOutsideCutoff(float cutoff) {
			audioMixer.SetFloat("OutsideCutoff", cutoff);
		}

		public void MuteHelmet(bool mute) {
			audioMixer.SetFloat("HelmetVolume", mute ? -80 : 0);
		}
		
		public static AudioMixerFacade InitializeMixer(string path) {
			AudioMixerFacade instance = new AudioMixerFacade ();
			if (audioMixer == null) {
				audioMixer = LoadBundle(path);
			}
			instance.MasterGroup = audioMixer.FindMatchingGroups("Master") [0];
			instance.InVesselGroup = audioMixer.FindMatchingGroups("InVessel") [0];
			instance.OutsideGroup = audioMixer.FindMatchingGroups("Outside") [0];
			instance.HelmetGroup = audioMixer.FindMatchingGroups("Helmet") [0];
			return instance;
		}
		
		public static AudioMixer LoadBundle(string path)
		{
			if (BundleLoaded) {
				return null;
			}
			
			using (WWW www = new WWW("file://" + path)) {
				if (www.error != null) {
					Muffler.Log("Mixer bundle not found!");
					return null;
				}
	
				AssetBundle bundle = www.assetBundle;
			
				AudioMixer audioMixer = bundle.LoadAsset<AudioMixer> ("KSPAudioMixer");
			
				bundle.Unload(false);
				www.Dispose();
			
				BundleLoaded = true;
				return audioMixer;
			}
		}
		
	}

}