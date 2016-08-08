using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using UnityEngine;

public enum MarkerType
{
	Square,      		// A standard ARToolKit template (pattern) marker
	SquareBarcode,      // A standard ARToolKit matrix (barcode) marker.
	Multimarker,        // Multiple markers treated as a single target
	NFT
}

public enum ARWMarkerOption : int {
	ARW_MARKER_OPTION_FILTERED = 1,
	ARW_MARKER_OPTION_FILTER_SAMPLE_RATE = 2,
	ARW_MARKER_OPTION_FILTER_CUTOFF_FREQ = 3,
	ARW_MARKER_OPTION_SQUARE_USE_CONT_POSE_ESTIMATION = 4,
	ARW_MARKER_OPTION_SQUARE_CONFIDENCE = 5,
	ARW_MARKER_OPTION_SQUARE_CONFIDENCE_CUTOFF = 6,
	ARW_MARKER_OPTION_NFT_SCALE = 7
}

[RequireComponent(typeof(Transform))]
[ExecuteInEditMode]
public class ARTrackedObject : MonoBehaviour
{
	public string modelName;


	//ARMarker
	public readonly static Dictionary<MarkerType, string> MarkerTypeNames = new Dictionary<MarkerType, string>
	{
		{MarkerType.Square, "Single AR pattern"},
		{MarkerType.SquareBarcode, "Single AR barcode"},
		{MarkerType.Multimarker, "Multimarker AR configuration"},
		{MarkerType.NFT, "NFT dataset"}
	};
	
	private const string LogTag = "ARMarker: ";
	
	// Quaternion to rotate from ART to Unity
	//public static Quaternion RotationCorrection = Quaternion.AngleAxis(90.0f, new Vector3(1.0f, 0.0f, 0.0f));
	
	// Value used when no underlying ARToolKit marker is assigned
	public const int NO_ID = -1;
	
	[NonSerialized]       // UID is not serialized because its value is only meaningful during a specific run.
	public int UID = NO_ID;      // Current Unique Identifier (UID) assigned to this marker.
	
	// Public members get serialized
	public MarkerType MarkerType = MarkerType.Square;
	public string Tag = "";
	
	// If the marker is single, then it has a filename and a width
	public int PatternFilenameIndex = 0;
	public string PatternFilename = "";
	public string PatternContents = ""; // Set by the editor.
	public float PatternWidth = 0.08f;
	
	// Barcode markers have a user-selected ID.
	public int BarcodeID = 0;
	
	// If the marker is multi, it just has a config filename
	public string MultiConfigFile = "";
	
	// NFT markers have a dataset pathname (less the extension).
	// Also, we need a list of the file extensions that make up an NFT dataset.
	public string NFTDataName = "";
	#if !UNITY_METRO
	private readonly string[] NFTDataExts = {"iset", "fset", "fset3"};
	#endif
	[NonSerialized]
	public float NFTWidth; // Once marker is loaded, this holds the width of the marker in Unity units.
	[NonSerialized]
	public float NFTHeight; // Once marker is loaded, this holds the height of the marker in Unity units.
	
	// Single markers have a single pattern, multi markers have one or more, NFT have none.
	private ARPattern[] patterns;
	
	// Private fields with accessors.
	// Marker configuration options.
	[SerializeField]
	private bool currentUseContPoseEstimation = false;						// Single marker only; whether continuous pose estimation should be used.
	[SerializeField]
	private bool currentFiltered = false;
	[SerializeField]
	private float currentFilterSampleRate = 30.0f;
	[SerializeField]
	private float currentFilterCutoffFreq = 15.0f;
	[SerializeField]
	private float currentNFTScale = 1.0f;									// NFT marker only; scale factor applied to marker size.
	
	// Realtime tracking information
	private bool markerVisible = false;                                           // Marker is visible or not
	private Matrix4x4 transformationMatrix;                                 // Full transformation matrix as a Unity matrix
	//    private Quaternion rotation = new Quaternion(0.0f, 0.0f, 0.0f, 1.0f);   // Rotation corrected for Unity
	//    private Vector3 position = new Vector3(0.0f, 0.0f, 0.0f);               // Position corrected for Unity





