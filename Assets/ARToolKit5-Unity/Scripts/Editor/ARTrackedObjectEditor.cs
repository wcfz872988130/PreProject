using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ARTrackedObject))] 
public class ARTrackedObjectEditor : Editor 
{
    public override void OnInspectorGUI()
    {
		ARTrackedObject arto = (ARTrackedObject)target;
		if (arto == null) return;

		string modelName = EditorGUILayout.TextField("modelName", arto.modelName);
		arto.modelName = modelName;
//		ARMarker marker = arto.GetMarker();
		ARTrackedObject marker = arto.GetMarker();
		EditorGUILayout.LabelField("Got marker", marker == null ? "no" : "yes");
		if (marker != null) {
			string type = ARTrackedObject.MarkerTypeNames[marker.MarkerType];
			EditorGUILayout.LabelField("Marker UID", (marker.UID != ARTrackedObject.NO_ID ? marker.UID.ToString() : "Not loaded") + " (" + type + ")");	
		}
		
		EditorGUILayout.Separator();
		
		arto.secondsToRemainVisible = EditorGUILayout.FloatField("Stay visible", arto.secondsToRemainVisible);
		
		EditorGUILayout.Separator();
		
		arto.eventReceiver = (GameObject)EditorGUILayout.ObjectField("Event Receiver:", arto.eventReceiver, typeof(GameObject), true);
	

		MarkerGUI ();
	}



	public bool showFilterOptions = false;
	private static TextAsset[] PatternAssets;
	private static int PatternAssetCount;
	private static string[] PatternFilenames;
	
	void OnDestroy()
	{
		// Classes inheriting from MonoBehavior need to set all static member variables to null on unload.
		PatternAssets = null;
		PatternAssetCount = 0;
		PatternFilenames = null;
	}
	
	private static void RefreshPatternFilenames() 
	{
		PatternAssets = Resources.LoadAll("ardata/markers", typeof(TextAsset)).Cast<TextAsset>().ToArray();
		PatternAssetCount = PatternAssets.Length;
		
		PatternFilenames = new string[PatternAssetCount];
		for (int i = 0; i < PatternAssetCount; i++) {					
			PatternFilenames[i] = PatternAssets[i].name;				
		}
	}
	
	public void MarkerGUI()
	{
		
		EditorGUILayout.BeginVertical();
		
		// Get the ARMarker that this panel will edit.
		ARTrackedObject m = (ARTrackedObject)target;
		if (m == null) return;
		
		// Attempt to load. Might not work out if e.g. for a single marker, pattern hasn't been
		// assigned yet, or for an NFT marker, dataset hasn't been specified.
		if (m.UID == ARTrackedObject.NO_ID) m.Load(); 
		
		// Marker tag
		m.Tag = EditorGUILayout.TextField("Tag", m.Tag);
		EditorGUILayout.LabelField("UID", (m.UID == ARTrackedObject.NO_ID ? "Not loaded": m.UID.ToString()));
		
		EditorGUILayout.Separator();
		
		// Marker type		
		MarkerType t = (MarkerType)EditorGUILayout.EnumPopup("Type", m.MarkerType);
		if (m.MarkerType != t) { // Reload on change.
			m.Unload();
			m.MarkerType = t;
			m.Load();
		}
		
		// Description of the type of marker
		EditorGUILayout.LabelField("Description", ARTrackedObject.MarkerTypeNames[m.MarkerType]);
		
		switch (m.MarkerType) {
			
		case MarkerType.Square:	
		case MarkerType.SquareBarcode:
			
			if (m.MarkerType == MarkerType.Square) {
				
				// For pattern markers, offer a popup with marker pattern file names.
				RefreshPatternFilenames(); // Update the list of available markers from the resources dir
				if (PatternFilenames.Length > 0) {
					int patternFilenameIndex = EditorGUILayout.Popup("Pattern file", m.PatternFilenameIndex, PatternFilenames);
					string patternFilename = PatternAssets[patternFilenameIndex].name;
					if (patternFilename != m.PatternFilename) {
						m.Unload();
						m.PatternFilenameIndex = patternFilenameIndex;
						m.PatternFilename = patternFilename;
						m.PatternContents = PatternAssets[m.PatternFilenameIndex].text;
						m.Load();
					}
				} else {
					m.PatternFilenameIndex = 0;
					EditorGUILayout.LabelField("Pattern file", "No patterns available");
					m.PatternFilename = "";
					m.PatternContents = "";
				}
				
			} else {
				
				// For barcode markers, allow the user to specify the barcode ID.
				int BarcodeID = EditorGUILayout.IntField("Barcode ID", m.BarcodeID);
				//EditorGUILayout.LabelField("(in range 0 to " + barcodeCounts[ARController.MatrixCodeType] + ")");
				if (BarcodeID != m.BarcodeID) {
					m.Unload();
					m.BarcodeID = BarcodeID;
					m.Load();
				}
				
			}
			
			float patternWidthPrev = m.PatternWidth;
			m.PatternWidth = EditorGUILayout.FloatField("Width", m.PatternWidth);
			if (patternWidthPrev != m.PatternWidth) {
				m.Unload();
				m.Load();
			}
			m.UseContPoseEstimation = EditorGUILayout.Toggle("Cont. pose estimation", m.UseContPoseEstimation);
			
			break;
			
		case MarkerType.Multimarker:
			string MultiConfigFile = EditorGUILayout.TextField("Multimarker config.", m.MultiConfigFile);
			if (MultiConfigFile != m.MultiConfigFile) {
				m.Unload();
				m.MultiConfigFile = MultiConfigFile;
				m.Load();
			}
			break;
			
		case MarkerType.NFT:
			string NFTDataSetName = EditorGUILayout.TextField("NFT dataset name", m.NFTDataName);
			if (NFTDataSetName != m.NFTDataName) {
				m.Unload();
				m.NFTDataName = NFTDataSetName;
				m.Load();
			}
			float nftScalePrev = m.NFTScale;
			m.NFTScale = EditorGUILayout.FloatField("NFT marker scalefactor", m.NFTScale);
			if (nftScalePrev != m.NFTScale) {
				EditorUtility.SetDirty(m);
			}
			break;
		}
		
		EditorGUILayout.Separator();
		
		showFilterOptions = EditorGUILayout.Foldout(showFilterOptions, "Filter Options");
		if (showFilterOptions) {
			m.Filtered = EditorGUILayout.Toggle("Filtered:", m.Filtered);
			m.FilterSampleRate = EditorGUILayout.Slider("Sample rate:", m.FilterSampleRate, 1.0f, 30.0f);
			m.FilterCutoffFreq = EditorGUILayout.Slider("Cutoff freq.:", m.FilterCutoffFreq, 1.0f, 30.0f);
		}
		
		EditorGUILayout.BeginHorizontal();
		
		// Draw all the marker images
		if (m.Patterns != null) {
			for (int i = 0; i < m.Patterns.Length; i++) {
				GUILayout.Label(new GUIContent("Pattern " + i + ", " + m.Patterns[i].width.ToString("n3") + " m", m.Patterns[i].texture), GUILayout.ExpandWidth(false)); // n3 -> 3 decimal places.
			}
		}
		
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.EndVertical();
		
	}
}
