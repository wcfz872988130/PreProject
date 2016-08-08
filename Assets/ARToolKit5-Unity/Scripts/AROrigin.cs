using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

[RequireComponent(typeof(Transform))]
[ExecuteInEditMode]
public class AROrigin : MonoBehaviour
{
	private const string LogTag = "AROrigin: ";

	public enum FindMode {
		AutoAll,
		AutoByTags,
		Manual
	}
	public List<String> findMarkerTags = new List<string>();

//	private ARMarker baseMarker = null;
//	private List<ARMarker> markersEligibleForBaseMarker = new List<ARMarker>();
	private ARTrackedObject baseMarker = null;
	private List<ARTrackedObject> markersEligibleForBaseMarker = new List<ARTrackedObject>();

	[SerializeField]
	private FindMode _findMarkerMode = FindMode.AutoAll;

	public FindMode findMarkerMode
	{
		get
		{
			return _findMarkerMode;
		}
		
		set
		{
			if (_findMarkerMode != value) {
				_findMarkerMode = value;
				FindMarkers();
			}
		}
	}

//	public void AddMarker(ARMarker marker, bool atHeadOfList = false)
	public void AddMarker(ARTrackedObject marker, bool atHeadOfList = false)
	{
		if (!atHeadOfList) {
			markersEligibleForBaseMarker.Add(marker);
		} else {
			markersEligibleForBaseMarker.Insert(0, marker);
		}
	}

//	public bool RemoveMarker(ARMarker marker)
	public bool RemoveMarker(ARTrackedObject marker)
	{
		if (baseMarker == marker) baseMarker = null;
		return markersEligibleForBaseMarker.Remove(marker);
	}
	
	public void RemoveAllMarkers()
	{
		baseMarker = null;
		markersEligibleForBaseMarker.Clear();
	}

	public void FindMarkers()
	{
		RemoveAllMarkers();
		if (findMarkerMode != FindMode.Manual) {
//			ARMarker[] ms = FindObjectsOfType<ARMarker>(); // Does not find inactive objects.
//			foreach (ARMarker m in ms) {
			ARTrackedObject[] ms = FindObjectsOfType<ARTrackedObject>(); // Does not find inactive objects.
			foreach (ARTrackedObject m in ms) {
				if (findMarkerMode == FindMode.AutoAll || (findMarkerMode == FindMode.AutoByTags && findMarkerTags.Contains(m.Tag))) {
					markersEligibleForBaseMarker.Add(m);
				}
			}
			ARController.Log(LogTag + "Found " + markersEligibleForBaseMarker.Count + " markers eligible to become base marker.");
		}
	}


	void Awake()
	{
		Debug.Log ("AROrigin Awake");
	}
	void Start()
	{
		Debug.Log ("AROrigin Start");
		FindMarkers();
	}

	// Get the marker, if any, currently acting as the base.
//	public ARMarker GetBaseMarker()
	public ARTrackedObject GetBaseMarker()
	{
		if (baseMarker != null) {
			if (baseMarker.MarkerVisible) return baseMarker;
			else baseMarker = null;
		}
		
//		foreach (ARMarker m in markersEligibleForBaseMarker) {
		foreach (ARTrackedObject m in markersEligibleForBaseMarker) {
			if (m.MarkerVisible) {
				baseMarker = m;
				//ARController.Log("Marker " + m.UID + " became base marker.");
				break;
			}
		}
		
		return baseMarker;
	}
	
	void OnApplicationQuit()
	{
		RemoveAllMarkers();
	}
}