	private AROrigin _origin = null;
//	private ARMarker _marker = null;
	private ARTrackedObject _marker = null;

	private bool visible = false;					// Current visibility from tracking
	private float timeTrackingLost = 0;				// Time when tracking was last lost
	public float secondsToRemainVisible = 0.0f;		// How long to remain visible after tracking is lost (to reduce flicker)
	private bool visibleOrRemain = false;			// Whether to show the content (based on above variables)

	public GameObject eventReceiver;

	// Private fields with accessors.
	[SerializeField]
	private string _markerTag = "";					// Unique tag for the marker to get tracking from

	// Return the marker associated with this component.
	// Uses cached value if available, otherwise performs a find operation.
//	public virtual ARMarker GetMarker()
//	{
//		if (_marker == null) {
//			// Locate the marker identified by the tag
//			_marker=this.GetComponent<ARMarker>();
//		}
//		return _marker;
//	}
	public virtual ARTrackedObject GetMarker()
	{
		_marker=this.GetComponent<ARTrackedObject>();
		return _marker;
	}

	// Return the origin associated with this component.
	// Uses cached value if available, otherwise performs a find operation.
	public virtual AROrigin GetOrigin()
	{
		if (_origin == null) {
			// Locate the origin in parent.
			_origin = this.gameObject.GetComponentInParent<AROrigin>(); // Unity v4.5 and later.
		}
		return _origin;
	}

	void Start()
	{
		//ARController.Log(LogTag + "Start()");

		if (Application.isPlaying) {
			// In Player, set initial visibility to not visible.
			for (int i = 0; i < this.transform.childCount; i++) this.transform.GetChild(i).gameObject.SetActive(false);
		} else {
			// In Editor, set initial visibility to visible.
			for (int i = 0; i < this.transform.childCount; i++) this.transform.GetChild(i).gameObject.SetActive(true);
		}
	}

	// Use LateUpdate to be sure the ARMarker has updated before we try and use the transformation.
	void LateUpdate()
	{
		// Local scale is always 1 for now
		transform.localScale = Vector3.one;
		
		// Update tracking if we are running in the Player.
		if (Application.isPlaying) {

			// Sanity check, make sure we have an AROrigin in parent hierachy.
			AROrigin origin = GetOrigin();
			if (origin == null) {
				//visible = visibleOrRemain = false;

			} else {

				// Sanity check, make sure we have an ARMarker assigned.
//				ARMarker marker = GetMarker();
				ARTrackedObject marker = GetMarker();
				if (marker == null) {
					//visible = visibleOrRemain = false;
				} else {

					// Note the current time
					float timeNow = Time.realtimeSinceStartup;
					
//					ARMarker baseMarker = origin.GetBaseMarker();
					ARTrackedObject baseMarker = origin.GetBaseMarker();
					if (baseMarker != null && marker.MarkerVisible) {

						if (!visible) {
							// Marker was hidden but now is visible.
							visible = visibleOrRemain = true;
							if (eventReceiver != null) eventReceiver.BroadcastMessage("OnMarkerFound", marker, SendMessageOptions.DontRequireReceiver);

							ARTrackObjectManager.GetInstance()._visibleMarker =this;
							ARTrackObjectManager.GetInstance().OnMarkerActivate();
							Debug.Log("ChildCount:"+this.transform.childCount);
							if(this.transform.childCount>0)
							{
								for (int i = 0; i < this.transform.childCount; i++) 
								{
									this.transform.GetChild(i).gameObject.SetActive(true);
									Debug.Log("childName:"+this.transform.GetChild(i).gameObject.name);
								}
							}
							else{
								// play the movie
								StartCoroutine(PlayVideoCoroutine());
							}
						}

                        Matrix4x4 pose;
                        if (marker == baseMarker) {
                            // If this marker is the base, no need to take base inverse etc.
                            pose = origin.transform.localToWorldMatrix;
                        } else {
						    pose = (origin.transform.localToWorldMatrix * baseMarker.TransformationMatrix.inverse * marker.TransformationMatrix);
						}
						transform.position = ARUtilityFunctions.PositionFromMatrix(pose);
						transform.rotation = ARUtilityFunctions.QuaternionFromMatrix(pose);

						if (eventReceiver != null) eventReceiver.BroadcastMessage("OnMarkerTracked", marker, SendMessageOptions.DontRequireReceiver);

					} else {

						if (visible) {
							// Marker was visible but now is hidden.
							visible = false;
							timeTrackingLost = timeNow;

							//------
							ARTrackObjectManager.GetInstance()._visibleMarker = null;
						}

						if (visibleOrRemain && (timeNow - timeTrackingLost >= secondsToRemainVisible)) {
							visibleOrRemain = false;
							if (eventReceiver != null) eventReceiver.BroadcastMessage("OnMarkerLost", marker, SendMessageOptions.DontRequireReceiver);
							for (int i = 0; i < this.transform.childCount; i++) this.transform.GetChild(i).gameObject.SetActive(false);
						}
					}
				} // marker

			} // origin
		} // Application.isPlaying

	}

	IEnumerator PlayVideoCoroutine()
	{
		#if UNITY_IPHONE || UNITY_ANDROID
		Handheld.PlayFullScreenMovie("file://" + Application.persistentDataPath + "/" + this.modelName, Color.black, FullScreenMovieControlMode.Full);
		#endif  
		yield return new WaitForEndOfFrame();
		Debug.Log("Video playback completed.");
	}



	void Awake()
	{
		//ARController.Log(LogTag + "ARMarker.Awake()");
		UID = NO_ID;
	}
	
	public void OnEnable()
	{
		//ARController.Log(LogTag + "ARMarker.OnEnable()");
		Load();
	}
	
	public void OnDisable()
	{
		//ARController.Log(LogTag + "ARMarker.OnDisable()");
		Unload();
	}
	
	#if !UNITY_METRO
	private bool unpackStreamingAssetToCacheDir(string basename)
	{
		if (!File.Exists(System.IO.Path.Combine(Application.temporaryCachePath, basename))) {
			string file = System.IO.Path.Combine(Application.streamingAssetsPath, basename); // E.g. "jar:file://" + Application.dataPath + "!/assets/" + basename;
			WWW unpackerWWW = new WWW(file);
			while (!unpackerWWW.isDone) { } // This will block in the webplayer. TODO: switch to co-routine.
			if (!string.IsNullOrEmpty(unpackerWWW.error)) {
				ARController.Log(LogTag + "Error unpacking '" + file + "'");
				return (false);
			}
			File.WriteAllBytes(System.IO.Path.Combine(Application.temporaryCachePath, basename), unpackerWWW.bytes); // 64MB limit on File.WriteAllBytes.
		}
		return (true);
	}
	#endif
	
	// Load the underlying ARToolKit marker structure(s) and set the UID.
	public void Load() 
	{
		//ARController.Log(LogTag + "ARMarker.Load()");
		if (UID != NO_ID) {
			//ARController.Log(LogTag + "Marker already loaded.");
			return;
		}
		
		if (!PluginFunctions.inited) {
			return;
		}
		
		// Work out the configuration string to pass to ARToolKit.
		//string dir = Application.streamingAssetsPath;
		string dir = AppStatus.IndexDir;
		string cfg = "";
		
		switch (MarkerType) {
			
		case MarkerType.Square:
			// Multiply width by 1000 to convert from metres to ARToolKit's millimetres.
			cfg = "single_buffer;" + PatternWidth*1000.0f + ";buffer=" + PatternContents;
			break;
			
		case MarkerType.SquareBarcode:
			// Multiply width by 1000 to convert from metres to ARToolKit's millimetres.
			cfg = "single_barcode;" + BarcodeID + ";" + PatternWidth*1000.0f;
			break;
			
		case MarkerType.Multimarker:
			#if !UNITY_METRO
			if (dir.Contains("://")) {
				// On Android, we need to unpack the StreamingAssets from the .jar file in which
				// they're archived into the native file system.
				dir = Application.temporaryCachePath;
				if (!unpackStreamingAssetToCacheDir(MultiConfigFile)) {
					dir = "";
				} else {
					
					//string[] unpackFiles = getPatternFiles;
					//foreach (string patternFile in patternFiles) {
					//if (!unpackStreamingAssetToCacheDir(patternFile)) {
					//    dir = "";
					//    break;
					//}
				}
			}
			#endif
			
			if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(MultiConfigFile)) {
				cfg = "multi;" + System.IO.Path.Combine(dir, MultiConfigFile);
			}
			break;
			
			
		case MarkerType.NFT:
			#if !UNITY_METRO
			if (dir.Contains("://")) {
				// On Android, we need to unpack the StreamingAssets from the .jar file in which
				// they're archived into the native file system.
				dir = Application.temporaryCachePath;
				foreach (string ext in NFTDataExts) {
					string basename = NFTDataName + "." + ext;
					if (!unpackStreamingAssetToCacheDir(basename)) {
						dir = "";
						break;
					}
				}
			}
			#endif
			
			if (!string.IsNullOrEmpty(dir) && !string.IsNullOrEmpty(NFTDataName)) {
				cfg = "nft;" + System.IO.Path.Combine(dir, NFTDataName);
			}
			break;
			
		default:
			// Unknown marker type?
			break;
			
		}
		
		// If a valid config. could be assembled, get ARToolKit to process it, and assign the resulting ARMarker UID.
		if (!string.IsNullOrEmpty(cfg)) {
			UID = PluginFunctions.arwAddMarker(cfg);
			if (UID == NO_ID) {
				ARController.Log(LogTag + "Error loading marker.");
			} else {
				
				// Marker loaded. Do any additional configuration.
				//ARController.Log("Added marker with cfg='" + cfg + "'");
				
				if (MarkerType == MarkerType.Square || MarkerType == MarkerType.SquareBarcode) UseContPoseEstimation = currentUseContPoseEstimation;
				Filtered = currentFiltered;
				FilterSampleRate = currentFilterSampleRate;
				FilterCutoffFreq = currentFilterCutoffFreq;
				if (MarkerType == MarkerType.NFT) NFTScale = currentNFTScale;
				
				// Retrieve any required information from the configured ARToolKit ARMarker.
				if (MarkerType == MarkerType.NFT) {
					
					int imageSizeX, imageSizeY;
					PluginFunctions.arwGetMarkerPatternConfig(UID, 0, null, out NFTWidth, out NFTHeight, out imageSizeX, out imageSizeY);
					NFTWidth *= 0.001f;
					NFTHeight *= 0.001f;
					//ARController.Log("Got NFTWidth=" + NFTWidth + ", NFTHeight=" + NFTHeight + ".");
					
				} else {
					
					// Create array of patterns. A single marker will have array length 1.
					int numPatterns = PluginFunctions.arwGetMarkerPatternCount(UID);
					//ARController.Log("Marker with UID=" + UID + " has " + numPatterns + " patterns.");
					if (numPatterns > 0) {
						patterns = new ARPattern[numPatterns];
						for (int i = 0; i < numPatterns; i++) {
							patterns[i] = new ARPattern(UID, i);
						}
					}
					
				}
			}
		}
	}
	
	// We use Update() here, but be aware that unless ARController has been configured to
	// execute first (Unity Editor->Edit->Project Settings->Script Execution Order) then
	// state produced by this update may lag by one frame.
	void Update()
	{
		float[] matrixRawArray = new float[16];
		
		//ARController.Log(LogTag + "ARMarker.Update()");
		if (UID == NO_ID || !PluginFunctions.inited) {
			markerVisible = false;
			return;
		}
		
		// Query visibility if we are running in the Player.
		if (Application.isPlaying) {
			
			markerVisible = PluginFunctions.arwQueryMarkerTransformation(UID, matrixRawArray);
			//ARController.Log(LogTag + "ARMarker.Update() UID=" + UID + ", visible=" + visible);
			
			if (markerVisible) {
				matrixRawArray[12] *= 0.001f; // Scale the position from ARToolKit units (mm) into Unity units (m).
				matrixRawArray[13] *= 0.001f;
				matrixRawArray[14] *= 0.001f;
				
				Matrix4x4 matrixRaw = ARUtilityFunctions.MatrixFromFloatArray(matrixRawArray);
				//ARController.Log("arwQueryMarkerTransformation(" + UID + ") got matrix: [" + Environment.NewLine + matrixRaw.ToString("F3").Trim() + "]");
				
				// ARToolKit uses right-hand coordinate system where the marker lies in x-y plane with right in direction of +x,
				// up in direction of +y, and forward (towards viewer) in direction of +z.
				// Need to convert to Unity's left-hand coordinate system where marker lies in x-y plane with right in direction of +x,
				// up in direction of +y, and forward (towards viewer) in direction of -z.
				transformationMatrix = ARUtilityFunctions.LHMatrixFromRHMatrix(matrixRaw);
			}
		}
	}
	
	// Unload any underlying ARToolKit structures, and clear the UID.
	public void Unload()
	{
		//ARController.Log(LogTag + "ARMarker.Unload()");
		
		if (UID == NO_ID) {
			//ARController.Log(LogTag + "Marker already unloaded.");
			return;
		}
		
		if (PluginFunctions.inited) {
			// Remove any currently loaded ARToolKit marker.
			PluginFunctions.arwRemoveMarker(UID);
		}
		
		UID = NO_ID;
		
		patterns = null; // Delete the patterns too.
	}
	
	public Matrix4x4 TransformationMatrix
	{
		get
		{                
			return transformationMatrix;
		}
	}
	
	//    public Vector3 Position
	//    {
	//        get
	//        {
	//            return position;
	//        }
	//    }
	//
	//    public Quaternion Rotation
	//    {
	//        get
	//        {
	//            return rotation;
	//        }
	//    }
	
	public bool MarkerVisible
	{
		get
		{
			return markerVisible;
		}
	}

	public bool Visible
	{
		get{
			return visible;
		}
	}
	
	
	public ARPattern[] Patterns
	{
		get
		{
			return patterns;
		}
	}
	
	public bool Filtered
	{
		get
		{
			return currentFiltered; // Serialised.
		}
		
		set
		{
			currentFiltered = value;
			if (UID != NO_ID) {
				PluginFunctions.arwSetMarkerOptionBool(UID, (int)ARWMarkerOption.ARW_MARKER_OPTION_FILTERED, value);
			}
		}
	}
	
	public float FilterSampleRate
	{
		get
		{
			return currentFilterSampleRate; // Serialised.
		}
		
		set
		{
			currentFilterSampleRate = value;
			if (UID != NO_ID) {
				PluginFunctions.arwSetMarkerOptionFloat(UID, (int)ARWMarkerOption.ARW_MARKER_OPTION_FILTER_SAMPLE_RATE, value);
			}
		}
	}
	
	public float FilterCutoffFreq
	{
		get
		{
			return currentFilterCutoffFreq; // Serialised.
		}
		
		set
		{
			currentFilterCutoffFreq = value;
			if (UID != NO_ID) {
				PluginFunctions.arwSetMarkerOptionFloat(UID, (int)ARWMarkerOption.ARW_MARKER_OPTION_FILTER_CUTOFF_FREQ, value);
			}
		}
	}
	
	public bool UseContPoseEstimation
	{
		get
		{
			return currentUseContPoseEstimation; // Serialised.
		}
		
		set
		{
			currentUseContPoseEstimation = value;
			if (UID != NO_ID && (MarkerType == MarkerType.Square || MarkerType == MarkerType.SquareBarcode)) {
				PluginFunctions.arwSetMarkerOptionBool(UID, (int)ARWMarkerOption.ARW_MARKER_OPTION_SQUARE_USE_CONT_POSE_ESTIMATION, value);
			}
		}
	}
	
	public float NFTScale
	{
		get
		{
			return currentNFTScale; // Serialised.
		}
		
		set
		{
			currentNFTScale = value;
			if (UID != NO_ID && (MarkerType == MarkerType.NFT)) {
				PluginFunctions.arwSetMarkerOptionFloat(UID, (int)ARWMarkerOption.ARW_MARKER_OPTION_NFT_SCALE, value);
			}
		}
	}

}

